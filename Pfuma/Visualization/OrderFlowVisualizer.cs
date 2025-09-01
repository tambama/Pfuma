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
    /// Handles visualization of Order Flow patterns
    /// </summary>
    public class OrderFlowVisualizer : BaseVisualizer<Level>
    {
        public OrderFlowVisualizer(
            Chart chart,
            VisualizationSettings settings,
            Action<string> logger = null)
            : base(chart, settings, logger)
        {
        }
        
        protected override bool ShouldDraw(Level orderFlow)
        {
            // Only check base conditions and level type
            // The detectors will control when to call Draw based on their respective settings
            return base.ShouldDraw(orderFlow) && 
                   orderFlow.LevelType == LevelType.Orderflow;
        }
        
        protected override string GetPatternId(Level orderFlow)
        {
            return $"of-{orderFlow.Direction}-{orderFlow.Index}-{orderFlow.IndexHigh}-{orderFlow.IndexLow}";
        }
        
        protected override void PerformDraw(Level orderFlow, string patternId, List<string> objectIds)
        {
            // Get colors
            Color baseColor = GetDirectionalColor(orderFlow.Direction);
            
            // Draw main rectangle
            DrawOrderFlowRectangle(orderFlow, patternId, objectIds, baseColor);
            
            // Draw liquidity sweep line if applicable
            if (orderFlow.SweptSwingPoint != null && Settings.Patterns.ShowLiquiditySweep)
            {
                DrawSweptLiquidityLine(orderFlow, patternId, objectIds);
            }
            
            // Draw quadrants if enabled
            if (Settings.Patterns.ShowQuadrants && orderFlow.Quadrants != null && orderFlow.Quadrants.Count > 0)
            {
                DrawQuadrantLevels(orderFlow, patternId, objectIds);
            }
            
            // Draw timeframe label if enabled
            if (ShouldShowTimeframeLabel(orderFlow.TimeFrame))
            {
                DrawTimeframeLabel(orderFlow, patternId, objectIds);
            }
        }
        
        private void DrawOrderFlowRectangle(Level orderFlow, string patternId, List<string> objectIds, Color baseColor)
        {
            DateTime startTime, endTime;
            
            // For HTF orderflows, we need to use the actual bar times from the chart
            // The orderFlow times might be HTF candle times which don't directly map to chart bars
            if (orderFlow.TimeFrame != null && orderFlow.IndexLow >= 0 && orderFlow.IndexHigh >= 0)
            {
                // Use the actual bar indices to get correct times for HTF patterns
                int startIndex = Math.Min(orderFlow.IndexLow, orderFlow.IndexHigh);
                int endIndex = Math.Max(orderFlow.IndexLow, orderFlow.IndexHigh);
                
                // Make sure indices are within bounds
                if (startIndex < Chart.BarsTotal && endIndex < Chart.BarsTotal)
                {
                    startTime = Chart.Bars[startIndex].OpenTime;
                    endTime = Chart.Bars[endIndex].OpenTime;
                }
                else
                {
                    // Fallback to stored times if indices are out of bounds
                    startTime = orderFlow.Direction == Direction.Up ? orderFlow.LowTime : orderFlow.HighTime;
                    endTime = orderFlow.Direction == Direction.Up ? orderFlow.HighTime : orderFlow.LowTime;
                }
            }
            else
            {
                // Regular orderflow - use stored times
                startTime = orderFlow.Direction == Direction.Up ? orderFlow.LowTime : orderFlow.HighTime;
                endTime = orderFlow.Direction == Direction.Up ? orderFlow.HighTime : orderFlow.LowTime;
            }
            
            double lowPrice = orderFlow.Low;
            double highPrice = orderFlow.High;
            
            var rectangle = Chart.DrawRectangle(
                patternId,
                startTime,
                lowPrice,
                endTime,
                highPrice,
                baseColor);
                
            rectangle.IsFilled = true;
            rectangle.Color = ApplyOpacity(baseColor, Settings.Opacity.OrderFlow);
            
            objectIds.Add(patternId);
            
            // Draw midpoint line
            string midLineId = $"{patternId}-midline";
            Chart.DrawTrendLine(
                midLineId,
                startTime,
                orderFlow.Mid,
                endTime,
                orderFlow.Mid,
                GetColorFromString(Settings.Colors.NeutralColor),
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Dots);
                
            objectIds.Add(midLineId);
        }
        
        private void DrawSweptLiquidityLine(Level orderFlow, string patternId, List<string> objectIds)
        {
            var sweptPoint = orderFlow.SweptSwingPoint;
            string id = $"swept-{orderFlow.Direction}-{orderFlow.Index}-{sweptPoint.Index}";
            
            DateTime startTime = sweptPoint.Time;
            double price = sweptPoint.Price;
            
            // Get the time of the actual sweeping candle
            DateTime endTime;
            if (orderFlow.IndexOfSweepingCandle >= 0 && 
                orderFlow.IndexOfSweepingCandle < Chart.BarsTotal)
            {
                endTime = Chart.Bars[orderFlow.IndexOfSweepingCandle].OpenTime;
            }
            else
            {
                endTime = orderFlow.Direction == Direction.Up ? orderFlow.HighTime : orderFlow.LowTime;
            }
            
            Chart.DrawTrendLine(
                id,
                startTime,
                price,
                endTime,
                price,
                GetColorFromString(Settings.Colors.WarningColor),
                Constants.Drawing.DefaultLineThickness,
                LineStyle.Dots);
                
            objectIds.Add(id);
        }
        
        private void DrawQuadrantLevels(Level orderFlow, string patternId, List<string> objectIds)
        {
            DateTime startTime, endTime;
            
            // For HTF orderflows, use actual bar times from indices
            if (orderFlow.TimeFrame != null && orderFlow.IndexLow >= 0 && orderFlow.IndexHigh >= 0)
            {
                int startIndex = Math.Min(orderFlow.IndexLow, orderFlow.IndexHigh);
                int endIndex = Math.Max(orderFlow.IndexLow, orderFlow.IndexHigh);
                
                if (startIndex < Chart.BarsTotal && endIndex < Chart.BarsTotal)
                {
                    startTime = Chart.Bars[startIndex].OpenTime;
                    endTime = Chart.Bars[endIndex].OpenTime;
                }
                else
                {
                    startTime = orderFlow.Direction == Direction.Up ? orderFlow.LowTime : orderFlow.HighTime;
                    endTime = orderFlow.Direction == Direction.Up ? orderFlow.HighTime : orderFlow.LowTime;
                }
            }
            else
            {
                startTime = orderFlow.Direction == Direction.Up ? orderFlow.LowTime : orderFlow.HighTime;
                endTime = orderFlow.Direction == Direction.Up ? orderFlow.HighTime : orderFlow.LowTime;
            }
            
            Color unsweptColor = GetColorFromString("Pink");
            Color sweptColor = GetColorFromString("Gray");
            
            LineStyle[] styles = new LineStyle[]
            {
                LineStyle.Solid,  // 0%
                LineStyle.Dots,   // 25%
                LineStyle.Solid,  // 50%
                LineStyle.Dots,   // 75%
                LineStyle.Solid   // 100%
            };
            
            for (int i = 0; i < orderFlow.Quadrants.Count && i < styles.Length; i++)
            {
                var quadrant = orderFlow.Quadrants[i];
                string quadId = $"quad-{orderFlow.Direction}-{orderFlow.Index}-{quadrant.Percent}";
                
                Chart.DrawTrendLine(
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
        /// Updates quadrant visualization when swept
        /// </summary>
        public void UpdateQuadrant(Level orderFlow, Quadrant quadrant)
        {
            if (!ShouldDraw(orderFlow))
                return;
            
            string quadId = $"quad-{orderFlow.Direction}-{orderFlow.Index}-{quadrant.Percent}";
            
            // Remove existing line
            Chart.RemoveObject(quadId);
            
            // Redraw with swept color
            DateTime startTime, endTime;
            
            // For HTF orderflows, use actual bar times from indices
            if (orderFlow.TimeFrame != null && orderFlow.IndexLow >= 0 && orderFlow.IndexHigh >= 0)
            {
                int startIndex = Math.Min(orderFlow.IndexLow, orderFlow.IndexHigh);
                int endIndex = Math.Max(orderFlow.IndexLow, orderFlow.IndexHigh);
                
                if (startIndex < Chart.BarsTotal && endIndex < Chart.BarsTotal)
                {
                    startTime = Chart.Bars[startIndex].OpenTime;
                    endTime = Chart.Bars[endIndex].OpenTime;
                }
                else
                {
                    startTime = orderFlow.Direction == Direction.Up ? orderFlow.LowTime : orderFlow.HighTime;
                    endTime = orderFlow.Direction == Direction.Up ? orderFlow.HighTime : orderFlow.LowTime;
                }
            }
            else
            {
                startTime = orderFlow.Direction == Direction.Up ? orderFlow.LowTime : orderFlow.HighTime;
                endTime = orderFlow.Direction == Direction.Up ? orderFlow.HighTime : orderFlow.LowTime;
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
        
        protected override void LogDraw(Level orderFlow, string patternId)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Drew Order Flow: {patternId}");
            }
        }
        
        /// <summary>
        /// Draws a timeframe label on the Order Flow
        /// </summary>
        private void DrawTimeframeLabel(Level orderFlow, string patternId, List<string> objectIds)
        {
            string labelId = $"{patternId}-tf-label";
            objectIds.Add(labelId);
            
            // Position the label at the center of the pattern
            DateTime labelTime = orderFlow.MidTime.AddMinutes(30);
            double labelPrice = orderFlow.Mid;
            
            string timeframeText = orderFlow.TimeFrame.GetShortName();
            
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