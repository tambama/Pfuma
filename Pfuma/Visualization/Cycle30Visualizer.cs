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
        /// Note: When ShowCycles30 is enabled, only sweep lines are drawn (no boxes or labels)
        /// </summary>
        public void DrawCurrentCycle(int currentIndex)
        {
            if (!_timeSettings.ShowCycles30)
                return;

            // For 30-minute cycles, we only draw sweep lines (handled in DrawLiquiditySweepLine)
            // No boxes or labels are drawn
            return;
        }

        /// <summary>
        /// Draw liquidity sweep line when cycle high/low is swept
        /// </summary>
        public void DrawLiquiditySweepLine(SwingPoint sweptCyclePoint, int sweepIndex, bool showLiquiditySweep)
        {
            if (!showLiquiditySweep || !_timeSettings.ShowCycles30 || sweptCyclePoint?.LiquidityType != LiquidityType.Cycle)
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
                lineColor,
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