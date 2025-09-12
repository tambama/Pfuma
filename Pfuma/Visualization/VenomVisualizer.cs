using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Visualizes Venom patterns on the chart
    /// </summary>
    public class VenomVisualizer : BaseVisualizer<Level>
    {
        public VenomVisualizer(
            Chart chart, 
            VisualizationSettings settings, 
            Action<string> logger = null)
            : base(chart, settings, logger)
        {
        }
        
        protected override bool ShouldDraw(Level venom)
        {
            return base.ShouldDraw(venom) && 
                   venom.LevelType == LevelType.Venom &&
                   Settings.Patterns.ShowVenom;
        }
        
        protected override string GetPatternId(Level venom)
        {
            return $"venom-{venom.Direction}-{venom.Index}";
        }
        
        protected override void PerformDraw(Level venom, string patternId, List<string> objectIds)
        {
            // Determine colors based on direction - Bullish is green, Bearish is pink
            Color rectangleColor = venom.Direction == Direction.Up 
                ? Color.Green 
                : Color.Pink;
            
            // Calculate rectangle extension (10 candlesticks to the right, same as Gauntlet)
            int startIndex = Math.Min(venom.IndexHigh, venom.IndexLow);
            int endIndex = startIndex + 10;
            
            // Draw the main rectangle (same style as Gauntlet)
            string rectId = $"{patternId}-rect";
            var rect = Chart.DrawRectangle(
                rectId,
                startIndex,         // Start index (where Venom was formed)
                venom.High,         // Top of Venom
                endIndex,           // End index (10 candles to the right)
                venom.Low,          // Bottom of Venom
                Color.FromArgb(30, rectangleColor),
                2                   // Thickness
            );
            
            rect.IsFilled = true;
            objectIds.Add(rectId);
            
            // Draw the midline (same style as Gauntlet)
            double midPrice = (venom.High + venom.Low) / 2.0;
            string midlineId = $"{patternId}-mid";
            Chart.DrawTrendLine(
                midlineId,
                startIndex,         // Start index
                midPrice,           // Mid price
                endIndex,           // End index
                midPrice,           // Mid price
                Color.FromArgb(60, rectangleColor),
                1,                  // Thickness
                LineStyle.Solid
            );
            objectIds.Add(midlineId);
            
            // Add label to identify it as a Venom
            string labelId = $"{patternId}-label";
            string labelText = venom.Direction == Direction.Up 
                ? "VEN ↑" 
                : "VEN ↓";
            
            Chart.DrawText(
                labelId,
                labelText,
                startIndex,
                venom.Direction == Direction.Up 
                    ? venom.Low - GetPricePadding() 
                    : venom.High + GetPricePadding(),
                rectangleColor
            );
            objectIds.Add(labelId);
            
            // Add confirmation status indicator if confirmed
            if (venom.IsConfirmed)
            {
                string confirmId = $"{patternId}-confirm";
                string confirmText = "✓";
                
                Chart.DrawText(
                    confirmId,
                    confirmText,
                    startIndex + 2,  // Slightly offset from main label
                    venom.Direction == Direction.Up 
                        ? venom.Low - GetPricePadding() 
                        : venom.High + GetPricePadding(),
                    Color.White
                );
                objectIds.Add(confirmId);
            }
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