using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Extensions;
using Pfuma.Helpers;
using Pfuma.Models;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;

namespace Pfuma.Services
{
    /// <summary>
    /// Manages the collection of custom Candle objects for the indicator
    /// </summary>
    public class CandleManager
    {
        private readonly List<Candle> _candles;
        private readonly Bars _bars;
        private readonly TimeFrame _timeFrame;
        private int _lastProcessedIndex = -1;
        
        // Multi-timeframe support
        private readonly Chart _chart;
        private int _utcOffset;
        private readonly Action<string> _logger;
        private readonly Dictionary<TimeFrame, List<Candle>> _htfCandles;
        private readonly List<TimeFrame> _higherTimeframes;
        private readonly bool _showHighTimeframeCandle;
        private readonly bool _showHtfSwingPoints;
        
        // HTF Swing Point Collections
        private readonly Dictionary<TimeFrame, List<SwingPoint>> _htfSwingPoints;
        private readonly Dictionary<TimeFrame, SwingPointState> _htfSwingStates;
        
        // HTF Processing State Tracking (for handling market gaps)
        private readonly Dictionary<TimeFrame, DateTime> _lastProcessedHtfTime;
        private readonly Dictionary<TimeFrame, List<Candle>> _pendingHtfCandles;
        private readonly Dictionary<TimeFrame, DateTime> _currentHtfPeriodStart;
        
        // Event aggregator for publishing events
        private readonly IEventAggregator _eventAggregator;

        public CandleManager(Bars bars, TimeFrame timeFrame, Chart chart = null, int utcOffset = -4, Action<string> logger = null, string timeframes = "H1", bool showHighTimeframeCandle = false, bool showHtfSwingPoints = false, IEventAggregator eventAggregator = null)
        {
            _bars = bars;
            _timeFrame = timeFrame;
            _chart = chart;
            _utcOffset = utcOffset;
            _logger = logger;
            _showHighTimeframeCandle = showHighTimeframeCandle;
            _showHtfSwingPoints = showHtfSwingPoints;
            _eventAggregator = eventAggregator;
            _candles = new List<Candle>();
            _htfCandles = new Dictionary<TimeFrame, List<Candle>>();
            _htfSwingPoints = new Dictionary<TimeFrame, List<SwingPoint>>();
            _htfSwingStates = new Dictionary<TimeFrame, SwingPointState>();
            _lastProcessedHtfTime = new Dictionary<TimeFrame, DateTime>();
            _pendingHtfCandles = new Dictionary<TimeFrame, List<Candle>>();
            _currentHtfPeriodStart = new Dictionary<TimeFrame, DateTime>();
            _higherTimeframes = new List<TimeFrame>();
            
            InitializeHigherTimeframes(timeframes);
        }
        
        /// <summary>
        /// Initialize higher timeframes from comma-separated string
        /// </summary>
        private void InitializeHigherTimeframes(string timeframes)
        {
            if (string.IsNullOrEmpty(timeframes))
                return;
                
            var timeframeStrings = timeframes.Split(',');
            
            foreach (var tfStr in timeframeStrings)
            {
                var tf = tfStr.Trim().GetTimeFrameFromString();
                
                // Validate that higher timeframe is a multiple of current timeframe
                if (IsValidHigherTimeframe(tf))
                {
                    _higherTimeframes.Add(tf);
                    _htfCandles[tf] = new List<Candle>();
                    _htfSwingPoints[tf] = new List<SwingPoint>();
                    _htfSwingStates[tf] = new SwingPointState();
                    _lastProcessedHtfTime[tf] = DateTime.MinValue;
                    _pendingHtfCandles[tf] = new List<Candle>();
                    _currentHtfPeriodStart[tf] = DateTime.MinValue;
                    
                    _logger?.Invoke($"Added higher timeframe: {tf.GetShortName()}");
                }
                else
                {
                    _logger?.Invoke($"Skipped invalid timeframe: {tfStr} (not a multiple of current timeframe {_timeFrame.GetShortName()})");
                }
            }
        }
        
        /// <summary>
        /// Check if a timeframe is a valid multiple of the current timeframe
        /// </summary>
        private bool IsValidHigherTimeframe(TimeFrame higherTimeframe)
        {
            var periodicity = _timeFrame.GetPeriodicity(higherTimeframe);
            
            // Log for debugging
            _logger?.Invoke($"Checking HTF validity: {_timeFrame.GetShortName()} -> {higherTimeframe.GetShortName()}, Periodicity: {periodicity}");
            
            // Must be a valid multiple (at least 2x for it to be "higher")
            // Changed from > 1 to >= 2 for clarity, though they're equivalent
            return periodicity >= 2;
        }
        
        /// <summary>
        /// Get the start time of the HTF period that contains the given LTF time
        /// This handles market gaps by calculating the proper HTF period boundary
        /// </summary>
        private DateTime GetHtfPeriodStart(DateTime ltfTime, TimeFrame htf)
        {
            var htfMinutes = htf.TimeFrameToMinutes();
            
            if (htfMinutes <= 0)
                return ltfTime; // Invalid timeframe
            
            // For minute and hour timeframes (anything less than a day)
            if (htfMinutes < 1440)
            {
                // Calculate the total minutes since midnight
                var totalMinutesInDay = (int)ltfTime.TimeOfDay.TotalMinutes;
                
                // Round down to the nearest HTF period boundary
                var periodStartMinutes = (totalMinutesInDay / htfMinutes) * htfMinutes;
                
                // Create the period start time
                return ltfTime.Date.AddMinutes(periodStartMinutes);
            }
            
            // For daily timeframes
            if (htfMinutes == 1440)
            {
                return new DateTime(ltfTime.Year, ltfTime.Month, ltfTime.Day, 0, 0, 0);
            }
            
            // For weekly timeframes, round down to Monday
            if (htfMinutes == 10080)
            {
                var daysFromMonday = ((int)ltfTime.DayOfWeek + 6) % 7; // Convert to Monday = 0
                var monday = ltfTime.Date.AddDays(-daysFromMonday);
                return new DateTime(monday.Year, monday.Month, monday.Day, 0, 0, 0);
            }
            
            // For monthly timeframes
            if (htfMinutes >= 43200)
            {
                return new DateTime(ltfTime.Year, ltfTime.Month, 1, 0, 0, 0);
            }
            
            // Default fallback
            return ltfTime.Date;
        }
        
        /// <summary>
        /// Process a new bar and add it to the candle collection
        /// </summary>
        public Candle ProcessBar(int index)
        {
            // Check if we already processed this index
            if (index <= _lastProcessedIndex && index < _candles.Count)
            {
                // Update the existing candle if the bar has changed
                var existingCandle = _candles[index];
                var bar = _bars[index];
                
                existingCandle.Open = bar.Open;
                existingCandle.High = bar.High;
                existingCandle.Low = bar.Low;
                existingCandle.Close = bar.Close;
                
                return existingCandle;
            }

            // Create a new candle
            var newBar = _bars[index];
            var candle = new Candle(newBar, index, _timeFrame);

            // Add or update the candle in the collection
            if (index < _candles.Count)
            {
                _candles[index] = candle;
            }
            else
            {
                // Fill any gaps
                while (_candles.Count < index)
                {
                    var gapBar = _bars[_candles.Count];
                    _candles.Add(new Candle(gapBar, _candles.Count, _timeFrame));
                }
                _candles.Add(candle);
            }

            _lastProcessedIndex = Math.Max(_lastProcessedIndex, index);
            
            // Process higher timeframe candles
            ProcessHigherTimeframeCandles(index, candle);
            
            return candle;
        }
        
        /// <summary>
        /// Process higher timeframe candles when a new LTF candle closes
        /// 
        /// IMPROVED VERSION: This handles market gaps by tracking HTF periods properly.
        /// 
        /// Key improvements over the old approach:
        /// - Tracks last processed HTF time for each timeframe to detect gaps
        /// - Uses actual time boundaries instead of simple periodicity calculations  
        /// - Handles markets that close/open with gaps (e.g., indices: 16:59 close -> 18:00 open, skipping 17:00)
        /// - Works correctly for both continuous markets (forex) and gapped markets (indices, commodities)
        /// - Accumulates LTF candles per HTF period and finalizes when period boundaries are crossed
        /// </summary>
        private void ProcessHigherTimeframeCandles(int currentIndex, Candle currentCandle)
        {
            if (currentIndex < 1 || _higherTimeframes.Count == 0 || currentCandle == null)
                return;

            foreach (var htf in _higherTimeframes)
            {
                ProcessHtfCandleForTimeframe(currentIndex, currentCandle, htf);
            }
        }
        
        /// <summary>
        /// Process HTF candle for a specific timeframe using gap-aware logic
        /// </summary>
        private void ProcessHtfCandleForTimeframe(int currentIndex, Candle currentCandle, TimeFrame htf)
        {
            var currentTime = currentCandle.Time;
            var currentHtfPeriodStart = GetHtfPeriodStart(currentTime, htf);
            
            // Check if we've moved to a new HTF period
            if (_currentHtfPeriodStart[htf] != DateTime.MinValue && 
                currentHtfPeriodStart != _currentHtfPeriodStart[htf])
            {
                // We've moved to a new HTF period - finalize the previous one
                FinalizePendingHtfCandle(htf);
            }
            
            // Start new HTF period if needed
            if (_currentHtfPeriodStart[htf] != currentHtfPeriodStart)
            {
                _currentHtfPeriodStart[htf] = currentHtfPeriodStart;
                _pendingHtfCandles[htf].Clear();
                
                _logger?.Invoke($"Started new {htf.GetShortName()} period at {currentHtfPeriodStart:yyyy-MM-dd HH:mm}");
            }
            
            // Add current LTF candle to pending HTF candles
            _pendingHtfCandles[htf].Add(currentCandle);
            
            // Check if we should finalize the current HTF candle
            // This happens when we detect the next period starting
            var nextLtfTime = GetNextLtfTime(currentTime);
            var nextHtfPeriodStart = GetHtfPeriodStart(nextLtfTime, htf);
            
            if (nextHtfPeriodStart != currentHtfPeriodStart)
            {
                // Current LTF candle is the last one in this HTF period
                FinalizePendingHtfCandle(htf);
            }
        }
        
        /// <summary>
        /// Finalize and create HTF candle from pending LTF candles
        /// </summary>
        private void FinalizePendingHtfCandle(TimeFrame htf)
        {
            var pendingCandles = _pendingHtfCandles[htf];
            
            if (pendingCandles.Count == 0)
                return;
                
            var htfCandle = CreateHtfCandleFromLtfCandles(pendingCandles, htf, pendingCandles.First().Index ?? 0);
            
            if (htfCandle != null)
            {
                _htfCandles[htf].Add(htfCandle);
                _lastProcessedHtfTime[htf] = _currentHtfPeriodStart[htf];
                
                // Process swing point detection for HTF candle
                ProcessHtfSwingPointDetection(htf, htfCandle);
                
                // Draw visualization if enabled
                if (_showHighTimeframeCandle && _chart != null)
                {
                    DrawHighTimeframeCandlePoints(htfCandle, pendingCandles);
                }
                
                _logger?.Invoke($"Finalized {htf.GetShortName()} candle for period {_currentHtfPeriodStart[htf]:yyyy-MM-dd HH:mm} using {pendingCandles.Count} LTF candles");
            }
            
            // Clear pending candles
            _pendingHtfCandles[htf].Clear();
        }
        
        /// <summary>
        /// Get the next LTF time (approximation for period boundary detection)
        /// </summary>
        private DateTime GetNextLtfTime(DateTime currentTime)
        {
            // Add the LTF timeframe duration to get next expected time
            var minutes = _timeFrame.TimeFrameToMinutes();
            
            if (minutes > 0 && minutes < 1440) // Valid minute/hour timeframes
            {
                return currentTime.AddMinutes(minutes);
            }
            else if (minutes == 1440) // Daily
            {
                return currentTime.AddDays(1);
            }
            else if (minutes == 10080) // Weekly
            {
                return currentTime.AddDays(7);
            }
            
            // Default fallback for any unhandled cases
            return currentTime.AddMinutes(1);
        }
        
        /// <summary>
        /// Check if we should create a new higher timeframe candle 
        /// DEPRECATED: This method doesn't handle market gaps properly. Use ProcessHtfCandleForTimeframe instead.
        /// </summary>
        [Obsolete("This method doesn't handle market gaps properly. The new gap-aware logic is handled in ProcessHtfCandleForTimeframe.")]
        private bool ShouldCreateNewHtfCandle(int currentIndex, TimeFrame htf)
        {
            if (currentIndex < 1 || currentIndex >= _bars.Count)
                return false;
                
            var currentTime = _bars[currentIndex].OpenTime;
            
            // Check if this is the start of a new HTF candle
            return currentTime.IsStartOfTimeframeBar(htf);
        }
        
        /// <summary>
        /// Create a higher timeframe candle from lower timeframe candles
        /// DEPRECATED: This method uses periodicity-based logic that doesn't handle market gaps.
        /// Use FinalizePendingHtfCandle instead which handles gaps properly.
        /// </summary>
        [Obsolete("This method uses periodicity-based logic that doesn't handle market gaps. Use FinalizePendingHtfCandle instead.")]
        private void CreateHigherTimeframeCandle(int currentIndex, TimeFrame htf)
        {
            var periodicity = _timeFrame.GetPeriodicity(htf);
            if (periodicity <= 1 || currentIndex < periodicity)
                return;
                
            // Get the range of LTF candles that make up this HTF candle
            var startIndex = currentIndex - periodicity;
            var endIndex = currentIndex - 1;
            
            // Validate indices
            if (startIndex < 0 || endIndex >= _candles.Count)
                return;
                
            // Get LTF candles that constitute the HTF candle
            var ltfCandles = _candles.GetRange(startIndex, periodicity);
            
            if (ltfCandles.Count == 0)
                return;
                
            // Create HTF candle
            var htfCandle = CreateHtfCandleFromLtfCandles(ltfCandles, htf, startIndex);
            
            if (htfCandle != null)
            {
                _htfCandles[htf].Add(htfCandle);
                
                // Process swing point detection for HTF candle
                ProcessHtfSwingPointDetection(htf, htfCandle);
                
                // Draw visualization if enabled
                if (_showHighTimeframeCandle && _chart != null)
                {
                    DrawHighTimeframeCandlePoints(htfCandle, ltfCandles);
                }
                
                _logger?.Invoke($"Created {htf.GetShortName()} candle from indices {startIndex}-{endIndex}");
            }
        }
        
        /// <summary>
        /// Create a higher timeframe candle from a collection of lower timeframe candles
        /// </summary>
        private Candle CreateHtfCandleFromLtfCandles(List<Candle> ltfCandles, TimeFrame htf, int startIndex)
        {
            if (ltfCandles == null || ltfCandles.Count == 0)
                return null;
                
            var firstCandle = ltfCandles.First();
            var lastCandle = ltfCandles.Last();
            
            // Use extension method to get min/max candles with their indices
            var (minCandle, minIndex, minTime, maxCandle, maxIndex, maxTime) = ltfCandles.GetMinMax();
            
            if (minCandle == null || maxCandle == null)
                return null;
                
            var htfCandle = new Candle
            {
                Index = startIndex,
                Time = firstCandle.Time,
                Open = firstCandle.Open,
                High = maxCandle.High,
                Low = minCandle.Low,
                Close = lastCandle.Close,
                IndexOfHigh = maxCandle.Index,
                IndexOfLow = minCandle.Index,
                TimeFrame = htf
            };
            
            return htfCandle;
        }
        
        /// <summary>
        /// Draw visualization for HTF candle high and low points
        /// </summary>
        private void DrawHighTimeframeCandlePoints(Candle htfCandle, List<Candle> ltfCandles)
        {
            if (_chart == null || htfCandle == null || ltfCandles == null)
                return;
                
            // Find the LTF candles that made the high and low
            var (minCandle, minIndex, minTime, maxCandle, maxIndex, maxTime) = ltfCandles.GetMinMax();
            
            if (minCandle != null && minCandle.Index.HasValue)
            {
                // Draw red dot for low
                var lowIconName = $"htf_low_{htfCandle.TimeFrame.GetShortName()}_{minCandle.Index}_{htfCandle.Time:yyyyMMddHHmm}";
                _chart.DrawIcon(lowIconName, ChartIconType.Circle, minTime, minCandle.Low, Color.Red);
            }
            
            if (maxCandle != null && maxCandle.Index.HasValue)
            {
                // Draw green dot for high  
                var highIconName = $"htf_high_{htfCandle.TimeFrame.GetShortName()}_{maxCandle.Index}_{htfCandle.Time:yyyyMMddHHmm}";
                _chart.DrawIcon(highIconName, ChartIconType.Circle, maxTime, maxCandle.High, Color.Green);
            }
        }
        
        /// <summary>
        /// Process swing point detection for HTF candles using similar logic to SwingPointDetector
        /// </summary>
        private void ProcessHtfSwingPointDetection(TimeFrame timeframe, Candle htfCandle)
        {
            if (!_htfSwingStates.ContainsKey(timeframe) || !_htfSwingPoints.ContainsKey(timeframe))
                return;
                
            var state = _htfSwingStates[timeframe];
            var swingPoints = _htfSwingPoints[timeframe];
            
            var index = htfCandle.Index ?? -1;
            var close = htfCandle.Close;
            var open = htfCandle.Open;
            var low = htfCandle.Low;
            var high = htfCandle.High;
            var time = htfCandle.Time;

            bool isDownCandle = close < open;
            bool isUpCandle = close > open;

            // SPECIAL CASE 1: Handle when previous candle has a swing low and current candle 
            // has both lower low and higher high than previous swing low candle
            if (state.LastSwingWasLow && state.LastSwingLowIndex >= 0 && state.LastLowSwingPoint != null)
            {
                double prevSwingLowValue = state.LastSwingLowValue;
                double prevSwingLowCandleHigh = state.LastLowSwingPoint.Bar.High;

                if (low < prevSwingLowValue && high > prevSwingLowCandleHigh)
                {
                    if (isDownCandle)
                    {
                        // For bearish candle: Set high as swing high first, then low as swing low
                        var highSwingPoint = new SwingPoint(
                            htfCandle.IndexOfHigh ?? index,
                            high,
                            time,
                            htfCandle,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(highSwingPoint);
                        state.LastHighSwingPoint = highSwingPoint;
                        state.LastSwingHighIndex = index;
                        state.LastSwingHighValue = high;
                        state.LastSwingWasHigh = true;
                        state.LastSwingWasLow = false;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, highSwingPoint, true);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(highSwingPoint, timeframe));

                        // Then set low as swing low
                        var lowSwingPoint = new SwingPoint(
                            htfCandle.IndexOfLow ?? index,
                            low,
                            time,
                            htfCandle,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(lowSwingPoint);
                        state.LastLowSwingPoint = lowSwingPoint;
                        state.LastSwingLowIndex = index;
                        state.LastSwingLowValue = low;
                        state.LastSwingWasLow = true;
                        state.LastSwingWasHigh = false;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, lowSwingPoint, false);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(lowSwingPoint, timeframe));
                        
                        _logger?.Invoke($"{timeframe.GetShortName()} Special case: Bearish candle created both High {high:F5} and Low {low:F5} at index {index}");
                        return;
                    }
                    else if (isUpCandle)
                    {
                        // For bullish candle: Move swing low to current candle, then create swing high
                        // Remove old swing point
                        if (swingPoints.Contains(state.LastLowSwingPoint))
                        {
                            swingPoints.Remove(state.LastLowSwingPoint);
                            if (_showHtfSwingPoints && _chart != null)
                            {
                                RemoveHtfSwingPointVisualization(timeframe, state.LastLowSwingPoint);
                            }
                        }
                        
                        // Create new low swing point at current candle
                        var lowSwingPoint = new SwingPoint(
                            htfCandle.IndexOfLow ?? index,
                            low,
                            time,
                            htfCandle,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(lowSwingPoint);
                        state.LastLowSwingPoint = lowSwingPoint;
                        state.LastSwingLowIndex = index;
                        state.LastSwingLowValue = low;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, lowSwingPoint, false);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(lowSwingPoint, timeframe));

                        // Then set high as swing high
                        var highSwingPoint = new SwingPoint(
                            htfCandle.IndexOfHigh ?? index,
                            high,
                            time,
                            htfCandle,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(highSwingPoint);
                        state.LastHighSwingPoint = highSwingPoint;
                        state.LastSwingHighIndex = index;
                        state.LastSwingHighValue = high;
                        state.LastSwingWasHigh = true;
                        state.LastSwingWasLow = false;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, highSwingPoint, true);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(highSwingPoint, timeframe));
                        
                        _logger?.Invoke($"{timeframe.GetShortName()} Special case: Bullish candle moved Low to {low:F5} and created High {high:F5} at index {index}");
                        return;
                    }
                }
            }

            // SPECIAL CASE 2: Handle when previous candle has a swing high and current candle
            // has both higher high and lower low than previous swing high candle
            if (state.LastSwingWasHigh && state.LastSwingHighIndex >= 0 && state.LastHighSwingPoint != null)
            {
                double prevSwingHighValue = state.LastSwingHighValue;
                double prevSwingHighCandleLow = state.LastHighSwingPoint.Bar.Low;

                if (high > prevSwingHighValue && low < prevSwingHighCandleLow)
                {
                    if (isUpCandle)
                    {
                        // For bullish candle: Set low as swing low first, then high as swing high
                        var lowSwingPoint = new SwingPoint(
                            htfCandle.IndexOfLow ?? index,
                            low,
                            time,
                            htfCandle,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(lowSwingPoint);
                        state.LastLowSwingPoint = lowSwingPoint;
                        state.LastSwingLowIndex = index;
                        state.LastSwingLowValue = low;
                        state.LastSwingWasLow = true;
                        state.LastSwingWasHigh = false;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, lowSwingPoint, false);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(lowSwingPoint, timeframe));

                        // Then set high as swing high
                        var highSwingPoint = new SwingPoint(
                            htfCandle.IndexOfHigh ?? index,
                            high,
                            time,
                            htfCandle,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(highSwingPoint);
                        state.LastHighSwingPoint = highSwingPoint;
                        state.LastSwingHighIndex = index;
                        state.LastSwingHighValue = high;
                        state.LastSwingWasHigh = true;
                        state.LastSwingWasLow = false;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, highSwingPoint, true);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(highSwingPoint, timeframe));
                        
                        _logger?.Invoke($"{timeframe.GetShortName()} Special case: Bullish candle created both Low {low:F5} and High {high:F5} at index {index}");
                        return;
                    }
                    else if (isDownCandle)
                    {
                        // For bearish candle: Move swing high to current candle, then create swing low
                        // Remove old swing point
                        if (swingPoints.Contains(state.LastHighSwingPoint))
                        {
                            swingPoints.Remove(state.LastHighSwingPoint);
                            if (_showHtfSwingPoints && _chart != null)
                            {
                                RemoveHtfSwingPointVisualization(timeframe, state.LastHighSwingPoint);
                            }
                        }
                        
                        // Create new high swing point at current candle
                        var highSwingPoint = new SwingPoint(
                            htfCandle.IndexOfHigh ?? index,
                            high,
                            time,
                            htfCandle,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(highSwingPoint);
                        state.LastHighSwingPoint = highSwingPoint;
                        state.LastSwingHighIndex = index;
                        state.LastSwingHighValue = high;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, highSwingPoint, true);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(highSwingPoint, timeframe));

                        // Then set low as swing low
                        var lowSwingPoint = new SwingPoint(
                            htfCandle.IndexOfLow ?? index,
                            low,
                            time,
                            htfCandle,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++state.CurrentSwingPointNumber;
                        
                        swingPoints.Add(lowSwingPoint);
                        state.LastLowSwingPoint = lowSwingPoint;
                        state.LastSwingLowIndex = index;
                        state.LastSwingLowValue = low;
                        state.LastSwingWasLow = true;
                        state.LastSwingWasHigh = false;
                        
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            DrawHtfSwingPoint(timeframe, lowSwingPoint, false);
                        }
                        _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(lowSwingPoint, timeframe));
                        
                        _logger?.Invoke($"{timeframe.GetShortName()} Special case: Bearish candle moved High to {high:F5} and created Low {low:F5} at index {index}");
                        return;
                    }
                }
            }

            // Normal swing high detection logic
            if (state.LastSwingWasLow || (!state.LastSwingWasHigh && !state.LastSwingWasLow))
            {
                if (high > state.LastSwingHighValue)
                {
                    var highSwingPoint = new SwingPoint(
                        htfCandle.IndexOfHigh ?? index,
                        high,
                        time,
                        htfCandle,
                        SwingType.H,
                        LiquidityType.Normal,
                        Direction.Up
                    );
                    highSwingPoint.Number = ++state.CurrentSwingPointNumber;
                    
                    swingPoints.Add(highSwingPoint);
                    state.LastHighSwingPoint = highSwingPoint;
                    state.LastSwingHighIndex = index;
                    state.LastSwingHighValue = high;
                    state.LastSwingWasHigh = true;
                    state.LastSwingWasLow = false;
                    
                    // Draw visualization if enabled
                    if (_showHtfSwingPoints && _chart != null)
                    {
                        DrawHtfSwingPoint(timeframe, highSwingPoint, true);
                    }
                    
                    // Publish HTF swing point event for order flow detection
                    _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(highSwingPoint, timeframe));
                    
                    _logger?.Invoke($"{timeframe.GetShortName()} Swing High detected at index {index}, price {high:F5}");
                    return;
                }
            }
            else if (state.LastSwingWasHigh && state.LastSwingHighIndex >= 0 && state.LastHighSwingPoint != null)
            {
                if (high > state.LastSwingHighValue)
                {
                    // Remove old swing point
                    if (swingPoints.Contains(state.LastHighSwingPoint))
                    {
                        swingPoints.Remove(state.LastHighSwingPoint);
                        
                        // Remove old visualization
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            RemoveHtfSwingPointVisualization(timeframe, state.LastHighSwingPoint);
                        }
                    }
                    
                    // Create new high swing point
                    var highSwingPoint = new SwingPoint(
                        htfCandle.IndexOfHigh ?? index,
                        high,
                        time,
                        htfCandle,
                        SwingType.H,
                        LiquidityType.Normal,
                        Direction.Up
                    );
                    highSwingPoint.Number = state.LastHighSwingPoint.Number; // Keep same number
                    
                    swingPoints.Add(highSwingPoint);
                    state.LastHighSwingPoint = highSwingPoint;
                    state.LastSwingHighIndex = index;
                    state.LastSwingHighValue = high;
                    
                    // Draw new visualization
                    if (_showHtfSwingPoints && _chart != null)
                    {
                        DrawHtfSwingPoint(timeframe, highSwingPoint, true);
                    }
                    
                    // Publish HTF swing point event for order flow detection
                    _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(highSwingPoint, timeframe));
                    
                    _logger?.Invoke($"{timeframe.GetShortName()} Swing High updated at index {index}, price {high:F5}");
                    return;
                }
                
                // Check for new swing low
                if (state.LastHighSwingPoint != null && low < state.LastHighSwingPoint.Bar.Low)
                {
                    var lowSwingPoint = new SwingPoint(
                        htfCandle.IndexOfLow ?? index,
                        low,
                        time,
                        htfCandle,
                        SwingType.L,
                        LiquidityType.Normal,
                        Direction.Down
                    );
                    lowSwingPoint.Number = ++state.CurrentSwingPointNumber;
                    
                    swingPoints.Add(lowSwingPoint);
                    state.LastLowSwingPoint = lowSwingPoint;
                    state.LastSwingLowIndex = index;
                    state.LastSwingLowValue = low;
                    state.LastSwingWasLow = true;
                    state.LastSwingWasHigh = false;
                    
                    // Draw visualization if enabled
                    if (_showHtfSwingPoints && _chart != null)
                    {
                        DrawHtfSwingPoint(timeframe, lowSwingPoint, false);
                    }
                    
                    // Publish HTF swing point event for order flow detection
                    _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(lowSwingPoint, timeframe));
                    
                    _logger?.Invoke($"{timeframe.GetShortName()} Swing Low detected at index {index}, price {low:F5}");
                    return;
                }
            }

            // Normal swing low detection logic
            if (state.LastSwingWasHigh || (!state.LastSwingWasHigh && !state.LastSwingWasLow))
            {
                if (low < state.LastSwingLowValue)
                {
                    var lowSwingPoint = new SwingPoint(
                        htfCandle.IndexOfLow ?? index,
                        low,
                        time,
                        htfCandle,
                        SwingType.L,
                        LiquidityType.Normal,
                        Direction.Down
                    );
                    lowSwingPoint.Number = ++state.CurrentSwingPointNumber;
                    
                    swingPoints.Add(lowSwingPoint);
                    state.LastLowSwingPoint = lowSwingPoint;
                    state.LastSwingLowIndex = index;
                    state.LastSwingLowValue = low;
                    state.LastSwingWasLow = true;
                    state.LastSwingWasHigh = false;
                    
                    // Draw visualization if enabled
                    if (_showHtfSwingPoints && _chart != null)
                    {
                        DrawHtfSwingPoint(timeframe, lowSwingPoint, false);
                    }
                    
                    // Publish HTF swing point event for order flow detection
                    _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(lowSwingPoint, timeframe));
                    
                    _logger?.Invoke($"{timeframe.GetShortName()} Swing Low detected at index {index}, price {low:F5}");
                    return;
                }
            }
            else if (state.LastSwingWasLow && state.LastSwingLowIndex >= 0 && state.LastLowSwingPoint != null)
            {
                if (low < state.LastSwingLowValue)
                {
                    // Remove old swing point
                    if (swingPoints.Contains(state.LastLowSwingPoint))
                    {
                        swingPoints.Remove(state.LastLowSwingPoint);
                        
                        // Remove old visualization
                        if (_showHtfSwingPoints && _chart != null)
                        {
                            RemoveHtfSwingPointVisualization(timeframe, state.LastLowSwingPoint);
                        }
                    }
                    
                    // Create new low swing point
                    var lowSwingPoint = new SwingPoint(
                        htfCandle.IndexOfLow ?? index,
                        low,
                        time,
                        htfCandle,
                        SwingType.L,
                        LiquidityType.Normal,
                        Direction.Down
                    );
                    lowSwingPoint.Number = state.LastLowSwingPoint.Number; // Keep same number
                    
                    swingPoints.Add(lowSwingPoint);
                    state.LastLowSwingPoint = lowSwingPoint;
                    state.LastSwingLowIndex = index;
                    state.LastSwingLowValue = low;
                    
                    // Draw new visualization
                    if (_showHtfSwingPoints && _chart != null)
                    {
                        DrawHtfSwingPoint(timeframe, lowSwingPoint, false);
                    }
                    
                    // Publish HTF swing point event for order flow detection
                    _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(lowSwingPoint, timeframe));
                    
                    _logger?.Invoke($"{timeframe.GetShortName()} Swing Low updated at index {index}, price {low:F5}");
                    return;
                }
                
                // Check for new swing high
                if (state.LastLowSwingPoint != null && high > state.LastLowSwingPoint.Bar.High)
                {
                    var highSwingPoint = new SwingPoint(
                        htfCandle.IndexOfHigh ?? index,
                        high,
                        time,
                        htfCandle,
                        SwingType.H,
                        LiquidityType.Normal,
                        Direction.Up
                    );
                    highSwingPoint.Number = ++state.CurrentSwingPointNumber;
                    
                    swingPoints.Add(highSwingPoint);
                    state.LastHighSwingPoint = highSwingPoint;
                    state.LastSwingHighIndex = index;
                    state.LastSwingHighValue = high;
                    state.LastSwingWasHigh = true;
                    state.LastSwingWasLow = false;
                    
                    // Draw visualization if enabled
                    if (_showHtfSwingPoints && _chart != null)
                    {
                        DrawHtfSwingPoint(timeframe, highSwingPoint, true);
                    }
                    
                    // Publish HTF swing point event for order flow detection
                    _eventAggregator?.Publish(new HtfSwingPointDetectedEvent(highSwingPoint, timeframe));
                    
                    _logger?.Invoke($"{timeframe.GetShortName()} Swing High detected at index {index}, price {high:F5}");
                    return;
                }
            }
        }
        
        /// <summary>
        /// Draw HTF swing point visualization (green for highs, red for lows)
        /// </summary>
        private void DrawHtfSwingPoint(TimeFrame timeframe, SwingPoint swingPoint, bool isHigh)
        {
            if (_chart == null || swingPoint == null)
                return;
                
            var color = isHigh ? Color.Green : Color.Red;
            var iconName = $"htf_swing_{timeframe.GetShortName()}_{(isHigh ? "high" : "low")}_{swingPoint.Index}_{swingPoint.Time:yyyyMMddHHmm}";
            
            // Draw at exact LTF index instead of HTF time for precise positioning
            _chart.DrawIcon(iconName, ChartIconType.Circle, swingPoint.Index, swingPoint.Price, color);
        }
        
        /// <summary>
        /// Remove HTF swing point visualization
        /// </summary>
        private void RemoveHtfSwingPointVisualization(TimeFrame timeframe, SwingPoint swingPoint)
        {
            if (_chart == null || swingPoint == null)
                return;
                
            var isHigh = swingPoint.SwingType == SwingType.H;
            var iconName = $"htf_swing_{timeframe.GetShortName()}_{(isHigh ? "high" : "low")}_{swingPoint.Index}_{swingPoint.Time:yyyyMMddHHmm}";
            
            _chart.RemoveObject(iconName);
        }
        
        /// <summary>
        /// Get a candle at a specific index
        /// </summary>
        public Candle GetCandle(int index)
        {
            if (index < 0 || index >= _candles.Count)
                return null;
            
            return _candles[index];
        }

        /// <summary>
        /// Get the last candle
        /// </summary>
        public Candle GetLastCandle()
        {
            if (_candles.Count == 0)
                return null;
                
            return _candles[_candles.Count - 1];
        }

        /// <summary>
        /// Get a range of candles
        /// </summary>
        public List<Candle> GetCandles(int startIndex, int count)
        {
            if (startIndex < 0 || startIndex >= _candles.Count)
                return new List<Candle>();

            var endIndex = Math.Min(startIndex + count, _candles.Count);
            return _candles.GetRange(startIndex, endIndex - startIndex);
        }

        /// <summary>
        /// Get all candles
        /// </summary>
        public List<Candle> GetAllCandles()
        {
            return new List<Candle>(_candles);
        }

        /// <summary>
        /// Get candles between two times
        /// </summary>
        public List<Candle> GetCandlesBetween(DateTime startTime, DateTime endTime)
        {
            return _candles.Where(c => c.Time >= startTime && c.Time <= endTime).ToList();
        }

        /// <summary>
        /// Find the highest candle in a range
        /// </summary>
        public (Candle candle, int index, double high) FindHighest(int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex >= _candles.Count || startIndex > endIndex)
                return (null, -1, double.MinValue);

            Candle highestCandle = null;
            int highestIndex = -1;
            double highestPrice = double.MinValue;

            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = _candles[i];
                if (candle.High > highestPrice)
                {
                    highestPrice = candle.High;
                    highestCandle = candle;
                    highestIndex = i;
                }
            }

            return (highestCandle, highestIndex, highestPrice);
        }

        /// <summary>
        /// Find the lowest candle in a range
        /// </summary>
        public (Candle candle, int index, double low) FindLowest(int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex >= _candles.Count || startIndex > endIndex)
                return (null, -1, double.MaxValue);

            Candle lowestCandle = null;
            int lowestIndex = -1;
            double lowestPrice = double.MaxValue;

            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = _candles[i];
                if (candle.Low < lowestPrice)
                {
                    lowestPrice = candle.Low;
                    lowestCandle = candle;
                    lowestIndex = i;
                }
            }

            return (lowestCandle, lowestIndex, lowestPrice);
        }

        /// <summary>
        /// Get the count of candles
        /// </summary>
        public int Count => _candles.Count;

        /// <summary>
        /// Get the current timeframe
        /// </summary>
        public TimeFrame TimeFrame => _timeFrame;
        
        /// <summary>
        /// Get higher timeframe candles for a specific timeframe
        /// </summary>
        public List<Candle> GetHigherTimeframeCandles(TimeFrame timeframe)
        {
            if (_htfCandles.ContainsKey(timeframe))
                return new List<Candle>(_htfCandles[timeframe]);
                
            return new List<Candle>();
        }
        
        /// <summary>
        /// Get all configured higher timeframes
        /// </summary>
        public List<TimeFrame> GetHigherTimeframes()
        {
            return new List<TimeFrame>(_higherTimeframes);
        }
        
        /// <summary>
        /// Get the last HTF candle for a specific timeframe
        /// </summary>
        public Candle GetLastHigherTimeframeCandle(TimeFrame timeframe)
        {
            if (_htfCandles.ContainsKey(timeframe) && _htfCandles[timeframe].Count > 0)
                return _htfCandles[timeframe].Last();
                
            return null;
        }
        
        /// <summary>
        /// Get count of HTF candles for a specific timeframe
        /// </summary>
        public int GetHigherTimeframeCandleCount(TimeFrame timeframe)
        {
            if (_htfCandles.ContainsKey(timeframe))
                return _htfCandles[timeframe].Count;
                
            return 0;
        }
        
        /// <summary>
        /// Get HTF swing points for a specific timeframe
        /// </summary>
        public List<SwingPoint> GetHtfSwingPoints(TimeFrame timeframe)
        {
            if (_htfSwingPoints.ContainsKey(timeframe))
                return new List<SwingPoint>(_htfSwingPoints[timeframe]);
                
            return new List<SwingPoint>();
        }
        
        /// <summary>
        /// Get HTF swing highs for a specific timeframe
        /// </summary>
        public List<SwingPoint> GetHtfSwingHighs(TimeFrame timeframe)
        {
            if (_htfSwingPoints.ContainsKey(timeframe))
                return _htfSwingPoints[timeframe].Where(sp => sp.SwingType == SwingType.H).ToList();
                
            return new List<SwingPoint>();
        }
        
        /// <summary>
        /// Get HTF swing lows for a specific timeframe
        /// </summary>
        public List<SwingPoint> GetHtfSwingLows(TimeFrame timeframe)
        {
            if (_htfSwingPoints.ContainsKey(timeframe))
                return _htfSwingPoints[timeframe].Where(sp => sp.SwingType == SwingType.L).ToList();
                
            return new List<SwingPoint>();
        }
        
        /// <summary>
        /// Get the last HTF swing high for a specific timeframe
        /// </summary>
        public SwingPoint GetLastHtfSwingHigh(TimeFrame timeframe)
        {
            if (_htfSwingStates.ContainsKey(timeframe))
                return _htfSwingStates[timeframe].LastHighSwingPoint;
                
            return null;
        }
        
        /// <summary>
        /// Get the last HTF swing low for a specific timeframe
        /// </summary>
        public SwingPoint GetLastHtfSwingLow(TimeFrame timeframe)
        {
            if (_htfSwingStates.ContainsKey(timeframe))
                return _htfSwingStates[timeframe].LastLowSwingPoint;
                
            return null;
        }
        
        /// <summary>
        /// Get count of HTF swing points for a specific timeframe
        /// </summary>
        public int GetHtfSwingPointCount(TimeFrame timeframe)
        {
            if (_htfSwingPoints.ContainsKey(timeframe))
                return _htfSwingPoints[timeframe].Count;
                
            return 0;
        }
        
    }
}