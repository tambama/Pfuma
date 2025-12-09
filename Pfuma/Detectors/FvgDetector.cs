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
    /// Detects Fair Value Gaps (FVGs) in price action using simple gap detection
    /// without volume imbalance analysis for boundary refinement
    /// </summary>
    public class FvgDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;

        public FvgDetector(
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
        
        protected override int GetMinimumBarsRequired()
        {
            return Constants.Patterns.FvgRequiredBars;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            var detectedFvgs = new List<Level>();
            
            // Need at least 3 completed candles to detect a FVG (plus current forming candle)
            if (currentIndex < 3)
                return detectedFvgs;
            
            // Get the three consecutive COMPLETED candles (not including current forming candle)
            var candle1 = CandleManager.GetCandle(currentIndex - 3); // First candle
            var candle2 = CandleManager.GetCandle(currentIndex - 2); // Middle candle
            var candle3 = CandleManager.GetCandle(currentIndex - 1); // Last completed candle
            
            if (candle1 == null || candle2 == null || candle3 == null)
                return detectedFvgs;
            
            
            // Check for bullish FVG (passing currentIndex - 1 as the detection index)
            var bullishFvg = DetectBullishFvg(candle1, candle2, candle3, currentIndex - 1);
            if (bullishFvg != null)
            {
                detectedFvgs.Add(bullishFvg);
            }
            
            // Check for bearish FVG (passing currentIndex - 1 as the detection index)
            var bearishFvg = DetectBearishFvg(candle1, candle2, candle3, currentIndex - 1);
            if (bearishFvg != null)
            {
                detectedFvgs.Add(bearishFvg);
            }

            return detectedFvgs;
        }
        
        private Level DetectBullishFvg(Candle candle1, Candle candle2, Candle candle3, int detectionIndex)
        {
            // Bullish FVG: candle1's high must be lower than candle3's low (gap condition)
            if (candle1.High >= candle3.Low)
                return null;
            
            // Simple boundary calculation without volume imbalance analysis
            double low = candle1.High;   // Top of first candle
            double high = candle3.Low;   // Bottom of third candle
            
            // Validate boundaries to ensure valid FVG  
            if (low >= high)
                return null; // Invalid gap - boundaries are inverted or equal
            
            // Create a bullish FVG level with proper index assignments
            var bullishFvg = new Level(
                LevelType.FairValueGap,
                low,                    // Low boundary (top of first candle)
                high,                   // High boundary (bottom of third candle)
                candle1.Time,           // Start time reference
                candle3.Time,           // End time reference
                candle2.Time,           // Middle candle time
                Direction.Up,           // Bullish direction
                detectionIndex - 2,     // Primary index reference (candle1)
                detectionIndex,         // High index (candle3 for bullish)
                detectionIndex - 2,     // Low index (candle1 for bullish)
                detectionIndex - 1,     // Middle index
                Zone.Premium            // Premium zone classification
            );
            
            // Set TimeFrame from candle
            bullishFvg.TimeFrame = candle1.TimeFrame;
            bullishFvg.IndexHighPrice = candle3.High;
            bullishFvg.IndexLowPrice = candle1.Low;
            
            candle1.PositionInFvg = 1;
            candle2.PositionInFvg = 2;
            candle3.PositionInFvg = 3;

            // Mark candles as being in an FVG
            candle1.IsInFvg = true;
            candle2.IsInFvg = true;
            candle3.IsInFvg = true;
            candle1.FvgDirection = Direction.Up;
            candle2.FvgDirection = Direction.Up;
            candle3.FvgDirection = Direction.Up;

            // Initialize quadrants for the bullish FVG
            bullishFvg.InitializeQuadrants();

            return bullishFvg;
        }

        private Level DetectBearishFvg(Candle candle1, Candle candle2, Candle candle3, int detectionIndex)
        {
            // Bearish FVG: candle1's low must be higher than candle3's high (gap condition)
            if (candle1.Low <= candle3.High)
                return null;
            
            // Simple boundary calculation without volume imbalance analysis
            double high = candle1.Low;   // Bottom of first candle
            double low = candle3.High;   // Top of third candle
            
            // Validate boundaries to ensure valid FVG  
            if (low >= high)
                return null; // Invalid gap - boundaries are inverted or equal
            
            // Create a bearish FVG level with proper index assignments
            var bearishFvg = new Level(
                LevelType.FairValueGap,
                low,                    // Low boundary (top of third candle)
                high,                   // High boundary (bottom of first candle)
                candle3.Time,           // Start time reference (reversed for bearish)
                candle1.Time,           // End time reference (reversed for bearish)
                candle2.Time,           // Middle candle time
                Direction.Down,         // Bearish direction
                detectionIndex - 2,     // Primary index reference (candle1)
                detectionIndex - 2,     // High index (candle1 for bearish)
                detectionIndex,         // Low index (candle3 for bearish)
                detectionIndex - 1,     // Middle index
                Zone.Discount           // Discount zone classification
            );
            
            // Set TimeFrame from candle
            bearishFvg.TimeFrame = candle1.TimeFrame;
            bearishFvg.IndexHighPrice = candle1.High;
            bearishFvg.IndexLowPrice = candle3.Low;

            candle1.PositionInFvg = 1;
            candle2.PositionInFvg = 2;
            candle3.PositionInFvg = 3;

            // Mark candles as being in an FVG
            candle1.IsInFvg = true;
            candle2.IsInFvg = true;
            candle3.IsInFvg = true;
            candle1.FvgDirection = Direction.Down;
            candle2.FvgDirection = Direction.Down;
            candle3.FvgDirection = Direction.Down;

            // Initialize quadrants for the bearish FVG
            bearishFvg.InitializeQuadrants();

            return bearishFvg;
        }

        protected override bool PostDetectionValidation(Level fvg, int currentIndex)
        {
            if (!base.PostDetectionValidation(fvg, currentIndex))
                return false;
            
            // Check if we already have this FVG to avoid duplicates
            bool isDuplicate = Repository.Any(existingFvg =>
                existingFvg.Index == fvg.Index &&
                existingFvg.Direction == fvg.Direction &&
                Math.Abs(existingFvg.Low - fvg.Low) < Constants.Calculations.PriceTolerance &&
                Math.Abs(existingFvg.High - fvg.High) < Constants.Calculations.PriceTolerance);
            
            return !isDuplicate;
        }
        
        protected override void PublishDetectionEvent(Level fvg, int currentIndex)
        {
            // Publish FVG detected event
            EventAggregator.Publish(new FvgDetectedEvent(fvg));
            
            // Draw the FVG if visualization is enabled
            if (Settings.Patterns.ShowFVG && _visualizer != null)
            {
                _visualizer.Draw(fvg);
            }
        }
        
        protected override void LogDetection(Level fvg, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"FVG detected: {fvg.Direction} at index {fvg.Index}, " +
                       $"Range: {fvg.Low:F5} - {fvg.High:F5}, " +
                       $"Gap Size: {(fvg.High - fvg.Low):F5}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(fvg => fvg.Direction == direction && fvg.LevelType == LevelType.FairValueGap);
        }
        
        public override bool IsValid(Level fvg, int currentIndex)
        {
            // An FVG is valid until it's been filled
            // Check if price has moved through the entire FVG range
            if (currentIndex >= fvg.Index && currentIndex < CandleManager.Count)
            {
                for (int i = fvg.Index; i <= currentIndex; i++)
                {
                    var bar = CandleManager.GetCandle(i);
                    
                    // For bullish FVG, check if a bar has filled the gap from above
                    if (fvg.Direction == Direction.Up && bar.Low <= fvg.Low)
                    {
                        return false; // FVG has been filled
                    }
                    
                    // For bearish FVG, check if a bar has filled the gap from below
                    if (fvg.Direction == Direction.Down && bar.High >= fvg.High)
                    {
                        return false; // FVG has been filled
                    }
                }
            }
            
            return true;
        }

        protected override void SubscribeToEvents()
        {
            // FVG detector might want to know about swing points for context
            EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            // Could use swing points to validate FVG quality or context
            // For example, FVGs near swing points might be more significant
        }
    }
}