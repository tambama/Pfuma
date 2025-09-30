using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Models;
using Pfuma.Visualization.Base;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Handles visualization of 369 time patterns on swing points using IndicatorSeries
    /// </summary>
    public class Pattern369Visualizer : BaseVisualizer<SwingPoint>
    {
        private readonly int _utcOffset;
        private readonly IndicatorDataSeries _pattern369Bullish;
        private readonly IndicatorDataSeries _pattern369Bearish;

        public Pattern369Visualizer(
            Chart chart,
            VisualizationSettings settings,
            IndicatorDataSeries pattern369Bullish,
            IndicatorDataSeries pattern369Bearish,
            int utcOffset = 0,
            Action<string> logger = null)
            : base(chart, settings, logger)
        {
            _utcOffset = utcOffset;
            _pattern369Bullish = pattern369Bullish;
            _pattern369Bearish = pattern369Bearish;
        }

        protected override bool ShouldDraw(SwingPoint swingPoint)
        {
            return base.ShouldDraw(swingPoint) &&
                   swingPoint.Has369 &&
                   swingPoint.Number369.HasValue &&
                   !swingPoint.Drawn369 &&
                   Settings.Patterns.Show369;
        }

        protected override string GetPatternId(SwingPoint swingPoint)
        {
            return $"369-{swingPoint.Index}-{swingPoint.SwingType}-{swingPoint.Number369}";
        }

        protected override void PerformDraw(SwingPoint swingPoint, string patternId, List<string> objectIds)
        {
            if (!swingPoint.Number369.HasValue)
                return;

            // Apply UTC offset to get local market time
            DateTime localTime = swingPoint.Time.AddHours(_utcOffset);

            // Create short time string (HH:mm format) from local time
            string timeText = localTime.ToString("HH:mm");

            // Write to appropriate IndicatorSeries based on swing point direction
            if (swingPoint.Direction == Direction.Up)
            {
                // Bullish swing point - green dot
                _pattern369Bullish[swingPoint.Index] = swingPoint.Price;
            }
            else if (swingPoint.Direction == Direction.Down)
            {
                // Bearish swing point - red dot
                _pattern369Bearish[swingPoint.Index] = swingPoint.Price;
            }

            // Mark as drawn to prevent redrawing
            swingPoint.Drawn369 = true;

            Logger?.Invoke($"369 pattern dot drawn: {timeText} (369: {swingPoint.Number369}) at index {swingPoint.Index} - {swingPoint.Direction}");
        }

        /// <summary>
        /// Draw 369 pattern for a swing point immediately if conditions are met
        /// </summary>
        public void DrawPattern369(SwingPoint swingPoint)
        {
            if (ShouldDraw(swingPoint))
            {
                Draw(swingPoint);
            }
        }

        /// <summary>
        /// Remove 369 pattern dot for a swing point
        /// </summary>
        public void RemovePattern369(SwingPoint swingPoint)
        {
            if (swingPoint?.Has369 == true && swingPoint.Drawn369)
            {
                // Clear the IndicatorSeries value at this index
                if (swingPoint.Direction == Direction.Up)
                {
                    _pattern369Bullish[swingPoint.Index] = double.NaN;
                }
                else if (swingPoint.Direction == Direction.Down)
                {
                    _pattern369Bearish[swingPoint.Index] = double.NaN;
                }

                // Reset the drawn flag
                swingPoint.Drawn369 = false;

                Logger?.Invoke($"369 pattern dot removed for swing point at index {swingPoint.Index}");
            }
        }
    }
}