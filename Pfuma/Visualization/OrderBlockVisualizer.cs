using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Handles visualization of Order Blocks
    /// </summary>
    public class OrderBlockVisualizer : BaseVisualizer<Level>
    {
        public OrderBlockVisualizer(
            Chart chart,
            VisualizationSettings settings,
            Action<string> logger = null)
            : base(chart, settings, logger)
        {
        }
        
        protected override bool ShouldDraw(Level orderBlock)
        {
            return base.ShouldDraw(orderBlock) && 
                   orderBlock.LevelType == LevelType.OrderBlock &&
                   Settings.Patterns.ShowOrderBlock;
        }
        
        protected override string GetPatternId(Level orderBlock)
        {
            return $"ob-{orderBlock.Direction}-{orderBlock.Index}";
        }
        
        protected override void PerformDraw(Level orderBlock, string patternId, List<string> objectIds)
        {
            // Get colors
            Color baseColor = GetDirectionalColor(orderBlock.Direction);
            
            // Draw main rectangle
            DrawOrderBlockRectangle(orderBlock, patternId, objectIds, baseColor);
            
            // Draw midpoint line
            DrawMidpointLine(orderBlock, patternId, objectIds);
            
            // Draw quadrants if enabled
            if (Settings.Patterns.ShowQuadrants && orderBlock.Quadrants != null && orderBlock.Quadrants.Count > 0)
            {
                DrawQuadrantLevels(orderBlock, patternId, objectIds);
            }
        }
        
        private void DrawOrderBlockRectangle(Level orderBlock, string patternId, List<string> objectIds, Color baseColor)
        {
            string rectangleId = $"rect-{patternId}";
            
            // Calculate end time - extend by 5 minutes by default
            DateTime endTime = orderBlock.StretchTo ?? 
                orderBlock.LowTime.AddMinutes(Constants.Time.LevelExtensionMinutes);
            
            var rectangle = Chart.DrawRectangle(
                rectangleId,
                orderBlock.LowTime,
                orderBlock.Low,
                endTime,
                orderBlock.High,
                baseColor);
            
            rectangle.IsFilled = true;
            rectangle.Color = ApplyOpacity(baseColor, Settings.Opacity.OrderBlock);
            
            objectIds.Add(rectangleId);
        }
        
        private void DrawMidpointLine(Level orderBlock, string patternId, List<string> objectIds)
        {
            string midLineId = $"rect-{patternId}-ce";
            
            DateTime endTime = orderBlock.StretchTo ?? 
                orderBlock.LowTime.AddMinutes(Constants.Time.LevelExtensionMinutes);
            
            var midLine = Chart.DrawTrendLine(
                midLineId,
                orderBlock.LowTime,
                orderBlock.Mid,
                endTime,
                orderBlock.Mid,
                GetColorFromString(Settings.Colors.NeutralColor),
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Dots);
            
            objectIds.Add(midLineId);
        }
        
        private void DrawQuadrantLevels(Level orderBlock, string patternId, List<string> objectIds)
        {
            DateTime startTime = orderBlock.Direction == Direction.Up ? 
                orderBlock.LowTime : orderBlock.HighTime;
            DateTime endTime = orderBlock.Direction == Direction.Up ? 
                orderBlock.HighTime : orderBlock.LowTime;
            
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
            for (int i = 0; i < orderBlock.Quadrants.Count && i < styles.Length; i++)
            {
                var quadrant = orderBlock.Quadrants[i];
                string quadId = $"quad-{orderBlock.Direction}-{orderBlock.Index}-{quadrant.Percent}";
                
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
        public void DrawActivation(Level orderBlock, SwingPoint activatingPoint)
        {
            if (!ShouldDraw(orderBlock) || activatingPoint == null)
                return;
            
            string patternId = GetPatternId(orderBlock);
            string activationId = $"activation-{patternId}-{activatingPoint.Time.Ticks}";
            
            var objectIds = new List<string>();
            
            // Define the start and end times
            DateTime startTime = orderBlock.LowTime < orderBlock.HighTime ? 
                orderBlock.LowTime : orderBlock.HighTime;
            DateTime endTime = activatingPoint.Time;
            
            // Set color based on direction
            Color color = GetDirectionalColor(orderBlock.Direction);
            
            // Draw the activation rectangle
            string rectId = $"{activationId}-rect";
            var rectangle = Chart.DrawRectangle(
                rectId,
                startTime,
                orderBlock.Low,
                endTime,
                orderBlock.High,
                color);
            
            rectangle.IsFilled = true;
            rectangle.Color = ApplyOpacity(color, Settings.Opacity.ActivationRectangle);
            
            objectIds.Add(rectId);
            
            // Draw a dotted line for the midpoint
            string midLineId = $"{activationId}-midline";
            Chart.DrawTrendLine(
                midLineId,
                startTime,
                orderBlock.Mid,
                endTime,
                orderBlock.Mid,
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
        public void UpdateQuadrant(Level orderBlock, Quadrant quadrant)
        {
            if (!ShouldDraw(orderBlock))
                return;
            
            string quadId = $"quad-{orderBlock.Direction}-{orderBlock.Index}-{quadrant.Percent}";
            
            // Remove existing line
            Chart.RemoveObject(quadId);
            
            // Redraw with swept color
            DateTime startTime = orderBlock.Direction == Direction.Up ? 
                orderBlock.LowTime : orderBlock.HighTime;
            DateTime endTime = orderBlock.Direction == Direction.Up ? 
                orderBlock.HighTime : orderBlock.LowTime;
            
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
        
        protected override void LogDraw(Level orderBlock, string patternId)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Drew Order Block: {patternId}, Range: {orderBlock.Low:F5} - {orderBlock.High:F5}");
            }
        }
        
        protected override void LogRemove(Level orderBlock, string patternId)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Removed Order Block: {patternId}");
            }
        }
    }
}