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
    /// Detects Rejection Blocks from swing points with significant wicks
    /// </summary>
    public class RejectionBlockDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        
        public RejectionBlockDetector(
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
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            // Rejection block detection is triggered by swing point events
            return new List<Level>();
        }
        
        /// <summary>
        /// Checks a swing point for rejection block patterns
        /// </summary>
        public void CheckForRejectionBlock(SwingPoint swingPoint)
        {
            if (swingPoint == null || swingPoint.Bar == null)
                return;
            
            var candle = swingPoint.Bar;
            bool isBullishCandle = candle.Close > candle.Open;
            double bodySize = Math.Abs(candle.Close - candle.Open);
            
            // Skip if body size is very small
            if (bodySize < Constants.Calculations.PriceTolerance)
                return;
            
            Level rejectionBlock = null;
            
            // For Bullish Swing Points (creating Bearish Rejection Blocks)
            if (swingPoint.Direction == Direction.Up)
            {
                rejectionBlock = CheckBearishRejectionBlock(swingPoint, candle, isBullishCandle, bodySize);
            }
            // For Bearish Swing Points (creating Bullish Rejection Blocks)
            else if (swingPoint.Direction == Direction.Down)
            {
                rejectionBlock = CheckBullishRejectionBlock(swingPoint, candle, isBullishCandle, bodySize);
            }
            
            if (rejectionBlock != null)
            {
                // Check if we already have this rejection block
                if (!PostDetectionValidation(rejectionBlock, swingPoint.Index))
                    return;
                
                // Store and publish
                Repository.Add(rejectionBlock);
                PublishDetectionEvent(rejectionBlock, swingPoint.Index);
            }
        }
        
        private Level CheckBearishRejectionBlock(SwingPoint swingPoint, Candle candle, bool isBullishCandle, double bodySize)
        {
            double upperWick;
            double lowerBoundary;
            
            if (isBullishCandle)
            {
                // For bullish candles: upper wick = High - Close
                upperWick = candle.High - candle.Close;
                lowerBoundary = candle.Close;
            }
            else
            {
                // For bearish candles: upper wick = High - Open
                upperWick = candle.High - candle.Open;
                lowerBoundary = candle.Open;
            }
            
            // Check if upper wick is significantly larger than body
            if (upperWick > bodySize * Constants.Calculations.RejectionWickMultiplier)
            {
                // Create a bearish rejection block
                var rejectionBlock = new Level(
                    LevelType.RejectionBlock,
                    lowerBoundary,
                    candle.High,
                    candle.Time,
                    candle.Time.AddMinutes(Constants.Time.LevelExtensionMinutes),
                    candle.Time,
                    Direction.Down,
                    swingPoint.Index,
                    swingPoint.Index,
                    swingPoint.Index,
                    swingPoint.Index,
                    Zone.Premium
                );
                
                // Set TimeFrame from candle
                rejectionBlock.TimeFrame = candle.TimeFrame;
                
                // Initialize quadrants
                rejectionBlock.InitializeQuadrants();
                
                return rejectionBlock;
            }
            
            return null;
        }
        
        private Level CheckBullishRejectionBlock(SwingPoint swingPoint, Candle candle, bool isBullishCandle, double bodySize)
        {
            double lowerWick;
            double upperBoundary;
            
            if (isBullishCandle)
            {
                // For bullish candles: lower wick = Open - Low
                lowerWick = candle.Open - candle.Low;
                upperBoundary = candle.Open;
            }
            else
            {
                // For bearish candles: lower wick = Close - Low
                lowerWick = candle.Close - candle.Low;
                upperBoundary = candle.Close;
            }
            
            // Check if lower wick is significantly larger than body
            if (lowerWick > bodySize * Constants.Calculations.RejectionWickMultiplier)
            {
                // Create a bullish rejection block
                var rejectionBlock = new Level(
                    LevelType.RejectionBlock,
                    candle.Low,
                    upperBoundary,
                    candle.Time,
                    candle.Time.AddMinutes(Constants.Time.LevelExtensionMinutes),
                    candle.Time,
                    Direction.Up,
                    swingPoint.Index,
                    swingPoint.Index,
                    swingPoint.Index,
                    swingPoint.Index,
                    Zone.Discount
                );
                
                // Set TimeFrame from candle
                rejectionBlock.TimeFrame = candle.TimeFrame;
                
                // Initialize quadrants
                rejectionBlock.InitializeQuadrants();
                
                return rejectionBlock;
            }
            
            return null;
        }
        
        protected override bool PostDetectionValidation(Level rejectionBlock, int currentIndex)
        {
            if (!base.PostDetectionValidation(rejectionBlock, currentIndex))
                return false;
            
            // Check if we already have this rejection block
            bool isDuplicate = Repository.Any(rb =>
                rb.Index == rejectionBlock.Index &&
                rb.Direction == rejectionBlock.Direction &&
                rb.LevelType == LevelType.RejectionBlock);
            
            return !isDuplicate;
        }
        
        protected override void PublishDetectionEvent(Level rejectionBlock, int currentIndex)
        {
            EventAggregator.Publish(new RejectionBlockDetectedEvent(rejectionBlock));
            
            if (Settings.Patterns.ShowRejectionBlock && _visualizer != null)
            {
                _visualizer.Draw(rejectionBlock);
            }
        }
        
        protected override void LogDetection(Level rejectionBlock, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Rejection Block detected: {rejectionBlock.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(rb => 
                rb.LevelType == LevelType.RejectionBlock && 
                rb.Direction == direction);
        }
        
        public override bool IsValid(Level rejectionBlock, int currentIndex)
        {
            return rejectionBlock != null && 
                   rejectionBlock.LevelType == LevelType.RejectionBlock;
        }
        
        /// <summary>
        /// Removes rejection block at a specific index (used when order block is detected at same index)
        /// </summary>
        public void RemoveRejectionBlockAtIndex(int index)
        {
            var rejectionBlock = Repository.Find(rb => 
                rb.Index == index && 
                rb.LevelType == LevelType.RejectionBlock)
                .FirstOrDefault();
            
            if (rejectionBlock != null)
            {
                Repository.Remove(rejectionBlock);
                _visualizer?.Remove(rejectionBlock);
                
                Logger($"Removed rejection block at index {index} (replaced by order block)");
            }
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            EventAggregator.Subscribe<OrderBlockDetectedEvent>(OnOrderBlockDetected);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            EventAggregator.Unsubscribe<OrderBlockDetectedEvent>(OnOrderBlockDetected);
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            CheckForRejectionBlock(evt.SwingPoint);
        }
        
        private void OnOrderBlockDetected(OrderBlockDetectedEvent evt)
        {
            // Remove any rejection block at the same index as the order block
            if (evt.OrderBlock != null)
            {
                RemoveRejectionBlockAtIndex(evt.OrderBlock.Index);
            }
        }
    }
}