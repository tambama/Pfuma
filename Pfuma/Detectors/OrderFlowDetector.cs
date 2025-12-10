using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors.Base;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects Order Flow patterns between swing points
    /// </summary>
    public class OrderFlowDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly SwingPointDetector _swingPointDetector;
        private readonly List<SwingPoint> _swingPointHistory;
        private readonly OrderflowFvgTracker _fvgTracker;

        public OrderFlowDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            SwingPointDetector swingPointDetector,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _swingPointDetector = swingPointDetector;
            _swingPointHistory = new List<SwingPoint>();
            _fvgTracker = new OrderflowFvgTracker();
        }
        
        protected override int GetMinimumBarsRequired()
        {
            return Constants.Calculations.MinimumSwingPointsForOrderFlow;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            var detectedOrderFlows = new List<Level>();
            
            // This method is called on bar update, but orderflow detection
            // is triggered by swing point events, so we return empty here
            return detectedOrderFlows;
        }
        
        /// <summary>
        /// Process a new swing point to update order flow tracking
        /// </summary>
        public void ProcessSwingPoint(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
            
            // Add to history
            _swingPointHistory.Add(swingPoint);
            _swingPointHistory.Sort((a, b) => a.Index.CompareTo(b.Index));
            
            // Detect order flow based on swing point direction
            if (swingPoint.Direction == Direction.Down)
            {
                ProcessNewSwingLow(swingPoint);
            }
            else if (swingPoint.Direction == Direction.Up)
            {
                ProcessNewSwingHigh(swingPoint);
            }
        }
        
        private void ProcessNewSwingLow(SwingPoint newSwingLow)
        {
            // Get swing highs and lows
            var swingHighs = _swingPointHistory
                .Where(p => p.Direction == Direction.Up)
                .OrderByDescending(p => p.Index)
                .ToList();
            
            var swingLows = _swingPointHistory
                .Where(p => p.Direction == Direction.Down)
                .OrderByDescending(p => p.Index)
                .ToList();
            
            // Need at least one swing high and two swing lows
            if (swingHighs.Count < 1 || swingLows.Count < 2)
                return;
            
            var recentSwingHigh = swingHighs.First();
            var previousSwingLow = swingLows.Count > 1 ? swingLows[1] : null;
            
            if (previousSwingLow != null && previousSwingLow.Index < recentSwingHigh.Index)
            {
                // Create bullish orderflow
                var bullishOrderFlow = new Level(
                    LevelType.Orderflow,
                    previousSwingLow.Price,
                    recentSwingHigh.Price,
                    previousSwingLow.Time,
                    recentSwingHigh.Time,
                    null,
                    Direction.Up,
                    previousSwingLow.Index,
                    recentSwingHigh.Index,
                    previousSwingLow.Index
                );
                
                // Set TimeFrame from swing point
                bullishOrderFlow.TimeFrame = previousSwingLow.TimeFrame;
                
                // Initialize quadrants
                bullishOrderFlow.InitializeQuadrants();
                
                // Check for swept swing highs
                CheckForSweptSwingHighs(bullishOrderFlow);
                
                // Validate and store
                if (PostDetectionValidation(bullishOrderFlow, newSwingLow.Index))
                {
                    Repository.Add(bullishOrderFlow);
                    PublishDetectionEvent(bullishOrderFlow, newSwingLow.Index);
                }
            }
        }
        
        private void ProcessNewSwingHigh(SwingPoint newSwingHigh)
        {
            // Get swing highs and lows
            var swingHighs = _swingPointHistory
                .Where(p => p.Direction == Direction.Up)
                .OrderByDescending(p => p.Index)
                .ToList();
            
            var swingLows = _swingPointHistory
                .Where(p => p.Direction == Direction.Down)
                .OrderByDescending(p => p.Index)
                .ToList();
            
            // Need at least two swing highs and one swing low
            if (swingHighs.Count < 2 || swingLows.Count < 1)
                return;
            
            var recentSwingLow = swingLows.First();
            var previousSwingHigh = swingHighs.Count > 1 ? swingHighs[1] : null;
            
            if (previousSwingHigh != null && previousSwingHigh.Index < recentSwingLow.Index)
            {
                // Create bearish orderflow with DIRECTIONAL indexing
                // For bearish orderflow: starts at the high (previousSwingHigh) and moves down to the low (recentSwingLow)
                var bearishOrderFlow = new Level(
                    LevelType.Orderflow,
                    recentSwingLow.Price,
                    previousSwingHigh.Price,
                    recentSwingLow.Time,
                    previousSwingHigh.Time,
                    null,
                    Direction.Down,
                    previousSwingHigh.Index,  // Index (starting point of bearish move - at the high)
                    previousSwingHigh.Index,  // IndexHigh (where the high is)
                    recentSwingLow.Index      // IndexLow (where the low is)
                );
                
                // Set TimeFrame from swing point
                bearishOrderFlow.TimeFrame = previousSwingHigh.TimeFrame;
                
                // Initialize quadrants
                bearishOrderFlow.InitializeQuadrants();
                
                // Check for swept swing lows
                CheckForSweptSwingLows(bearishOrderFlow);
                
                // Validate and store
                if (PostDetectionValidation(bearishOrderFlow, newSwingHigh.Index))
                {
                    Repository.Add(bearishOrderFlow);
                    PublishDetectionEvent(bearishOrderFlow, newSwingHigh.Index);
                }
            }
        }
        
        private void CheckForSweptSwingHighs(Level orderflow)
        {
            var unsweptSwingHighs = _swingPointHistory
                .Where(p => p.Direction == Direction.Up && !p.Swept)
                .OrderByDescending(p => p.Price)
                .ToList();
            
            var sweptHighs = unsweptSwingHighs
                .Where(h => orderflow.High > h.Price && orderflow.Low < h.Price && h.Index < orderflow.IndexHigh)
                .ToList();
            
            if (sweptHighs.Count > 0)
            {
                orderflow.SweptSwingPoints = new List<SwingPoint>();
                
                var highestSweptPoint = sweptHighs.OrderByDescending(h => h.Price).First();
                int sweepingCandleIndex = FindSweepingCandleForPoint(orderflow, highestSweptPoint);
                orderflow.IndexOfSweepingCandle = sweepingCandleIndex;
                
                foreach (var sweptPoint in sweptHighs)
                {
                    sweptPoint.Swept = true;
                    sweptPoint.SweptLiquidity = true;
                    sweptPoint.SweptLiquidityPrice = sweptPoint.Price;
                    sweptPoint.IndexOfSweepingCandle = sweepingCandleIndex;
                    orderflow.SweptSwingPoints.Add(sweptPoint);
                }
                
                orderflow.SweptSwingPoint = highestSweptPoint;
            }
        }
        
        private void CheckForSweptSwingLows(Level orderflow)
        {
            var unsweptSwingLows = _swingPointHistory
                .Where(p => p.Direction == Direction.Down && !p.Swept)
                .OrderBy(p => p.Price)
                .ToList();
            
            var sweptLows = unsweptSwingLows
                .Where(l => orderflow.Low < l.Price && orderflow.High > l.Price && l.Index < orderflow.IndexLow)
                .ToList();
            
            if (sweptLows.Count > 0)
            {
                orderflow.SweptSwingPoints = new List<SwingPoint>();
                
                var lowestSweptPoint = sweptLows.OrderBy(l => l.Price).First();
                int sweepingCandleIndex = FindSweepingCandleForPoint(orderflow, lowestSweptPoint);
                orderflow.IndexOfSweepingCandle = sweepingCandleIndex;
                
                foreach (var sweptPoint in sweptLows)
                {
                    sweptPoint.Swept = true;
                    sweptPoint.SweptLiquidity = true;
                    sweptPoint.SweptLiquidityPrice = sweptPoint.Price;
                    sweptPoint.IndexOfSweepingCandle = sweepingCandleIndex;
                    orderflow.SweptSwingPoints.Add(sweptPoint);
                }
                
                orderflow.SweptSwingPoint = lowestSweptPoint;
            }
        }
        
        private int FindSweepingCandleForPoint(Level orderflow, SwingPoint sweptPoint)
        {
            if (sweptPoint == null)
                return orderflow.Direction == Direction.Up ? orderflow.IndexHigh : orderflow.IndexLow;
            
            double sweepPrice = sweptPoint.Price;
            int startIndex = orderflow.Direction == Direction.Up ? orderflow.IndexLow : orderflow.IndexHigh;
            int endIndex = orderflow.Direction == Direction.Up ? orderflow.IndexHigh : orderflow.IndexLow;
            
            if (startIndex < 0 || endIndex < 0 || startIndex >= CandleManager.Count || endIndex >= CandleManager.Count)
                return orderflow.Direction == Direction.Up ? orderflow.IndexHigh : orderflow.IndexLow;
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (orderflow.Direction == Direction.Up && CandleManager.GetCandle(i).High > sweepPrice)
                    return i;
                else if (orderflow.Direction == Direction.Down && CandleManager.GetCandle(i).Low < sweepPrice)
                    return i;
            }
            
            return orderflow.Direction == Direction.Up ? orderflow.IndexHigh : orderflow.IndexLow;
        }
        
        protected override bool PostDetectionValidation(Level orderflow, int currentIndex)
        {
            if (!base.PostDetectionValidation(orderflow, currentIndex))
                return false;

            if (orderflow.LevelType != LevelType.Orderflow)
                return false;

            // Check for duplicate orderflows
            bool isDuplicate = Repository.Any(existing =>
                existing.LevelType == LevelType.Orderflow &&
                existing.Direction == orderflow.Direction &&
                existing.IndexLow == orderflow.IndexLow &&
                existing.IndexHigh == orderflow.IndexHigh);

            return !isDuplicate;
        }
        
        protected override void PublishDetectionEvent(Level orderflow, int currentIndex)
        {
            EventAggregator.Publish(new OrderFlowDetectedEvent(orderflow));

            if (Settings.Patterns.ShowOrderFlow && _visualizer != null)
            {
                _visualizer.Draw(orderflow);
            }

            // Consume FVGs from the tracker for this orderflow's direction
            var matchingFvgs = _fvgTracker.ConsumeFvgs(orderflow.Direction);

            if (matchingFvgs.Count > 0)
            {
                // Assign the OrderflowRootIndex to each FVG
                foreach (var fvg in matchingFvgs)
                {
                    fvg.OrderflowRootIndex = orderflow.Index;
                }

                DrawFvgCountLabel(orderflow, matchingFvgs.Count);
            }
        }

        private void DrawFvgCountLabel(Level orderflow, int fvgCount)
        {
            // Get the time at the orderflow's starting index
            int labelIndex = orderflow.Index;
            if (labelIndex < 0 || labelIndex >= Chart.BarsTotal)
                return;

            DateTime labelTime = Chart.Bars[labelIndex].OpenTime;

            // Position label at the orderflow's starting price based on direction
            double labelPrice = orderflow.Direction == Direction.Up
                ? orderflow.Low  // For bullish, label at low (swing low)
                : orderflow.High; // For bearish, label at high (swing high)

            // Get directional color
            Color labelColor = orderflow.Direction == Direction.Up
                ? Color.FromArgb(255, 0, 200, 100)  // Green for bullish
                : Color.FromArgb(255, 200, 50, 50); // Red for bearish

            string labelId = $"of-fvg-count-{orderflow.Direction}-{orderflow.Index}";

            var text = Chart.DrawText(
                labelId,
                fvgCount.ToString(),
                labelTime,
                labelPrice,
                labelColor);

            text.FontSize = 10;
            text.IsBold = true;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = orderflow.Direction == Direction.Up
                ? VerticalAlignment.Top
                : VerticalAlignment.Bottom;
        }
        
        protected override void LogDetection(Level orderflow, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Order Flow detected: {orderflow.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(of => of.Direction == direction && of.LevelType == LevelType.Orderflow);
        }
        
        public override bool IsValid(Level orderflow, int currentIndex)
        {
            return orderflow != null && orderflow.LevelType == LevelType.Orderflow;
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            EventAggregator.Subscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
            EventAggregator.Subscribe<FvgDetectedEvent>(OnFvgDetected);
        }

        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            EventAggregator.Unsubscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
            EventAggregator.Unsubscribe<FvgDetectedEvent>(OnFvgDetected);
        }

        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            ProcessSwingPoint(evt.SwingPoint);
        }

        private void OnFvgDetected(FvgDetectedEvent evt)
        {
            _fvgTracker.AddFvg(evt.FvgLevel);
        }
        
        private void OnSwingPointRemoved(SwingPointRemovedEvent evt)
        {
            // Remove from history
            _swingPointHistory.RemoveAll(p => 
                p.Index == evt.SwingPoint.Index && 
                p.Direction == evt.SwingPoint.Direction);
            
            // Remove affected orderflows
            var affectedOrderflows = Repository.Find(of => 
                (of.Direction == Direction.Up && 
                 (of.IndexLow == evt.SwingPoint.Index || of.IndexHigh == evt.SwingPoint.Index)) ||
                (of.Direction == Direction.Down && 
                 (of.IndexHigh == evt.SwingPoint.Index || of.IndexLow == evt.SwingPoint.Index)));
            
            foreach (var orderflow in affectedOrderflows)
            {
                Repository.Remove(orderflow);
                _visualizer?.Remove(orderflow);
            }
        }
        
        protected override void OnDispose()
        {
            base.OnDispose();
            _swingPointHistory.Clear();
            _fvgTracker.Clear();
        }
    }
}