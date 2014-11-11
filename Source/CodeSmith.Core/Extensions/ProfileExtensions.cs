using System;
using System.Globalization;
using System.Diagnostics;
using Exceptionless.DateTimeExtensions;

namespace CodeSmith.Core.Extensions
{
    /// <summary>
    /// Helper class to print out performance related data like number of runs, elapsed time and frequency
    /// </summary>
    public static class ProfileExtensions
    {
        static NumberFormatInfo _myNumberFormat;

        static NumberFormatInfo NumberFormat
        {
            get
            {
                if (_myNumberFormat == null)
                {
                    var local = CultureInfo.CurrentCulture.NumberFormat;
                    _myNumberFormat = local; // make a thread safe assignment with a fully initialized variable
                }
                return _myNumberFormat;
            }
        }

        private const string DefaultFormat = "Executed {runs} in {time} ({frequency}/s).";

        private static void ProfileInternal(Func<int> func, string format = DefaultFormat)
        {
            Stopwatch watch = Stopwatch.StartNew();
            int runs = func();  // Execute function and get number of iterations back
            watch.Stop();

            string replacedFormat = format.Replace("{runs}", "{3}")
                                          .Replace("{time}", "{4}")
                                          .Replace("{frequency}", "{5}");

            // get elapsed time back
            string time = watch.Elapsed.ToWords(true);
            float sec = watch.ElapsedMilliseconds / 1000.0f;
            float frequency = runs / sec; // calculate frequency of the operation in question

            try
            {
                Console.WriteLine(replacedFormat,
                                    runs,  // {0} is the number of runs
                                    time,   // {1} is the elapsed time as float
                                    frequency, // {2} is the call frequency as float
                                    runs.ToString("N0", NumberFormat),  // Expanded token {runs} is formatted with thousand separators
                                    time,   // expanded token {time} is formatted as float in seconds with two digits precision
                                    frequency.ToString("N0", NumberFormat)); // expanded token {frequency} is formatted as float with thousands separators
            }
            catch (FormatException ex)
            {
                throw new FormatException(
                    String.Format("The input string format string did contain not an expected token like " +
                                  "{{runs}}/{{0}}, {{time}}/{{1}} or {{frequency}}/{{2}} or the format string " +
                                  "itself was invalid: \"{0}\"", format), ex);
            }
        }

        /// <summary>
        /// Execute the given function n-times and print the timing values (number of runs, elapsed time, call frequency)
        /// to the console window.
        /// </summary>
        /// <param name="func">Function to call in a for loop.</param>
        /// <param name="runs">Number of iterations.</param>
        /// <param name="format">Format string which can contain {runs} or {0},{time} or {1} and {frequency} or {2}.</param>
        public static void ProfileConcurrently(this Action func, int runs, string format = DefaultFormat)
        {
#if !PFX_LEGACY_3_5
            Func<int> f = () =>
            {
                System.Threading.Tasks.Parallel.For(0, runs, i => func());
                return runs;
            };
            ProfileInternal(f, format);
#endif
        }

        /// <summary>
        /// Execute the given function n-times and print the timing values (number of runs, elapsed time, call frequency)
        /// to the console window.
        /// </summary>
        /// <param name="func">Function to call in a for loop.</param>
        /// <param name="runs">Number of iterations.</param>
        /// <param name="format">Format string which can contain {runs} or {0},{time} or {1} and {frequency} or {2}.</param>
        public static void Profile(this Action func, int runs, string format = DefaultFormat)
        {
            Func<int> f = () =>
            {
                for (int i = 0; i < runs; i++ )
                    func();

                return runs;
            };
            ProfileInternal(f, format);
        }

        /// <summary>
        /// Call a function in a for loop n-times. The first function call will be measured independently to measure
        /// first call effects.
        /// </summary>
        /// <param name="func">Function to call in a loop.</param>
        /// <param name="runs">Number of iterations.</param>
        /// <param name="format">Format string for first function call performance.</param>
        /// <remarks>
        /// The format string can contain {runs} or {0},{time} or {1} and {frequency} or {2}.
        /// </remarks>
        public static void ProfileWithWarmup(this Action func, int runs, string format = DefaultFormat)
        {
            func();
            func.Profile(runs, format);
        }

        /// <summary>
        /// Call a function in a for loop n-times. The first function call will be measured independently to measure
        /// first call effects.
        /// </summary>
        /// <param name="func">Function to call in a loop.</param>
        /// <param name="runs">Number of iterations.</param>
        /// <param name="format">Format string for first function call performance.</param>
        /// <remarks>
        /// The format string can contain {runs} or {0},{time} or {1} and {frequency} or {2}.
        /// </remarks>
        public static void ProfileConcurrentlyWithWarmup(this Action func, int runs, string format = DefaultFormat)
        {
            func();
            func.ProfileConcurrently(runs, format);
        }
    }
}