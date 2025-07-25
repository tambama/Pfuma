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
            // Bullish FVG: bar1's high is lower than bar3's low
            if (bar1.High >= bar3.Low)
                return null;
            
            // Check for volume imbalance between candle1 and candle2
            bool hasVolumeImbalance1 = bar1.Close < bar2.Open;
            
            // Determine low boundary based on volume imbalance
            double low = hasVolumeImbalance1 ? bar1.Close : bar1.High;
            
            // Check for volume imbalance between candle2 and candle3
            bool hasVolumeImbalance2 = bar2.Close < bar3.Open;
            
            // Determine high boundary based on volume imbalance
            double high = hasVolumeImbalance2 ? bar3.Open : bar3.Low;
            
            // Create a bullish FVG level
            var bullishFVG = new Level(
                LevelType.FairValueGap,
                low,
                high,
                bar1.OpenTime,
                bar3.OpenTime,
                bar2.OpenTime,
                Direction.Up,
                currentIndex - 2,
                currentIndex,
                currentIndex - 2,
                currentIndex - 1, // Store the middle candle index
                Zone.Premium // FVGs in an uptrend are typically in the Premium zone
            );
            
            // Initialize quadrants for the bullish FVG
            bullishFVG.InitializeQuadrants();
            
            return bullishFVG;
        }
        
        private Level DetectBearishFvg(Bar bar1, Bar bar2, Bar bar3, int currentIndex)
        {
            // Bearish FVG: bar1's low is higher than bar3's high
            if (bar1.Low <= bar3.High)
                return null;
            
            // Check for volume imbalance between candle1 and candle2
            bool hasVolumeImbalance1 = bar1.Close > bar2.Open;
            
            // Determine high boundary based on volume imbalance
            double high = hasVolumeImbalance1 ? bar1.Close : bar1.Low;
            
            // Check for volume imbalance between candle2 and candle3
            bool hasVolumeImbalance2 = bar2.Close > bar3.Open;
            
            // Determine low boundary based on volume imbalance
            double low = hasVolumeImbalance2 ? bar3.Open : bar3.High;
            
            // Create a bearish FVG level
            var bearishFVG = new Level(
                LevelType.FairValueGap,
                low,
                high,
                bar3.OpenTime,
                bar1.OpenTime,
                bar2.OpenTime,
                Direction.Down,
                currentIndex - 2,
                currentIndex - 2,
                currentIndex,
                currentIndex - 1, // Store the middle candle index
                Zone.Discount // FVGs in a downtrend are typically in the Discount zone
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
                       $"Range: {fvg.Low:F5} - {fvg.High:F5}");
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