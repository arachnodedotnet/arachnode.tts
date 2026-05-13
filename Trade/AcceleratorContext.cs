// AcceleratorContext.cs  (target: .NET Framework 4.7.2 / C# 7.3)
// Helper that owns a single ILGPU Context + Accelerator,
// prefers CUDA (configurable), can fall back to CPU, and caches compiled kernels.
//
// IMPORTANT:
// - If you target .NET Framework 4.7.2, verify your ILGPU package supports it.
//   (Newer ILGPU versions may require newer runtimes. If so, use a Framework-
/*   compatible ILGPU release, or upgrade your runtime.) */

using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace Trade
{
    /// <summary>Creation options for <see cref="AcceleratorContext"/>.</summary>
    public sealed class AcceleratorOptions
    {
        /// <summary>Prefer a CUDA device when available.</summary>
        public bool PreferCuda { get; set; }

        /// <summary>Allow falling back to CPU accelerator if no GPU is available.</summary>
        public bool AllowCpuFallback { get; set; }

        /// <summary>
        /// Choose a specific CUDA device index (0-based). If null, the "best" device is selected.
        /// Ignored when <see cref="PreferCuda"/> is false or CUDA is unavailable.
        /// </summary>
        public int? CudaDeviceIndex { get; set; }

        /// <summary>Enable ILGPU.Algorithms (XMath, etc.) in the context.</summary>
        public bool EnableAlgorithms { get; set; }

        /// <summary>Math mode used during compilation.</summary>
        public MathMode MathMode { get; set; }

        /// <summary>Print diagnostics to Console during initialization.</summary>
        public bool VerboseDiagnostics { get; set; }

        public AcceleratorOptions()
        {
            PreferCuda = true;
            AllowCpuFallback = true;
            CudaDeviceIndex = null;
            EnableAlgorithms = true;
            MathMode = MathMode.Default;
            VerboseDiagnostics = true;
        }

        /// <summary>
        /// Optional environment overrides:
        ///   ILGPU_ACCELERATOR = "CUDA" | "CPU"
        ///   CUDA_DEVICE_INDEX = integer
        /// </summary>
        public static AcceleratorOptions FromEnvironment(AcceleratorOptions defaults)
        {
            var opt = defaults != null ? Clone(defaults) : new AcceleratorOptions();

            var accelEnv = (Environment.GetEnvironmentVariable("ILGPU_ACCELERATOR") ?? string.Empty)
                .Trim().ToUpperInvariant();
            if (accelEnv == "CPU") opt.PreferCuda = false;
            else if (accelEnv == "CUDA") opt.PreferCuda = true;

            var cudaIdxEnv = Environment.GetEnvironmentVariable("CUDA_DEVICE_INDEX");
            int idx;
            if (int.TryParse(cudaIdxEnv, out idx) && idx >= 0)
                opt.CudaDeviceIndex = idx;

            return opt;
        }

        private static AcceleratorOptions Clone(AcceleratorOptions s)
        {
            return new AcceleratorOptions
            {
                PreferCuda = s.PreferCuda,
                AllowCpuFallback = s.AllowCpuFallback,
                CudaDeviceIndex = s.CudaDeviceIndex,
                EnableAlgorithms = s.EnableAlgorithms,
                MathMode = s.MathMode,
                VerboseDiagnostics = s.VerboseDiagnostics
            };
        }
    }

    /// <summary>
    /// Singleton-style owner of ILGPU <see cref="Context"/> and <see cref="Accelerator"/>.
    /// Provides device selection (CUDA-first), basic diagnostics, and kernel caching.
    /// </summary>
    public sealed class AcceleratorContext : IDisposable
    {
        private static readonly object Sync = new object();
        private static AcceleratorContext _shared;

        /// <summary>Process-wide shared instance (lazy-initialized).</summary>
        public static AcceleratorContext Shared
        {
            get
            {
                lock (Sync)
                {
                    if (_shared == null)
                        _shared = Create(null);
                    return _shared;
                }
            }
        }

        /// <summary>Create a fresh instance with optional options (env can override).</summary>
        public static AcceleratorContext Create(AcceleratorOptions options)
        {
            options = AcceleratorOptions.FromEnvironment(options);

            // Ensure Algorithms assembly is loaded (older runtimes sometimes need a nudge).
            try
            {
                var _ = typeof(XMath);
                //var __ = XMath.PI;
                var ___ = CudaAccelerator.PitchedAllocationAlignmentInBytes;
            }
            catch { /* ignore */ }

            Context ctx = null;
            try
            {
                if (options.EnableAlgorithms)
                {
                    ctx = Context.Create(b => b
                        .Cuda()
                        .CPU()
                        .EnableAlgorithms()
                        .Math(options.MathMode));
                }
                else
                {
                    ctx = Context.Create(b => b
                        .Cuda()
                        .CPU()
                        .Math(options.MathMode));
                }
            }
            catch (Exception ex)
            {
                if (options.VerboseDiagnostics)
                {
                    Console.WriteLine("[AcceleratorContext] Context.Create(...) failed: " + ex.Message);
                    Console.WriteLine("[AcceleratorContext] Falling back to Context.CreateDefault()…");
                }
                if (ctx != null) ctx.Dispose();
                ctx = Context.CreateDefault();
            }

            Accelerator accel = null;
            CudaDevice selectedCuda = null;

            try
            {
                if (options.PreferCuda)
                {
                    var cudaDevices = ctx.GetCudaDevices();
                    if (cudaDevices.Count > 0)
                    {
                        var pick = options.CudaDeviceIndex.HasValue ? options.CudaDeviceIndex.Value : 0;
                        if (pick < 0 || pick >= cudaDevices.Count) pick = 0;
                        selectedCuda = cudaDevices[pick];
                        accel = selectedCuda.CreateAccelerator(ctx);
                    }
                }

                if (accel == null)
                {
                    var device = ctx.GetPreferredDevice(preferCPU: !options.PreferCuda);
                    accel = device.CreateAccelerator(ctx);
                }
            }
            catch (Exception ex)
            {
                if (options.VerboseDiagnostics)
                    Console.WriteLine("[AcceleratorContext] Device selection failed: " + ex.Message);

                if (options.AllowCpuFallback)
                {
                    try
                    {
                        if (accel != null) accel.Dispose();
                        var cpu = ctx.Devices.First(d => d.AcceleratorType == AcceleratorType.CPU);
                        accel = cpu.CreateAccelerator(ctx);
                    }
                    catch
                    {
                        ctx.Dispose();
                        throw;
                    }
                }
                else
                {
                    ctx.Dispose();
                    throw;
                }
            }

            var instance = new AcceleratorContext(ctx, accel, selectedCuda, options);
            if (options.VerboseDiagnostics)
                instance.PrintDiagnostics();

            return instance;
        }

        private AcceleratorContext(Context context, Accelerator accelerator, CudaDevice cudaDevice, AcceleratorOptions options)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (accelerator == null) throw new ArgumentNullException("accelerator");

            Context = context;
            Accelerator = accelerator;
            SelectedCudaDevice = cudaDevice;
            Options = options ?? new AcceleratorOptions();
        }

        /// <summary>The ILGPU compilation/backends context.</summary>
        public Context Context { get; private set; }

        /// <summary>The active accelerator (CUDA GPU or CPU).</summary>
        public Accelerator Accelerator { get; private set; }

        /// <summary>If a CUDA device was selected, this holds its descriptor.</summary>
        public CudaDevice SelectedCudaDevice { get; private set; }

        /// <summary>Options used for this instance.</summary>
        public AcceleratorOptions Options { get; private set; }

        /// <summary>True if the active accelerator is a CUDA GPU.</summary>
        public bool IsCuda
        {
            get { return Accelerator.AcceleratorType == AcceleratorType.Cuda; }
        }

        /// <summary>True if algorithms (XMath) were requested.</summary>
        public bool AlgorithmsEnabled
        {
            get { return Options.EnableAlgorithms; }
        }

        // Kernel cache keyed by method identity.
        private readonly ConcurrentDictionary<string, object> _kernelCache =
            new ConcurrentDictionary<string, object>();

        private static string MakeKernelKey(Delegate kernel)
        {
            var m = kernel.Method;
            // Use module MVID + metadata token + type&method name as a pseudo-unique key.
            return string.Format("{0}:{1}:{2}.{3}",
                m.Module.ModuleVersionId.ToString("N"),
                m.MetadataToken,
                m.DeclaringType != null ? m.DeclaringType.FullName : "<null>",
                m.Name);
        }

        /// <summary>
        /// Loads (and caches) an auto-grouped stream kernel from a delegate.
        /// Usage:
        ///   var k = ctx.LoadAutoGroupedKernel&lt;Index1D, ArrayView&lt;float&gt;&gt;((i, a) => { ... });
        ///   k(extent, viewA);
        /// </summary>
        public Action<TIndex, TArg1> LoadAutoGroupedKernel<TIndex, TArg1>(Action<TIndex, TArg1> kernel)
            where TIndex : struct, IIndex
            where TArg1 : struct
        {
            if (kernel == null) throw new ArgumentNullException(nameof(kernel));

            var key = MakeKernelKey(kernel);

            if (_kernelCache.TryGetValue(key, out object compiled))
                return (Action<TIndex, TArg1>)compiled;

            var loaded = Accelerator.LoadAutoGroupedStreamKernel<TIndex, TArg1>(kernel);
            _kernelCache[key] = loaded;
            return loaded;
        }

        /// <summary>Clears cached compiled kernels.</summary>
        public void ClearKernelCache()
        {
            _kernelCache.Clear();
        }

        /// <summary>Create a new stream (caller must dispose).</summary>
        public AcceleratorStream CreateStream()
        {
            return Accelerator.CreateStream();
        }

        /// <summary>Prints device/context diagnostics to Console.</summary>
        public void PrintDiagnostics()
        {
            try
            {
                Console.WriteLine("=== AcceleratorContext Diagnostics ===");
                Console.WriteLine("Context.TargetPlatform : " + Context.TargetPlatform);
                Console.WriteLine("Accelerator.Name       : " + Accelerator.Name);
                Console.WriteLine("Accelerator.Type       : " + Accelerator.AcceleratorType);
                Console.WriteLine("Accelerator.Memory     : " + (Accelerator.MemorySize / (1024 * 1024)).ToString("N0") + " MB");
                Console.WriteLine("MaxThreadsPerGroup     : " + Accelerator.MaxNumThreadsPerGroup);
                Console.WriteLine("WarpSize               : " + Accelerator.WarpSize);
                Console.WriteLine("AlgorithmsEnabled      : " + AlgorithmsEnabled);
                if (SelectedCudaDevice != null)
                {
                    Console.WriteLine("CUDA Device            : " + SelectedCudaDevice.Name);
                    Console.WriteLine("Compute Capability     : " + SelectedCudaDevice.Capabilities);
                }
                Console.WriteLine("=====================================");
            }
            catch { /* never throw from diagnostics */ }
        }

        /// <summary>
        /// Executes a compiled kernel immediately (default stream) and synchronizes.
        /// Example:
        ///   ctx.Run(k => k(extent, viewA, viewB), compiledKernel);
        /// </summary>
        public void Run<TKernel>(Action<TKernel> invoker, TKernel compiledKernel)
            where TKernel : class
        {
            if (invoker == null) throw new ArgumentNullException("invoker");
            if (compiledKernel == null) throw new ArgumentNullException("compiledKernel");
            invoker(compiledKernel);
            Accelerator.Synchronize();
        }

        /// <summary>
        /// Compiles and runs an auto-grouped kernel in one call.
        /// Example:
        ///   ctx.RunAutoGrouped((Index1D i, ArrayView&lt;float&gt; a) => { ... }, k => k(extent, view));
        /// </summary>
        public void RunAutoGrouped<TIndex, TArg1>(Action<TIndex, TArg1> kernel, Action<Action<TIndex, TArg1>> dispatch)
            where TIndex : struct, IIndex
            where TArg1 : struct
        {
            var compiled = LoadAutoGroupedKernel(kernel);
            Run(dispatch, compiled);
        }

        /// <summary>Times an action (ms).</summary>
        public static long TimeMs(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _kernelCache.Clear(); } catch { }
            try { if (Accelerator != null) Accelerator.Dispose(); } catch { }
            try { if (Context != null) Context.Dispose(); } catch { }

            lock (Sync)
            {
                if (object.ReferenceEquals(_shared, this))
                    _shared = null;
            }
        }
    }
}