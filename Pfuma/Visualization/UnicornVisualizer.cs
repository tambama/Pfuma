using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
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
        Color unicornColor = unicorn.Direction == Direction.Up ? Color.Green : Color.Pink;
            
        var rectangle = Chart.DrawRectangle(
            patternId,
            unicorn.LowTime,
            unicorn.Low,
            unicorn.HighTime,
            unicorn.High,
            unicornColor);
                
        rectangle.IsFilled = true;
        rectangle.Color = ApplyOpacity(unicornColor, 50); // Higher opacity for Unicorns
            
        objectIds.Add(patternId);
            
        // Draw solid midline to make it stand out
        string midLineId = $"{patternId}-midline";
        Chart.DrawTrendLine(
            midLineId,
            unicorn.LowTime,
            unicorn.Mid,
            unicorn.HighTime,
            unicorn.Mid,
            unicornColor,
            Constants.Drawing.BoldLineThickness,
            LineStyle.Dots);
                
        objectIds.Add(midLineId);
    }
}