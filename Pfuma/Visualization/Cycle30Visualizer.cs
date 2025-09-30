using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Models;
using Pfuma.Services.Time;
using Pfuma.Services;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Handles visualization of 30-minute cycles
    /// </summary>
    public class Cycle30Visualizer
    {
        private readonly Chart _chart;
        private readonly TimeSettings _timeSettings;
        private readonly Cycle30Manager _cycle30Manager;
        private readonly CandleManager _candleManager;
        private readonly Action<string> _logger;
        private readonly Dictionary<string, List<string>> _drawnObjects;

        public Cycle30Visualizer(
            Chart chart,
            TimeSettings timeSettings,
            Cycle30Manager cycle30Manager,
            CandleManager candleManager,
            Action<string> logger = null)
        {
            _chart = chart;
            _timeSettings = timeSettings;
            _cycle30Manager = cycle30Manager;
            _candleManager = candleManager;
            _logger = logger ?? (_ => { });
            _drawnObjects = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Draw current 30-minute cycle rectangle
        /// </summary>
        public void DrawCurrentCycle(int currentIndex)
        {
            if (!_timeSettings.ShowCycles30)
                return;

            var cycleStart = _cycle30Manager.GetCurrentCycleStart();
            var cycleStartIndex = _cycle30Manager.GetCurrentCycleStartIndex();

            if (cycleStart == DateTime.MinValue || cycleStartIndex == -1)
                return;

            // Determine cycle type and color
            bool isMinute00Cycle = _cycle30Manager.IsCycleStartingAtMinute00(cycleStart);
            Color rectangleColor = isMinute00Cycle ? Color.Green : Color.Pink;
            LineStyle lineStyle = isMinute00Cycle ? LineStyle.Solid : LineStyle.Dots;

            // Calculate rectangle bounds
            int endIndex = currentIndex + 10; // Extend 10 bars into the future
            double highPrice = GetHighestPriceInRange(cycleStartIndex, currentIndex);
            double lowPrice = GetLowestPriceInRange(cycleStartIndex, currentIndex);

            // Add some padding to the rectangle
            double priceRange = highPrice - lowPrice;
            double padding = priceRange * 0.1; // 10% padding
            highPrice += padding;
            lowPrice -= padding;

            // Create unique ID for this cycle
            string cycleId = $"cycle30-{cycleStart:yyyyMMdd-HHmm}";

            // Remove existing rectangle for this cycle
            RemoveCycleRectangle(cycleId);

            // Draw rectangle
            string rectId = $"{cycleId}-rect";
            var rectangle = _chart.DrawRectangle(
                rectId,
                cycleStartIndex,
                highPrice,
                endIndex,
                lowPrice,
                Color.FromArgb(30, rectangleColor), // Semi-transparent fill
                2, // Border thickness
                lineStyle
            );

            rectangle.IsFilled = true;

            // Store the drawn object
            if (!_drawnObjects.ContainsKey(cycleId))
                _drawnObjects[cycleId] = new List<string>();
            _drawnObjects[cycleId].Add(rectId);

            // Draw cycle label
            string labelId = $"{cycleId}-label";
            var label = _chart.DrawText(
                labelId,
                $"C30-{cycleStart:HH:mm}",
                cycleStartIndex,
                highPrice,
                rectangleColor
            );

            label.FontSize = 10;
            label.HorizontalAlignment = HorizontalAlignment.Left;
            label.VerticalAlignment = VerticalAlignment.Top;

            _drawnObjects[cycleId].Add(labelId);

            _logger($"Cycle30 rectangle drawn: {cycleStart:HH:mm} ({(isMinute00Cycle ? "Green" : "Pink")})");
        }

        /// <summary>
        /// Draw liquidity sweep line when cycle high/low is swept
        /// </summary>
        public void DrawLiquiditySweepLine(SwingPoint sweptCyclePoint, int sweepIndex, bool showLiquiditySweep)
        {
            if (!showLiquiditySweep || sweptCyclePoint?.LiquidityType != LiquidityType.Cycle)
                return;

            // Determine line color based on swing type
            Color lineColor = sweptCyclePoint.SwingType == SwingType.H ? Color.Green : Color.Pink;

            // Draw dotted line from swept point to sweep point
            string lineId = $"cycle30-sweep-{sweptCyclePoint.Index}-{sweepIndex}";
            var line = _chart.DrawTrendLine(
                lineId,
                sweptCyclePoint.Index,
                sweptCyclePoint.Price,
                sweepIndex,
                sweptCyclePoint.Price,
                Color.FromArgb(70, lineColor),
                1,
                LineStyle.Dots
            );

            _logger($"Cycle30 sweep line drawn: {sweptCyclePoint.SwingType} at {sweptCyclePoint.Price:F5}");
        }

        /// <summary>
        /// Remove cycle rectangle and associated objects
        /// </summary>
        private void RemoveCycleRectangle(string cycleId)
        {
            if (_drawnObjects.ContainsKey(cycleId))
            {
                foreach (var objectId in _drawnObjects[cycleId])
                {
                    _chart.RemoveObject(objectId);
                }
                _drawnObjects[cycleId].Clear();
            }
        }

        /// <summary>
        /// Get highest price in the given bar range
        /// </summary>
        private double GetHighestPriceInRange(int startIndex, int endIndex)
        {
            double highest = double.MinValue;
            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = _candleManager?.GetCandle(i);
                if (candle != null && candle.High > highest)
                    highest = candle.High;
            }
            return highest == double.MinValue ? 0 : highest;
        }

        /// <summary>
        /// Get lowest price in the given bar range
        /// </summary>
        private double GetLowestPriceInRange(int startIndex, int endIndex)
        {
            double lowest = double.MaxValue;
            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = _candleManager?.GetCandle(i);
                if (candle != null && candle.Low < lowest)
                    lowest = candle.Low;
            }
            return lowest == double.MaxValue ? 0 : lowest;
        }

        /// <summary>
        /// Clean up all drawn objects
        /// </summary>
        public void Cleanup()
        {
            foreach (var cycleObjects in _drawnObjects.Values)
            {
                foreach (var objectId in cycleObjects)
                {
                    _chart.RemoveObject(objectId);
                }
            }
            _drawnObjects.Clear();
        }
    }
}