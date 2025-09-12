using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Visualizes Propulsion Block patterns on the chart
    /// </summary>
    public class PropulsionBlockVisualizer : BaseVisualizer<Level>
    {
        public PropulsionBlockVisualizer(
            Chart chart, 
            VisualizationSettings settings, 
            Action<string> logger = null)
            : base(chart, settings, logger)
        {
        }
        
        protected override bool ShouldDraw(Level propulsionBlock)
        {
            return base.ShouldDraw(propulsionBlock) && 
                   propulsionBlock.LevelType == LevelType.PropulsionBlock &&
                   Settings.Patterns.ShowPropulsionBlock;
        }
        
        protected override string GetPatternId(Level propulsionBlock)
        {
            return $"pb-{propulsionBlock.Direction}-{propulsionBlock.Index}";
        }
        
        protected override void PerformDraw(Level propulsionBlock, string patternId, List<string> objectIds)
        {
            // Determine colors based on direction
            Color rectangleColor = propulsionBlock.Direction == Direction.Up 
                ? Color.Green 
                : Color.Red;
            
            // Calculate rectangle extension (10 candlesticks to the right, same as Gauntlet)
            int startIndex = Math.Min(propulsionBlock.IndexHigh, propulsionBlock.IndexLow);
            int endIndex = startIndex + 10;
            
            // Draw the main rectangle
            string rectId = $"{patternId}-rect";
            var rect = Chart.DrawRectangle(
                rectId,
                startIndex,                 // Start index
                propulsionBlock.High,       // Top of propulsion block
                endIndex,                   // End index (10 candles to the right)
                propulsionBlock.Low,        // Bottom of propulsion block
                Color.FromArgb(30, rectangleColor),
                2                           // Thickness
            );
            
            rect.IsFilled = true;
            objectIds.Add(rectId);
            
            // Draw the midline
            double midPrice = (propulsionBlock.High + propulsionBlock.Low) / 2.0;
            string midlineId = $"{patternId}-mid";
            Chart.DrawTrendLine(
                midlineId,
                startIndex,                 // Start index
                midPrice,                   // Mid price
                endIndex,                   // End index
                midPrice,                   // Mid price
                Color.FromArgb(60, rectangleColor),
                1,                          // Thickness
                LineStyle.Solid
            );
            objectIds.Add(midlineId);
            
            // Add label
            string labelId = $"{patternId}-label";
            string labelText = propulsionBlock.Direction == Direction.Up 
                ? "PB ↑" 
                : "PB ↓";
            
            Chart.DrawText(
                labelId,
                labelText,
                startIndex,
                propulsionBlock.Direction == Direction.Up 
                    ? propulsionBlock.Low - GetPricePadding() 
                    : propulsionBlock.High + GetPricePadding(),
                rectangleColor
            );
            objectIds.Add(labelId);
        }
        
        private double GetPricePadding()
        {
            // Calculate a small padding based on the chart's price range
            if (Chart == null) return 0.0001;
            
            double range = Chart.BottomY - Chart.TopY;
            return Math.Abs(range * 0.01); // 1% of the visible range
        }
    }
}