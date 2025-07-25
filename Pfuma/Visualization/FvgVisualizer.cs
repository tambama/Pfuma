using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
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
            // Calculate drawing parameters
            double lowPrice = fvg.Low;
            double highPrice = fvg.High;
            double midPrice = fvg.Mid;
            
            DateTime startTime = fvg.MidTime;
            DateTime endTime = startTime.AddMinutes(Constants.Time.LevelExtensionMinutes);
            
            Color baseColor = GetDirectionalColor(fvg.Direction);
            
            // Draw main rectangle
            DrawFvgRectangle(fvg, patternId, objectIds, baseColor);
            
            // Draw level lines
            DrawFvgLevelLines(fvg, patternId, objectIds, baseColor);
            
            // Draw quadrants if enabled
            if (Settings.Patterns.ShowQuadrants && fvg.Quadrants != null && fvg.Quadrants.Count > 0)
            {
                DrawQuadrantLevels(fvg, patternId, objectIds);
            }
        }
        
        private void DrawFvgRectangle(Level fvg, string patternId, List<string> objectIds, Color baseColor)
        {
            string rectangleId = patternId;
            
            var rectangle = Chart.DrawRectangle(
                rectangleId,
                fvg.MidTime,
                fvg.Low,
                fvg.MidTime.AddMinutes(Constants.Time.LevelExtensionMinutes),
                fvg.High,
                baseColor);
            
            rectangle.IsFilled = true;
            rectangle.Color = ApplyOpacity(baseColor, Settings.Opacity.FVG);
            
            objectIds.Add(rectangleId);
        }
        
        private void DrawFvgLevelLines(Level fvg, string patternId, List<string> objectIds, Color baseColor)
        {
            DateTime startTime = fvg.MidTime;
            DateTime endTime = startTime.AddMinutes(1); // Short lines for levels
            
            // Low line
            string lowLineId = $"{patternId}-low";
            Chart.DrawTrendLine(
                lowLineId,
                startTime,
                fvg.Low,
                endTime,
                fvg.Low,
                baseColor,
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Solid);
            objectIds.Add(lowLineId);
            
            // Mid line
            string midLineId = $"{patternId}-mid";
            Chart.DrawTrendLine(
                midLineId,
                startTime,
                fvg.Mid,
                endTime,
                fvg.Mid,
                baseColor,
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Dots);
            objectIds.Add(midLineId);
            
            // High line
            string highLineId = $"{patternId}-high";
            Chart.DrawTrendLine(
                highLineId,
                startTime,
                fvg.High,
                endTime,
                fvg.High,
                baseColor,
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Solid);
            objectIds.Add(highLineId);
        }
        
        private void DrawQuadrantLevels(Level fvg, string patternId, List<string> objectIds)
        {
            DateTime startTime = fvg.Direction == Direction.Up ? fvg.LowTime : fvg.HighTime;
            DateTime endTime = fvg.Direction == Direction.Up ? fvg.HighTime : fvg.LowTime;
            
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
                
                var line = Chart.DrawTrendLine(
                    quadId,
                    startTime,
                    quadrant.Price,
                    endTime,
                    quadrant.Price,
                    quadrant.IsSwept ? sweptColor : unsweptColor,
                    Constants.Drawing.DefaultLineThickness,
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
    }
}