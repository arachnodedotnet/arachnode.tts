using System;
using System.Diagnostics;
using System.IO;
using System.Linq; // Added for assembly inspection

namespace Trade
{
    internal class ConsoleUtilities
    {
        #region Private Fields

        private static DateTime? _lastWriteTime;
        private static readonly Process _currentProcess = Process.GetCurrentProcess();
        private static TextWriter _logWriter;
        private static string _logFilePath;
        private static DateTime _startTime; // Track process start for runtime minutes prefix
        private static readonly bool _isTestHost; // Cached detection of test host
        private static readonly bool _isDebuggingTest; // Cached detection of debugging test

        #endregion

        #region Public Properties

        public static bool Enabled { get; set; } = true;
        public static bool ShowMemoryUsage { get; set; } = true;
        public static bool LogToFile { get; set; } = false;
        public static string LogFilePath
        {
            get => _logFilePath ?? "ConsoleOutput.txt";
            set => _logFilePath = value;
        }

        /// <summary>
        /// Optional external writer (e.g., MSTest TestContext.WriteLine) that, when set, receives all log lines.
        /// Tests can set this in [TestInitialize] without creating a direct dependency here.
        /// </summary>
        public static Action<string> ExternalWriter { get; set; }

        /// <summary>
        /// True if current process appears to be a test host (vstest / testhost).
        /// </summary>
        public static bool IsTestHost => _isTestHost;

        /// <summary>
        /// True if a debugger is attached AND this is a test host (used to route output to Debug window).
        /// </summary>
        public static bool IsDebuggingTest => _isDebuggingTest;

        #endregion

        #region Static Constructor

        static ConsoleUtilities()
        {
            _startTime = DateTime.Now;
            _isTestHost = DetectTestHostProcess();
            _isDebuggingTest = Debugger.IsAttached && _isTestHost;
            InitializeLogging();
        }

        #endregion

        #region Public Methods

        public static void WriteLine(string message, ConsoleColor? color = null)
        {
            if (!Enabled) return;
            var fullMessage = GetFullPrefix() + message;
            RouteWrite(fullMessage, addNewLine: true, color: color);
            LogToFileIfEnabled(fullMessage);
            UpdateLastWriteTime();
        }

        public static void WriteLine(int message, ConsoleColor? color = null)
        {
            if (!Enabled) return;
            var fullMessage = GetFullPrefix() + message;
            RouteWrite(fullMessage, addNewLine: true, color: color);
            LogToFileIfEnabled(fullMessage);
            UpdateLastWriteTime();
        }

        public static void WriteLine()
        {
            if (!Enabled) return;
            var fullMessage = GetFullPrefix();
            RouteWrite(fullMessage, addNewLine: true, color: null);
            LogToFileIfEnabled(fullMessage);
            UpdateLastWriteTime();
        }

        public static void Write(string message, ConsoleColor? color = null)
        {
            if (!Enabled) return;
            RouteWrite(message, addNewLine: false, color: color, prefix: false);
            LogToFileIfEnabled(message, false);
            // Not updating last write time for partial writes to avoid noisy deltas.
        }

        public static void Write(char character, ConsoleColor? color = null)
        {
            if (!Enabled) return;
            RouteWrite(character.ToString(), addNewLine: false, color: color, prefix: false);
            LogToFileIfEnabled(character.ToString(), false);
        }

        /// <summary>
        /// Register an external writer (e.g., MSTest TestContext.WriteLine) to mirror all output.
        /// </summary>
        public static void RegisterExternalWriter(Action<string> writer)
        {
            ExternalWriter = writer;
        }

        public static string GetDetailedMemoryInfo()
        {
            try
            {
                _currentProcess.Refresh();
                var workingSet = _currentProcess.WorkingSet64;
                var privateMemory = _currentProcess.PrivateMemorySize64;
                var virtualMemory = _currentProcess.VirtualMemorySize64;
                return $"Memory Usage: Working Set: {workingSet / (1024.0 * 1024.0):F1}MB, Private: {privateMemory / (1024.0 * 1024.0):F1}MB, Virtual: {virtualMemory / (1024.0 * 1024.0):F1}MB";
            }
            catch (Exception exception)
            {
                return $"Memory Usage: Error retrieving memory info - {exception.Message}";
            }
        }

        public static void WriteMemoryInfo(ConsoleColor? color = ConsoleColor.Cyan)
        {
            WriteLine(GetDetailedMemoryInfo(), color);
        }

        public static void WriteGCInfo(ConsoleColor? color = ConsoleColor.Yellow)
        {
            if (!Enabled) return;
            var beforeMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            WriteLine($"GC: Before collection - Managed Memory: {beforeMemory:F1}MB", color);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var afterMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            _currentProcess.Refresh();
            var workingSet = _currentProcess.WorkingSet64 / (1024.0 * 1024.0);
            WriteLine($"GC: After collection - Managed Memory: {afterMemory:F1}MB, Working Set: {workingSet:F1}MB, Freed: {beforeMemory - afterMemory:F1}MB", color);
        }

        public static void EnableFileLogging(string filePath = null)
        {
            if (!string.IsNullOrEmpty(filePath)) LogFilePath = filePath;
            LogToFile = true;
            InitializeLogging();
        }

        public static void DisableFileLogging()
        {
            LogToFile = false;
            CloseLogFile();
        }

        public static void FlushLog()
        {
            try { _logWriter?.Flush(); } catch { }
        }

        public static void ResetConsoleColors()
        {
            try { Console.ResetColor(); } catch { }
        }

        #endregion

        #region Private Helper Methods

        private static void RouteWrite(string message, bool addNewLine, ConsoleColor? color, bool prefix = true)
        {
            // If an external writer (TestContext) is registered, use it first (always gets full line form)
            if (addNewLine && ExternalWriter != null)
            {
                try { ExternalWriter(message); } catch { }
            }

            // If debugging a test, prefer Debug.WriteLine (cheap + visible in Output window)
            if (_isDebuggingTest)
            {
                if (addNewLine) Debug.WriteLine(message); else Debug.Write(message);
                return; // Avoid double-writing to Console for cleaner test output
            }

            // Fallback: write to console with color support
            var originalColor = ConsoleColor.White;
            try
            {
                if (color.HasValue) Console.ForegroundColor = color.Value;
                if (addNewLine) Console.WriteLine(message); else Console.Write(message);
            }
            finally
            {
                try { Console.ForegroundColor = originalColor; } catch { }
            }
        }

        private static void UpdateLastWriteTime() => _lastWriteTime = DateTime.Now;

        private static string GetTimeDeltaPrefix()
        {
            if (_lastWriteTime == null) return "[Initial] ";
            var elapsed = DateTime.Now - _lastWriteTime.Value;
            return $"[+{elapsed.TotalMilliseconds:F0}ms] ";
        }

        private static string GetRunTimePrefix()
        {
            var minutes = (DateTime.Now - _startTime).TotalMinutes;
            return $"[{minutes:F2} mins] ";
        }

        private static string GetMemoryUsagePrefix()
        {
            if (!ShowMemoryUsage) return "";
            try
            {
                _currentProcess.Refresh();
                var workingSetBytes = _currentProcess.WorkingSet64;
                var workingSetMB = workingSetBytes / (1024.0 * 1024.0);
                return $"[RAM:{workingSetMB:F1}MB] ";
            }
            catch { return "[RAM:N/A] "; }
        }

        private static string GetFullPrefix() => GetRunTimePrefix() + GetTimeDeltaPrefix() + GetMemoryUsagePrefix();

        public static void InitializeLogging()
        {
            if (!LogToFile) return;
            try
            {
                CloseLogFile();
                if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
                var fileStream = new FileStream(LogFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _logWriter = new StreamWriter(fileStream) { AutoFlush = true };
                _logWriter.WriteLine($"=== Console Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            }
            catch
            {
                LogToFile = false;
                _logWriter = null;
            }
        }

        public static void CloseLogFile()
        {
            try { _logWriter?.Dispose(); } catch { }
            finally { _logWriter = null; }
        }

        private static void LogToFileIfEnabled(string message, bool addNewLine = true)
        {
            if (!LogToFile || _logWriter == null) return;
            try
            {
                if (addNewLine) _logWriter.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
                else _logWriter.Write(message);
            }
            catch
            {
                LogToFile = false;
                CloseLogFile();
            }
        }

        private static bool DetectTestHostProcess()
        {
            try
            {
                var name = _currentProcess.ProcessName;
                if (name.StartsWith("testhost", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("vstest", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            catch { }

            // Environment variable set by vstest
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSTEST_CURRENT_TEST_NAME")))
                return true;

            // Assembly heuristic (cached once)
            try
            {
                return AppDomain.CurrentDomain.GetAssemblies().Any(a =>
                    a.FullName.StartsWith("Microsoft.VisualStudio.TestPlatform", StringComparison.OrdinalIgnoreCase) ||
                    a.FullName.StartsWith("Microsoft.VisualStudio.QualityTools", StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        #endregion
    }
}