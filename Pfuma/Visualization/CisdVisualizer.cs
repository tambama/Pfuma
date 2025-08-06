using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization;

/// <summary>
/// Handles visualization of CISD patterns
/// </summary>
public class CisdVisualizer : BaseVisualizer<Level>
{
    public CisdVisualizer(
        Chart chart,
        VisualizationSettings settings,
        Action<string> logger = null)
        : base(chart, settings, logger)
    {
    }
        
    protected override bool ShouldDraw(Level cisd)
    {
        return base.ShouldDraw(cisd) && 
               cisd.LevelType == LevelType.CISD &&
               Settings.Patterns.ShowCISD;
    }
        
    protected override string GetPatternId(Level cisd)
    {
        return $"cisd-{cisd.Direction}-{cisd.Index}";
    }
        
    protected override void PerformDraw(Level cisd, string patternId, List<string> objectIds)
    {
        Color cisdColor = cisd.Direction == Direction.Up 
            ? ApplyOpacity(Color.Green, 70)
            : ApplyOpacity(Color.Pink, 50);
            
        // If activated, draw activation line
        if (cisd.Activated)
        {
            DrawActivationLine(cisd, patternId, objectIds, cisdColor);
        }
            
        // If confirmed, draw confirmation line
        if (cisd.IsConfirmed)
        {
            DrawConfirmationLine(cisd, patternId, objectIds, cisd.Direction == Direction.Up ? Color.Green : Color.Pink);
        }
        
        // Draw timeframe label if enabled
        if (ShouldShowTimeframeLabel(cisd.TimeFrame))
        {
            DrawTimeframeLabel(cisd, patternId, objectIds);
        }
    }
        
    private void DrawActivationLine(Level cisd, string patternId, List<string> objectIds, Color color)
    {
        string id = $"{patternId}-activated";
        double priceLevel = cisd.Direction == Direction.Up ? cisd.High : cisd.Low;
        DateTime startTime = cisd.Direction == Direction.Up ? cisd.HighTime : cisd.LowTime;
            
        // Extend to current time or a reasonable distance
        DateTime endTime = startTime.AddMinutes(60);
            
        Chart.DrawTrendLine(
            id,
            startTime,
            priceLevel,
            endTime,
            priceLevel,
            color,
            Constants.Drawing.DefaultLineThickness,
            LineStyle.Dots);
                
        objectIds.Add(id);
    }
        
    private void DrawConfirmationLine(Level cisd, string patternId, List<string> objectIds, Color color)
    {
        string id = $"{patternId}-confirm";
        double priceLevel = cisd.Direction == Direction.Up ? cisd.High : cisd.Low;
        DateTime startTime = cisd.Direction == Direction.Up ? cisd.HighTime : cisd.LowTime;
            
        // Draw to the confirming candle
        DateTime endTime = Chart.Bars[cisd.IndexOfConfirmingCandle].OpenTime;
            
        Chart.DrawTrendLine(
            id,
            startTime,
            priceLevel,
            endTime,
            priceLevel,
            color,
            Constants.Drawing.DefaultLineThickness,
            LineStyle.Solid);
                
        objectIds.Add(id);
    }
    
    /// <summary>
    /// Draws a timeframe label on the CISD
    /// </summary>
    private void DrawTimeframeLabel(Level cisd, string patternId, List<string> objectIds)
    {
        string labelId = $"{patternId}-tf-label";
        objectIds.Add(labelId);
        
        // Position the label at the center of the CISD
        DateTime labelTime = cisd.Direction == Direction.Up ? cisd.HighTime : cisd.LowTime;
        labelTime = labelTime.AddMinutes(30); // Offset slightly for visibility
        double labelPrice = cisd.Mid;
        
        string timeframeText = cisd.TimeFrame.GetShortName();
        
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