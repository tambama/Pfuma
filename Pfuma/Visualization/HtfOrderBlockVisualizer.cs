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
    /// Visualizes Higher Timeframe Order Blocks with distinct styling.
    /// Uses bar indices for drawing (same approach as HtfFvgVisualizer).
    /// </summary>
    public class HtfOrderBlockVisualizer : BaseVisualizer<Level>
    {
        private readonly IndicatorSettings _indicatorSettings;

        public HtfOrderBlockVisualizer(Chart chart, IndicatorSettings settings)
            : base(chart, settings.Visualization, null)
        {
            _indicatorSettings = settings;
        }

        protected override bool ShouldDraw(Level orderBlock)
        {
            return base.ShouldDraw(orderBlock) &&
                   orderBlock.LevelType == LevelType.OrderBlock &&
                   orderBlock.TimeFrame != null &&
                   _indicatorSettings.Patterns.ShowHtfOrderBlock;
        }

        protected override string GetPatternId(Level orderBlock)
        {
            var tfLabel = orderBlock.TimeFrame?.GetShortName() ?? "HTF";
            // Use unique ID with htf_ob prefix to avoid conflicts
            return $"htf_ob_{tfLabel}_{orderBlock.Direction}_{orderBlock.IndexLow}_{orderBlock.IndexHigh}_{orderBlock.Index}";
        }

        protected override void PerformDraw(Level orderBlock, string patternId, List<string> objectIds)
        {
            if (orderBlock == null || orderBlock.LevelType != LevelType.OrderBlock)
                return;

            var tfLabel = orderBlock.TimeFrame?.GetShortName() ?? "HTF";

            // Determine color based on direction
            Color rectangleColor = orderBlock.Direction == Direction.Up ? Color.Green : Color.Red;

            // Get start and end indices for the rectangle
            int startIndex = Math.Min(orderBlock.IndexLow, orderBlock.IndexHigh);
            int endIndex = Math.Max(orderBlock.IndexLow, orderBlock.IndexHigh);

            // Draw the main rectangle using bar indices
            string rectId = $"{patternId}_rect";
            var rect = Chart.DrawRectangle(
                rectId,
                startIndex,
                orderBlock.High,
                endIndex,
                orderBlock.Low,
                Color.FromArgb(50, rectangleColor),
                2
            );
            rect.IsFilled = true;
            objectIds.Add(rectId);

            // Draw the midline
            double midPrice = (orderBlock.High + orderBlock.Low) / 2.0;
            string midlineId = $"{patternId}_mid";
            Chart.DrawTrendLine(
                midlineId,
                startIndex,
                midPrice,
                endIndex,
                midPrice,
                Color.FromArgb(100, rectangleColor),
                1,
                LineStyle.Dots
            );
            objectIds.Add(midlineId);

            // Draw OB label at the middle of the rectangle
            string labelId = $"{patternId}_label";
            int labelIndex = (startIndex + endIndex) / 2;
            var label = Chart.DrawText(labelId, $"{tfLabel} OB", labelIndex, midPrice, Color.White);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.FontSize = 9;
            objectIds.Add(labelId);

            // Draw quadrants if enabled
            if (_indicatorSettings.Patterns.ShowQuadrants && orderBlock.Quadrants != null && orderBlock.Quadrants.Count > 0)
            {
                DrawQuadrants(orderBlock, patternId, objectIds, startIndex, endIndex, rectangleColor);
            }
        }

        private void DrawQuadrants(Level orderBlock, string patternId, List<string> objectIds, int startIndex, int endIndex, Color baseColor)
        {
            // Use subtle color for quadrant lines
            var quadrantColor = Color.FromArgb(60, baseColor);

            var range = orderBlock.High - orderBlock.Low;
            var q1 = orderBlock.Low + (range * 0.25);
            var q3 = orderBlock.Low + (range * 0.75);

            // Draw Q1 (25%)
            string q1Id = $"{patternId}_q1";
            Chart.DrawTrendLine(
                q1Id,
                startIndex,
                q1,
                endIndex,
                q1,
                quadrantColor,
                1,
                LineStyle.DotsRare
            );
            objectIds.Add(q1Id);

            // Draw Q3 (75%)
            string q3Id = $"{patternId}_q3";
            Chart.DrawTrendLine(
                q3Id,
                startIndex,
                q3,
                endIndex,
                q3,
                quadrantColor,
                1,
                LineStyle.DotsRare
            );
            objectIds.Add(q3Id);
        }
    }
}
