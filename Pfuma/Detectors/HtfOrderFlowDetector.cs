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
            
            // Validate and store
            if (PostDetectionValidation(bullishOrderFlow, newSwingLow.Index))
            {
                Repository.Add(bullishOrderFlow);
                PublishDetectionEvent(bullishOrderFlow, newSwingLow.Index);
                
                Logger?.Invoke($"{timeFrame.GetShortName()} Bullish Order Flow detected from {previousSwingLow.Index} to {recentSwingHigh.Index}");
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
            
            // Validate and store
            if (PostDetectionValidation(bearishOrderFlow, newSwingHigh.Index))
            {
                Repository.Add(bearishOrderFlow);
                PublishDetectionEvent(bearishOrderFlow, newSwingHigh.Index);
                
                Logger?.Invoke($"{timeFrame.GetShortName()} Bearish Order Flow detected from {previousSwingHigh.Index} to {recentSwingLow.Index}");
            }
        }
        
        /// <summary>
        /// Create an order flow Level object
        /// </summary>
        private Level CreateOrderFlow(SwingPoint fromPoint, SwingPoint toPoint, Direction direction, TimeFrame timeframe)
        {
            var orderFlow = new Level(
                LevelType.Orderflow,                                          // levelType
                Math.Min(fromPoint.Price, toPoint.Price),                     // low
                Math.Max(fromPoint.Price, toPoint.Price),                     // high
                direction == Direction.Up ? fromPoint.Time : toPoint.Time,   // lowTime
                direction == Direction.Up ? toPoint.Time : fromPoint.Time,   // highTime
                null,                                                         // midTime
                direction,                                                    // direction
                direction == Direction.Up ? toPoint.Index : fromPoint.Index, // index
                direction == Direction.Up ? toPoint.Index : fromPoint.Index, // indexHigh
                direction == Direction.Up ? fromPoint.Index : toPoint.Index  // indexLow
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
            
            if (Settings.Patterns.ShowOrderFlow && _visualizer != null)
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