using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors.Base;
using Pfuma.Models;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects Fair Value Gaps (FVGs) in price action
    /// </summary>
    public class FvgDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        
        public FvgDetector(
            Chart chart,
            Bars bars,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, bars, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
        }
        
        protected override int GetMinimumBarsRequired()
        {
            return Constants.Patterns.FvgRequiredBars;
        }
        
        protected override List<Level> PerformDetection(Bars bars, int currentIndex)
        {
            var detectedFvgs = new List<Level>();
            
            // Need at least 3 bars to detect a FVG
            if (currentIndex < 2)
                return detectedFvgs;
            
            // Get the three consecutive bars
            var bar1 = bars[currentIndex - 2]; // First candle
            var bar2 = bars[currentIndex - 1]; // Middle candle
            var bar3 = bars[currentIndex];     // Last candle
            
            
            // Check for bullish FVG
            var bullishFvg = DetectBullishFvg(bar1, bar2, bar3, currentIndex);
            if (bullishFvg != null)
            {
                detectedFvgs.Add(bullishFvg);
            }
            
            // Check for bearish FVG
            var bearishFvg = DetectBearishFvg(bar1, bar2, bar3, currentIndex);
            if (bearishFvg != null)
            {
                detectedFvgs.Add(bearishFvg);
            }
            
            return detectedFvgs;
        }
        
        private Level DetectBullishFvg(Bar bar1, Bar bar2, Bar bar3, int currentIndex)
        {
            // Bullish FVG: bar1's high must be lower than bar3's low (primary gap condition)
            if (bar1.High >= bar3.Low)
                return null;
            
            // Volume imbalance analysis for boundary refinement per documentation
            bool hasVolumeImbalance1 = bar1.Close < bar2.Open;
            bool hasVolumeImbalance2 = bar2.Close < bar3.Open;
            
            // Determine boundaries based on volume imbalance presence (per documentation table)
            // Low boundary: Use bar1.Close if imbalance1 present, otherwise bar1.High
            // High boundary: Use bar3.Open if imbalance2 present, otherwise bar3.Low
            double low = hasVolumeImbalance1 ? bar1.Close : bar1.High;
            double high = hasVolumeImbalance2 ? bar3.Open : bar3.Low;
            
            // Validate boundaries to ensure valid FVG  
            if (low >= high)
                return null; // Invalid gap - boundaries are inverted or equal
            
            // No gap size restriction - detect all valid FVGs regardless of size
            
            // Create a bullish FVG level with proper index assignments per documentation
            var bullishFVG = new Level(
                LevelType.FairValueGap,
                low,                    // Refined low boundary
                high,                   // Refined high boundary
                bar1.OpenTime,          // Start time reference
                bar3.OpenTime,          // End time reference
                bar2.OpenTime,          // Middle candle time
                Direction.Up,           // Bullish direction
                currentIndex - 2,       // Primary index reference (bar1)
                currentIndex,           // High index (bar3 for bullish)
                currentIndex - 2,       // Low index (bar1 for bullish)
                currentIndex - 1,       // Middle index
                Zone.Premium            // Premium zone classification
            );
            
            // Initialize quadrants for the bullish FVG
            bullishFVG.InitializeQuadrants();
            
            return bullishFVG;
        }
        
        private Level DetectBearishFvg(Bar bar1, Bar bar2, Bar bar3, int currentIndex)
        {
            // Bearish FVG: bar1's low must be higher than bar3's high (primary gap condition)
            if (bar1.Low <= bar3.High)
                return null;
            
            // Volume imbalance analysis for boundary refinement per documentation
            bool hasVolumeImbalance1 = bar1.Close > bar2.Open;
            bool hasVolumeImbalance2 = bar2.Close > bar3.Open;
            
            // Determine boundaries based on volume imbalance presence (per documentation table)
            // High boundary: Use bar1.Close if imbalance1 present, otherwise bar1.Low
            // Low boundary: Use bar3.Open if imbalance2 present, otherwise bar3.High
            double high = hasVolumeImbalance1 ? bar1.Close : bar1.Low;
            double low = hasVolumeImbalance2 ? bar3.Open : bar3.High;
            
            // Validate boundaries to ensure valid FVG  
            if (low >= high)
                return null; // Invalid gap - boundaries are inverted or equal
            
            // No gap size restriction - detect all valid FVGs regardless of size
            
            // Create a bearish FVG level with proper index assignments per documentation
            var bearishFVG = new Level(
                LevelType.FairValueGap,
                low,                    // Refined low boundary
                high,                   // Refined high boundary
                bar3.OpenTime,          // Start time reference (reversed for bearish)
                bar1.OpenTime,          // End time reference (reversed for bearish)
                bar2.OpenTime,          // Middle candle time
                Direction.Down,         // Bearish direction
                currentIndex - 2,       // Primary index reference (bar1)
                currentIndex - 2,       // High index (bar1 for bearish)
                currentIndex,           // Low index (bar3 for bearish)
                currentIndex - 1,       // Middle index
                Zone.Discount           // Discount zone classification
            );
            
            // Initialize quadrants for the bearish FVG
            bearishFVG.InitializeQuadrants();
            
            return bearishFVG;
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
                Logger($"FVG detected: {fvg.Direction} at index {currentIndex}, " +
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
            if (currentIndex >= fvg.Index && currentIndex < Bars.Count)
            {
                for (int i = fvg.Index; i <= currentIndex; i++)
                {
                    var bar = Bars[i];
                    
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