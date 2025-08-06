using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization;

/// <summary>
/// Handles visualization of Breaker Blocks
/// </summary>
public class BreakerBlockVisualizer : BaseVisualizer<Level>
{
    public BreakerBlockVisualizer(
        Chart chart,
        VisualizationSettings settings,
        Action<string> logger = null)
        : base(chart, settings, logger)
    {
    }
        
    protected override bool ShouldDraw(Level breakerBlock)
    {
        return base.ShouldDraw(breakerBlock) && 
               breakerBlock.LevelType == LevelType.BreakerBlock &&
               Settings.Patterns.ShowBreakerBlock;
    }
        
    protected override string GetPatternId(Level breakerBlock)
    {
        return $"breaker-{breakerBlock.Direction}-{breakerBlock.Index}-{breakerBlock.IndexHigh}-{breakerBlock.IndexLow}";
    }
        
    protected override void PerformDraw(Level breakerBlock, string patternId, List<string> objectIds)
    {
        Color color = GetDirectionalColor(breakerBlock.Direction);
            
        var rectangle = Chart.DrawRectangle(
            patternId,
            breakerBlock.LowTime,
            breakerBlock.Low,
            breakerBlock.HighTime.AddMinutes(Constants.Time.LevelExtensionMinutes),
            breakerBlock.High,
            color);
                
        rectangle.IsFilled = true;
        rectangle.Color = ApplyOpacity(color, 20);
            
        objectIds.Add(patternId);
            
        // Draw midpoint
        string midLineId = $"{patternId}-midline";
        Chart.DrawTrendLine(
            midLineId,
            breakerBlock.LowTime,
            breakerBlock.Mid,
            breakerBlock.HighTime.AddMinutes(Constants.Time.LevelExtensionMinutes),
            breakerBlock.Mid,
            GetColorFromString(Settings.Colors.NeutralColor),
            Constants.Drawing.DefaultLineThickness,
            LineStyle.Dots);
                
        objectIds.Add(midLineId);
        
        // Draw timeframe label if enabled
        if (ShouldShowTimeframeLabel(breakerBlock.TimeFrame))
        {
            DrawTimeframeLabel(breakerBlock, patternId, objectIds);
        }
    }
    
    /// <summary>
    /// Draws a timeframe label on the Breaker Block
    /// </summary>
    private void DrawTimeframeLabel(Level breakerBlock, string patternId, List<string> objectIds)
    {
        string labelId = $"{patternId}-tf-label";
        objectIds.Add(labelId);
        
        // Position the label at the center of the pattern
        DateTime labelTime = breakerBlock.MidTime.AddMinutes(30);
        double labelPrice = breakerBlock.Mid;
        
        string timeframeText = breakerBlock.TimeFrame.GetShortName();
        
        var text = Chart.DrawText(
            labelId,
            timeframeText,
            labelTime,
            labelPrice,
            Color.White);
            
        text.FontSize = 8;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
    }
}