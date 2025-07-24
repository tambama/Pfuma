using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Extensions;
using Pfuma.Models;
using FibonacciLevel = Pfuma.Models.FibonacciLevel;

namespace Pfuma.Services
{
    /// <summary>
    /// Manages time-related functionality, including ICT macro time periods, 
    /// daily high/low levels, session high/low levels, and Fibonacci levels
    /// </summary>
    public class TimeManager
    {
        private readonly Chart _chart;
        private readonly List<TimeRange> _macros;
        private readonly bool _showMacros;
        private readonly int _utcOffset;
        private readonly Bars _bars;
        private readonly SwingPointDetector _swingPointDetector;

        // Fibonacci-related fields
        private readonly List<FibonacciLevel> _fibonacciLevels = new List<FibonacciLevel>();
        private readonly bool _showFibLevels;

        // Keep track of which macro times we've already drawn lines for to avoid duplication
        private readonly HashSet<string> _drawnMacroTimes = new HashSet<string>();

        // Daily level tracking
        private DateTime _lastDayStartTime = DateTime.MinValue;
        private bool _processingDailyLevels = false;

        // Session trackers
        private readonly Dictionary<SessionType, SessionTracker> _sessionTrackers;
        private SessionType _lastProcessedSession = SessionType.None;

        // Dictionary to map hour to session
        private readonly Dictionary<int, SessionType> _hourToSessionMap;
        
        // Notifications Service
        private readonly NotificationService _notificationService;

        /// <summary>
        /// Creates a new instance of the TimeManager
        /// </summary>
        public TimeManager(
            Chart chart,
            Bars bars,
            SwingPointDetector swingPointDetector,
            NotificationService notificationService,
            bool showMacros = true,
            bool showFibLevels = false,
            int utcOffset = -4)
        {
            _chart = chart;
            _showMacros = showMacros;
            _showFibLevels = showFibLevels;
            _utcOffset = utcOffset;
            _macros = InitializeMacros();
            _bars = bars;
            _swingPointDetector = swingPointDetector;
            _notificationService = notificationService;

            _swingPointDetector.LiquiditySwept += OnLiquiditySwept;

            // Initialize session trackers
            _sessionTrackers = new Dictionary<SessionType, SessionTracker>();
            foreach (SessionType sessionType in Enum.GetValues(typeof(SessionType)))
            {
                if (sessionType != SessionType.None)
                {
                    _sessionTrackers[sessionType] = new SessionTracker();
                }
            }

            // Initialize hour to session map based on provided session times
            _hourToSessionMap = new Dictionary<int, SessionType>
            {
                // Asia: 18:00 - 23:59
                { 18, SessionType.Asia },
                { 19, SessionType.Asia },
                { 20, SessionType.Asia },
                { 21, SessionType.Asia },
                { 22, SessionType.Asia },
                { 23, SessionType.Asia },

                // London Pre: 00:00 - 00:59
                { 0, SessionType.LondonPre },

                // London: 01:00 - 04:59
                { 1, SessionType.London },
                { 2, SessionType.London },
                { 3, SessionType.London },
                { 4, SessionType.London },

                // London Lunch: 05:00 - 06:59
                { 5, SessionType.LondonLunch },
                { 6, SessionType.LondonLunch },

                // New York Pre: 07:00 - 09:29
                { 7, SessionType.NewYorkPre },
                { 8, SessionType.NewYorkPre },
                { 9, SessionType.NewYorkPre }, // Note: This will be refined by minute check

                // New York AM: 09:30 - 11:29
                // (9 is handled in GetCurrentSession with minute check)
                { 10, SessionType.NewYorkAM },
                { 11, SessionType.NewYorkAM }, // Note: This will be refined by minute check

                // New York Lunch: 11:30 - 13:29
                // (11 is handled in GetCurrentSession with minute check)
                { 12, SessionType.NewYorkLunch },
                { 13, SessionType.NewYorkLunch }, // Note: This will be refined by minute check

                // New York PM Pre: 13:30 - 14:29
                // (13 is handled in GetCurrentSession with minute check)
                { 14, SessionType.NewYorkPMPre }, // Note: This will be refined by minute check

                // New York PM: 14:30 - 17:59
                // (14 is handled in GetCurrentSession with minute check)
                { 15, SessionType.NewYorkPM },
                { 16, SessionType.NewYorkPM },
                { 17, SessionType.NewYorkPM }
            };
        }

        // Add the event handler method
        private void OnLiquiditySwept(SwingPoint sweptPoint, int sweepingCandleIndex, Candle sweepingCandle)
        {
            if (_chart == null)
                return;

            // Get the label based on the liquidity name
            string label = GetLiquidityLabel(sweptPoint);
            if (string.IsNullOrEmpty(label))
                return;

            // Create unique IDs for the original and extended lines
            string originalLineId = $"{label.ToLower()}-{sweptPoint.Time.Ticks}";
            string extendedLineId =
                $"{label.ToLower()}-extended-{sweptPoint.Time.Ticks}-{sweepingCandle.Time.Ticks}";

            // Remove the original line
            _chart.RemoveObject(originalLineId);

            // Draw the extended line
            _chart.DrawStraightLine(
                extendedLineId,
                sweptPoint.Time,
                sweptPoint.Price,
                sweepingCandle.Time,
                sweptPoint.Price,
                label, // Use the same label when extending
                LineStyle.Solid,
                Color.FromArgb(40, Color.Red),
                true, // Show label
                true, // Remove existing
                labelOnRight: true
            );
        }

        // Helper method to get a label from liquidity type
        private string GetLiquidityLabel(SwingPoint sweptPoint)
        {
            // Use the LiquidityName directly as it's already the short code we want
            return sweptPoint.LiquidityName.ToString();
        }

        /// <summary>
        /// Initializes the list of ICT macro time ranges
        /// </summary>
        public static List<TimeRange> InitializeMacros()
        {
            var macros = new List<TimeRange>
            {
                new TimeRange(new TimeSpan(1, 50, 0), new TimeSpan(2, 10, 0)),
                new TimeRange(new TimeSpan(2, 50, 0), new TimeSpan(3, 10, 0)),
                new TimeRange(new TimeSpan(3, 50, 0), new TimeSpan(4, 10, 0)),
                new TimeRange(new TimeSpan(4, 50, 0), new TimeSpan(5, 10, 0)),
                new TimeRange(new TimeSpan(5, 50, 0), new TimeSpan(6, 10, 0)),
                new TimeRange(new TimeSpan(6, 50, 0), new TimeSpan(7, 10, 0)),
                new TimeRange(new TimeSpan(7, 50, 0), new TimeSpan(8, 10, 0)),
                new TimeRange(new TimeSpan(8, 50, 0), new TimeSpan(9, 10, 0)),
                new TimeRange(new TimeSpan(9, 50, 0), new TimeSpan(10, 10, 0)),
                new TimeRange(new TimeSpan(10, 50, 0), new TimeSpan(11, 10, 0)),
                new TimeRange(new TimeSpan(11, 50, 0), new TimeSpan(12, 10, 0)),
                new TimeRange(new TimeSpan(12, 50, 0), new TimeSpan(13, 10, 0)),
                new TimeRange(new TimeSpan(13, 50, 0), new TimeSpan(14, 10, 0)),
                new TimeRange(new TimeSpan(14, 50, 0), new TimeSpan(15, 10, 0)),
                new TimeRange(new TimeSpan(15, 45, 0), new TimeSpan(16, 00, 0))
            };

            return macros;
        }

        /// <summary>
        /// Process a bar to check if it matches the start time of any macro, session boundary, or daily boundary
        /// </summary>
        public void ProcessBar(int index, DateTime time)
        {
            if (_chart == null || _bars == null || index >= _bars.Count)
                return;

            // Adjust time for UTC offset to get the market time
            DateTime marketTime = time.AddHours(_utcOffset);
            DateTime dateOnly = marketTime.Date;
            TimeSpan timeOfDay = marketTime.TimeOfDay;

            // Process macro times if enabled
            if (_showMacros)
            {
                // Check if this bar's market time matches any macro start time
                foreach (var macro in _macros)
                {
                    // Create a unique identifier for this macro on this date
                    string macroKey = $"{dateOnly:yyyyMMdd}-{macro.StartTime.Hours:D2}{macro.StartTime.Minutes:D2}";

                    // See if this bar's market time is close to a macro start time (within 1 minute)
                    bool closeToMacroStart = Math.Abs((timeOfDay - macro.StartTime).TotalMinutes) < 1;

                    // If this is a macro start time and we haven't drawn it yet
                    if (closeToMacroStart && !_drawnMacroTimes.Contains(macroKey))
                    {
                        // Draw both the start and end lines for this macro
                        DrawMacroLine(time, "start");

                        // Calculate the end time in chart time
                        TimeSpan macroDuration = macro.EndTime - macro.StartTime;
                        DateTime endTime = time.Add(macroDuration);

                        DrawMacroLine(endTime, "end");

                        // Mark this macro as drawn
                        _drawnMacroTimes.Add(macroKey);
                        
                        // Send notification for macro time entry - ALWAYS send regardless of notification settings
                        if (_notificationService != null)
                        {
                            _notificationService.NotifyMacroTimeEntered(time);
                        }
                    }
                }
            }

            // Handle daily boundaries (18:00)
            if (marketTime.Hour == 18 && marketTime.Minute == 0)
            {
                ProcessDailyLevels(index);
            }

            // Session tracking
            SessionType currentBarSession = GetCurrentSession(marketTime);

            // Handle session transitions
            if (_lastProcessedSession != currentBarSession && _lastProcessedSession != SessionType.None)
            {
                // We've transitioned to a new session, process the previous one
                ProcessSessionLevels(index, _lastProcessedSession);
            }

            // Update session high/low tracking
            if (currentBarSession != SessionType.None)
            {
                UpdateSessionTracker(currentBarSession, index, _bars[index].High, _bars[index].Low);
            }

            // Store the current session for the next bar
            _lastProcessedSession = currentBarSession;
        }

        /// <summary>
        /// Draws a vertical line on the chart for a macro time
        /// </summary>
        private void DrawMacroLine(DateTime time, string lineType)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this macro line, including whether it's a start or end line
            string id = $"macro-{lineType}-{time.Ticks}";

            // Draw a dotted light gray vertical line
            _chart.DrawVerticalLine(id, time, Color.Gray, 1, LineStyle.DotsRare);
        }

        /// <summary>
        /// Determines the current trading session based on market time
        /// </summary>
        private SessionType GetCurrentSession(DateTime marketTime)
        {
            int hour = marketTime.Hour;
            int minute = marketTime.Minute;

            // Handle special cases with minute checks
            if (hour == 9)
            {
                return minute < 30 ? SessionType.NewYorkPre : SessionType.NewYorkAM;
            }
            else if (hour == 11)
            {
                return minute < 30 ? SessionType.NewYorkAM : SessionType.NewYorkLunch;
            }
            else if (hour == 13)
            {
                return minute < 30 ? SessionType.NewYorkLunch : SessionType.NewYorkPMPre;
            }
            else if (hour == 14)
            {
                return minute < 30 ? SessionType.NewYorkPMPre : SessionType.NewYorkPM;
            }

            // Use the hour mapping for standard hours
            if (_hourToSessionMap.TryGetValue(hour, out SessionType session))
            {
                return session;
            }

            // Default case
            return SessionType.None;
        }

        /// <summary>
        /// Updates session high/low tracking for the current bar
        /// </summary>
        private void UpdateSessionTracker(SessionType session, int index, double high, double low)
        {
            // Skip if not a valid session
            if (session == SessionType.None)
                return;

            // Get tracker for this session
            var tracker = _sessionTrackers[session];

            // If this is the first bar of this session, store the start time
            if (tracker.StartTime == DateTime.MinValue)
                tracker.StartTime = _bars[index].OpenTime;

            // Always update the end time to the current bar
            tracker.EndTime = _bars[index].OpenTime;

            // Update high/low tracking
            if (high > tracker.High)
            {
                tracker.High = high;
                tracker.HighIndex = index;
                tracker.HighTime = _bars[index].OpenTime;
            }

            if (low < tracker.Low)
            {
                tracker.Low = low;
                tracker.LowIndex = index;
                tracker.LowTime = _bars[index].OpenTime;
            }
        }

        /// <summary>
        /// Processes daily high/low levels at the 18:00 boundary
        /// </summary>
        private void ProcessDailyLevels(int currentIndex)
        {
            // Avoid reentrance
            if (_processingDailyLevels)
                return;

            _processingDailyLevels = true;

            try
            {
                // If this is not the first day we're processing
                if (_lastDayStartTime != DateTime.MinValue)
                {
                    // Calculate day boundaries
                    DateTime dayStart = _lastDayStartTime;
                    DateTime dayEnd = _bars[currentIndex].OpenTime;

                    // Find min/max prices for the previous day
                    var minMax = _bars.GetMinMax(dayStart, dayEnd);

                    // Create candles for high and low
                    var highCandle = new Candle(_bars[minMax.maxIndex], minMax.maxIndex);
                    var lowCandle = new Candle(_bars[minMax.minIndex], minMax.minIndex);

                    // Check if there are already swing points at these indices and update them instead of creating new ones
                    CreateOrUpdateSpecialSwingPoint(
                        minMax.maxIndex,
                        minMax.max,
                        minMax.maxTime,
                        highCandle,
                        SwingType.H,
                        LiquidityType.PDH,
                        Direction.Up,
                        LiquidityName.PDH,
                        dayEnd);

                    CreateOrUpdateSpecialSwingPoint(
                        minMax.minIndex,
                        minMax.min,
                        minMax.minTime,
                        lowCandle,
                        SwingType.L,
                        LiquidityType.PDL,
                        Direction.Down,
                        LiquidityName.PDL,
                        dayEnd);

                    // Clean up Fibonacci levels from previous day if they exist
                    if (_showFibLevels)
                    {
                        // Remove Fibonacci visuals from chart but keep sweep lines
                        foreach (var fib in _fibonacciLevels)
                        {
                            RemoveFibonacciFromChart(fib);
                        }

                        // Explicitly clear the collection at day boundary
                        _fibonacciLevels.Clear();
                    }
                }

                // Update the last day start time
                _lastDayStartTime = _bars[currentIndex].OpenTime;
            }
            finally
            {
                _processingDailyLevels = false;
            }
        }

        /// <summary>
        /// Processes session high/low levels when transitioning between sessions
        /// </summary>
        private void ProcessSessionLevels(int currentIndex, SessionType session)
        {
            // Get the tracker for this session
            var tracker = _sessionTrackers[session];

            // Skip if we don't have valid session data
            if (tracker.HighIndex < 0 || tracker.LowIndex < 0)
                return;

            // Create candles for session high and low
            var highCandle = new Candle(_bars[tracker.HighIndex], tracker.HighIndex);
            var lowCandle = new Candle(_bars[tracker.LowIndex], tracker.LowIndex);

            // Map session type to LiquidityName
            LiquidityName highLiquidityName;
            LiquidityName lowLiquidityName;

            // Set appropriate liquidity names based on session
            switch (session)
            {
                case SessionType.Asia:
                    highLiquidityName = LiquidityName.AH;
                    lowLiquidityName = LiquidityName.AL;
                    break;
                case SessionType.LondonPre:
                    highLiquidityName = LiquidityName.LPH;
                    lowLiquidityName = LiquidityName.LPL;
                    break;
                case SessionType.London:
                    highLiquidityName = LiquidityName.LH;
                    lowLiquidityName = LiquidityName.LL;
                    break;
                case SessionType.LondonLunch:
                    highLiquidityName = LiquidityName.LLH;
                    lowLiquidityName = LiquidityName.LLL;
                    break;
                case SessionType.NewYorkPre:
                    highLiquidityName = LiquidityName.NYPH;
                    lowLiquidityName = LiquidityName.NYPL;
                    break;
                case SessionType.NewYorkAM:
                    highLiquidityName = LiquidityName.NYAMH;
                    lowLiquidityName = LiquidityName.NYAML;
                    break;
                case SessionType.NewYorkLunch:
                    highLiquidityName = LiquidityName.NYLH;
                    lowLiquidityName = LiquidityName.NYLL;
                    break;
                case SessionType.NewYorkPMPre:
                    highLiquidityName = LiquidityName.NYPPH;
                    lowLiquidityName = LiquidityName.NYPPL;
                    break;
                case SessionType.NewYorkPM:
                    highLiquidityName = LiquidityName.NYPMH;
                    lowLiquidityName = LiquidityName.NYPML;
                    break;
                default:
                    highLiquidityName = LiquidityName.N;
                    lowLiquidityName = LiquidityName.N;
                    break;
            }

            // Check if there are already swing points at these indices and update them instead of creating new ones
            CreateOrUpdateSpecialSwingPoint(
                tracker.HighIndex,
                tracker.High,
                tracker.HighTime,
                highCandle,
                SwingType.HH,
                LiquidityType.PSH,
                Direction.Up,
                highLiquidityName,
                tracker.EndTime);

            CreateOrUpdateSpecialSwingPoint(
                tracker.LowIndex,
                tracker.Low,
                tracker.LowTime,
                lowCandle,
                SwingType.LL,
                LiquidityType.PSL,
                Direction.Down,
                lowLiquidityName,
                tracker.EndTime);

            // Calculate Fibonacci levels for this session if enabled
            if (_showFibLevels)
            {
                CalculateFibonacciLevels(tracker, session);
            }

            // Reset the tracker for the next occurrence of this session
            _sessionTrackers[session] = new SessionTracker();
        }

        /// <summary>
        /// Creates a new swing point or updates an existing one at the same index
        /// </summary>
        private void CreateOrUpdateSpecialSwingPoint(
            int index,
            double price,
            DateTime time,
            Candle candle,
            SwingType swingType,
            LiquidityType liquidityType,
            Direction direction,
            LiquidityName liquidityName,
            DateTime endTime)
        {
            if (_swingPointDetector == null)
                return;

            // Check if a swing point already exists at this index
            var existingPoint = _swingPointDetector.GetSwingPointAtIndex(index);

            if (existingPoint != null)
            {
                // If we're applying a PDH or PDL label and the existing point is not already a daily marker,
                // then we need to clean up any existing session labels
                if ((liquidityType == LiquidityType.PDH || liquidityType == LiquidityType.PDL) &&
                    existingPoint.LiquidityType != LiquidityType.PDH &&
                    existingPoint.LiquidityType != LiquidityType.PDL)
                {
                    // Remove existing session lines and labels before updating to daily
                    RemoveExistingSessionLabels(time, price);
                }

                // Update the existing swing point with the new liquidity type and name
                existingPoint.LiquidityType = liquidityType;
                existingPoint.LiquidityName = liquidityName;
            }
            else
            {
                // Create a new swing point with both liquidity type and name
                var swingPoint = new SwingPoint(
                    index,
                    price,
                    time,
                    candle,
                    swingType,
                    liquidityType,
                    direction,
                    liquidityName
                );

                // Add to the swing detector
                _swingPointDetector.AddSpecialSwingPoint(swingPoint);
            }

            // Draw the level line regardless of whether we created or updated the swing point
            if (_chart != null)
            {
                string id = $"{liquidityName.ToString().ToLower()}-{time.Ticks}";

                _chart.DrawStraightLine(
                    id,
                    time,
                    price,
                    endTime,
                    price,
                    liquidityName.ToString(),
                    LineStyle.Solid,
                    Color.Wheat,
                    true, // Show label
                    true // Remove existing
                );
            }
        }

        // Helper method to remove all possible session labels at a given time and price
        private void RemoveExistingSessionLabels(DateTime time, double price)
        {
            if (_chart == null)
                return;

            // Common session label prefixes used in your code
            string[] sessionPrefixes =
            {
                "ah", "al", "lh", "ll", "lph", "lpl", "llh", "lll",
                "nyph", "nypl", "nyamh", "nyaml", "nylh", "nyll",
                "nypph", "nyppl", "nypmh", "nypml", "sh", "sl"
            };

            // Remove each possible session label
            foreach (var prefix in sessionPrefixes)
            {
                string lineId = $"{prefix}-{time.Ticks}";
                string labelId = $"{lineId}-label";

                // Remove both the line and its label
                _chart.RemoveObject(lineId);
                _chart.RemoveObject(labelId);
            }
        }

        /// <summary>
        /// Checks if a given time falls within any macro time period
        /// </summary>
        public bool IsInMacroTime(DateTime time)
        {
            // Adjust time for UTC offset to get the market time
            DateTime marketTime = time.AddHours(_utcOffset);
            TimeSpan timeOfDay = marketTime.TimeOfDay;

            foreach (var macro in _macros)
            {
                if (timeOfDay >= macro.StartTime && timeOfDay <= macro.EndTime)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets all macro time ranges
        /// </summary>
        public List<TimeRange> GetMacros()
        {
            return _macros;
        }

        //////////////////////////////////
        // Fibonacci-related functionality
        //////////////////////////////////

        /// <summary>
        /// Draw Fibonacci levels on the chart
        /// </summary>
        private void DrawFibonacciLevels(FibonacciLevel fib)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this Fibonacci object
            string id = $"fib-{fib.SessionType}-{fib.StartTime.Ticks}";

            // Remove any existing Fibonacci object with this ID
            _chart.RemoveObject(id);

            // Get range for calculation (always high - low)
            double range = fib.HighPrice - fib.LowPrice;

            // Draw level lines for Fibonacci ratios we're tracking in ascending order
            double[] levels = { -2.0, -1.5, -1.0, -0.5, -0.25, 0.0, 0.114, 0.886, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 };

            foreach (double ratio in levels)
            {
                // Calculate price at this ratio - always ascending with ratio
                double levelPrice = fib.LowPrice + (ratio * range);

                // Draw the level line
                var line = _chart.DrawTrendLine(
                    $"{id}-level-{ratio}",
                    fib.StartTime,
                    levelPrice,
                    fib.EndTime.AddHours(8), // Extend for visibility
                    levelPrice,
                    Color.Pink
                );

                // Make it semi-transparent
                line.Color = Color.FromArgb(50, line.Color);

                // Make it editable
                line.IsInteractive = true;

                // Add level label with the ratio (e.g., -2.0, 0.5, 1.0, etc.)
                var label = _chart.DrawText(
                    $"{id}-label-{ratio}",
                    $"{ratio:0.###}",
                    fib.StartTime,
                    levelPrice,
                    Color.Pink
                );

                // Make label semi-transparent too
                label.Color = Color.FromArgb(50, label.Color);
            }

            // Store the ID for later cleanup
            fib.FibonacciId = id;
        }

        /// <summary>
        /// Calculate Fibonacci levels for a session
        /// </summary>
        private void CalculateFibonacciLevels(SessionTracker tracker, SessionType session)
        {
            // Skip if we don't have valid data
            if (tracker.HighIndex < 0 || tracker.LowIndex < 0)
                return;

            // Determine chronological order for drawing (left to right)
            int startIndex, endIndex;
            DateTime startTime, endTime;

            if (tracker.HighIndex < tracker.LowIndex)
            {
                // High came first, then low
                startIndex = tracker.HighIndex;
                endIndex = tracker.LowIndex;
                startTime = tracker.HighTime;
                endTime = tracker.LowTime;
            }
            else
            {
                // Low came first, then high
                startIndex = tracker.LowIndex;
                endIndex = tracker.HighIndex;
                startTime = tracker.LowTime;
                endTime = tracker.HighTime;
            }

            // Create the Fibonacci level object (pass chronological order for drawing,
            // and it will calculate levels from low to high internally)
            var fibLevel = new FibonacciLevel(
                startIndex,
                endIndex,
                tracker.High, // Pass high price value
                tracker.Low, // Pass low price value
                startTime,
                endTime,
                session
            );

            // Add to our collection
            _fibonacciLevels.Add(fibLevel);

            // Draw on chart
            DrawFibonacciLevels(fibLevel);
        }

        /// <summary>
        /// Check if a swing point sweeps any Fibonacci levels
        /// </summary>
        public void CheckFibonacciSweep(SwingPoint swingPoint)
        {
            // Skip if not enabled or no data
            if (!_showFibLevels || _fibonacciLevels.Count == 0 || swingPoint == null || swingPoint.Bar == null)
                return;

            // Create a list to track all swept levels
            List<(FibonacciLevel fibLevel, double ratio, double levelPrice)> sweptLevels =
                new List<(FibonacciLevel, double, double)>();

            // Create a list of levels to remove (if all ratios are swept)
            List<FibonacciLevel> levelsToRemove = new List<FibonacciLevel>();

            foreach (var fibLevel in _fibonacciLevels)
            {
                // Skip if this swing point is too old (created before the Fibonacci level)
                if (swingPoint.Index <= fibLevel.EndIndex)
                    continue;

                // Check each ratio that we're tracking for sweeps
                foreach (double ratio in FibonacciLevel.TrackedRatios)
                {
                    // Skip if this level has already been swept
                    if (fibLevel.SweptLevels.ContainsKey(ratio) && fibLevel.SweptLevels[ratio])
                        continue;

                    // Get the price for this level - using consistent calculation
                    double levelPrice = fibLevel.LowPrice + (ratio * (fibLevel.HighPrice - fibLevel.LowPrice));
                    bool isSweep = false;

                    // Determine if candle is bullish or bearish
                    bool isBullishCandle = swingPoint.Bar.Close > swingPoint.Bar.Open;

                    // Check for sweep based on direction
                    if (swingPoint.Direction == Direction.Down) // Bearish swing point
                    {
                        if (isBullishCandle)
                        {
                            // Bearish swing point, bullish candle: Low <= level AND Open > level
                            isSweep = swingPoint.Bar.Low <= levelPrice && swingPoint.Bar.Open > levelPrice;
                        }
                        else
                        {
                            // Bearish swing point, bearish candle: Low <= level AND Close > level
                            isSweep = swingPoint.Bar.Low <= levelPrice && swingPoint.Bar.Close > levelPrice;
                        }
                    }
                    else // Bullish swing point
                    {
                        if (isBullishCandle)
                        {
                            // Bullish swing point, bullish candle: High >= level AND Close < level
                            isSweep = swingPoint.Bar.High >= levelPrice && swingPoint.Bar.Close < levelPrice;
                        }
                        else
                        {
                            // Bullish swing point, bearish candle: High >= level AND Open < level
                            isSweep = swingPoint.Bar.High >= levelPrice && swingPoint.Bar.Open < levelPrice;
                        }
                    }

                    if (isSweep)
                    {
                        // Mark this level as swept
                        fibLevel.SweptLevels[ratio] = true;

                        // Add to our collection of swept levels
                        sweptLevels.Add((fibLevel, ratio, levelPrice));

                        // Mark the swing point
                        swingPoint.SweptFib = true;
                        swingPoint.SweptFibLevel = fibLevel;

                        // Set zone based on the ratio
                        swingPoint.FibZone = fibLevel.GetZone(ratio);

                        // Check if all levels for this Fibonacci retracement are now swept
                        if (fibLevel.AreAllLevelsSwept())
                        {
                            levelsToRemove.Add(fibLevel);
                        }
                    }
                }
            }

            // After checking all levels, draw only the extreme one if any levels were swept
            if (sweptLevels.Count > 0)
            {
                // Find the extreme level based on swing point direction
                (FibonacciLevel extremeFib, double extremeRatio, double extremePrice) =
                    swingPoint.Direction == Direction.Up
                        ? sweptLevels.OrderByDescending(x => x.levelPrice).First() // For bullish, get highest
                        : sweptLevels.OrderBy(x => x.levelPrice).First(); // For bearish, get lowest

                // Draw only the extreme one
                DrawFibSweepLine(swingPoint, extremeFib, extremeRatio, extremePrice);
            }

            // Remove any fully swept levels
            foreach (var level in levelsToRemove)
            {
                // Remove the chart visualization (but keep sweep lines)
                if (_chart != null && !string.IsNullOrEmpty(level.FibonacciId))
                {
                    RemoveFibonacciFromChart(level);
                }

                // Remove from our collection
                _fibonacciLevels.Remove(level);
            }
        }

        /// <summary>
        /// Draw a white line showing where a Fibonacci level was swept
        /// </summary>
        private void DrawFibSweepLine(SwingPoint swingPoint, FibonacciLevel fibLevel, double ratio, double level)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this sweep line
            string id = $"fib-sweep-{fibLevel.SessionType}-{ratio}-{swingPoint.Time.Ticks}";

            // Remove any existing line
            _chart.RemoveObject(id);

            // Calculate 2 minutes before and after the swing point
            DateTime startTime = swingPoint.Time.AddMinutes(-1);
            DateTime endTime = swingPoint.Time.AddMinutes(1);

            // Draw a white horizontal line at the level price
            var line = _chart.DrawTrendLine(
                id,
                startTime,
                level,
                endTime,
                level,
                Color.White,
                1,
                LineStyle.Solid
            );

            // Store the line ID for cleanup
            if (!fibLevel.SweptLevelLineIds.ContainsKey(ratio))
            {
                fibLevel.SweptLevelLineIds[ratio] = id;
            }
        }

        /// <summary>
        /// Remove a single Fibonacci level from the chart
        /// </summary>
        private void RemoveFibonacciFromChart(FibonacciLevel fib)
        {
            if (_chart == null || string.IsNullOrEmpty(fib.FibonacciId))
                return;

            string baseId = fib.FibonacciId;

            // Remove main trend line
            _chart.RemoveObject($"{baseId}-main");

            // Remove all level lines and labels
            double[] levels = { -2.0, -1.5, -1.0, -0.5, -0.25, 0.0, 0.114, 0.886, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 };
            foreach (double level in levels)
            {
                _chart.RemoveObject($"{baseId}-level-{level}");
                _chart.RemoveObject($"{baseId}-label-{level}");
            }

            // DO NOT remove sweep lines - we want to keep these even after the Fibonacci level is removed
            // This ensures traders can still see where levels were swept
            // The sweep lines are:
            // fib-sweep-{SessionType}-{ratio}-{TimeStamp}
        }

        /// <summary>
        /// Clean up all Fibonacci levels from the chart
        /// </summary>
        private void CleanupFibonacciLevels()
        {
            if (_chart == null)
                return;

            // Remove all Fibonacci visualization objects EXCEPT the sweep level lines
            foreach (var fib in _fibonacciLevels)
            {
                RemoveFibonacciFromChart(fib);

                // Important: Do NOT remove swept level lines as these should persist
                // even after the Fibonacci level is removed
            }

            // Clear the collection
            _fibonacciLevels.Clear();
        }
    }
}