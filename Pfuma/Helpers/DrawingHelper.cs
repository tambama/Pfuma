using System;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Helpers;

public static class DrawingHelper
{
    /// <summary>
    /// Draws an iFVG (Institutional FVG) with rectangle, midline, and label.
    /// Used by both OrderBlockDetector and CisdDetector.
    /// </summary>
    public static void DrawIFvg(Chart chart, Level fvg, Direction parentDirection, string idPrefix)
    {
        if (chart == null || fvg == null)
            return;

        Color rectangleColor = parentDirection == Direction.Up ? Color.Green : Color.Pink;

        int startIndex = Math.Min(fvg.IndexHigh, fvg.IndexLow);
        int endIndex = startIndex + 10;

        string patternId = $"ifvg-{idPrefix}-{parentDirection}-{fvg.Index}-{fvg.IndexHigh}-{fvg.IndexLow}";

        // Draw the main rectangle
        string rectId = $"{patternId}-rect";
        var rect = chart.DrawRectangle(
            rectId,
            startIndex,
            fvg.High,
            endIndex,
            fvg.Low,
            Color.FromArgb(30, rectangleColor),
            2
        );
        rect.IsFilled = true;

        // Draw the midline
        double midPrice = (fvg.High + fvg.Low) / 2.0;
        string midlineId = $"{patternId}-mid";
        chart.DrawTrendLine(
            midlineId,
            startIndex,
            midPrice,
            endIndex,
            midPrice,
            Color.FromArgb(60, rectangleColor),
            1,
            LineStyle.Solid
        );

        // Draw "iFVG" label at center
        string labelId = $"{patternId}-label";
        var text = chart.DrawText(
            labelId,
            "iFVG",
            (startIndex + endIndex) / 2,
            midPrice,
            rectangleColor
        );
        text.FontSize = 8;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
    }
}
