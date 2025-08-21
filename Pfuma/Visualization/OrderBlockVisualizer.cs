using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Handles visualization of Order Blocks including liquidity sweep updates
    /// </summary>
    public class OrderBlockVisualizer : BaseVisualizer<Level>
    {
        private readonly IEventAggregator _eventAggregator;

        public OrderBlockVisualizer(
            Chart chart,
            VisualizationSettings settings,
            IEventAggregator eventAggregator,
            Action<string> logger = null)
            : base(chart, settings, logger)
        {
            _eventAggregator = eventAggregator;
        }

        /// <summary>
        /// Initialize visualizer and subscribe to liquidity sweep events
        /// </summary>
        public void Initialize()
        {
            _eventAggregator?.Subscribe<OrderBlockLiquiditySweptEvent>(OnOrderBlockLiquiditySwept);
        }

        /// <summary>
        /// Handle order block liquidity sweep events
        /// </summary>
        private void OnOrderBlockLiquiditySwept(OrderBlockLiquiditySweptEvent evt)
        {
            if (evt?.OrderBlock == null)
                return;

            // Redraw the order block with updated state (inactive)
            UpdateLiquiditySweptOrderBlock(evt.OrderBlock);
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
            DrawMidpointLine(orderBlock, patternId, objectIds, baseColor);
            
            // Draw quadrants if enabled
            if (Settings.Patterns.ShowQuadrants && orderBlock.Quadrants != null && orderBlock.Quadrants.Count > 0)
            {
                DrawQuadrantLevels(orderBlock, patternId, objectIds);
            }
            
            // Draw label only if order block is active (not liquidity swept)
            if (orderBlock.IsActive)
            {
                DrawOrderBlockLabel(orderBlock, patternId, objectIds);
            }
        }

        /// <summary>
        /// Update visualization for liquidity swept order block
        /// Remove label and increase opacity of rectangle
        /// </summary>
        private void UpdateLiquiditySweptOrderBlock(Level orderBlock)
        {
            try
            {
                // If the order block was not extended, don't redraw it (it should be deleted)
                if (!orderBlock.IsExtended)
                {
                    Logger?.Invoke($"Skipping redraw of non-extended swept order block: {orderBlock.Direction} at {orderBlock.High:F5}-{orderBlock.Low:F5}");
                    return;
                }
                
                var patternId = GetPatternId(orderBlock);
                
                // Remove the existing label
                var labelId = $"{patternId}-label";
                Chart.RemoveObject(labelId);
                
                // Redraw rectangle with increased opacity
                var rectangleId = $"{patternId}-rect";
                Chart.RemoveObject(rectangleId);
                
                // Draw rectangle with higher opacity for liquidity swept state
                Color baseColor = GetDirectionalColor(orderBlock.Direction);
                var sweptRectangleColor = Color.FromArgb(Math.Min(255, Settings.Opacity.OrderBlock + 50), baseColor.R, baseColor.G, baseColor.B);
                
                var startTime = orderBlock.LowTime;
                var endTime = orderBlock.StretchTo ?? orderBlock.HighTime;
                
                var rect = Chart.DrawRectangle(rectangleId, startTime, orderBlock.High, endTime, orderBlock.Low, sweptRectangleColor);
                
                Logger?.Invoke($"Updated liquidity swept order block visualization: {orderBlock.Direction} at {orderBlock.High:F5}-{orderBlock.Low:F5}");
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error updating liquidity swept order block: {ex.Message}");
            }
        }
        
        private void DrawOrderBlockRectangle(Level orderBlock, string patternId, List<string> objectIds, Color baseColor)
        {
            try
            {
                var rectangleId = $"{patternId}-rect";
                
                // Use lower opacity for the rectangle
                var rectangleColor = Color.FromArgb(Settings.Opacity.OrderBlock, baseColor.R, baseColor.G, baseColor.B);
                
                var startTime = orderBlock.LowTime;
                var endTime = orderBlock.StretchTo ?? orderBlock.HighTime;
                
                Chart.DrawRectangle(rectangleId, startTime, orderBlock.High, endTime, orderBlock.Low, rectangleColor);
                
                objectIds.Add(rectangleId);
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error drawing order block rectangle: {ex.Message}");
            }
        }
        
        private void DrawMidpointLine(Level orderBlock, string patternId, List<string> objectIds, Color baseColor)
        {
            try
            {
                var lineId = $"{patternId}-mid";
                var midpoint = (orderBlock.High + orderBlock.Low) / 2;
                var startTime = orderBlock.LowTime;
                var endTime = orderBlock.StretchTo ?? orderBlock.HighTime;
                
                Chart.DrawTrendLine(lineId, startTime, midpoint, endTime, midpoint, baseColor, 1, LineStyle.Dots);
                
                objectIds.Add(lineId);
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error drawing order block midpoint line: {ex.Message}");
            }
        }
        
        private void DrawQuadrantLevels(Level orderBlock, string patternId, List<string> objectIds)
        {
            try
            {
                var range = orderBlock.High - orderBlock.Low;
                var q1 = orderBlock.Low + (range * 0.25);
                var q2 = orderBlock.Low + (range * 0.5); // Same as midpoint
                var q3 = orderBlock.Low + (range * 0.75);
                
                var startTime = orderBlock.LowTime;
                var endTime = orderBlock.StretchTo ?? orderBlock.HighTime;
                
                Color quadrantColor = Color.FromArgb(100, Color.Gray);
                
                // Draw Q1 (25%)
                var q1Id = $"{patternId}-q1";
                Chart.DrawTrendLine(q1Id, startTime, q1, endTime, q1, quadrantColor, 1, LineStyle.DotsRare);
                objectIds.Add(q1Id);
                
                // Draw Q3 (75%)
                var q3Id = $"{patternId}-q3";
                Chart.DrawTrendLine(q3Id, startTime, q3, endTime, q3, quadrantColor, 1, LineStyle.DotsRare);
                objectIds.Add(q3Id);
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error drawing order block quadrants: {ex.Message}");
            }
        }
        
        private void DrawOrderBlockLabel(Level orderBlock, string patternId, List<string> objectIds)
        {
            try
            {
                var labelId = $"{patternId}-label";
                var labelText = $"OB {orderBlock.Direction}";
                var labelPrice = orderBlock.Direction == Direction.Up ? orderBlock.High : orderBlock.Low;
                var labelTime = orderBlock.LowTime;
                
                Color labelColor = GetDirectionalColor(orderBlock.Direction);
                
                Chart.DrawText(labelId, labelText, labelTime, labelPrice, labelColor);
                
                objectIds.Add(labelId);
            }
            catch (Exception ex)
            {
                Logger?.Invoke($"Error drawing order block label: {ex.Message}");
            }
        }
        
        private new Color GetDirectionalColor(Direction direction)
        {
            return direction == Direction.Up ? Color.Green : Color.Red;
        }

        /// <summary>
        /// Dispose of resources and unsubscribe from events
        /// </summary>
        public void Dispose()
        {
            _eventAggregator?.Unsubscribe<OrderBlockLiquiditySweptEvent>(OnOrderBlockLiquiditySwept);
        }
    }
}