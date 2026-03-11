using System;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Services.Time;

/// <summary>
/// Shared drawing utilities for time gap levels (RTOG, FPFVG, etc.)
/// </summary>
public static class TimeGapDrawingHelper
{
    /// <summary>
    /// Draws a time gap rectangle with midline, quadrants, and label.
    /// </summary>
    public static void DrawTimeGap(
        Chart chart,
        TimeGapLevel gap,
        string label,
        Color color,
        DateTime startTime,
        int lifespanDays)
    {
        if (chart == null || gap?.Level == null) return;

        var level = gap.Level;
        DateTime endTime = startTime.AddDays(lifespanDays);
        Color fillColor = Color.FromArgb(10, color);

        // Draw extended rectangle
        string rectId = $"{gap.ChartId}-rect";
        var rect = chart.DrawRectangle(
            rectId,
            startTime,
            level.High,
            endTime,
            level.Low,
            fillColor,
            1
        );
        rect.IsFilled = true;

        // Draw dotted midline
        string midId = $"{gap.ChartId}-mid";
        chart.DrawTrendLine(
            midId,
            startTime,
            level.Mid,
            endTime,
            level.Mid,
            Color.FromArgb(100, color),
            1,
            LineStyle.Dots
        );

        // Draw quadrant lines
        if (level.Quadrants != null && level.Quadrants.Count > 0)
        {
            DrawQuadrants(chart, gap.ChartId, level, color, startTime, endTime);
        }

        // Draw label
        string labelId = $"{gap.ChartId}-label";
        var text = chart.DrawText(
            labelId,
            label,
            startTime,
            level.High,
            color
        );
        text.VerticalAlignment = VerticalAlignment.Top;
        text.HorizontalAlignment = HorizontalAlignment.Left;
        text.FontSize = 8;
    }

    /// <summary>
    /// Draws quadrant lines for a time gap level.
    /// </summary>
    public static void DrawQuadrants(
        Chart chart,
        string chartId,
        Level level,
        Color color,
        DateTime startTime,
        DateTime endTime)
    {
        foreach (var quadrant in level.Quadrants)
        {
            // Skip 0% and 100% (rectangle edges) and 50% (midline)
            if (quadrant.Percent == 0 || quadrant.Percent == 100 || quadrant.Percent == 50)
                continue;

            string quadId = $"{chartId}-quad-{quadrant.Percent}";
            chart.DrawTrendLine(
                quadId,
                startTime,
                quadrant.Price,
                endTime,
                quadrant.Price,
                Color.FromArgb(90, color),
                1,
                LineStyle.DotsRare
            );
        }
    }

    /// <summary>
    /// Removes all chart objects for a time gap level.
    /// </summary>
    public static void RemoveTimeGap(Chart chart, TimeGapLevel gap)
    {
        if (chart == null || gap?.ChartId == null) return;

        string id = gap.ChartId;

        chart.RemoveObject($"{id}-rect");
        chart.RemoveObject($"{id}-mid");
        chart.RemoveObject($"{id}-label");

        if (gap.Level?.Quadrants != null)
        {
            foreach (var quadrant in gap.Level.Quadrants)
            {
                if (quadrant.Percent == 0 || quadrant.Percent == 100 || quadrant.Percent == 50)
                    continue;
                chart.RemoveObject($"{id}-quad-{quadrant.Percent}");
            }
        }
    }
}
