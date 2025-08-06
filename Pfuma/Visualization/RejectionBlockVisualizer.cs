using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization;

/// <summary>
/// Handles visualization of Rejection Blocks
/// </summary>
public class RejectionBlockVisualizer : BaseVisualizer<Level>
{
    public RejectionBlockVisualizer(
        Chart chart,
        VisualizationSettings settings,
        Action<string> logger = null)
        : base(chart, settings, logger)
    {
    }
        
    protected override bool ShouldDraw(Level rejectionBlock)
    {
        return base.ShouldDraw(rejectionBlock) && 
               rejectionBlock.LevelType == LevelType.RejectionBlock &&
               Settings.Patterns.ShowRejectionBlock;
    }
        
    protected override string GetPatternId(Level rejectionBlock)
    {
        return $"rb-{rejectionBlock.Direction}-{rejectionBlock.Index}";
    }
        
    protected override void PerformDraw(Level rejectionBlock, string patternId, List<string> objectIds)
    {
        Color color = rejectionBlock.Direction == Direction.Up
            ? ApplyOpacity(Color.Green, Settings.Opacity.RejectionBlock)
            : ApplyOpacity(Color.Red, Settings.Opacity.RejectionBlock);
            
        var rectangle = Chart.DrawRectangle(
            patternId,
            rejectionBlock.LowTime,
            rejectionBlock.Low,
            rejectionBlock.HighTime,
            rejectionBlock.High,
            color);
                
        rectangle.IsFilled = true;
            
        objectIds.Add(patternId);
            
        // Draw dotted midline
        string midLineId = $"{patternId}-midline";
        Chart.DrawTrendLine(
            midLineId,
            rejectionBlock.LowTime,
            rejectionBlock.Mid,
            rejectionBlock.HighTime,
            rejectionBlock.Mid,
            ApplyOpacity(Color.White, 70),
            Constants.Drawing.DefaultLineThickness,
            LineStyle.Dots);
                
        objectIds.Add(midLineId);
            
        // Draw quadrants if enabled
        if (Settings.Patterns.ShowQuadrants && rejectionBlock.Quadrants != null && rejectionBlock.Quadrants.Count > 0)
        {
            DrawQuadrantLevels(rejectionBlock, patternId, objectIds);
        }
        
        // Draw timeframe label if enabled
        if (ShouldShowTimeframeLabel(rejectionBlock.TimeFrame))
        {
            DrawTimeframeLabel(rejectionBlock, patternId, objectIds);
        }
    }
        
    private void DrawQuadrantLevels(Level rejectionBlock, string patternId, List<string> objectIds)
    {
        Color unsweptColor = GetColorFromString("Pink");
        Color sweptColor = GetColorFromString("Gray");
            
        LineStyle[] styles = new LineStyle[]
        {
            LineStyle.Solid,  // 0%
            LineStyle.Dots,   // 25%
            LineStyle.Solid,  // 50%
            LineStyle.Dots,   // 75%
            LineStyle.Solid   // 100%
        };
            
        for (int i = 0; i < rejectionBlock.Quadrants.Count && i < styles.Length; i++)
        {
            var quadrant = rejectionBlock.Quadrants[i];
            string quadId = $"quad-{rejectionBlock.Direction}-{rejectionBlock.Index}-{quadrant.Percent}";
                
            Chart.DrawTrendLine(
                quadId,
                rejectionBlock.LowTime,
                quadrant.Price,
                rejectionBlock.HighTime,
                quadrant.Price,
                quadrant.IsSwept ? sweptColor : unsweptColor,
                Constants.Drawing.DefaultLineThickness,
                styles[i]);
                    
            objectIds.Add(quadId);
        }
    }
    
    /// <summary>
    /// Draws a timeframe label on the Rejection Block
    /// </summary>
    private void DrawTimeframeLabel(Level rejectionBlock, string patternId, List<string> objectIds)
    {
        string labelId = $"{patternId}-tf-label";
        objectIds.Add(labelId);
        
        // Position the label at the center of the pattern
        DateTime labelTime = rejectionBlock.MidTime.AddMinutes(30);
        double labelPrice = rejectionBlock.Mid;
        
        string timeframeText = rejectionBlock.TimeFrame.GetShortName();
        
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