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
    /// Detects Higher Timeframe Order Blocks based on HTF swing point analysis.
    ///
    /// Bullish HTF Order Block Detection:
    /// - Triggered when a new bullish HTF swing point is created
    /// - Check: currentHigh > previousHigh AND currentLow &lt; previousLow
    /// - Check: HTF candle at currentHigh closes above previousHigh.Price
    /// - Create bullish order block from previousHigh to currentLow
    ///
    /// Bearish HTF Order Block Detection:
    /// - Triggered when a new bearish HTF swing point is created
    /// - Check: currentLow &lt; previousLow AND currentHigh > previousHigh
    /// - Check: HTF candle at currentLow closes below previousLow.Price
    /// - Create bearish order block from previousLow to currentHigh
    /// </summary>
    public class HtfOrderBlockDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly Dictionary<TimeFrame, List<SwingPoint>> _htfSwingPointHistory;
        private readonly Dictionary<TimeFrame, HashSet<string>> _detectedOrderBlockSignatures;

        public HtfOrderBlockDetector(
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
            _htfSwingPointHistory = new Dictionary<TimeFrame, List<SwingPoint>>();
            _detectedOrderBlockSignatures = new Dictionary<TimeFrame, HashSet<string>>();

            // Initialize dictionaries for each higher timeframe
            foreach (var htf in candleManager.GetHigherTimeframes())
            {
                _htfSwingPointHistory[htf] = new List<SwingPoint>();
                _detectedOrderBlockSignatures[htf] = new HashSet<string>();
            }
        }

        protected override List<Level> PerformDetection(int currentIndex)
        {
            // HTF Order Block detection is triggered by HTF swing point events
            return new List<Level>();
        }

        /// <summary>
        /// Process a new HTF swing point to detect order block patterns
        /// </summary>
        private void ProcessHtfSwingPoint(SwingPoint swingPoint, TimeFrame timeFrame)
        {
            if (swingPoint == null || timeFrame == null)
                return;

            // Initialize history if needed
            if (!_htfSwingPointHistory.ContainsKey(timeFrame))
                _htfSwingPointHistory[timeFrame] = new List<SwingPoint>();
            if (!_detectedOrderBlockSignatures.ContainsKey(timeFrame))
                _detectedOrderBlockSignatures[timeFrame] = new HashSet<string>();

            var swingPoints = _htfSwingPointHistory[timeFrame];

            // Add to history and sort by index
            swingPoints.Add(swingPoint);
            swingPoints.Sort((a, b) => a.Index.CompareTo(b.Index));

            Level orderBlock = null;

            // Check for order block based on swing point type
            if (swingPoint.SwingType == SwingType.H || swingPoint.SwingType == SwingType.HH)
            {
                // New bullish swing point - check for bullish order block
                orderBlock = CheckForBullishHtfOrderBlock(timeFrame, swingPoints);
            }
            else if (swingPoint.SwingType == SwingType.L || swingPoint.SwingType == SwingType.LL)
            {
                // New bearish swing point - check for bearish order block
                orderBlock = CheckForBearishHtfOrderBlock(timeFrame, swingPoints);
            }

            if (orderBlock != null)
            {
                orderBlock.TimeFrame = timeFrame;
                Repository.Add(orderBlock);
                PublishDetectionEvent(orderBlock, swingPoint.Index);
                LogDetection(orderBlock, swingPoint.Index);
            }
        }

        /// <summary>
        /// Check for bullish HTF order block when a new bullish swing point is created
        /// </summary>
        private Level CheckForBullishHtfOrderBlock(TimeFrame timeFrame, List<SwingPoint> swingPoints)
        {
            try
            {
                // Get bullish and bearish swing points ordered by index descending
                var bullishSwingPoints = swingPoints
                    .Where(sp => sp.Direction == Direction.Up)
                    .OrderByDescending(sp => sp.Index)
                    .ToList();

                var bearishSwingPoints = swingPoints
                    .Where(sp => sp.Direction == Direction.Down)
                    .OrderByDescending(sp => sp.Index)
                    .ToList();

                // Need at least 2 of each type
                if (bullishSwingPoints.Count < 2 || bearishSwingPoints.Count < 2)
                    return null;

                var currentHigh = bullishSwingPoints[0];
                var previousHigh = bullishSwingPoints[1];
                var currentLow = bearishSwingPoints[0];
                var previousLow = bearishSwingPoints[1];

                // Create unique signature
                string signature = $"BULLISH_{timeFrame.GetShortName()}_{previousHigh.Index}_{currentLow.Index}";
                if (_detectedOrderBlockSignatures[timeFrame].Contains(signature))
                    return null;

                // Check conditions:
                // 1. Current high > previous high (higher high)
                bool condition1 = currentHigh.Price > previousHigh.Price;

                // 2. Current low < previous low (lower low)
                bool condition2 = currentLow.Price < previousLow.Price;

                if (!condition1 || !condition2)
                    return null;

                // 3. Get HTF candle at currentHigh and check if it closes above previousHigh
                var htfCandles = CandleManager.GetHigherTimeframeCandles(timeFrame);
                var currentHighCandle = htfCandles?.FirstOrDefault(c => c.Index == currentHigh.Index);

                if (currentHighCandle == null)
                    return null;

                bool closeAbovePreviousHigh = currentHighCandle.Close > previousHigh.Price;

                if (!closeAbovePreviousHigh)
                    return null;

                // Check if order block has already been swept
                if (IsHtfOrderBlockSwept(currentLow.Price, previousHigh.Price,
                    previousHigh.Index, currentLow.Index, Direction.Up, timeFrame))
                    return null;

                // Create bullish order block from previousHigh to currentLow
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    currentLow.Price,           // low
                    previousHigh.Price,         // high
                    currentLow.Time,            // lowTime
                    previousHigh.Time,          // highTime
                    null,                       // midTime
                    Direction.Up,               // direction (bullish)
                    previousHigh.Index,         // index
                    previousHigh.Index,         // indexHigh
                    currentLow.Index,           // indexLow
                    0,                          // indexMid
                    Zone.Equilibrium,
                    null,
                    true                        // isConfirmed
                );

                // Copy properties from the low swing point
                orderBlock.SweptLiquidity = currentLow.SweptLiquidity;
                orderBlock.SweptFib = currentLow.SweptFib;
                orderBlock.InsidePda = currentLow.InsidePda;
                orderBlock.InsideMacro = currentLow.InsideMacro;

                // Mark signature as detected
                _detectedOrderBlockSignatures[timeFrame].Add(signature);

                return orderBlock;
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error checking bullish HTF order block: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check for bearish HTF order block when a new bearish swing point is created
        /// </summary>
        private Level CheckForBearishHtfOrderBlock(TimeFrame timeFrame, List<SwingPoint> swingPoints)
        {
            try
            {
                // Get bullish and bearish swing points ordered by index descending
                var bearishSwingPoints = swingPoints
                    .Where(sp => sp.Direction == Direction.Down)
                    .OrderByDescending(sp => sp.Index)
                    .ToList();

                var bullishSwingPoints = swingPoints
                    .Where(sp => sp.Direction == Direction.Up)
                    .OrderByDescending(sp => sp.Index)
                    .ToList();

                // Need at least 2 of each type
                if (bearishSwingPoints.Count < 2 || bullishSwingPoints.Count < 2)
                    return null;

                var currentLow = bearishSwingPoints[0];
                var previousLow = bearishSwingPoints[1];
                var currentHigh = bullishSwingPoints[0];
                var previousHigh = bullishSwingPoints[1];

                // Create unique signature
                string signature = $"BEARISH_{timeFrame.GetShortName()}_{previousLow.Index}_{currentHigh.Index}";
                if (_detectedOrderBlockSignatures[timeFrame].Contains(signature))
                    return null;

                // Check conditions:
                // 1. Current low < previous low (lower low)
                bool condition1 = currentLow.Price < previousLow.Price;

                // 2. Current high > previous high (higher high)
                bool condition2 = currentHigh.Price > previousHigh.Price;

                if (!condition1 || !condition2)
                    return null;

                // 3. Get HTF candle at currentLow and check if it closes below previousLow
                var htfCandles = CandleManager.GetHigherTimeframeCandles(timeFrame);
                var currentLowCandle = htfCandles?.FirstOrDefault(c => c.Index == currentLow.Index);

                if (currentLowCandle == null)
                    return null;

                bool closeBelowPreviousLow = currentLowCandle.Close < previousLow.Price;

                if (!closeBelowPreviousLow)
                    return null;

                // Check if order block has already been swept
                if (IsHtfOrderBlockSwept(previousLow.Price, currentHigh.Price,
                    previousLow.Index, currentHigh.Index, Direction.Down, timeFrame))
                    return null;

                // Create bearish order block from previousLow to currentHigh
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    previousLow.Price,          // low
                    currentHigh.Price,          // high
                    previousLow.Time,           // lowTime
                    currentHigh.Time,           // highTime
                    null,                       // midTime
                    Direction.Down,             // direction (bearish)
                    previousLow.Index,          // index
                    currentHigh.Index,          // indexHigh
                    previousLow.Index,          // indexLow
                    0,                          // indexMid
                    Zone.Equilibrium,
                    null,
                    true                        // isConfirmed
                );

                // Copy properties from the high swing point
                orderBlock.SweptLiquidity = currentHigh.SweptLiquidity;
                orderBlock.SweptFib = currentHigh.SweptFib;
                orderBlock.InsidePda = currentHigh.InsidePda;
                orderBlock.InsideMacro = currentHigh.InsideMacro;

                // Mark signature as detected
                _detectedOrderBlockSignatures[timeFrame].Add(signature);

                return orderBlock;
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error checking bearish HTF order block: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if an HTF order block has been swept by price action
        /// </summary>
        private bool IsHtfOrderBlockSwept(double low, double high, int startIndex, int endIndex, Direction direction, TimeFrame timeFrame)
        {
            try
            {
                var htfCandles = CandleManager.GetHigherTimeframeCandles(timeFrame);
                if (htfCandles == null || htfCandles.Count == 0)
                    return false;

                int checkStart = Math.Max(startIndex, endIndex) + 1;

                // Check HTF candles after the order block formation
                foreach (var candle in htfCandles.Where(c => c.Index.HasValue && c.Index.Value > checkStart))
                {
                    if (direction == Direction.Up)
                    {
                        // Bullish order block is swept if price goes below its low
                        if (candle.Low < low)
                            return true;
                    }
                    else
                    {
                        // Bearish order block is swept if price goes above its high
                        if (candle.High > high)
                            return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error checking if HTF order block is swept: {ex.Message}");
                return false;
            }
        }

        protected override void PublishDetectionEvent(Level orderBlock, int currentIndex)
        {
            EventAggregator.Publish(new HtfOrderBlockDetectedEvent(orderBlock, orderBlock.TimeFrame));

            if (Settings.Patterns.ShowHtfOrderBlock && _visualizer != null)
            {
                _visualizer.Draw(orderBlock);
            }
        }

        protected override void LogDetection(Level orderBlock, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"HTF Order Block detected: {orderBlock.TimeFrame?.GetShortName()} {orderBlock.Direction} at index {currentIndex}, " +
                       $"Range: {orderBlock.Low:F5} - {orderBlock.High:F5}");
            }
        }

        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(l => l.LevelType == LevelType.OrderBlock &&
                                       l.Direction == direction &&
                                       l.TimeFrame != null);
        }

        public List<Level> GetByTimeFrame(TimeFrame timeframe)
        {
            return Repository.Find(l => l.LevelType == LevelType.OrderBlock &&
                                       l.TimeFrame != null &&
                                       l.TimeFrame.Equals(timeframe));
        }

        public override bool IsValid(Level level, int currentIndex)
        {
            return level?.LevelType == LevelType.OrderBlock && level.TimeFrame != null;
        }

        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<HtfSwingPointDetectedEvent>(OnHtfSwingPointDetected);
        }

        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<HtfSwingPointDetectedEvent>(OnHtfSwingPointDetected);
        }

        private void OnHtfSwingPointDetected(HtfSwingPointDetectedEvent evt)
        {
            if (evt?.SwingPoint != null && evt.TimeFrame != null)
            {
                ProcessHtfSwingPoint(evt.SwingPoint, evt.TimeFrame);
            }
        }

        /// <summary>
        /// Get all HTF Order Blocks across all timeframes
        /// </summary>
        public List<Level> GetAllHtfOrderBlocks()
        {
            return Repository.Find(l => l.LevelType == LevelType.OrderBlock && l.TimeFrame != null);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _htfSwingPointHistory.Clear();
            _detectedOrderBlockSignatures.Clear();
        }
    }
}
