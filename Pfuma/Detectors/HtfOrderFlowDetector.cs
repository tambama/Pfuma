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
    /// Detects Higher Timeframe Order Flow patterns between HTF swing points
    /// </summary>
    public class HtfOrderFlowDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly Dictionary<TimeFrame, List<SwingPoint>> _htfSwingPointHistory;
        
        public HtfOrderFlowDetector(
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
        }
        
        protected override int GetMinimumBarsRequired()
        {
            return 3; // Need at least 3 bars to form order flow pattern
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            // HTF order flow detection is triggered by HTF swing point events,
            // not by bar updates, so we return empty here
            return new List<Level>();
        }
        
        /// <summary>
        /// Process a new HTF swing point to detect order flow patterns
        /// </summary>
        public void ProcessHtfSwingPoint(SwingPoint swingPoint, TimeFrame timeFrame)
        {
            if (swingPoint == null)
                return;
                
            // Initialize timeframe history if needed
            if (!_htfSwingPointHistory.ContainsKey(timeFrame))
            {
                _htfSwingPointHistory[timeFrame] = new List<SwingPoint>();
            }
            
            var swingPoints = _htfSwingPointHistory[timeFrame];
            
            // Add to history
            swingPoints.Add(swingPoint);
            swingPoints.Sort((a, b) => a.Index.CompareTo(b.Index));
            
            // Detect order flow based on swing point direction
            if (swingPoint.Direction == Direction.Down)
            {
                ProcessNewSwingLow(swingPoint, timeFrame, swingPoints);
            }
            else if (swingPoint.Direction == Direction.Up)
            {
                ProcessNewSwingHigh(swingPoint, timeFrame, swingPoints);
            }
        }
        
        /// <summary>
        /// Process new swing low for bullish order flow detection
        /// </summary>
        private void ProcessNewSwingLow(SwingPoint newSwingLow, TimeFrame timeFrame, List<SwingPoint> swingPoints)
        {
            // Get most recent swing high before this low
            var recentSwingHighs = swingPoints
                .Where(p => p.Direction == Direction.Up && p.Index < newSwingLow.Index)
                .OrderByDescending(p => p.Index)
                .ToList();
                
            if (recentSwingHighs.Count == 0)
                return;
                
            var recentSwingHigh = recentSwingHighs.First();
            
            // Get previous swing low before the recent swing high
            var previousSwingLows = swingPoints
                .Where(p => p.Direction == Direction.Down && p.Index < recentSwingHigh.Index)
                .OrderByDescending(p => p.Index)
                .ToList();
                
            if (previousSwingLows.Count == 0)
                return;
                
            var previousSwingLow = previousSwingLows.First();
            
            // Create bullish order flow from previous low to recent high
            var bullishOrderFlow = CreateOrderFlow(
                previousSwingLow,
                recentSwingHigh,
                Direction.Up,
                timeFrame
            );

            // Check for swept swing highs (bullish orderflow sweeps bullish swing points)
            CheckForSweptSwingHighs(bullishOrderFlow, timeFrame, swingPoints);

            // Validate and store
            if (PostDetectionValidation(bullishOrderFlow, newSwingLow.Index))
            {
                Repository.Add(bullishOrderFlow);
                PublishDetectionEvent(bullishOrderFlow, newSwingLow.Index);

                // HTF bullish order flow detected
            }
        }
        
        /// <summary>
        /// Process new swing high for bearish order flow detection
        /// </summary>
        private void ProcessNewSwingHigh(SwingPoint newSwingHigh, TimeFrame timeFrame, List<SwingPoint> swingPoints)
        {
            // Get most recent swing low before this high
            var recentSwingLows = swingPoints
                .Where(p => p.Direction == Direction.Down && p.Index < newSwingHigh.Index)
                .OrderByDescending(p => p.Index)
                .ToList();
                
            if (recentSwingLows.Count == 0)
                return;
                
            var recentSwingLow = recentSwingLows.First();
            
            // Get previous swing high before the recent swing low
            var previousSwingHighs = swingPoints
                .Where(p => p.Direction == Direction.Up && p.Index < recentSwingLow.Index)
                .OrderByDescending(p => p.Index)
                .ToList();
                
            if (previousSwingHighs.Count == 0)
                return;
                
            var previousSwingHigh = previousSwingHighs.First();
            
            // Create bearish order flow from previous high to recent low
            var bearishOrderFlow = CreateOrderFlow(
                previousSwingHigh,
                recentSwingLow,
                Direction.Down,
                timeFrame
            );

            // Check for swept swing lows (bearish orderflow sweeps bearish swing points)
            CheckForSweptSwingLows(bearishOrderFlow, timeFrame, swingPoints);

            // Validate and store
            if (PostDetectionValidation(bearishOrderFlow, newSwingHigh.Index))
            {
                Repository.Add(bearishOrderFlow);
                PublishDetectionEvent(bearishOrderFlow, newSwingHigh.Index);

                // HTF bearish order flow detected
            }
        }
        
        /// <summary>
        /// Check for swept swing lows within the bearish orderflow range
        /// For bearish orderflow: check if any previous swing lows (bearish swing points) were swept (price went below them)
        /// Only sweeps unswept swing points
        /// </summary>
        private void CheckForSweptSwingLows(Level orderflow, TimeFrame _, List<SwingPoint> swingPoints)
        {
            // For bearish orderflow, check if any swing lows were swept
            // A swing low is swept if the orderflow's low went below it
            // Only consider unswept swing points
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);

            var sweptLows = swingPoints
                .Where(p => p.Direction == Direction.Down &&
                           !p.Swept &&
                           p.Index >= startIndex &&
                           p.Index <= endIndex &&
                           orderflow.Low < p.Price)
                .ToList();

            if (sweptLows.Count > 0)
            {
                orderflow.SweptSwingPoints = new List<SwingPoint>();

                // Get the lowest swept point (most significant liquidity)
                var lowestSweptPoint = sweptLows.OrderBy(l => l.Price).First();

                // Mark ALL swept swing points as swept
                foreach (var sweptPoint in sweptLows)
                {
                    sweptPoint.Swept = true;
                    sweptPoint.SweptLiquidity = true;
                    sweptPoint.SweptLiquidityPrice = sweptPoint.Price;
                    orderflow.SweptSwingPoints.Add(sweptPoint);
                }

                // Set the most significant swept point as the main one
                orderflow.SweptSwingPoint = lowestSweptPoint;

                Logger?.Invoke($"HTF Bearish OrderFlow swept {sweptLows.Count} swing low(s), lowest at {lowestSweptPoint.Price:F5}");
            }
        }

        /// <summary>
        /// Check for swept swing highs within the bullish orderflow range
        /// For bullish orderflow: check if any previous swing highs (bullish swing points) were swept (price went above them)
        /// Only sweeps unswept swing points
        /// </summary>
        private void CheckForSweptSwingHighs(Level orderflow, TimeFrame _, List<SwingPoint> swingPoints)
        {
            // For bullish orderflow, check if any swing highs were swept
            // A swing high is swept if the orderflow's high went above it
            // Only consider unswept swing points
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);

            var sweptHighs = swingPoints
                .Where(p => p.Direction == Direction.Up &&
                           !p.Swept &&
                           p.Index >= startIndex &&
                           p.Index <= endIndex &&
                           orderflow.High > p.Price)
                .ToList();

            if (sweptHighs.Count > 0)
            {
                orderflow.SweptSwingPoints = new List<SwingPoint>();

                // Get the highest swept point (most significant liquidity)
                var highestSweptPoint = sweptHighs.OrderByDescending(h => h.Price).First();

                // Mark ALL swept swing points as swept
                foreach (var sweptPoint in sweptHighs)
                {
                    sweptPoint.Swept = true;
                    sweptPoint.SweptLiquidity = true;
                    sweptPoint.SweptLiquidityPrice = sweptPoint.Price;
                    orderflow.SweptSwingPoints.Add(sweptPoint);
                }

                // Set the most significant swept point as the main one
                orderflow.SweptSwingPoint = highestSweptPoint;

                Logger?.Invoke($"HTF Bullish OrderFlow swept {sweptHighs.Count} swing high(s), highest at {highestSweptPoint.Price:F5}");
            }
        }

        /// <summary>
        /// Create an order flow Level object
        /// </summary>
        private Level CreateOrderFlow(SwingPoint fromPoint, SwingPoint toPoint, Direction direction, TimeFrame timeframe)
        {
            // Determine which point is high/low based on price, not direction
            bool fromIsLow = fromPoint.Price < toPoint.Price;
            var lowPoint = fromIsLow ? fromPoint : toPoint;
            var highPoint = fromIsLow ? toPoint : fromPoint;
            
            // For consistent chronological ordering:
            // - Bullish: index = starting point (fromPoint which is the low)
            // - Bearish: index = ending point (toPoint which is the low)
            int mainIndex = direction == Direction.Up ? fromPoint.Index : toPoint.Index;
            
            var orderFlow = new Level(
                LevelType.Orderflow,           // levelType
                lowPoint.Price,                // low
                highPoint.Price,               // high
                lowPoint.Time,                 // lowTime
                highPoint.Time,                // highTime
                null,                          // midTime
                direction,                     // direction
                mainIndex,                     // index (starting point)
                highPoint.Index,               // indexHigh (where high occurs)
                lowPoint.Index                 // indexLow (where low occurs)
            );
            
            orderFlow.TimeFrame = timeframe;
            orderFlow.InitializeQuadrants();
            
            return orderFlow;
        }
        
        protected override bool PostDetectionValidation(Level orderFlow, int currentIndex)
        {
            return base.PostDetectionValidation(orderFlow, currentIndex) && 
                   orderFlow.LevelType == LevelType.Orderflow &&
                   orderFlow.TimeFrame != null;
        }
        
        protected override void PublishDetectionEvent(Level orderFlow, int currentIndex)
        {
            EventAggregator.Publish(new HtfOrderFlowDetectedEvent(orderFlow, orderFlow.TimeFrame));
            
            if (Settings.Patterns.ShowHtfOrderFlow && _visualizer != null)
            {
                _visualizer.Draw(orderFlow);
            }
        }
        
        protected override void LogDetection(Level orderFlow, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"HTF Order Flow detected: {orderFlow.Direction} at timeframe {orderFlow.TimeFrame?.GetShortName()} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(of => of.Direction == direction && 
                                        of.LevelType == LevelType.Orderflow &&
                                        of.TimeFrame != null);
        }
        
        public override bool IsValid(Level orderFlow, int currentIndex)
        {
            return orderFlow != null && 
                   orderFlow.LevelType == LevelType.Orderflow &&
                   orderFlow.TimeFrame != null;
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
            ProcessHtfSwingPoint(evt.SwingPoint, evt.TimeFrame);
        }
        
        /// <summary>
        /// Get HTF order flows for a specific timeframe
        /// </summary>
        public List<Level> GetHtfOrderFlows(TimeFrame timeframe)
        {
            return Repository.Find(of => of.LevelType == LevelType.Orderflow && 
                                        of.TimeFrame != null && 
                                        of.TimeFrame.Equals(timeframe));
        }
        
        /// <summary>
        /// Clear swing point history for a specific timeframe
        /// </summary>
        public void ClearTimeframeHistory(TimeFrame timeframe)
        {
            if (_htfSwingPointHistory.ContainsKey(timeframe))
            {
                _htfSwingPointHistory[timeframe].Clear();
            }
        }
        
        protected override void OnDispose()
        {
            base.OnDispose();
            _htfSwingPointHistory.Clear();
        }
    }
}