using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace Pfuma.Helpers
{
    /// <summary>
    /// Helper class for timeframe parsing and validation
    /// </summary>
    public static class TimeframeHelper
    {
        private static readonly Dictionary<string, TimeFrame> TimeframeMap = new()
        {
            {"M1", TimeFrame.Minute },
            {"M2", TimeFrame.Minute2 },
            {"M3", TimeFrame.Minute3 },
            {"M4", TimeFrame.Minute4 },
            {"M5", TimeFrame.Minute5 },
            {"M6", TimeFrame.Minute6 },
            {"M7", TimeFrame.Minute7 },
            {"M8", TimeFrame.Minute8 },
            {"M9", TimeFrame.Minute9 },
            {"M10", TimeFrame.Minute10 },
            {"M15", TimeFrame.Minute15 },
            {"M20", TimeFrame.Minute20 },
            {"M30", TimeFrame.Minute30 },
            {"M45", TimeFrame.Minute45 },
            {"H1", TimeFrame.Hour },
            {"H2", TimeFrame.Hour2 },
            {"H3", TimeFrame.Hour3 },
            {"H4", TimeFrame.Hour4 },
            {"H6", TimeFrame.Hour6 },
            {"H8", TimeFrame.Hour8 },
            {"H12", TimeFrame.Hour12 },
            {"D1", TimeFrame.Daily },
            {"W1", TimeFrame.Weekly },
            {"MN1", TimeFrame.Monthly }
        };

        /// <summary>
        /// Parse comma-separated timeframe string into list of TimeFrame objects
        /// </summary>
        public static List<TimeFrame> ParseTimeframes(string timeframesString)
        {
            if (string.IsNullOrWhiteSpace(timeframesString))
                return new List<TimeFrame>();

            var timeframes = new List<TimeFrame>();
            var parts = timeframesString.Split(',')
                .Select(s => s.Trim().ToUpper())
                .Where(s => !string.IsNullOrEmpty(s));

            foreach (var part in parts)
            {
                if (TimeframeMap.TryGetValue(part, out var timeframe))
                {
                    timeframes.Add(timeframe);
                }
            }

            return timeframes;
        }

        /// <summary>
        /// Get timeframe in minutes
        /// </summary>
        public static int GetTimeframeMinutes(TimeFrame timeframe)
        {
            if (timeframe == TimeFrame.Minute) return 1;
            if (timeframe == TimeFrame.Minute2) return 2;
            if (timeframe == TimeFrame.Minute3) return 3;
            if (timeframe == TimeFrame.Minute4) return 4;
            if (timeframe == TimeFrame.Minute5) return 5;
            if (timeframe == TimeFrame.Minute6) return 6;
            if (timeframe == TimeFrame.Minute7) return 7;
            if (timeframe == TimeFrame.Minute8) return 8;
            if (timeframe == TimeFrame.Minute9) return 9;
            if (timeframe == TimeFrame.Minute10) return 10;
            if (timeframe == TimeFrame.Minute15) return 15;
            if (timeframe == TimeFrame.Minute20) return 20;
            if (timeframe == TimeFrame.Minute30) return 30;
            if (timeframe == TimeFrame.Minute45) return 45;
            if (timeframe == TimeFrame.Hour) return 60;
            if (timeframe == TimeFrame.Hour2) return 120;
            if (timeframe == TimeFrame.Hour3) return 180;
            if (timeframe == TimeFrame.Hour4) return 240;
            if (timeframe == TimeFrame.Hour6) return 360;
            if (timeframe == TimeFrame.Hour8) return 480;
            if (timeframe == TimeFrame.Hour12) return 720;
            if (timeframe == TimeFrame.Daily) return 1440;
            if (timeframe == TimeFrame.Weekly) return 10080;
            if (timeframe == TimeFrame.Monthly) return 43200; // Approximate

            return 0;
        }

        /// <summary>
        /// Check if a higher timeframe is a valid multiple of the current timeframe
        /// </summary>
        public static bool IsValidMultiple(TimeFrame currentTf, TimeFrame higherTf)
        {
            var currentMinutes = GetTimeframeMinutes(currentTf);
            var higherMinutes = GetTimeframeMinutes(higherTf);

            if (currentMinutes == 0 || higherMinutes == 0)
                return false;

            // Higher timeframe must be larger than current
            if (higherMinutes <= currentMinutes)
                return false;

            // Check if it's a clean multiple
            return higherMinutes % currentMinutes == 0;
        }

        /// <summary>
        /// Filter timeframes to only include valid multiples of the current timeframe
        /// </summary>
        public static List<TimeFrame> FilterValidTimeframes(TimeFrame currentTf, List<TimeFrame> timeframes)
        {
            return timeframes
                .Where(tf => IsValidMultiple(currentTf, tf))
                .OrderBy(tf => GetTimeframeMinutes(tf))
                .ToList();
        }

        /// <summary>
        /// Get the period start time for a given timeframe
        /// </summary>
        public static DateTime GetPeriodStartTime(DateTime time, TimeFrame timeframe)
        {
            var minutes = GetTimeframeMinutes(timeframe);
            
            if (minutes <= 0)
                return time;

            if (minutes < 1440) // Less than daily
            {
                var totalMinutes = (int)time.TimeOfDay.TotalMinutes;
                var periodStartMinutes = (totalMinutes / minutes) * minutes;
                return time.Date.AddMinutes(periodStartMinutes);
            }
            else if (minutes == 1440) // Daily
            {
                return time.Date;
            }
            else if (minutes == 10080) // Weekly
            {
                var daysFromMonday = ((int)time.DayOfWeek + 6) % 7;
                return time.Date.AddDays(-daysFromMonday);
            }
            else // Monthly
            {
                return new DateTime(time.Year, time.Month, 1);
            }
        }

        /// <summary>
        /// Check if current time is the start of a new period for the given timeframe
        /// </summary>
        public static bool IsNewPeriod(DateTime currentTime, DateTime previousTime, TimeFrame timeframe)
        {
            var currentPeriod = GetPeriodStartTime(currentTime, timeframe);
            var previousPeriod = GetPeriodStartTime(previousTime, timeframe);
            
            // Special handling for session gaps (16:59 to 18:00)
            if (HasSessionGap(previousTime, currentTime))
            {
                return ShouldCreatePeriodAfterGap(currentTime, timeframe);
            }
            
            return currentPeriod > previousPeriod;
        }
        
        /// <summary>
        /// Check if there's a trading session gap between two times
        /// </summary>
        public static bool HasSessionGap(DateTime previousTime, DateTime currentTime)
        {
            var timeDiff = currentTime - previousTime;
            
            // If more than 1.5 hours gap, consider it a session break
            if (timeDiff.TotalHours > 1.5)
            {
                // Additional check: previous time around 16:xx-17:xx and current time around 17:xx-18:xx
                var prevHour = previousTime.Hour;
                var currHour = currentTime.Hour;
                
                return (prevHour >= 16 && prevHour <= 17) && (currHour >= 17 && currHour <= 19);
            }
            
            return false;
        }
        
        /// <summary>
        /// Determine if we should create a period after a session gap
        /// </summary>
        private static bool ShouldCreatePeriodAfterGap(DateTime currentTime, TimeFrame timeframe)
        {
            var minutes = GetTimeframeMinutes(timeframe);
            
            // For daily timeframes, always create at market open (18:00/17:00)
            if (minutes >= 1440)
            {
                return currentTime.Hour >= 17 && currentTime.Hour <= 18;
            }
            
            // For intraday timeframes, check if we're at a timeframe boundary
            if (minutes <= 0) return false;
            
            if (minutes < 60) // Minutes timeframes
            {
                // Check if current time aligns with timeframe boundary
                var totalMinutes = (int)currentTime.TimeOfDay.TotalMinutes;
                return totalMinutes % minutes == 0;
            }
            else // Hourly timeframes
            {
                var hours = minutes / 60;
                return currentTime.Hour % hours == 0 && currentTime.Minute == 0;
            }
        }
    }
}