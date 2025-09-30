using System;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Service for detecting 369 time patterns based on swing point candle open time
    /// </summary>
    public class Pattern369Service
    {
        private readonly int _utcOffset;

        /// <summary>
        /// Initialize the Pattern369Service with UTC offset
        /// </summary>
        /// <param name="utcOffset">The UTC offset to apply for local time calculations</param>
        public Pattern369Service(int utcOffset = 0)
        {
            _utcOffset = utcOffset;
        }

        /// <summary>
        /// Detect 369 pattern for a swing point based on its time
        /// </summary>
        /// <param name="swingPoint">The swing point to analyze</param>
        /// <returns>Tuple indicating if 369 pattern exists and the resulting number</returns>
        public (bool Has369, int? Number369) DetectPattern(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return (false, null);

            // Use the swing point's direct Time property instead of Bar.Time
            // This ensures we use the exact time when the swing point was detected
            return Calculate369Pattern(swingPoint.Time);
        }

        /// <summary>
        /// Calculate 369 pattern for a given time
        /// </summary>
        /// <param name="time">The candle open time (in UTC)</param>
        /// <returns>Tuple indicating if 369 pattern exists and the resulting number</returns>
        public (bool Has369, int? Number369) Calculate369Pattern(DateTime time)
        {
            // Apply UTC offset to get local market time
            DateTime localTime = time.AddHours(_utcOffset);

            // Extract time components (hours and minutes) from local time
            int hours = localTime.Hour;
            int minutes = localTime.Minute;

            // Add all digits together
            int sum = AddDigits(hours) + AddDigits(minutes);

            // Reduce to single digit
            int result = ReduceToSingleDigit(sum);

            // Check if result is 3, 6, or 9
            bool has369 = result == 3 || result == 6 || result == 9;

            return (has369, has369 ? result : null);
        }

        /// <summary>
        /// Add individual digits of a number
        /// </summary>
        /// <param name="number">The number to process</param>
        /// <returns>Sum of individual digits</returns>
        private int AddDigits(int number)
        {
            int sum = 0;
            while (number > 0)
            {
                sum += number % 10;
                number /= 10;
            }
            return sum;
        }

        /// <summary>
        /// Reduce a number to single digit by repeatedly adding its digits
        /// </summary>
        /// <param name="number">The number to reduce</param>
        /// <returns>Single digit result</returns>
        private int ReduceToSingleDigit(int number)
        {
            while (number >= 10)
            {
                number = AddDigits(number);
            }
            return number;
        }
    }
}