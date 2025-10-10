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
using Pfuma.Visualization;
using FibonacciLevel = Pfuma.Models.FibonacciLevel;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects CISD (Consecutive Identical Sequential Direction) patterns
    /// </summary>
    public class CisdDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly IFibonacciService _fibonacciService;
        private readonly FibonacciVisualizer _fibonacciVisualizer;
        private readonly IRepository<SwingPoint> _swingPointRepository;
        private readonly TimeManager _timeManager;
        private readonly int _maxCisdsPerDirection;
        
        public CisdDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            IFibonacciService fibonacciService,
            FibonacciVisualizer fibonacciVisualizer,
            IRepository<SwingPoint> swingPointRepository,
            TimeManager timeManager,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _fibonacciService = fibonacciService;
            _fibonacciVisualizer = fibonacciVisualizer;
            _swingPointRepository = swingPointRepository;
            _timeManager = timeManager;
            _maxCisdsPerDirection = settings.Patterns.MaxCisdsPerDirection;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            // CISD detection is triggered by order flow events
            return new List<Level>();
        }
        
        /// <summary>
        /// Detects CISD from an orderflow that swept liquidity
        /// </summary>
        public void DetectCisdFromOrderFlow(Level orderflow)
        {
            if (orderflow == null || orderflow.SweptSwingPoint == null)
                return;
            
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);
            
            if (startIndex < 0 || endIndex < 0 || startIndex >= CandleManager.Count || endIndex >= CandleManager.Count)
                return;
            
            Level cisdLevel = null;
            
            if (orderflow.Direction == Direction.Up)
            {
                cisdLevel = DetectBearishCisd(orderflow, startIndex, endIndex);
            }
            else
            {
                cisdLevel = DetectBullishCisd(orderflow, startIndex, endIndex);
            }
            
            if (cisdLevel != null)
            {
                // Copy properties from the boundary swing point of the order flow
                SwingPoint boundarySwingPoint = null;
                
                if (cisdLevel.Direction == Direction.Up) // Bullish CISD
                {
                    // Copy properties from low boundary swing point of the order flow
                    boundarySwingPoint = FindSwingPointAtIndex(orderflow.IndexLow);
                }
                else if (cisdLevel.Direction == Direction.Down) // Bearish CISD
                {
                    // Copy properties from high boundary swing point of the order flow
                    boundarySwingPoint = FindSwingPointAtIndex(orderflow.IndexHigh);
                }
                
                if (boundarySwingPoint != null)
                {
                    cisdLevel.SweptLiquidity = boundarySwingPoint.SweptLiquidity;
                    cisdLevel.SweptFib = boundarySwingPoint.SweptFib;
                    cisdLevel.InsidePda = boundarySwingPoint.InsidePda;
                    cisdLevel.InsideMacro = boundarySwingPoint.InsideMacro;
                }
                
                // Associate with orderflow
                orderflow.CISDLevel = cisdLevel;
                
                // Manage max CISDs before adding
                ManageMaxCisdCount(cisdLevel.Direction);
                
                // Store and publish
                Repository.Add(cisdLevel);
                PublishDetectionEvent(cisdLevel, endIndex);
            }
        }
        
        private Level DetectBearishCisd(Level orderflow, int startIndex, int endIndex)
        {
            // Find all sets of consecutive bullish candles within the orderflow
            List<List<int>> bullishSets = new List<List<int>>();
            List<int> currentSet = new List<int>();
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                var bar = CandleManager.GetCandle(i);
                var direction = bar.GetCandleDirection();
                
                if (direction == Direction.Up)
                {
                    currentSet.Add(i);
                }
                else if (currentSet.Count > 0)
                {
                    // If this is the last candle and it's bearish, add it to the current set
                    if (i == endIndex)
                    {
                        currentSet.Add(i);
                    }
                    bullishSets.Add(new List<int>(currentSet));
                    currentSet.Clear();
                }
            }
            
            if (currentSet.Count > 0)
            {
                bullishSets.Add(new List<int>(currentSet));
            }
            
            if (bullishSets.Count == 0)
                return null;
            
            // Use the last set of consecutive bullish candles
            var lastBullishSet = bullishSets[bullishSets.Count - 1];
            
            if (lastBullishSet.Count == 0)
                return null;
            
            int firstBullishIndex = lastBullishSet.Min();
            int lastBullishIndex = lastBullishSet.Max();
            
            // Create a BEARISH CISD level
            var cisdLevel = new Level(
                LevelType.CISD,
                CandleManager.GetCandle(firstBullishIndex).Open,
                CandleManager.GetCandle(lastBullishIndex).High,
                CandleManager.GetCandle(firstBullishIndex).Time,
                CandleManager.GetCandle(lastBullishIndex).Time,
                null,
                Direction.Down,
                firstBullishIndex,
                lastBullishIndex,
                firstBullishIndex
            );
            
            cisdLevel.OrderFlowId = orderflow.Id;
            
            // Set TimeFrame from candle
            cisdLevel.TimeFrame = CandleManager.GetCandle(firstBullishIndex).TimeFrame;
            
            return cisdLevel;
        }
        
        private Level DetectBullishCisd(Level orderflow, int startIndex, int endIndex)
        {
            // Find all sets of consecutive bearish candles within the orderflow
            List<List<int>> bearishSets = new List<List<int>>();
            List<int> currentSet = new List<int>();
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                var bar = CandleManager.GetCandle(i);
                var direction = bar.GetCandleDirection();
                
                if (direction == Direction.Down)
                {
                    currentSet.Add(i);
                }
                else if (currentSet.Count > 0)
                {
                    // If this is the last candle and it's bullish, add it to the current set
                    if (i == endIndex)
                    {
                        currentSet.Add(i);
                    }
                    bearishSets.Add(new List<int>(currentSet));
                    currentSet.Clear();
                }
            }
            
            if (currentSet.Count > 0)
            {
                bearishSets.Add(new List<int>(currentSet));
            }
            
            if (bearishSets.Count == 0)
                return null;
            
            // Use the last set of consecutive bearish candles
            var lastBearishSet = bearishSets[bearishSets.Count - 1];
            
            if (lastBearishSet.Count == 0)
                return null;
            
            int firstBearishIndex = lastBearishSet.Min();
            int lastBearishIndex = lastBearishSet.Max();
            
            // Create a BULLISH CISD level
            var cisdLevel = new Level(
                LevelType.CISD,
                CandleManager.GetCandle(lastBearishIndex).Low,
                CandleManager.GetCandle(firstBearishIndex).Open,
                CandleManager.GetCandle(lastBearishIndex).Time,
                CandleManager.GetCandle(firstBearishIndex).Time,
                null,
                Direction.Up,
                firstBearishIndex,
                firstBearishIndex,
                lastBearishIndex
            );
            
            cisdLevel.OrderFlowId = orderflow.Id;
            
            // Set TimeFrame from candle
            cisdLevel.TimeFrame = CandleManager.GetCandle(firstBearishIndex).TimeFrame;
            
            return cisdLevel;
        }
        
        /// <summary>
        /// Checks for CISD confirmation on a swing point
        /// </summary>
        public void CheckCisdConfirmation(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
            
            var pendingCisdLevels = Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && !cisd.IsConfirmed);
            
            foreach (var cisd in pendingCisdLevels)
            {
                bool isConfirmed = false;
                
                if (cisd.Direction == Direction.Up) // Bullish CISD
                {
                    // Bullish CISD is confirmed when a bullish candle closes above the CISD high
                    if (swingPoint.CandleDirection == Direction.Up && 
                        swingPoint.Bar.Open < cisd.High &&
                        swingPoint.Bar.Close > cisd.High)
                    {
                        isConfirmed = true;
                    }
                }
                else // Bearish CISD
                {
                    // Bearish CISD is confirmed when a bearish candle closes below the CISD low
                    if (swingPoint.CandleDirection == Direction.Down && 
                        swingPoint.Bar.Open > cisd.Low &&
                        swingPoint.Bar.Close < cisd.Low)
                    {
                        isConfirmed = true;
                    }
                }
                
                if (isConfirmed)
                {
                    cisd.IsConfirmed = true;
                    cisd.IndexOfConfirmingCandle = swingPoint.Index;

                    // Check if CISD time range overlaps with macro time
                    CheckCisdMacroTimeOverlap(cisd, swingPoint);

                    // Create Fibonacci levels for the confirmed CISD
                    CreateCisdFibonacciLevels(cisd);

                    // Check for OTE condition if enabled
                    if (Settings.Patterns.ShowOTE)
                    {
                        CheckOTECondition(cisd);
                    }

                    // Publish confirmation event
                    EventAggregator.Publish(new CisdConfirmedEvent(cisd, cisd.Direction));

                    // Update visualization
                    _visualizer?.Update(cisd);
                }
            }
        }
        
        /// <summary>
        /// Checks for CISD activation on a bar
        /// </summary>
        public void CheckCisdActivation(Bar bar, int barIndex)
        {
            if (bar == null)
                return;
            
            var confirmedCisdLevels = Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && cisd.IsConfirmed && !cisd.Activated);
            
            foreach (var cisd in confirmedCisdLevels)
            {
                bool isActivated = false;
                
                if (cisd.Direction == Direction.Up) // Bullish CISD
                {
                    // Activated when price moves below the CISD level
                    if (bar.Open > cisd.High && bar.Low < cisd.High)
                    {
                        isActivated = true;
                    }
                }
                else // Bearish CISD
                {
                    // Activated when price moves above the CISD level
                    if (bar.Open < cisd.Low && bar.High > cisd.Low)
                    {
                        isActivated = true;
                    }
                }
                
                if (isActivated)
                {
                    cisd.Activated = true;
                    
                    // Publish activation event
                    EventAggregator.Publish(new CisdActivatedEvent(cisd, barIndex));
                    
                    // Update visualization
                    _visualizer?.Update(cisd);
                }
            }
        }
        
        /// <summary>
        /// Checks if the CISD confirmation time range overlaps with macro time
        /// </summary>
        private void CheckCisdMacroTimeOverlap(Level cisd, SwingPoint confirmingSwingPoint)
        {
            if (cisd == null || confirmingSwingPoint == null || string.IsNullOrEmpty(cisd.OrderFlowId))
                return;
            
            // Find the orderflow that created this CISD
            var orderFlow = Repository.Find(level => 
                level.Id == cisd.OrderFlowId && 
                level.LevelType == LevelType.Orderflow)
                .FirstOrDefault();
            
            if (orderFlow == null)
                return;
            
            DateTime orderFlowBoundaryTime;
            DateTime confirmingTime = confirmingSwingPoint.Time;
            
            if (cisd.Direction == Direction.Up) // Bullish CISD
            {
                // For bullish CISD, use HighTime of the bearish orderflow
                orderFlowBoundaryTime = orderFlow.HighTime;
            }
            else // Bearish CISD
            {
                // For bearish CISD, use LowTime of the bullish orderflow
                orderFlowBoundaryTime = orderFlow.LowTime;
            }
            
            // Check if the time range between orderflow boundary and confirmation overlaps with macro time
            if (_timeManager != null && DoesTimeRangeOverlapMacro(orderFlowBoundaryTime, confirmingTime))
            {
                cisd.InsideMacro = true;
            }
        }
        
        /// <summary>
        /// Checks if a time range overlaps with any macro time periods
        /// </summary>
        private bool DoesTimeRangeOverlapMacro(DateTime startTime, DateTime endTime)
        {
            if (_timeManager == null)
                return false;
            
            // Ensure start is before end
            if (startTime > endTime)
            {
                (startTime, endTime) = (endTime, startTime);
            }
            
            // Check each time point in the range to see if any falls within macro time
            var current = startTime;
            while (current <= endTime)
            {
                if (_timeManager.IsInsideMacroTime(current))
                {
                    return true;
                }
                // Check every minute in the range
                current = current.AddMinutes(1);
            }
            
            return false;
        }
        
        /// <summary>
        /// Creates Fibonacci levels for a confirmed CISD based on its orderflow
        /// </summary>
        private void CreateCisdFibonacciLevels(Level cisd)
        {
            if (cisd == null || string.IsNullOrEmpty(cisd.OrderFlowId))
                return;
            
            // Find the orderflow that created this CISD
            var orderFlow = Repository.Find(level => 
                level.Id == cisd.OrderFlowId && 
                level.LevelType == LevelType.Orderflow)
                .FirstOrDefault();
            
            if (orderFlow == null)
                return;
            
            FibonacciLevel fibLevel = null;
            
            if (cisd.Direction == Direction.Up) // Bullish CISD
            {
                // For bullish CISD, use low to high of the bearish orderflow
                fibLevel = new FibonacciLevel(
                    orderFlow.IndexLow,  // Start at the low
                    orderFlow.IndexHigh, // End at the high
                    orderFlow.Low,       // Start price (low)
                    orderFlow.High,      // End price (high)
                    orderFlow.LowTime,   // Start time
                    orderFlow.HighTime,  // End time
                    FibType.CISD
                );
            }
            else // Bearish CISD
            {
                // For bearish CISD, use high to low of the bullish orderflow
                fibLevel = new FibonacciLevel(
                    orderFlow.IndexHigh, // Start at the high
                    orderFlow.IndexLow,  // End at the low
                    orderFlow.High,      // Start price (high)
                    orderFlow.Low,       // End price (low)
                    orderFlow.HighTime,  // Start time
                    orderFlow.LowTime,   // End time
                    FibType.CISD
                );
            }
                
            // Add to the CISD Fibonacci levels collection
            _fibonacciService?.AddCisdFibonacciLevel(fibLevel);
                
            // Draw the Fibonacci levels on the chart
            // The visualizer will draw them when DrawFibonacciLevels is called
        }
        
        private void ManageMaxCisdCount(Direction direction)
        {
            var unconfirmedCisds = Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && 
                cisd.Direction == direction && 
                !cisd.IsConfirmed)
                .OrderBy(cisd => cisd.Index)
                .ToList();
            
            while (unconfirmedCisds.Count >= _maxCisdsPerDirection && unconfirmedCisds.Count > 0)
            {
                var oldestCisd = unconfirmedCisds.First();
                Repository.Remove(oldestCisd);
                _visualizer?.Remove(oldestCisd);
                unconfirmedCisds.Remove(oldestCisd);
            }
        }
        
        protected override void PublishDetectionEvent(Level cisd, int currentIndex)
        {
            EventAggregator.Publish(new CisdDetectedEvent(cisd));
            
            if (Settings.Patterns.ShowCISD && _visualizer != null)
            {
                _visualizer.Draw(cisd);
            }
        }
        
        protected override void LogDetection(Level cisd, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"CISD detected: {cisd.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && 
                cisd.Direction == direction);
        }
        
        public override bool IsValid(Level cisd, int currentIndex)
        {
            return cisd != null && 
                   cisd.LevelType == LevelType.CISD && 
                   (!cisd.Activated || cisd.Index < currentIndex);
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
            EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
            EventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        private void OnOrderFlowDetected(OrderFlowDetectedEvent evt)
        {
            if (evt.OrderFlow != null && evt.OrderFlow.SweptSwingPoint != null)
            {
                DetectCisdFromOrderFlow(evt.OrderFlow);
            }
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            CheckCisdConfirmation(evt.SwingPoint);
        }

        /// <summary>
        /// Checks if a confirmed CISD meets the OTE (Optimum Trade Entry) condition
        /// </summary>
        private void CheckOTECondition(Level cisd)
        {
            if (cisd == null || !cisd.IsConfirmed)
                return;

            if (cisd.Direction == Direction.Down) // Bearish CISD
            {
                CheckBearishOTE(cisd);
            }
            else if (cisd.Direction == Direction.Up) // Bullish CISD
            {
                CheckBullishOTE(cisd);
            }
        }

        /// <summary>
        /// Checks for Bearish OTE condition
        /// </summary>
        private void CheckBearishOTE(Level cisd)
        {
            // Find the nearest unswept session high (PSH or PDH)
            // Order by index descending and get the first one
            var unsweptSessionHighs = _swingPointRepository
                .Find(sp => (sp.LiquidityType == LiquidityType.PSH) &&
                           !sp.Swept)
                .OrderByDescending(sp => sp.Index)
                .ToList();

            if (unsweptSessionHighs.Count == 0)
                return;

            var oteHigh = unsweptSessionHighs.First();

            // Get candles between oteHigh.Time (inclusive) and CISD high time (exclusive)
            var cisdHighCandle = CandleManager.GetCandle(cisd.IndexHigh);
            if (cisdHighCandle == null)
                return;

            var candlesBetween = CandleManager.GetCandlesBetween(oteHigh.Time, cisdHighCandle.Time)
                .Where(c => c.Time < cisdHighCandle.Time) // Exclude CISD high time
                .ToList();

            if (candlesBetween.Count == 0)
                return;

            // Find the lowest point between oteHigh and CISD high
            var (lowestIndex, lowestTime, oteLow) = candlesBetween.FindLowestPoint();

            if (lowestIndex == -1)
                return;

            // Check if CISD's swing high is above 61% of the distance from oteLow up to oteHigh
            // For bearish OTE, we measure 61% UP from oteLow towards oteHigh
            double sixtyOnePercent = oteLow + (oteHigh.Price - oteLow) * 0.75;

            if (cisd.High > sixtyOnePercent)
            {
                // This is an OTE entry - convert CISD to OTE
                cisd.LevelType = LevelType.OTE;

                // Find the swing point at oteLow for the event
                var oteLowSwingPoint = _swingPointRepository
                    .Find(sp => sp.Index == lowestIndex)
                    .FirstOrDefault();

                // Fire OTE detected event
                EventAggregator.Publish(new OteDetectedEvent(cisd, oteHigh, oteLowSwingPoint));
            }
        }

        /// <summary>
        /// Checks for Bullish OTE condition
        /// </summary>
        private void CheckBullishOTE(Level cisd)
        {
            // Find the nearest unswept session low (PSL or PDL)
            // Order by index descending and get the first one
            var unsweptSessionLows = _swingPointRepository
                .Find(sp => (sp.LiquidityType == LiquidityType.PSL) &&
                           !sp.Swept)
                .OrderByDescending(sp => sp.Index)
                .ToList();

            if (unsweptSessionLows.Count == 0)
                return;

            var oteLow = unsweptSessionLows.First();

            // Get candles between oteLow.Time (inclusive) and CISD low time (exclusive)
            var cisdLowCandle = CandleManager.GetCandle(cisd.IndexLow);
            if (cisdLowCandle == null)
                return;

            var candlesBetween = CandleManager.GetCandlesBetween(oteLow.Time, cisdLowCandle.Time)
                .Where(c => c.Time < cisdLowCandle.Time) // Exclude CISD low time
                .ToList();

            if (candlesBetween.Count == 0)
                return;

            // Find the highest point between oteLow and CISD low
            var (highestIndex, highestTime, oteHigh) = candlesBetween.FindHighestPoint();

            if (highestIndex == -1)
                return;

            // Check if CISD's swing low is below 61% of the distance from oteHigh down to oteLow
            // For bullish OTE, we measure 61% DOWN from oteHigh towards oteLow
            double sixtyOnePercent = oteHigh - (oteHigh - oteLow.Price) * 0.75;

            if (cisd.Low < sixtyOnePercent)
            {
                // This is an OTE entry - convert CISD to OTE
                cisd.LevelType = LevelType.OTE;

                // Find the swing point at oteHigh for the event
                var oteHighSwingPoint = _swingPointRepository
                    .Find(sp => Math.Abs(sp.Index - highestIndex) <= 2)
                    .OrderBy(sp => Math.Abs(sp.Index - highestIndex))
                    .FirstOrDefault();

                // Fire OTE detected event
                EventAggregator.Publish(new OteDetectedEvent(cisd, oteHighSwingPoint, oteLow));
            }
        }

        /// <summary>
        /// Helper method to find swing point at a specific index with tolerance
        /// </summary>
        private SwingPoint FindSwingPointAtIndex(int targetIndex)
        {
            // Try exact match first
            var exactMatch = _swingPointRepository
                .Find(sp => sp.Index == targetIndex)
                .FirstOrDefault();

            if (exactMatch != null)
                return exactMatch;

            // If no exact match, look within a small range (±2 indices)
            var toleranceMatch = _swingPointRepository
                .Find(sp => Math.Abs(sp.Index - targetIndex) <= 2)
                .OrderBy(sp => Math.Abs(sp.Index - targetIndex))
                .FirstOrDefault();

            return toleranceMatch;
        }
    }
}