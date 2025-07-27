using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization;

/// <summary>
/// Handles visualization of Gauntlet patterns
/// </summary>
public class GauntletVisualizer : BaseVisualizer<Level>
{
    public GauntletVisualizer(
        Chart chart,
        VisualizationSettings settings,
        Action<string> logger = null)
        : base(chart, settings, logger)
    {
    }
        
    protected override bool ShouldDraw(Level gauntlet)
    {
        return base.ShouldDraw(gauntlet) && 
               gauntlet.IsGauntlet &&
               Settings.Patterns.ShowGauntlet;
    }
        
    protected override string GetPatternId(Level gauntlet)
    {
        return $"gauntlet-{gauntlet.Direction}-{gauntlet.Index}-{gauntlet.IndexHigh}-{gauntlet.IndexLow}";
    }
        
    protected override void PerformDraw(Level gauntlet, string patternId, List<string> objectIds)
    {
        Color color = GetDirectionalColor(gauntlet.Direction);
            
        var rectangle = Chart.DrawRectangle(
            patternId,
            gauntlet.LowTime,
            gauntlet.Low,
            gauntlet.HighTime,
            gauntlet.High,
            color);
                
        rectangle.IsFilled = true;
        rectangle.Color = ApplyOpacity(color, Settings.Opacity.Gauntlet);
            
        objectIds.Add(patternId);
            
        // Draw midpoint
        string midLineId = $"{patternId}-midline";
        Chart.DrawTrendLine(
            midLineId,
            gauntlet.LowTime,
            gauntlet.Mid,
            gauntlet.HighTime,
            gauntlet.Mid,
            GetColorFromString(Settings.Colors.NeutralColor),
            Constants.Drawing.DefaultLineThickness,
            LineStyle.Dots);
                
        objectIds.Add(midLineId);
    }
}