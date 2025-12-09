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
    /// Handles visualization of Fair Value Gaps (FVGs)
    /// </summary>
    public class FvgVisualizer : BaseVisualizer<Level>
    {
        public FvgVisualizer(
            Chart chart,
            VisualizationSettings settings,
            Action<string> logger = null)
            : base(chart, settings, logger)
        {
        }
        
        protected override bool ShouldDraw(Level fvg)
        {
            return base.ShouldDraw(fvg) && 
                   fvg.LevelType == LevelType.FairValueGap &&
                   Settings.Patterns.ShowFVG;
        }
        
        protected override string GetPatternId(Level fvg)
        {
            return $"fvg-{fvg.Direction}-{fvg.Index}-{fvg.IndexHigh}-{fvg.IndexLow}";
        }
        
        protected override void PerformDraw(Level fvg, string patternId, List<string> objectIds)
        {
            // Determine colors based on direction (same style as Unicorn)
            Color rectangleColor = fvg.Direction == Direction.Up ? Color.Green : Color.Pink;

            // Calculate rectangle extension (10 candlesticks to the right from detection point)
            int startIndex = Math.Min(fvg.IndexHigh, fvg.IndexLow);
            int endIndex = startIndex + 10;

            // Draw the main rectangle (same style as Unicorn)
            string rectId = $"{patternId}-rect";
            var rect = Chart.DrawRectangle(
                rectId,
                startIndex,     // Start index (where FVG was formed)
                fvg.High,       // Top of FVG
                endIndex,       // End index (10 candles to the right)
                fvg.Low,        // Bottom of FVG
                Color.FromArgb(30, rectangleColor),
                2               // Thickness
            );

            rect.IsFilled = true;
            objectIds.Add(rectId);

            // Draw the midline (same style as Unicorn)
            double midPrice = (fvg.High + fvg.Low) / 2.0;
            string midlineId = $"{patternId}-mid";
            Chart.DrawTrendLine(
                midlineId,
                startIndex,     // Start index
                midPrice,       // Mid price
                endIndex,       // End index
                midPrice,       // Mid price
                Color.FromArgb(60, rectangleColor),
                1,              // Thickness
                LineStyle.Solid
            );
            objectIds.Add(midlineId);

            // Draw quadrants if enabled
            if (Settings.Patterns.ShowQuadrants && fvg.Quadrants != null && fvg.Quadrants.Count > 0)
            {
                DrawQuadrantLevels(fvg, patternId, objectIds, startIndex, endIndex);
            }

            // Draw timeframe label if enabled
            if (ShouldShowTimeframeLabel(fvg.TimeFrame))
            {
                DrawTimeframeLabel(fvg, patternId, objectIds, endIndex);
            }
        }

        private void DrawQuadrantLevels(Level fvg, string patternId, List<string> objectIds, int startIndex, int endIndex)
        {
            Color unsweptColor = GetColorFromString("Pink");
            Color sweptColor = GetColorFromString("Gray");

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
            for (int i = 0; i < fvg.Quadrants.Count && i < styles.Length; i++)
            {
                var quadrant = fvg.Quadrants[i];
                string quadId = $"{patternId}-quad-{quadrant.Percent}";

                Chart.DrawTrendLine(
                    quadId,
                    startIndex,
                    quadrant.Price,
                    endIndex,
                    quadrant.Price,
                    quadrant.IsSwept ? sweptColor : unsweptColor,
                    1,
                    styles[i]);

                objectIds.Add(quadId);
            }
        }
        
        protected override void LogDraw(Level fvg, string patternId)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Drew FVG: {patternId}, Range: {fvg.Low:F5} - {fvg.High:F5}");
            }
        }
        
        protected override void LogRemove(Level fvg, string patternId)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Removed FVG: {patternId}");
            }
        }
        
        /// <summary>
        /// Updates FVG visualization when quadrants are swept
        /// </summary>
        public void UpdateQuadrant(Level fvg, Quadrant quadrant)
        {
            if (!ShouldDraw(fvg))
                return;
            
            string patternId = GetPatternId(fvg);
            string quadId = $"{patternId}-quad-{quadrant.Percent}";
            
            // Remove existing line
            Chart.RemoveObject(quadId);
            
            // Redraw with swept color
            DateTime startTime = fvg.Direction == Direction.Up ? fvg.LowTime : fvg.HighTime;
            DateTime endTime = fvg.Direction == Direction.Up ? fvg.HighTime : fvg.LowTime;
            
            LineStyle style = (quadrant.Percent % 50 == 0) ? LineStyle.Solid : LineStyle.Dots;
            
            Chart.DrawTrendLine(
                quadId,
                startTime,
                quadrant.Price,
                endTime,
                quadrant.Price,
                GetColorFromString("Gray"),
                Constants.Drawing.DefaultLineThickness,
                style);
        }
        
        /// <summary>
        /// Draws a timeframe label on the FVG
        /// </summary>
        private void DrawTimeframeLabel(Level fvg, string patternId, List<string> objectIds, int endIndex)
        {
            string labelId = $"{patternId}-tf-label";
            objectIds.Add(labelId);

            // Position the label at the middle of the rectangle extension
            int labelIndex = (Math.Min(fvg.IndexHigh, fvg.IndexLow) + endIndex) / 2;
            double labelPrice = fvg.Mid;

            string timeframeText = fvg.TimeFrame.GetShortName();

            var text = Chart.DrawText(
                labelId,
                timeframeText,
                labelIndex,
                labelPrice,
                Color.White
            );

            text.FontSize = 8;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
        }
    }
}