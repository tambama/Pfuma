using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors.Base;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects Fair Value Gaps (FVGs) in Higher Timeframe (HTF) candles using simple gap detection
    /// without volume imbalance analysis for boundary refinement
    /// </summary>
    public class HtfFvgDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly Dictionary<TimeFrame, List<Level>> _htfFvgs;
        private readonly Dictionary<TimeFrame, int> _lastProcessedHtfIndex;
        
        public HtfFvgDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _htfFvgs = new Dictionary<TimeFrame, List<Level>>();
            _lastProcessedHtfIndex = new Dictionary<TimeFrame, int>();
            
            // Initialize dictionaries for each higher timeframe
            foreach (var htf in candleManager.GetHigherTimeframes())
            {
                _htfFvgs[htf] = new List<Level>();
                _lastProcessedHtfIndex[htf] = -1;
            }
        }
        
        protected override int GetMinimumBarsRequired()
        {
            return Constants.Patterns.FvgRequiredBars;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            var detectedFvgs = new List<Level>();
            
            // Check for HTF FVG sweeps by price action
            CheckHtfFvgSweepsByPriceAction(currentIndex);
            
            // Process each higher timeframe
            foreach (var htf in CandleManager.GetHigherTimeframes())
            {
                var htfCandles = CandleManager.GetHigherTimeframeCandles(htf);
                
                // Need at least 3 HTF candles to detect a FVG
                if (htfCandles.Count < 3)
                    continue;
                
                // Check if we have new HTF candles to process
                var currentHtfCount = htfCandles.Count;
                var lastProcessed = _lastProcessedHtfIndex.ContainsKey(htf) ? _lastProcessedHtfIndex[htf] : -1;
                
                // Process only if we have new HTF candles
                if (currentHtfCount > lastProcessed)
                {
                    // Get the three most recent HTF candles
                    var htfIndex = currentHtfCount - 1;
                    var candle1 = htfCandles[htfIndex - 2]; // First candle
                    var candle2 = htfCandles[htfIndex - 1]; // Middle candle
                    var candle3 = htfCandles[htfIndex];     // Last candle
                    
                    // Detect FVGs in HTF candles
                    var htfFvgs = DetectHtfFvgs(candle1, candle2, candle3, htf, currentIndex);
                    
                    foreach (var fvg in htfFvgs)
                    {
                        // Check for duplicates before adding
                        if (!IsDuplicateHtfFvg(fvg, htf))
                        {
                            detectedFvgs.Add(fvg);
                            _htfFvgs[htf].Add(fvg);
                        }
                    }
                    
                    // Update last processed index
                    _lastProcessedHtfIndex[htf] = currentHtfCount;
                }
            }
            
            return detectedFvgs;
        }
        
        private List<Level> DetectHtfFvgs(Candle candle1, Candle candle2, Candle candle3, TimeFrame htf, int ltfIndex)
        {
            var detectedFvgs = new List<Level>();
            
            // Check for bullish FVG
            var bullishFvg = DetectBullishHtfFvg(candle1, candle2, candle3, htf, ltfIndex);
            if (bullishFvg != null)
            {
                detectedFvgs.Add(bullishFvg);
            }
            
            // Check for bearish FVG
            var bearishFvg = DetectBearishHtfFvg(candle1, candle2, candle3, htf, ltfIndex);
            if (bearishFvg != null)
            {
                detectedFvgs.Add(bearishFvg);
            }
            
            return detectedFvgs;
        }
        
        private Level DetectBullishHtfFvg(Candle candle1, Candle candle2, Candle candle3, TimeFrame htf, int ltfIndex)
        {
            // Bullish FVG: candle1's high must be lower than candle3's low (gap condition)
            if (candle1.High >= candle3.Low)
                return null;
            
            // Simple boundary calculation without volume imbalance analysis
            double low = candle1.High;   // Top of first candle
            double high = candle3.Low;   // Bottom of third candle
            
            // Validate boundaries
            if (low >= high)
                return null;
            
            // For bullish HTF FVG:
            // - The FVG starts at the LTF index where candle1 made its HIGH
            // - The FVG ends at the LTF index where candle3 made its LOW
            int indexStart = candle1.IndexOfHigh ?? FindCurrentTimeframeIndexForTime(candle1.Time);
            int indexEnd = candle3.IndexOfLow ?? FindCurrentTimeframeIndexForTime(candle3.Time);
            int indexMid = candle2.Index ?? FindCurrentTimeframeIndexForTime(candle2.Time);
            
            // Get the times for these specific LTF indices
            var startCandle = CandleManager.GetCandle(indexStart);
            var endCandle = CandleManager.GetCandle(indexEnd);
            DateTime startTime = startCandle?.Time ?? candle1.Time;
            DateTime endTime = endCandle?.Time ?? candle3.Time;
            
            // Create a bullish HTF FVG level
            var bullishFvg = new Level(
                LevelType.FairValueGap,
                low,
                high,
                startTime,          // Start time (where candle1 made its high)
                endTime,            // End time (where candle3 made its low)
                candle2.Time,       // Mid time (candle 2 time)
                Direction.Up,
                ltfIndex,           // Use LTF index for reference
                indexEnd,           // High index (end of rectangle)
                indexStart,         // Low index (start of rectangle)
                indexMid,           // Middle index
                Zone.Premium
            );
            
            // Set HTF TimeFrame
            bullishFvg.TimeFrame = htf;
            
            // Initialize quadrants
            bullishFvg.InitializeQuadrants();
            
            return bullishFvg;
        }
        
        private Level DetectBearishHtfFvg(Candle candle1, Candle candle2, Candle candle3, TimeFrame htf, int ltfIndex)
        {
            // Bearish FVG: candle1's low must be higher than candle3's high (gap condition)
            if (candle1.Low <= candle3.High)
                return null;
            
            // Simple boundary calculation without volume imbalance analysis
            double high = candle1.Low;   // Bottom of first candle
            double low = candle3.High;   // Top of third candle
            
            // Validate boundaries
            if (low >= high)
                return null;
            
            // For bearish HTF FVG:
            // - The FVG starts at the LTF index where candle1 made its LOW  
            // - The FVG ends at the LTF index where candle3 made its HIGH
            int indexStart = candle1.IndexOfLow ?? FindCurrentTimeframeIndexForTime(candle1.Time);
            int indexEnd = candle3.IndexOfHigh ?? FindCurrentTimeframeIndexForTime(candle3.Time);
            int indexMid = candle2.Index ?? FindCurrentTimeframeIndexForTime(candle2.Time);
            
            // Get the times for these specific LTF indices
            var startCandle = CandleManager.GetCandle(indexStart);
            var endCandle = CandleManager.GetCandle(indexEnd);
            DateTime startTime = startCandle?.Time ?? candle1.Time;
            DateTime endTime = endCandle?.Time ?? candle3.Time;
            
            // Create a bearish HTF FVG level
            var bearishFvg = new Level(
                LevelType.FairValueGap,
                low,
                high,
                startTime,          // Start time (where candle1 made its low)
                endTime,            // End time (where candle3 made its high)
                candle2.Time,       // Mid time (candle 2 time)
                Direction.Down,
                ltfIndex,           // Use LTF index for reference
                indexEnd,           // High index (end of rectangle)
                indexStart,         // Low index (start of rectangle)
                indexMid,           // Middle index
                Zone.Discount
            );
            
            // Set HTF TimeFrame
            bearishFvg.TimeFrame = htf;
            
            // Initialize quadrants
            bearishFvg.InitializeQuadrants();
            
            return bearishFvg;
        }
        
        /// <summary>
        /// Finds the current timeframe index that corresponds to a specific time
        /// </summary>
        private int FindCurrentTimeframeIndexForTime(DateTime targetTime)
        {
            // Find the closest current timeframe candle that matches or comes before the target time
            for (int i = CandleManager.Count - 1; i >= 0; i--)
            {
                var candle = CandleManager.GetCandle(i);
                if (candle != null && candle.Time <= targetTime)
                {
                    return i;
                }
            }
            
            // If no match found, return the current index
            return CandleManager.Count - 1;
        }
        
        private bool IsDuplicateHtfFvg(Level fvg, TimeFrame htf)
        {
            if (!_htfFvgs.ContainsKey(htf))
                return false;
            
            return _htfFvgs[htf].Any(existingFvg =>
                existingFvg.Direction == fvg.Direction &&
                Math.Abs(existingFvg.Low - fvg.Low) < Constants.Calculations.PriceTolerance &&
                Math.Abs(existingFvg.High - fvg.High) < Constants.Calculations.PriceTolerance &&
                Math.Abs((existingFvg.LowTime - fvg.LowTime).TotalMinutes) < 1);
        }
        
        protected override bool PostDetectionValidation(Level fvg, int currentIndex)
        {
            // HTF FVGs are always valid if they passed initial detection
            return true;
        }
        
        protected override void PublishDetectionEvent(Level fvg, int currentIndex)
        {
            // Publish HTF FVG detected event
            EventAggregator.Publish(new HtfFvgDetectedEvent(fvg));
            
            // Draw the HTF FVG if visualization is enabled
            if (Settings.Patterns.ShowHtfFvg && _visualizer != null)
            {
                _visualizer.Draw(fvg);
            }
        }
        
        protected override void LogDetection(Level fvg, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"HTF FVG detected: {fvg.TimeFrame?.GetShortName() ?? "Unknown"} {fvg.Direction} at index {currentIndex}, " +
                       $"Range: {fvg.Low:F5} - {fvg.High:F5}, " +
                       $"Gap Size: {(fvg.High - fvg.Low):F5}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            var result = new List<Level>();
            
            foreach (var htfList in _htfFvgs.Values)
            {
                result.AddRange(htfList.Where(fvg => fvg.Direction == direction));
            }
            
            return result;
        }
        
        public List<Level> GetByTimeFrame(TimeFrame timeframe)
        {
            if (_htfFvgs.ContainsKey(timeframe))
                return new List<Level>(_htfFvgs[timeframe]);
            
            return new List<Level>();
        }
        
        public List<Level> GetByTimeFrameAndDirection(TimeFrame timeframe, Direction direction)
        {
            if (_htfFvgs.ContainsKey(timeframe))
                return _htfFvgs[timeframe].Where(fvg => fvg.Direction == direction).ToList();
            
            return new List<Level>();
        }
        
        public override bool IsValid(Level fvg, int currentIndex)
        {
            // HTF FVG is valid until it's been broken through (not just swept)
            // If it's been broken through, it's no longer valid
            if (fvg.IsBrokenThrough)
            {
                return false;
            }
            
            // Otherwise, HTF FVG remains valid even if swept (just wicked through)
            return true;
        }
        
        protected override void SubscribeToEvents()
        {
            // HTF FVG detector doesn't need to subscribe to other events
        }
        
        protected override void UnsubscribeFromEvents()
        {
            // No events to unsubscribe from
        }
        
        /// <summary>
        /// Get all HTF FVGs across all timeframes
        /// </summary>
        public List<Level> GetAllHtfFvgs()
        {
            var result = new List<Level>();
            
            foreach (var htfList in _htfFvgs.Values)
            {
                result.AddRange(htfList);
            }
            
            return result;
        }
        
        /// <summary>
        /// Clear HTF FVGs for a specific timeframe
        /// </summary>
        public void ClearTimeFrameFvgs(TimeFrame timeframe)
        {
            if (_htfFvgs.ContainsKey(timeframe))
            {
                _htfFvgs[timeframe].Clear();
            }
        }
        
        /// <summary>
        /// Get count of HTF FVGs for a specific timeframe
        /// </summary>
        public int GetTimeFrameFvgCount(TimeFrame timeframe)
        {
            if (_htfFvgs.ContainsKey(timeframe))
                return _htfFvgs[timeframe].Count;
            
            return 0;
        }
        
        /// <summary>
        /// Check for HTF FVG break through by price action on each bar
        /// </summary>
        private void CheckHtfFvgSweepsByPriceAction(int currentIndex)
        {
            try
            {
                var currentBar = CandleManager.GetCandle(currentIndex);
                if (currentBar == null)
                    return;
                
                var brokenFvgs = new List<(TimeFrame tf, Level fvg)>();
                
                // Check all timeframes for break through
                foreach (var kvp in _htfFvgs)
                {
                    var timeframe = kvp.Key;
                    var fvgList = kvp.Value;
                    
                    foreach (var htfFvg in fvgList.ToList()) // ToList to avoid modification during enumeration
                    {
                        // Skip already broken FVGs
                        if (htfFvg.IsBrokenThrough)
                            continue;
                            
                        bool isBrokenThrough = false;
                        
                        if (htfFvg.Direction == Direction.Up) // Bullish HTF FVG
                        {
                            // Check if candle closes below FVG low (broken through)
                            if (currentBar.Close < htfFvg.Low)
                            {
                                isBrokenThrough = true;
                            }
                        }
                        else if (htfFvg.Direction == Direction.Down) // Bearish HTF FVG
                        {
                            // Check if candle closes above FVG high (broken through)
                            if (currentBar.Close > htfFvg.High)
                            {
                                isBrokenThrough = true;
                            }
                        }
                        
                        if (isBrokenThrough)
                        {
                            // Mark HTF FVG as broken through
                            htfFvg.IsBrokenThrough = true;
                            htfFvg.IsLiquiditySwept = true; // Also mark as swept for compatibility
                            htfFvg.SweptIndex = currentIndex;
                            
                            brokenFvgs.Add((timeframe, htfFvg));
                            
                            // HTF FVG broken through by price action
                            
                            // If HTF FVG is unextended, remove it completely from chart and storage
                            if (!htfFvg.IsExtended)
                            {
                                RemoveHtfFvgFromChart(htfFvg);
                                fvgList.Remove(htfFvg);
                                // Broken HTF FVG removed
                            }
                        }
                    }
                }
                
                // Publish break through events for any broken FVGs
                foreach (var (timeframe, fvg) in brokenFvgs)
                {
                    EventAggregator.Publish(new HtfFvgSweptEvent(fvg, currentIndex, true));
                }
            }
            
            catch (Exception ex)
            {
                Logger?.Invoke($"Error checking HTF FVG break through by price action: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Remove HTF FVG from chart (rectangle, quadrants, and label)
        /// </summary>
        private void RemoveHtfFvgFromChart(Level htfFvg)
        {
            try
            {
                if (htfFvg == null || htfFvg.LevelType != LevelType.FairValueGap)
                    return;

                // Generate the HTF FVG pattern ID to match the visualizer
                var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
                var patternId = $"HTF_FVG_{tfLabel}_{htfFvg.Direction}_{htfFvg.Index}_{htfFvg.LowTime:yyyyMMddHHmmss}";
                
                // Remove the main rectangle
                Chart.RemoveObject(patternId);
                
                // Remove the label
                Chart.RemoveObject($"{patternId}_Label");
                
                // Remove quadrant lines if they exist
                Chart.RemoveObject($"{patternId}_Q25");
                Chart.RemoveObject($"{patternId}_Q50");
                Chart.RemoveObject($"{patternId}_Q75");
                
                // HTF FVG removed from chart
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error removing HTF FVG from chart: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Event for HTF FVG detection
    /// </summary>
    public class HtfFvgDetectedEvent : PatternEventBase
    {
        public Level HtfFvg { get; }
        
        public HtfFvgDetectedEvent(Level htfFvg) : base(htfFvg.Index)
        {
            HtfFvg = htfFvg;
        }
    }
    
    /// <summary>
    /// Event for HTF FVG being swept by price action
    /// </summary>
    public class HtfFvgSweptEvent : PatternEventBase
    {
        public Level HtfFvg { get; }
        public bool IsBrokenThrough { get; }
        
        public HtfFvgSweptEvent(Level htfFvg, int sweptIndex, bool isBrokenThrough = false) : base(sweptIndex)
        {
            HtfFvg = htfFvg;
            IsBrokenThrough = isBrokenThrough;
        }
    }
}