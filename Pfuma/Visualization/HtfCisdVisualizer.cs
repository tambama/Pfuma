using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Visualizes Higher Timeframe CISD (Change in State of Delivery) patterns.
    /// Uses bar indices for drawing (same approach as HtfFvgVisualizer).
    /// </summary>
    public class HtfCisdVisualizer : BaseVisualizer<Level>
    {
        private readonly IndicatorSettings _indicatorSettings;

        public HtfCisdVisualizer(Chart chart, IndicatorSettings settings)
            : base(chart, settings.Visualization, null)
        {
            _indicatorSettings = settings;
        }

        protected override bool ShouldDraw(Level cisd)
        {
            return base.ShouldDraw(cisd) &&
                   cisd.LevelType == LevelType.CISD &&
                   cisd.TimeFrame != null &&
                   _indicatorSettings.Patterns.ShowHtfCisd;
        }

        protected override string GetPatternId(Level cisd)
        {
            var tfLabel = cisd.TimeFrame?.GetShortName() ?? "HTF";
            // Use unique ID with htf_cisd prefix to avoid conflicts
            return $"htf_cisd_{tfLabel}_{cisd.Direction}_{cisd.IndexLow}_{cisd.IndexHigh}_{cisd.Index}";
        }

        protected override void PerformDraw(Level cisd, string patternId, List<string> objectIds)
        {
            if (cisd == null || cisd.LevelType != LevelType.CISD)
                return;

            var tfLabel = cisd.TimeFrame?.GetShortName() ?? "HTF";

            // Determine color based on direction
            // Bullish CISD = green, Bearish CISD = red/pink
            Color rectangleColor = cisd.Direction == Direction.Up ? Color.Green : Color.Pink;

            // Get start and end indices for the rectangle
            int startIndex = Math.Min(cisd.IndexLow, cisd.IndexHigh);
            int endIndex = Math.Max(cisd.IndexLow, cisd.IndexHigh);

            // Draw the main rectangle using bar indices
            string rectId = $"{patternId}_rect";
            var rect = Chart.DrawRectangle(
                rectId,
                startIndex,
                cisd.High,
                endIndex,
                cisd.Low,
                Color.FromArgb(50, rectangleColor),
                2
            );
            rect.IsFilled = true;
            objectIds.Add(rectId);

            // Draw the entry line (high for bullish, low for bearish)
            double entryPrice = cisd.Direction == Direction.Up ? cisd.High : cisd.Low;
            string entryLineId = $"{patternId}_entry";
            Chart.DrawTrendLine(
                entryLineId,
                startIndex,
                entryPrice,
                endIndex,
                entryPrice,
                Color.FromArgb(150, rectangleColor),
                2,
                LineStyle.Solid
            );
            objectIds.Add(entryLineId);

            // Draw CISD label at the middle of the rectangle
            string labelId = $"{patternId}_label";
            int labelIndex = (startIndex + endIndex) / 2;
            double midPrice = (cisd.High + cisd.Low) / 2.0;
            var label = Chart.DrawText(labelId, $"{tfLabel} CISD", labelIndex, midPrice, Color.White);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.FontSize = 9;
            objectIds.Add(labelId);

            // Draw confirmation line if confirmed
            if (cisd.IsConfirmed)
            {
                DrawConfirmationLine(cisd, patternId, objectIds, rectangleColor);
            }

            // Draw quadrants if enabled
            if (_indicatorSettings.Patterns.ShowQuadrants && cisd.Quadrants != null && cisd.Quadrants.Count > 0)
            {
                DrawQuadrants(cisd, patternId, objectIds, startIndex, endIndex, rectangleColor);
            }
        }

        private void DrawConfirmationLine(Level cisd, string patternId, List<string> objectIds, Color baseColor)
        {
            string confirmId = $"{patternId}_confirm";

            // Entry level that was confirmed
            double priceLevel = cisd.Direction == Direction.Up ? cisd.High : cisd.Low;

            // Draw from CISD boundary to confirming candle
            int startIndex = cisd.Direction == Direction.Up ? cisd.IndexHigh : cisd.IndexLow;
            int endIndex = cisd.IndexOfConfirmingCandle;

            if (endIndex > startIndex)
            {
                Chart.DrawTrendLine(
                    confirmId,
                    startIndex,
                    priceLevel,
                    endIndex,
                    priceLevel,
                    Color.FromArgb(200, baseColor),
                    2,
                    LineStyle.Solid
                );
                objectIds.Add(confirmId);
            }
        }

        private void DrawQuadrants(Level cisd, string patternId, List<string> objectIds, int startIndex, int endIndex, Color baseColor)
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
            for (int i = 0; i < cisd.Quadrants.Count && i < styles.Length; i++)
            {
                var quadrant = cisd.Quadrants[i];
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
