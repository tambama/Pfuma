using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization;

/// <summary>
/// Handles visualization of Unicorn patterns
/// </summary>
public class UnicornVisualizer : BaseVisualizer<Level>
{
    public UnicornVisualizer(
        Chart chart,
        VisualizationSettings settings,
        Action<string> logger = null)
        : base(chart, settings, logger)
    {
    }
        
    protected override bool ShouldDraw(Level unicorn)
    {
        return base.ShouldDraw(unicorn) && 
               unicorn.LevelType == LevelType.Unicorn &&
               Settings.Patterns.ShowUnicorn;
    }
        
    protected override string GetPatternId(Level unicorn)
    {
        return $"unicorn-{unicorn.Direction}-{unicorn.Index}-{unicorn.IndexHigh}-{unicorn.IndexLow}";
    }
        
    protected override void PerformDraw(Level unicorn, string patternId, List<string> objectIds)
    {
        // Determine colors based on direction (same as Gauntlet)
        Color rectangleColor = unicorn.Direction == Direction.Up ? Color.Green : Color.Pink;
        Color midlineColor = rectangleColor;
        
        // Calculate rectangle extension (10 candlesticks to the right from detection point, same as Gauntlet)
        int startIndex = Math.Min(unicorn.IndexHigh, unicorn.IndexLow);
        int endIndex = startIndex + 10;
        
        // Draw the main rectangle (same style as Gauntlet)
        string rectId = $"{patternId}-rect";
        var rect = Chart.DrawRectangle(
            rectId,
            startIndex,     // Start index (where Unicorn was formed)
            unicorn.High,   // Top of Unicorn
            endIndex,       // End index (10 candles to the right)
            unicorn.Low,    // Bottom of Unicorn
            Color.FromArgb(30, rectangleColor),
            2               // Thickness
        );
        
        rect.IsFilled = true;
        objectIds.Add(rectId);
        
        // Draw the midline (same style as Gauntlet)
        double midPrice = (unicorn.High + unicorn.Low) / 2.0;
        string midlineId = $"{patternId}-mid";
        Chart.DrawTrendLine(
            midlineId,
            startIndex,     // Start index
            midPrice,       // Mid price
            endIndex,       // End index
            midPrice,       // Mid price
            Color.FromArgb(60, midlineColor),
            1,              // Thickness
            LineStyle.Solid
        );
        objectIds.Add(midlineId);
        
        // Add label to identify it as a Unicorn
        string labelId = $"{patternId}-label";
        string labelText = unicorn.Direction == Direction.Up 
            ? "UNI ↑" 
            : "UNI ↓";
        
        Chart.DrawText(
            labelId,
            labelText,
            startIndex,
            unicorn.Direction == Direction.Up 
                ? unicorn.Low - GetPricePadding() 
                : unicorn.High + GetPricePadding(),
            rectangleColor
        );
        objectIds.Add(labelId);
        
        // Draw timeframe label if enabled
        if (ShouldShowTimeframeLabel(unicorn.TimeFrame))
        {
            DrawTimeframeLabel(unicorn, patternId, objectIds, endIndex);
        }
    }
    
    /// <summary>
    /// Draws a timeframe label on the Unicorn
    /// </summary>
    private void DrawTimeframeLabel(Level unicorn, string patternId, List<string> objectIds, int endIndex)
    {
        string labelId = $"{patternId}-tf-label";
        objectIds.Add(labelId);
        
        // Position the label at the middle of the rectangle extension
        int labelIndex = (Math.Min(unicorn.IndexHigh, unicorn.IndexLow) + endIndex) / 2;
        double labelPrice = unicorn.Mid;
        
        string timeframeText = unicorn.TimeFrame.GetShortName();
        
        var text = Chart.DrawText(
            labelId,
            timeframeText,
            labelIndex,
            labelPrice,
            Color.White
        );
            
        text.FontSize = 8;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
    }
    
    private double GetPricePadding()
    {
        // Calculate a small padding based on the chart's price range
        if (Chart == null) return 0.0001;
        
        double range = Chart.BottomY - Chart.TopY;
        return Math.Abs(range * 0.01); // 1% of the visible range
    }
}