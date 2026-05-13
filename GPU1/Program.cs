using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Note: Add these NuGet packages to your project:
// - ILGPU (1.5.1)
// - ILGPU.Algorithms (1.5.1)
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Algorithms;

namespace GPU1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== NVIDIA CUDA GPU Computing Examples ===");
            Console.WriteLine();

            // First, let's diagnose the system
            DiagnoseCudaEnvironment();

            try
            {
                // Add this at the top of Main, before creating the context
                _ = typeof(ILGPU.Algorithms.XMath);
                // before creating the Context
                _ = ILGPU.Algorithms.XMath.PI;                  // root Algorithms
                _ = ILGPU.Runtime.Cuda.CudaAccelerator.PitchedAllocationAlignmentInBytes; // root CUDA backend

                // Try multiple context creation strategies
                Context context = null;

                try
                {
                    Console.WriteLine("🔧 Attempting to create context with algorithms enabled...");
                    context = Context.Create(b => b.Cuda().EnableAlgorithms().Math(MathMode.Default));

                    //context = Context.CreateDefault();
                    Console.WriteLine("✅ Context with algorithms created successfully!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to create context with algorithms: {ex.Message}");
                    Console.WriteLine("🔧 Falling back to default context...");

                    try
                    {
                        context = Context.CreateDefault();
                        Console.WriteLine("✅ Default context created successfully!");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"❌ Failed to create default context: {ex2.Message}");
                        Console.WriteLine("This indicates a serious CUDA/ILGPU installation issue.");
                        return;
                    }
                }

                using (context)
                {
                    Console.WriteLine($"📋 ILGPU Context Info:");
                    Console.WriteLine($"   - Platform: {context.TargetPlatform}");

                    // Try to get CUDA device first (NVIDIA)
                    var cudaDevices = context.GetCudaDevices();
                    Console.WriteLine($"📋 CUDA Devices Found: {cudaDevices.Count}");

                    for (int i = 0; i < cudaDevices.Count; i++)
                    {
                        var device = cudaDevices[i];
                        Console.WriteLine($"   Device {i}: {device.Name}");
                        Console.WriteLine($"   - Compute Capability: {device.Capabilities}");
                        Console.WriteLine($"   - Memory: {device.MemorySize / (1024 * 1024):N0} MB");
                        Console.WriteLine($"   - Max Threads per Block: {device.MaxNumThreadsPerGroup}");
                        Console.WriteLine($"   - Warp Size: {device.WarpSize}");
                    }

                    if (cudaDevices.Count == 0)
                    {
                        Console.WriteLine("❌ No NVIDIA CUDA devices found!");
                        Console.WriteLine("🔧 Checking other available devices...");

                        // List all available devices
                        var allDevices = context.Devices;
                        Console.WriteLine($"📋 All Available Devices: {allDevices.Count()}");

                        foreach (var device in allDevices)
                        {
                            Console.WriteLine($"   - {device.AcceleratorType}: {device.Name}");
                        }

                        Console.WriteLine("Falling back to OpenCL or CPU...");
                        using (var accelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context))
                        {
                            Console.WriteLine($"✅ Using fallback device: {accelerator.Name}");
                            RunBasicExamples(accelerator);
                        }
                        return;
                    }

                    // Use first CUDA device
                    Console.WriteLine("🔧 Creating CUDA accelerator...");
                    using (var accelerator = cudaDevices[0].CreateAccelerator(context))
                    {
                        Console.WriteLine($"✅ Using NVIDIA GPU: {accelerator.Name}");
                        Console.WriteLine($"   Memory: {accelerator.MemorySize / (1024 * 1024):N0} MB");
                        Console.WriteLine($"   Max Threads per Group: {accelerator.MaxNumThreadsPerGroup}");
                        Console.WriteLine($"   Warp Size: {accelerator.WarpSize}");
                        Console.WriteLine();

                        PrecisionComparisonExample(accelerator);

                        // Test basic functionality first
                        Console.WriteLine("🧪 Testing basic GPU functionality...");
                        TestBasicGpuFunctionality(accelerator);

                        // Run all examples
                        RunBasicExamples(accelerator);
                        RunMathematicalExamples(accelerator);

                        // Only run financial examples if algorithms are available
                        if (context.HasAlgorithms())
                        {
                            Console.WriteLine("✅ Algorithms available - running financial examples...");
                            RunFinancialExamples(accelerator);
                        }
                        else
                        {
                            Console.WriteLine("⚠️ Algorithms not available - skipping financial examples that require XMath.Log");
                        }

                        RunPerformanceBenchmarks(accelerator);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                Console.WriteLine("Make sure you have CUDA installed and compatible drivers.");

                // Additional diagnostic information
                Console.WriteLine("\n🔍 System Diagnostic Information:");
                Console.WriteLine($"   - OS: {Environment.OSVersion}");
                Console.WriteLine($"   - .NET Version: {Environment.Version}");
                Console.WriteLine($"   - 64-bit Process: {Environment.Is64BitProcess}");
                Console.WriteLine($"   - 64-bit OS: {Environment.Is64BitOperatingSystem}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void DiagnoseCudaEnvironment()
        {
            Console.WriteLine("🔍 === CUDA ENVIRONMENT DIAGNOSIS ===");

            try
            {
                // Check if CUDA runtime is available
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("✅ nvidia-smi found - CUDA driver is installed");
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("Driver Version:"))
                        {
                            Console.WriteLine($"   {line.Trim()}");
                        }
                        if (line.Contains("CUDA Version:"))
                        {
                            Console.WriteLine($"   {line.Trim()}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ nvidia-smi not found - CUDA driver may not be installed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Could not run nvidia-smi: {ex.Message}");
                Console.WriteLine("   This might indicate CUDA is not installed or not in PATH");
            }

            Console.WriteLine();
        }

        static void TestBasicGpuFunctionality(Accelerator accelerator)
        {
            try
            {
                Console.WriteLine("🧪 Testing basic vector addition...");

                const int testSize = 1000;
                var a = Enumerable.Range(0, testSize).Select(i => (float)i).ToArray();
                var b = Enumerable.Range(0, testSize).Select(i => (float)i * 2).ToArray();

                var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(
                    (index, aView, bView, cView) => cView[index] = aView[index] + bView[index]
                );

                using (var gpuA = accelerator.Allocate1D<float>(a))
                using (var gpuB = accelerator.Allocate1D<float>(b))
                using (var gpuC = accelerator.Allocate1D<float>(testSize))
                {
                    kernel(testSize, gpuA.View, gpuB.View, gpuC.View);
                    accelerator.Synchronize();

                    var result = gpuC.GetAsArray1D();
                    Console.WriteLine($"✅ Basic GPU test successful: {a[0]} + {b[0]} = {result[0]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Basic GPU test failed: {ex.Message}");
                throw;
            }
        }

        #region Basic GPU Examples

        static void RunBasicExamples(Accelerator accelerator)
        {
            Console.WriteLine("📚 === BASIC GPU EXAMPLES ===");

            // Example 1: Vector Addition
            VectorAdditionExample(accelerator);

            // Example 2: Matrix Multiplication
            MatrixMultiplicationExample(accelerator);

            // Example 3: Parallel Reduction
            ReductionExample(accelerator);

            Console.WriteLine();
        }

        static void VectorAdditionExample(Accelerator accelerator)
        {
            Console.WriteLine("🔢 Vector Addition Example:");

            const int dataSize = 100000;
            var a = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();
            var b = Enumerable.Range(0, dataSize).Select(i => (float)i * 2).ToArray();

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuResult = new float[dataSize];
            for (int i = 0; i < dataSize; i++)
            {
                cpuResult[i] = a[i] + b[i];
            }
            cpuStopwatch.Stop();

            // GPU implementation
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(
                (index, aView, bView, cView) => cView[index] = aView[index] + bView[index]
            );

            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuA = accelerator.Allocate1D<float>(a))
            using (var gpuB = accelerator.Allocate1D<float>(b))
            using (var gpuC = accelerator.Allocate1D<float>(dataSize))
            {
                kernel(dataSize, gpuA.View, gpuB.View, gpuC.View);
                accelerator.Synchronize();
                var gpuResult = gpuC.GetAsArray1D();
                gpuStopwatch.Stop();

                // Verify results match
                var resultsMatch = cpuResult.SequenceEqual(gpuResult);
                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   ✅ Added {dataSize:N0} elements");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Results match: {resultsMatch}");
                Console.WriteLine($"   Sample: {a[0]} + {b[0]} = {cpuResult[0]}");
            }
        }

        static void MatrixMultiplicationExample(Accelerator accelerator)
        {
            Console.WriteLine("🧮 Matrix Multiplication Example:");

            const int size = 512;
            var matrixA = GenerateMatrix(size, size);
            var matrixB = GenerateMatrix(size, size);

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuResult = new float[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    float sum = 0.0f;
                    for (int k = 0; k < size; k++)
                    {
                        sum += matrixA[i, k] * matrixB[k, j];
                    }
                    cpuResult[i, j] = sum;
                }
            }
            cpuStopwatch.Stop();

            // GPU implementation
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index2D, ArrayView2D<float, Stride2D.DenseX>, ArrayView2D<float, Stride2D.DenseX>, ArrayView2D<float, Stride2D.DenseX>>(
                MatrixMultiplyKernel
            );

            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuA = accelerator.Allocate2DDenseX<float>(new Index2D(size, size)))
            using (var gpuB = accelerator.Allocate2DDenseX<float>(new Index2D(size, size)))
            using (var gpuC = accelerator.Allocate2DDenseX<float>(new Index2D(size, size)))
            {
                gpuA.CopyFromCPU(matrixA);
                gpuB.CopyFromCPU(matrixB);

                kernel(new Index2D(size, size), gpuA.View, gpuB.View, gpuC.View);
                accelerator.Synchronize();
                var gpuResult = gpuC.GetAsArray2D();
                gpuStopwatch.Stop();

                // Verify results (check a few elements due to floating point precision)
                var resultsMatch = Math.Abs(cpuResult[0, 0] - gpuResult[0, 0]) < 0.001f;
                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   ✅ Multiplied {size}x{size} matrices");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Results match: {resultsMatch}");
                Console.WriteLine($"   Result[0,0] = {cpuResult[0, 0]:F2}");
            }
        }

        static void MatrixMultiplyKernel(Index2D index, ArrayView2D<float, Stride2D.DenseX> a, ArrayView2D<float, Stride2D.DenseX> b, ArrayView2D<float, Stride2D.DenseX> c)
        {
            var x = index.X;
            var y = index.Y;
            var sum = 0.0f;

            for (int k = 0; k < a.IntExtent.Y; k++)
                sum += a[x, k] * b[k, y];

            c[x, y] = sum;
        }

        static void PrecisionComparisonExample(Accelerator accelerator)
        {
            Console.WriteLine("⚖️ 32-bit vs 64-bit Precision Comparison:");

            const int dataSize = 1000000;

            // Test with 32-bit floats
            var floatData = Enumerable.Range(1, dataSize).Select(i => (float)i).ToArray();
            var float32Stopwatch = Stopwatch.StartNew();
            using (var gpuFloatData = accelerator.Allocate1D<float>(floatData))
            using (var gpuFloatResult = accelerator.Allocate1D<float>(1))
            {
                var floatKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(
                    (index, input, output) => {
                        if (index.X == 0)
                        {
                            float sum = 0.0f;
                            for (int i = 0; i < input.Length; i++) sum += input[i];
                            output[0] = sum;
                        }
                    });

                floatKernel(1, gpuFloatData.View, gpuFloatResult.View);
                accelerator.Synchronize();
                var floatResult = gpuFloatResult.GetAsArray1D()[0];
                float32Stopwatch.Stop();
            }

            // Test with 64-bit doubles  
            var doubleData = Enumerable.Range(1, dataSize).Select(i => (double)i).ToArray();
            var float64Stopwatch = Stopwatch.StartNew();
            using (var gpuDoubleData = accelerator.Allocate1D<double>(doubleData))
            using (var gpuDoubleResult = accelerator.Allocate1D<double>(1))
            {
                var doubleKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>, ArrayView<double>>(
                    (index, input, output) => {
                        if (index.X == 0)
                        {
                            double sum = 0.0;
                            for (int i = 0; i < input.Length; i++) sum += input[i];
                            output[0] = sum;
                        }
                    });

                doubleKernel(1, gpuDoubleData.View, gpuDoubleResult.View);
                accelerator.Synchronize();
                var doubleResult = gpuDoubleResult.GetAsArray1D()[0];
                float64Stopwatch.Stop();
            }

            var performanceRatio = (double)float64Stopwatch.ElapsedMilliseconds / Math.Max(1, float32Stopwatch.ElapsedMilliseconds);

            Console.WriteLine($"   32-bit float: {float32Stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"   64-bit double: {float64Stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"   Performance ratio (64-bit/32-bit): {performanceRatio:F1}x slower");
            Console.WriteLine($"   Theoretical sum: {(dataSize * (dataSize + 1L)) / 2:N0}");
        }

        static void ReductionExample(Accelerator accelerator)
        {
            Console.WriteLine("🔄 Parallel Reduction Example (Sum):");

            const int dataSize = 1000000;
            var data = Enumerable.Range(1, dataSize).Select(i => (float)i).ToArray();

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuSum = data.Sum();
            cpuStopwatch.Stop();

            // GPU implementation
            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuData = accelerator.Allocate1D<float>(data))
            using (var gpuResult = accelerator.Allocate1D<float>(1))
            {
                var reductionKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(
                    (index, input, output) =>
                    {
                        var globalIndex = index.X;
                        if (globalIndex == 0)
                        {
                            float sum = 0.0f;
                            for (int i = 0; i < input.Length; i++)
                            {
                                sum += input[i];
                            }
                            output[0] = sum;
                        }
                    }
                );

                reductionKernel(1, gpuData.View, gpuResult.View);
                accelerator.Synchronize();
                var gpuSum = gpuResult.GetAsArray1D()[0];
                gpuStopwatch.Stop();

                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);
                var resultsMatch = Math.Abs(cpuSum - gpuSum) < 0.1f;

                Console.WriteLine($"   ✅ Reduced {dataSize:N0} elements");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Results match: {resultsMatch}");
                Console.WriteLine($"   CPU Sum: {cpuSum:N0}, GPU Sum: {gpuSum:N0}");
            }
        }

        #endregion

        #region Mathematical Examples

        static void RunMathematicalExamples(Accelerator accelerator)
        {
            Console.WriteLine("🧠 === MATHEMATICAL EXAMPLES ===");

            // Complex mathematical operations
            TrigonometricExample(accelerator);
            StatisticsExample(accelerator);
            FourierTransformExample(accelerator);

            Console.WriteLine();
        }

        static void TrigonometricExample(Accelerator accelerator)
        {
            Console.WriteLine("📐 Trigonometric Functions Example:");

            const int dataSize = 100000;
            var angles = Enumerable.Range(0, dataSize).Select(i => (float)i * 0.01f).ToArray();

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuSinResults = new float[dataSize];
            var cpuCosResults = new float[dataSize];
            for (int i = 0; i < dataSize; i++)
            {
                cpuSinResults[i] = (float)Math.Sin(angles[i]);
                cpuCosResults[i] = (float)Math.Cos(angles[i]);
            }
            cpuStopwatch.Stop();

            // GPU implementation
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(
                (index, input, sinOutput, cosOutput) =>
                {
                    var angle = input[index];
                    sinOutput[index] = XMath.Sin(angle);
                    cosOutput[index] = XMath.Cos(angle);
                }
            );

            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuAngles = accelerator.Allocate1D<float>(angles))
            using (var gpuSin = accelerator.Allocate1D<float>(dataSize))
            using (var gpuCos = accelerator.Allocate1D<float>(dataSize))
            {
                kernel(dataSize, gpuAngles.View, gpuSin.View, gpuCos.View);
                accelerator.Synchronize();
                var gpuSinResults = gpuSin.GetAsArray1D();
                var gpuCosResults = gpuCos.GetAsArray1D();
                gpuStopwatch.Stop();

                // Verify results (check first few elements due to potential precision differences)
                var sinMatch = Math.Abs(cpuSinResults[0] - gpuSinResults[0]) < 0.0001f;
                var cosMatch = Math.Abs(cpuCosResults[0] - gpuCosResults[0]) < 0.0001f;
                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   ✅ Computed sin/cos for {dataSize:N0} angles");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Results match: {sinMatch && cosMatch}");
                Console.WriteLine($"   sin(0) = {cpuSinResults[0]:F4}, cos(0) = {cpuCosResults[0]:F4}");
            }
        }

        static void StatisticsExample(Accelerator accelerator)
        {
            Console.WriteLine("📊 Statistics Example (Mean & Variance):");

            const int dataSize = 1000000;
            var random = new Random(42);
            var data = Enumerable.Range(0, dataSize).Select(_ => (float)random.NextDouble() * 100).ToArray();

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuMean = data.Average();
            var cpuVariance = data.Select(x => (x - cpuMean) * (x - cpuMean)).Average();
            var cpuStdDev = Math.Sqrt(cpuVariance);
            cpuStopwatch.Stop();

            // GPU implementation
            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuData = accelerator.Allocate1D<float>(data))
            using (var gpuMean = accelerator.Allocate1D<float>(1))
            using (var gpuVariance = accelerator.Allocate1D<float>(1))
            {
                // Calculate mean
                var meanKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(
                    (index, input, output) =>
                    {
                        if (index.X == 0)
                        {
                            float sum = 0.0f;
                            for (int i = 0; i < input.Length; i++)
                            {
                                sum += input[i];
                            }
                            output[0] = sum / input.Length;
                        }
                    }
                );

                meanKernel(1, gpuData.View, gpuMean.View);
                accelerator.Synchronize();

                var meanArray = gpuMean.GetAsArray1D();
                var mean = meanArray[0];

                // Calculate variance
                var varianceKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, float>(
                    (index, input, output, meanValue) =>
                    {
                        var diff = input[index] - meanValue;
                        output[index] = diff * diff;
                    }
                );

                using (var gpuSquaredDiffs = accelerator.Allocate1D<float>(dataSize))
                {
                    varianceKernel(dataSize, gpuData.View, gpuSquaredDiffs.View, mean);
                    accelerator.Synchronize();

                    var sumSquaredDiffsKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(
                        (index, input, output) =>
                        {
                            if (index.X == 0)
                            {
                                float sum = 0.0f;
                                for (int i = 0; i < input.Length; i++)
                                {
                                    sum += input[i];
                                }
                                output[0] = sum / input.Length;
                            }
                        }
                    );

                    sumSquaredDiffsKernel(1, gpuSquaredDiffs.View, gpuVariance.View);
                    accelerator.Synchronize();

                    var varianceArray = gpuVariance.GetAsArray1D();
                    var variance = varianceArray[0];
                    var stdDev = Math.Sqrt(variance);
                    gpuStopwatch.Stop();

                    var meanMatch = Math.Abs(cpuMean - mean) < 0.01;
                    var stdDevMatch = Math.Abs(cpuStdDev - stdDev) < 0.01;
                    var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                    Console.WriteLine($"   ✅ Computed statistics for {dataSize:N0} values");
                    Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                    Console.WriteLine($"   Speedup: {speedup:F1}x, Results match: {meanMatch && stdDevMatch}");
                    Console.WriteLine($"   Mean: {cpuMean:F2}, Std Dev: {cpuStdDev:F2}");
                }
            }
        }

        static void FourierTransformExample(Accelerator accelerator)
        {
            Console.WriteLine("🌊 Simple Fourier Transform Example:");

            const int dataSize = 1024;

            // Generate sample signal (sine wave + noise)
            var signal = new float[dataSize];
            var random = new Random(42);
            for (int i = 0; i < dataSize; i++)
            {
                signal[i] = (float)(Math.Sin(2 * Math.PI * 50 * i / dataSize) + 0.1 * random.NextDouble());
            }

            // CPU implementation (simple DFT)
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuRealPart = new float[dataSize];
            var cpuImagPart = new float[dataSize];
            for (int k = 0; k < dataSize; k++)
            {
                float realSum = 0.0f;
                float imagSum = 0.0f;
                for (int n = 0; n < dataSize; n++)
                {
                    var angle = -2.0f * Math.PI * k * n / dataSize;
                    realSum += signal[n] * (float)Math.Cos(angle);
                    imagSum += signal[n] * (float)Math.Sin(angle);
                }
                cpuRealPart[k] = realSum;
                cpuImagPart[k] = imagSum;
            }
            cpuStopwatch.Stop();

            // GPU implementation
            var dftKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>>(
                DFTKernel
            );

            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuSignal = accelerator.Allocate1D<float>(signal))
            using (var gpuReal = accelerator.Allocate1D<float>(dataSize))
            using (var gpuImag = accelerator.Allocate1D<float>(dataSize))
            {
                dftKernel(dataSize, gpuSignal.View, gpuReal.View, gpuImag.View);
                accelerator.Synchronize();
                var gpuRealPart = gpuReal.GetAsArray1D();
                var gpuImagPart = gpuImag.GetAsArray1D();
                gpuStopwatch.Stop();

                // Calculate magnitude spectrum for both
                var cpuMagnitude = new float[dataSize];
                var gpuMagnitude = new float[dataSize];
                for (int i = 0; i < dataSize; i++)
                {
                    cpuMagnitude[i] = (float)Math.Sqrt(cpuRealPart[i] * cpuRealPart[i] + cpuImagPart[i] * cpuImagPart[i]);
                    gpuMagnitude[i] = (float)Math.Sqrt(gpuRealPart[i] * gpuRealPart[i] + gpuImagPart[i] * gpuImagPart[i]);
                }

                var cpuPeakBin = Array.IndexOf(cpuMagnitude, cpuMagnitude.Max());
                var gpuPeakBin = Array.IndexOf(gpuMagnitude, gpuMagnitude.Max());
                var resultsMatch = cpuPeakBin == gpuPeakBin;
                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   ✅ Computed DFT for {dataSize} samples");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Peak bins match: {resultsMatch}");
                Console.WriteLine($"   Peak frequency bin: {cpuPeakBin}");
            }
        }

        static void DFTKernel(Index1D index, ArrayView<float> signal, ArrayView<float> realOut, ArrayView<float> imagOut)
        {
            var k = index.X;
            var N = signal.IntLength;
            var realSum = 0.0f;
            var imagSum = 0.0f;

            for (int n = 0; n < N; n++)
            {
                var angle = -2.0f * XMath.PI * k * n / N;
                realSum += signal[n] * XMath.Cos(angle);
                imagSum += signal[n] * XMath.Sin(angle);
            }

            realOut[k] = realSum;
            imagOut[k] = imagSum;
        }

        #endregion

        #region Financial Examples

        static void RunFinancialExamples(Accelerator accelerator)
        {
            Console.WriteLine("💰 === FINANCIAL COMPUTING EXAMPLES ===");

            // Financial calculations that would benefit from GPU acceleration
            OptionPricingExample(accelerator);
            TechnicalIndicatorsExample(accelerator);
            MonteCarloSimulationExample(accelerator);

            Console.WriteLine();
        }

        static void OptionPricingExample(Accelerator accelerator)
        {
            Console.WriteLine("📈 Black-Scholes Option Pricing:");

            const int numOptions = 100000;
            var stockPrices = GenerateRandomArray(numOptions, 50, 150, 42);
            var strikePrices = GenerateRandomArray(numOptions, 80, 120, 123);
            var timeToExpiry = GenerateRandomArray(numOptions, 0.1f, 2.0f, 456);

            const float riskFreeRate = 0.05f;
            const float volatility = 0.2f;

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuOptionPrices = new float[numOptions];
            for (int i = 0; i < numOptions; i++)
            {
                cpuOptionPrices[i] = CalculateBlackScholesPrice(stockPrices[i], strikePrices[i], timeToExpiry[i], riskFreeRate, volatility);
            }
            cpuStopwatch.Stop();

            // GPU implementation
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, float, float>(
                BlackScholesKernel
            );

            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuStockPrices = accelerator.Allocate1D<float>(stockPrices))
            using (var gpuStrikePrices = accelerator.Allocate1D<float>(strikePrices))
            using (var gpuTimeToExpiry = accelerator.Allocate1D<float>(timeToExpiry))
            using (var gpuOptionPrices = accelerator.Allocate1D<float>(numOptions))
            {
                kernel(numOptions, gpuStockPrices.View, gpuStrikePrices.View, gpuTimeToExpiry.View, gpuOptionPrices.View, riskFreeRate, volatility);
                accelerator.Synchronize();
                var gpuOptionPricesArray = gpuOptionPrices.GetAsArray1D();
                gpuStopwatch.Stop();

                // Verify results
                var priceMatch = Math.Abs(cpuOptionPrices[0] - gpuOptionPricesArray[0]) < 0.01f;
                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   ✅ Priced {numOptions:N0} options");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Results match: {priceMatch}");
                Console.WriteLine($"   Average option price: ${cpuOptionPrices.Average():F2}");
                Console.WriteLine($"   Min: ${cpuOptionPrices.Min():F2}, Max: ${cpuOptionPrices.Max():F2}");
            }
        }

        static float CalculateBlackScholesPrice(float S, float K, float T, float r, float sigma)
        {
            var d1 = (Math.Log(S / K) + (r + 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));
            var d2 = d1 - sigma * Math.Sqrt(T);

            var nd1 = ApproximateNormalCDFCpu((float)d1);
            var nd2 = ApproximateNormalCDFCpu((float)d2);

            return (float)(S * nd1 - K * Math.Exp(-r * T) * nd2);
        }

        static float ApproximateNormalCDFCpu(float x)
        {
            // Abramowitz and Stegun approximation
            var a1 = 0.254829592f;
            var a2 = -0.284496736f;
            var a3 = 1.421413741f;
            var a4 = -1.453152027f;
            var a5 = 1.061405429f;
            var p = 0.3275911f;

            var sign = x < 0 ? -1 : 1;
            x = Math.Abs(x);

            var t = 1.0f / (1.0f + p * x);
            var y = 1.0f - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return (float)(0.5f * (1.0f + sign * y));
        }

        static void BlackScholesKernel(Index1D index, ArrayView<float> stockPrices, ArrayView<float> strikePrices, ArrayView<float> timeToExpiry, ArrayView<float> optionPrices, float riskFreeRate, float volatility)
        {
            var S = stockPrices[index];  // Stock price
            var K = strikePrices[index]; // Strike price
            var T = timeToExpiry[index]; // Time to expiry
            var r = riskFreeRate;        // Risk-free rate
            var sigma = volatility;      // Volatility

            // Black-Scholes formula
            var d1 = (XMath.Log(S / K) + (r + 0.5f * sigma * sigma) * T) / (sigma * XMath.Sqrt(T));
            var d2 = d1 - sigma * XMath.Sqrt(T);

            // Approximate cumulative normal distribution
            var nd1 = ApproximateNormalCDF(d1);
            var nd2 = ApproximateNormalCDF(d2);

            // Call option price
            optionPrices[index] = S * nd1 - K * XMath.Exp(-r * T) * nd2;
        }

        static float ApproximateNormalCDF(float x)
        {
            // Abramowitz and Stegun approximation
            var a1 = 0.254829592f;
            var a2 = -0.284496736f;
            var a3 = 1.421413741f;
            var a4 = -1.453152027f;
            var a5 = 1.061405429f;
            var p = 0.3275911f;

            var sign = x < 0 ? -1 : 1;
            x = XMath.Abs(x);

            var t = 1.0f / (1.0f + p * x);
            var y = 1.0f - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * XMath.Exp(-x * x);

            return 0.5f * (1.0f + sign * y);
        }

        static void TechnicalIndicatorsExample(Accelerator accelerator)
        {
            Console.WriteLine("📊 Technical Indicators (Moving Average):");

            const int dataSize = 10000;
            const int windowSize = 20;
            var prices = GenerateRandomArray(dataSize, 90, 110, 789);

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuMovingAverages = new float[dataSize - windowSize + 1];
            for (int i = 0; i <= dataSize - windowSize; i++)
            {
                float sum = 0.0f;
                for (int j = 0; j < windowSize; j++)
                {
                    sum += prices[i + j];
                }
                cpuMovingAverages[i] = sum / windowSize;
            }
            cpuStopwatch.Stop();

            // GPU implementation
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int>(
                MovingAverageKernel
            );

            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuPrices = accelerator.Allocate1D<float>(prices))
            using (var gpuMA = accelerator.Allocate1D<float>(dataSize))
            {
                kernel(dataSize - windowSize + 1, gpuPrices.View, gpuMA.View, windowSize);
                accelerator.Synchronize();
                var gpuMovingAverages = gpuMA.GetAsArray1D();
                gpuStopwatch.Stop();

                // Verify results
                var resultsMatch = Math.Abs(cpuMovingAverages[0] - gpuMovingAverages[0]) < 0.001f;
                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   ✅ Computed {dataSize - windowSize + 1:N0} moving averages");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Results match: {resultsMatch}");
                Console.WriteLine($"   First MA: {cpuMovingAverages[0]:F2}, Last MA: {cpuMovingAverages[cpuMovingAverages.Length - 1]:F2}");
            }
        }

        static void MovingAverageKernel(Index1D index, ArrayView<float> prices, ArrayView<float> movingAverages, int windowSize)
        {
            var sum = 0.0f;
            for (int i = 0; i < windowSize; i++)
            {
                sum += prices[index + i];
            }
            movingAverages[index] = sum / windowSize;
        }

        static void MonteCarloSimulationExample(Accelerator accelerator)
        {
            Console.WriteLine("🎲 Monte Carlo Simulation (Portfolio Risk):");

            const int numSimulations = 100000;
            const int numAssets = 5;
            const int timeSteps = 252; // Trading days in a year

            // Portfolio parameters
            var initialPrices = new float[] { 100f, 120f, 80f, 150f, 90f };
            var weights = new float[] { 0.2f, 0.3f, 0.15f, 0.25f, 0.1f };
            var expectedReturns = new float[] { 0.08f, 0.12f, 0.06f, 0.15f, 0.07f };
            var volatilities = new float[] { 0.2f, 0.25f, 0.15f, 0.3f, 0.18f };

            // CPU implementation
            var cpuStopwatch = Stopwatch.StartNew();
            var cpuPortfolioValues = new float[numSimulations];
            var random = new Random(42);
            for (int sim = 0; sim < numSimulations; sim++)
            {
                var portfolioValue = 100.0f;
                var dt = 1.0f / 252.0f;

                for (int day = 0; day < timeSteps; day++)
                {
                    var dailyReturn = 0.0f;
                    for (int asset = 0; asset < numAssets; asset++)
                    {
                        // Simple Box-Muller for normal distribution
                        var u1 = random.NextDouble();
                        var u2 = random.NextDouble();
                        var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

                        var assetReturn = expectedReturns[asset] * dt + volatilities[asset] * Math.Sqrt(dt) * normal;
                        dailyReturn += weights[asset] * (float)assetReturn;
                    }
                    portfolioValue *= (1.0f + dailyReturn);
                }
                cpuPortfolioValues[sim] = portfolioValue;
            }
            cpuStopwatch.Stop();

            // GPU implementation
            var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>, ArrayView<float>>(
                MonteCarloKernel
            );

            var gpuStopwatch = Stopwatch.StartNew();
            using (var gpuInitialPrices = accelerator.Allocate1D<float>(initialPrices))
            using (var gpuWeights = accelerator.Allocate1D<float>(weights))
            using (var gpuExpectedReturns = accelerator.Allocate1D<float>(expectedReturns))
            using (var gpuVolatilities = accelerator.Allocate1D<float>(volatilities))
            using (var gpuRandomSeeds = accelerator.Allocate1D<float>(Enumerable.Range(0, numSimulations).Select(i => (float)i).ToArray()))
            using (var gpuPortfolioValues = accelerator.Allocate1D<float>(numSimulations))
            {
                kernel(numSimulations, gpuInitialPrices.View, gpuWeights.View, gpuExpectedReturns.View, gpuVolatilities.View, gpuRandomSeeds.View, gpuPortfolioValues.View);
                accelerator.Synchronize();
                var gpuPortfolioValuesArray = gpuPortfolioValues.GetAsArray1D();
                gpuStopwatch.Stop();

                // Calculate risk metrics for both
                var cpuFinalValues = cpuPortfolioValues.OrderBy(v => v).ToArray();
                var gpuFinalValues = gpuPortfolioValuesArray.OrderBy(v => v).ToArray();

                var cpuVar95 = cpuFinalValues[(int)(numSimulations * 0.05)];
                var gpuVar95 = gpuFinalValues[(int)(numSimulations * 0.05)];

                var resultsClose = Math.Abs(cpuPortfolioValues.Average() - gpuPortfolioValuesArray.Average()) < 1.0f;
                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   ✅ Ran {numSimulations:N0} Monte Carlo simulations");
                Console.WriteLine($"   CPU: {cpuStopwatch.ElapsedMilliseconds}ms, GPU: {gpuStopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"   Speedup: {speedup:F1}x, Results close: {resultsClose}");
                Console.WriteLine($"   Expected Portfolio Value: ${cpuPortfolioValues.Average():F2}");
                Console.WriteLine($"   95% VaR: ${100 - cpuVar95:F2}");
            }
        }

        static void MonteCarloKernel(Index1D index, ArrayView<float> initialPrices, ArrayView<float> weights, ArrayView<float> expectedReturns, ArrayView<float> volatilities, ArrayView<float> randomSeeds, ArrayView<float> portfolioValues)
        {
            // Simple random number generator based on index
            var seed = (uint)(randomSeeds[index] * 1000 + 1);

            var portfolioValue = 100.0f; // Initial portfolio value
            var dt = 1.0f / 252.0f; // Daily time step

            for (int day = 0; day < 252; day++)
            {
                var dailyReturn = 0.0f;

                for (int asset = 0; asset < initialPrices.IntLength; asset++)
                {
                    // Generate pseudo-random normal variable
                    seed = seed * 1103515245 + 12345;
                    var random1 = (seed % 10000) / 10000.0f;
                    seed = seed * 1103515245 + 12345;
                    var random2 = (seed % 10000) / 10000.0f;

                    // Box-Muller transformation for normal distribution
                    var normal = XMath.Sqrt(-2.0f * XMath.Log(random1)) * XMath.Cos(2.0f * XMath.PI * random2);

                    // Asset return using geometric Brownian motion
                    var assetReturn = expectedReturns[asset] * dt + volatilities[asset] * XMath.Sqrt(dt) * normal;
                    dailyReturn += weights[asset] * assetReturn;
                }

                portfolioValue *= (1.0f + dailyReturn);
            }

            portfolioValues[index] = portfolioValue;
        }

        #endregion

        #region Performance Benchmarks

        static void RunPerformanceBenchmarks(Accelerator accelerator)
        {
            Console.WriteLine("⚡ === PERFORMANCE BENCHMARKS ===");

            BenchmarkVectorOperations(accelerator);
            BenchmarkMemoryBandwidth(accelerator);

            Console.WriteLine();
        }

        static void BenchmarkVectorOperations(Accelerator accelerator)
        {
            Console.WriteLine("🏃 Vector Operations Benchmark:");

            var sizes = new int[] { 1000, 10000, 100000, 1000000 };

            foreach (var size in sizes)
            {
                var data = Enumerable.Range(0, size).Select(i => (float)i).ToArray();

                // GPU benchmark
                var gpuStopwatch = Stopwatch.StartNew();
                using (var gpuData = accelerator.Allocate1D<float>(data))
                using (var gpuResult = accelerator.Allocate1D<float>(size))
                {
                    var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(
                        (index, input, output) => output[index] = XMath.Sqrt(input[index]) + XMath.Sin(input[index])
                    );

                    kernel(size, gpuData.View, gpuResult.View);
                    accelerator.Synchronize();
                    gpuStopwatch.Stop();
                }

                // CPU benchmark
                var cpuStopwatch = Stopwatch.StartNew();
                var cpuResult = new float[size];
                for (int i = 0; i < size; i++)
                {
                    cpuResult[i] = (float)(Math.Sqrt(data[i]) + Math.Sin(data[i]));
                }
                cpuStopwatch.Stop();

                var speedup = (double)cpuStopwatch.ElapsedMilliseconds / Math.Max(1, gpuStopwatch.ElapsedMilliseconds);

                Console.WriteLine($"   Size {size,8:N0}: GPU {gpuStopwatch.ElapsedMilliseconds,3}ms, CPU {cpuStopwatch.ElapsedMilliseconds,4}ms, Speedup: {speedup,5:F1}x");
            }
        }

        static void BenchmarkMemoryBandwidth(Accelerator accelerator)
        {
            Console.WriteLine("💾 Memory Bandwidth Benchmark:");

            const int dataSize = 10000000; // 10M floats = 40MB
            var data = new float[dataSize];

            var stopwatch = Stopwatch.StartNew();

            using (var gpuData = accelerator.Allocate1D<float>(dataSize))
            {
                // Upload benchmark
                var uploadStart = Stopwatch.StartNew();
                gpuData.CopyFromCPU(data);
                accelerator.Synchronize();
                uploadStart.Stop();

                // Download benchmark
                var downloadStart = Stopwatch.StartNew();
                var result = gpuData.GetAsArray1D();
                downloadStart.Stop();

                var dataGBBytes = dataSize * sizeof(float) / (1024.0 * 1024.0 * 1024.0);
                var uploadBandwidth = dataGBBytes / (uploadStart.ElapsedMilliseconds / 1000.0);
                var downloadBandwidth = dataGBBytes / (downloadStart.ElapsedMilliseconds / 1000.0);

                Console.WriteLine($"   Data size: {dataGBBytes * 1024:F1} MB");
                Console.WriteLine($"   Upload: {uploadStart.ElapsedMilliseconds}ms ({uploadBandwidth:F2} GB/s)");
                Console.WriteLine($"   Download: {downloadStart.ElapsedMilliseconds}ms ({downloadBandwidth:F2} GB/s)");
            }
        }

        #endregion

        #region Utility Methods

        static float[] GenerateRandomArray(int size, float min, float max, int seed)
        {
            var random = new Random(seed);
            var array = new float[size];
            for (int i = 0; i < size; i++)
            {
                array[i] = min + (float)random.NextDouble() * (max - min);
            }
            return array;
        }

        static float[,] GenerateMatrix(int rows, int cols)
        {
            var matrix = new float[rows, cols];
            var random = new Random(42);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    matrix[i, j] = (float)random.NextDouble();
                }
            }

            return matrix;
        }

        #endregion
    }

    // Extension method to check if algorithms are available
    public static class ContextExtensions
    {
        public static bool HasAlgorithms(this Context context)
        {
            try
            {
                // Try to create a simple kernel that uses XMath.Log to test if algorithms are available
                using (var testAccelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context))
                {
                    var testKernel = testAccelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>>(
                        (index, input, output) => output[index] = XMath.Log(input[index])
                    );
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}