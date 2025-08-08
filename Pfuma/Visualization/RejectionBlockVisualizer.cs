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
            // Get colors
            Color baseColor = GetDirectionalColor(rejectionBlock.Direction);
            
            // Draw main rectangle
            DrawRejectionBlockRectangle(rejectionBlock, patternId, objectIds, baseColor);
            
            // Draw midpoint line
            DrawMidpointLine(rejectionBlock, patternId, objectIds);
            
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
        
        private void DrawRejectionBlockRectangle(Level rejectionBlock, string patternId, List<string> objectIds, Color baseColor)
        {
            string rectangleId = $"rect-{patternId}";
            
            // Calculate end time - extend by 5 minutes by default
            DateTime endTime = rejectionBlock.StretchTo ?? 
                rejectionBlock.LowTime.AddMinutes(Constants.Time.LevelExtensionMinutes);
            
            var rectangle = Chart.DrawRectangle(
                rectangleId,
                rejectionBlock.LowTime,
                rejectionBlock.Low,
                endTime,
                rejectionBlock.High,
                baseColor);
            
            rectangle.IsFilled = true;
            rectangle.Color = ApplyOpacity(baseColor, Settings.Opacity.RejectionBlock);
            
            objectIds.Add(rectangleId);
        }
        
        private void DrawMidpointLine(Level rejectionBlock, string patternId, List<string> objectIds)
        {
            string midLineId = $"rect-{patternId}-ce";
            
            DateTime endTime = rejectionBlock.StretchTo ?? 
                rejectionBlock.LowTime.AddMinutes(Constants.Time.LevelExtensionMinutes);
            
            var midLine = Chart.DrawTrendLine(
                midLineId,
                rejectionBlock.LowTime,
                rejectionBlock.Mid,
                endTime,
                rejectionBlock.Mid,
                GetColorFromString(Settings.Colors.NeutralColor),
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Dots);
            
            objectIds.Add(midLineId);
        }
        
        private void DrawQuadrantLevels(Level rejectionBlock, string patternId, List<string> objectIds)
        {
            DateTime startTime = rejectionBlock.Direction == Direction.Up ? 
                rejectionBlock.LowTime : rejectionBlock.HighTime;
            DateTime endTime = rejectionBlock.Direction == Direction.Up ? 
                rejectionBlock.HighTime : rejectionBlock.LowTime;
            
            // Ensure we have a reasonable end time
            if (endTime <= startTime)
            {
                endTime = startTime.AddMinutes(Constants.Time.LevelExtensionMinutes);
            }
            
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
            for (int i = 0; i < rejectionBlock.Quadrants.Count && i < styles.Length; i++)
            {
                var quadrant = rejectionBlock.Quadrants[i];
                string quadId = $"quad-{rejectionBlock.Direction}-{rejectionBlock.Index}-{quadrant.Percent}";
                
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
        
        /// <summary>
        /// Updates order block visualization when it's activated
        /// </summary>
        public void DrawActivation(Level rejectionBlock, SwingPoint activatingPoint)
        {
            if (!ShouldDraw(rejectionBlock) || activatingPoint == null)
                return;
            
            string patternId = GetPatternId(rejectionBlock);
            string activationId = $"activation-{patternId}-{activatingPoint.Time.Ticks}";
            
            var objectIds = new List<string>();
            
            // Define the start and end times
            DateTime startTime = rejectionBlock.LowTime < rejectionBlock.HighTime ? 
                rejectionBlock.LowTime : rejectionBlock.HighTime;
            DateTime endTime = activatingPoint.Time;
            
            // Set color based on direction
            Color color = GetDirectionalColor(rejectionBlock.Direction);
            
            // Draw the activation rectangle
            string rectId = $"{activationId}-rect";
            var rectangle = Chart.DrawRectangle(
                rectId,
                startTime,
                rejectionBlock.Low,
                endTime,
                rejectionBlock.High,
                color);
            
            rectangle.IsFilled = true;
            rectangle.Color = ApplyOpacity(color, Settings.Opacity.ActivationRectangle);
            
            objectIds.Add(rectId);
            
            // Draw a dotted line for the midpoint
            string midLineId = $"{activationId}-midline";
            Chart.DrawTrendLine(
                midLineId,
                startTime,
                rejectionBlock.Mid,
                endTime,
                rejectionBlock.Mid,
                GetColorFromString(Settings.Colors.NeutralColor),
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Dots);
            
            objectIds.Add(midLineId);
            
            // Register these objects
            RegisterObjects(activationId, objectIds);
        }
        
        /// <summary>
        /// Updates order block visualization when quadrants are swept
        /// </summary>
        public void UpdateQuadrant(Level rejectionBlock, Quadrant quadrant)
        {
            if (!ShouldDraw(rejectionBlock))
                return;
            
            string quadId = $"quad-{rejectionBlock.Direction}-{rejectionBlock.Index}-{quadrant.Percent}";
            
            // Remove existing line
            Chart.RemoveObject(quadId);
            
            // Redraw with swept color
            DateTime startTime = rejectionBlock.Direction == Direction.Up ? 
                rejectionBlock.LowTime : rejectionBlock.HighTime;
            DateTime endTime = rejectionBlock.Direction == Direction.Up ? 
                rejectionBlock.HighTime : rejectionBlock.LowTime;
            
            // Ensure we have a reasonable end time
            if (endTime <= startTime)
            {
                endTime = startTime.AddMinutes(Constants.Time.LevelExtensionMinutes);
            }
            
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
        
        protected override void LogDraw(Level rejectionBlock, string patternId)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Drew Rejection Block: {patternId}, Range: {rejectionBlock.Low:F5} - {rejectionBlock.High:F5}");
            }
        }
        
        protected override void LogRemove(Level rejectionBlock, string patternId)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Removed Rejection Block: {patternId}");
            }
        }
        
        /// <summary>
        /// Draws a timeframe label on the Rejection Block
        /// </summary>
        private void DrawTimeframeLabel(Level rejectionBlock, string patternId, List<string> objectIds)
        {
            string labelId = $"{patternId}-tf-label";
            objectIds.Add(labelId);
            
            // Position the label at the center of the Rejection Block
            DateTime labelTime = rejectionBlock.LowTime.AddMinutes(Constants.Time.LevelExtensionMinutes / 2);
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
}