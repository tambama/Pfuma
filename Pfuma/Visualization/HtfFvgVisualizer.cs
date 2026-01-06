using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Visualizes Higher Timeframe Fair Value Gaps with distinct styling.
    /// Uses bar indices for drawing (same approach as regular FvgVisualizer).
    /// </summary>
    public class HtfFvgVisualizer : BaseVisualizer<Level>
    {
        private readonly IndicatorSettings _indicatorSettings;

        public HtfFvgVisualizer(Chart chart, IndicatorSettings settings)
            : base(chart, settings.Visualization, null)
        {
            _indicatorSettings = settings;
        }

        protected override string GetPatternId(Level htfFvg)
        {
            var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
            // Use unique ID with HTF prefix to avoid conflicts with regular FVGs
            return $"htf_fvg_{tfLabel}_{htfFvg.Direction}_{htfFvg.IndexLow}_{htfFvg.IndexHigh}_{htfFvg.Index}";
        }

        protected override void PerformDraw(Level htfFvg, string patternId, List<string> objectIds)
        {
            if (htfFvg == null || htfFvg.LevelType != LevelType.FairValueGap)
                return;

            // Only draw if ShowHtfFvg is enabled
            if (!_indicatorSettings.Patterns.ShowHtfFvg)
                return;

            var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";

            // Determine color based on direction
            Color rectangleColor = htfFvg.Direction == Direction.Up ? Color.Green : Color.Red;

            // Get start and end indices for the rectangle
            // IndexLow is the LTF index where the FVG boundary started (candle1's high for bullish, candle1's low for bearish)
            // IndexHigh is the LTF index where the FVG boundary ended (candle3's low for bullish, candle3's high for bearish)
            int startIndex = Math.Min(htfFvg.IndexLow, htfFvg.IndexHigh);
            int endIndex = Math.Max(htfFvg.IndexLow, htfFvg.IndexHigh);

            // Draw the main rectangle using bar indices
            string rectId = $"{patternId}_rect";
            var rect = Chart.DrawRectangle(
                rectId,
                startIndex,
                htfFvg.High,
                endIndex,
                htfFvg.Low,
                Color.FromArgb(50, rectangleColor),
                2
            );
            rect.IsFilled = true;
            objectIds.Add(rectId);

            // Draw the midline
            double midPrice = (htfFvg.High + htfFvg.Low) / 2.0;
            string midlineId = $"{patternId}_mid";
            Chart.DrawTrendLine(
                midlineId,
                startIndex,
                midPrice,
                endIndex,
                midPrice,
                Color.FromArgb(100, rectangleColor),
                1,
                LineStyle.Solid
            );
            objectIds.Add(midlineId);

            // Draw FVG label at the middle of the gap
            string labelId = $"{patternId}_label";
            int labelIndex = (startIndex + endIndex) / 2;
            var label = Chart.DrawText(labelId, $"{tfLabel} FVG", labelIndex, midPrice, Color.White);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.FontSize = 9;
            objectIds.Add(labelId);

            // Draw quadrants if enabled
            if (_indicatorSettings.Patterns.ShowQuadrants && htfFvg.Quadrants != null && htfFvg.Quadrants.Count > 0)
            {
                DrawQuadrants(htfFvg, patternId, objectIds, startIndex, endIndex, rectangleColor);
            }
        }

        private void DrawQuadrants(Level htfFvg, string patternId, List<string> objectIds, int startIndex, int endIndex, Color baseColor)
        {
            // Use subtle color for quadrant lines
            var quadrantColor = Color.FromArgb(60, baseColor);

            // Line styles for each quadrant
            LineStyle[] styles = new LineStyle[]
            {
                LineStyle.Solid,  // 0%
                LineStyle.Dots,   // 25%
                LineStyle.Solid,  // 50% (mid)
                LineStyle.Dots,   // 75%
                LineStyle.Solid   // 100%
            };

            // Draw each quadrant line
            for (int i = 0; i < htfFvg.Quadrants.Count && i < styles.Length; i++)
            {
                var quadrant = htfFvg.Quadrants[i];
                string quadId = $"{patternId}_quad_{quadrant.Percent}";

                Chart.DrawTrendLine(
                    quadId,
                    startIndex,
                    quadrant.Price,
                    endIndex,
                    quadrant.Price,
                    quadrant.IsSwept ? Color.Gray : quadrantColor,
                    1,
                    styles[i]
                );

                objectIds.Add(quadId);
            }
        }
    }
}