using System;
using System.Diagnostics;

namespace Trade
{
    /// <summary>
    /// Simple performance timing utility compatible with .NET Framework 4.7.2
    /// </summary>
    internal static class PerformanceTimer
    {
        /// <summary>
        /// Times the execution of an action and returns the elapsed milliseconds
        /// </summary>
        public static double TimeAction(Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Times the execution of a function and returns both the result and elapsed milliseconds
        /// </summary>
        public static (T result, double elapsedMs) TimeFunction<T>(Func<T> function)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = function();
            stopwatch.Stop();
            return (result, stopwatch.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// Times multiple executions of an action and returns average time
        /// </summary>
        public static double TimeActionAverage(Action action, int iterations = 100)
        {
            // Warmup
            for (int i = 0; i < Math.Min(10, iterations / 10); i++)
            {
                action();
            }

            var totalTime = 0.0;
            for (int i = 0; i < iterations; i++)
            {
                totalTime += TimeAction(action);
            }
            return totalTime / iterations;
        }

        /// <summary>
        /// Times an action and outputs the result to ConsoleUtilities
        /// </summary>
        public static void TimeAndLog(string description, Action action, ConsoleColor? color = null)
        {
            var elapsed = TimeAction(action);
            ConsoleUtilities.WriteLine($"[TIMER] {description}: {elapsed:F2}ms", color);
        }

        /// <summary>
        /// Times an action with multiple iterations and outputs average to ConsoleUtilities
        /// </summary>
        public static void TimeAndLogAverage(string description, Action action, int iterations = 100, ConsoleColor? color = null)
        {
            var averageElapsed = TimeActionAverage(action, iterations);
            ConsoleUtilities.WriteLine($"[TIMER] {description} (avg of {iterations}): {averageElapsed:F3}ms", color);
        }
    }
}