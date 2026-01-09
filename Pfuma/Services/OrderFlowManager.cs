using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Manages orderflow confirmation based on candle events
    /// </summary>
    public class OrderFlowManager : IDisposable
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IRepository<Level> _orderFlowRepository;
        private readonly IVisualization<Level> _visualizer;
        private readonly Chart _chart;
        private readonly IndicatorSettings _settings;
        private readonly Action<string> _logger;
        private bool _disposed;

        public OrderFlowManager(
            IEventAggregator eventAggregator,
            IRepository<Level> orderFlowRepository,
            IVisualization<Level> visualizer,
            Chart chart,
            IndicatorSettings settings,
            Action<string> logger = null)
        {
            _eventAggregator = eventAggregator;
            _orderFlowRepository = orderFlowRepository;
            _visualizer = visualizer;
            _chart = chart;
            _settings = settings;
            _logger = logger ?? (_ => { });

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<OrderFlowProcessingCompleteEvent>(OnOrderFlowProcessingComplete);
        }

        private void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<OrderFlowProcessingCompleteEvent>(OnOrderFlowProcessingComplete);
        }

        /// <summary>
        /// Called after OrderFlowDetector has finished processing a swing point.
        /// This ensures new orderflows are created before we check for confirmations/deactivations.
        /// </summary>
        private void OnOrderFlowProcessingComplete(OrderFlowProcessingCompleteEvent evt)
        {
            if (evt?.SwingPoint?.Bar == null)
                return;

            var swingPoint = evt.SwingPoint;
            var candle = swingPoint.Bar;
            var timeFrame = evt.TimeFrame;

            if (timeFrame == null)
                return;

            bool isBullishCandle = candle.Close > candle.Open;
            bool isBearishCandle = candle.Close < candle.Open;

            if (isBullishCandle)
            {
                ProcessBullishCandle(candle, timeFrame);
            }
            else if (isBearishCandle)
            {
                ProcessBearishCandle(candle, timeFrame);
            }

            // When a bearish swing point is detected, check for middle line break/sweep on bearish orderflows
            if (swingPoint.Direction == Direction.Down)
            {
                ProcessBearishSwingPoint(swingPoint, candle, timeFrame);
            }
            // When a bullish swing point is detected, check for middle line break/sweep on bullish orderflows
            else if (swingPoint.Direction == Direction.Up)
            {
                ProcessBullishSwingPoint(swingPoint, candle, timeFrame);
            }
        }

        /// <summary>
        /// Process bullish candle:
        /// - Confirm bearish orderflows (candle opens below and closes above the High)
        /// - Deactivate bearish orderflows (candle high above Low and candle low below Low)
        /// </summary>
        private void ProcessBullishCandle(Candle candle, TimeFrame timeFrame)
        {
            // Bearish orderflow confirmation
            // Get all bearish regular orderflows matching the candle's timeframe
            // where IsBrokenThrough is false, Activated is true, and IsConfirmed is false
            var bearishOrderflowsToConfirm = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Down &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                !of.IsConfirmed &&
                candle.Low <= of.High &&
                candle.Close >= of.High);

            foreach (var orderflow in bearishOrderflowsToConfirm)
            {
                orderflow.IsConfirmed = true;
                _logger($"Bearish orderflow confirmed at index {orderflow.Index}");

                // Draw the confirmed orderflow if visualization is enabled
                if (_settings.Patterns.ShowOrderFlow && _visualizer != null)
                {
                    _visualizer.Draw(orderflow);
                }
            }

            // Bearish orderflow deactivation
            // Get all bearish regular orderflows matching the candle's timeframe
            // where IsBrokenThrough is false, Activated is true, and IsConfirmed is false
            var bearishOrderflowsToDeactivate = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Down &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                !of.IsConfirmed &&
                candle.High > of.Low &&
                candle.Low < of.Low);

            foreach (var orderflow in bearishOrderflowsToDeactivate)
            {
                orderflow.Activated = false;
                _logger($"Bearish orderflow deactivated at index {orderflow.Index}");

                // Remove visualization for deactivated orderflow if RemoveSweptOrderflow is enabled
                if (_settings.Patterns.RemoveSweptOrderflow && _visualizer != null)
                {
                    _visualizer.Remove(orderflow);
                }
            }

            // Bullish orderflow deactivation
            // Get all bullish regular orderflows matching the candle's timeframe
            // where IsBrokenThrough is false, Activated is true, and IsConfirmed is false
            var bullishOrderflowsToDeactivate = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Up &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                !of.IsConfirmed &&
                candle.Low < of.High &&
                candle.High > of.High);

            foreach (var orderflow in bullishOrderflowsToDeactivate)
            {
                orderflow.Activated = false;
                _logger($"Bullish orderflow deactivated at index {orderflow.Index}");

                // Remove visualization for deactivated orderflow if RemoveSweptOrderflow is enabled
                if (_settings.Patterns.RemoveSweptOrderflow && _visualizer != null)
                {
                    _visualizer.Remove(orderflow);
                }
            }
        }

        /// <summary>
        /// Process bearish candle:
        /// - Confirm bullish orderflows (candle opens above and closes below the Low)
        /// - Deactivate bullish orderflows (candle low below High and candle high above High)
        /// - Deactivate bearish orderflows (candle high above Low and candle low below Low)
        /// </summary>
        private void ProcessBearishCandle(Candle candle, TimeFrame timeFrame)
        {
            // Bullish orderflow confirmation
            // Get all bullish regular orderflows matching the candle's timeframe
            // where IsBrokenThrough is false, Activated is true, and IsConfirmed is false
            var bullishOrderflowsToConfirm = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Up &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                !of.IsConfirmed &&
                candle.High >= of.Low &&
                candle.Close <= of.Low);

            foreach (var orderflow in bullishOrderflowsToConfirm)
            {
                orderflow.IsConfirmed = true;
                _logger($"Bullish orderflow confirmed at index {orderflow.Index}");

                // Draw the confirmed orderflow if visualization is enabled
                if (_settings.Patterns.ShowOrderFlow && _visualizer != null)
                {
                    _visualizer.Draw(orderflow);
                }
            }

            // Bullish orderflow deactivation
            // Get all bullish regular orderflows matching the candle's timeframe
            // where IsBrokenThrough is false, Activated is true, and IsConfirmed is false
            var bullishOrderflowsToDeactivate = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Up &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                !of.IsConfirmed &&
                candle.Low < of.High &&
                candle.High > of.High);

            foreach (var orderflow in bullishOrderflowsToDeactivate)
            {
                orderflow.Activated = false;
                _logger($"Bullish orderflow deactivated at index {orderflow.Index}");

                // Remove visualization for deactivated orderflow if RemoveSweptOrderflow is enabled
                if (_settings.Patterns.RemoveSweptOrderflow && _visualizer != null)
                {
                    _visualizer.Remove(orderflow);
                }
            }

            // Bearish orderflow deactivation
            // Get all bearish regular orderflows matching the candle's timeframe
            // where IsBrokenThrough is false, Activated is true, and IsConfirmed is false
            var bearishOrderflowsToDeactivate = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Down &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                !of.IsConfirmed &&
                candle.High > of.Low &&
                candle.Low < of.Low);

            foreach (var orderflow in bearishOrderflowsToDeactivate)
            {
                orderflow.Activated = false;
                _logger($"Bearish orderflow deactivated at index {orderflow.Index}");

                // Remove visualization for deactivated orderflow if RemoveSweptOrderflow is enabled
                if (_settings.Patterns.RemoveSweptOrderflow && _visualizer != null)
                {
                    _visualizer.Remove(orderflow);
                }
            }
        }

        /// <summary>
        /// Process bearish swing point:
        /// - Check for BreakOrderflowMiddleLine: candle high above Mid and close below Mid
        /// - Check for SweepOrderflowMiddleLine: candle close above Mid and low below Mid
        /// </summary>
        private void ProcessBearishSwingPoint(SwingPoint swingPoint, Candle candle, TimeFrame timeFrame)
        {
            // Get all bearish orderflows where IsBrokenThrough=false, Activated=true, IsConfirmed=true
            var confirmedBearishOrderflows = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Down &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                of.IsConfirmed);

            foreach (var orderflow in confirmedBearishOrderflows)
            {
                double mid = orderflow.Mid;

                // i. BreakOrderflowMiddleLine
                // If candle's high is above Mid and candle's close is below Mid, mark IsBrokenThrough as true
                if (candle.High > mid && candle.Close < mid)
                {
                    orderflow.IsBrokenThrough = true;
                    _logger($"Bearish orderflow middle line broken at index {orderflow.Index}");
                    continue; // Skip sweep check if already broken through
                }

                // ii. SweepOrderflowMiddleLine
                // If candle's close is above Mid and candle's low is below Mid, set Swept=true and swingPoint.SweptOrderflow=true
                if (!orderflow.Swept && candle.Close > mid && candle.Low < mid)
                {
                    orderflow.Swept = true;
                    swingPoint.SweptOrderflow = true;
                    _logger($"Bearish orderflow middle line swept at index {orderflow.Index}");

                    // If ShowSweptOrderflow is true, extend the midline to the sweeping candle
                    // When ShowMacros is true, only show swept orderflow when swing point is inside Macro time
                    bool shouldShowSwept = _settings.Patterns.ShowSweptOrderflow && _chart != null && candle.Index.HasValue;
                    if (shouldShowSwept)
                    {
                        bool macroConditionMet = !_settings.Time.ShowMacros || swingPoint.InsideMacro;
                        if (macroConditionMet)
                        {
                            ExtendOrderflowMidline(orderflow, candle.Index.Value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Process bullish swing point:
        /// - Check for BreakOrderflowMiddleLine: candle low below Mid and close above Mid
        /// - Check for SweepOrderflowMiddleLine: candle close below Mid and high above Mid
        /// </summary>
        private void ProcessBullishSwingPoint(SwingPoint swingPoint, Candle candle, TimeFrame timeFrame)
        {
            // Get all bullish orderflows where IsBrokenThrough=false, Activated=true, IsConfirmed=true
            var confirmedBullishOrderflows = _orderFlowRepository.Find(of =>
                of.LevelType == LevelType.Orderflow &&
                of.Direction == Direction.Up &&
                of.TimeFrame != null &&
                of.TimeFrame.Equals(timeFrame) &&
                !of.IsBrokenThrough &&
                of.Activated &&
                of.IsConfirmed);

            foreach (var orderflow in confirmedBullishOrderflows)
            {
                double mid = orderflow.Mid;

                // i. BreakOrderflowMiddleLine
                // If candle's low is below Mid and candle's close is above Mid, mark IsBrokenThrough as true
                if (candle.Low < mid && candle.Close > mid)
                {
                    orderflow.IsBrokenThrough = true;
                    _logger($"Bullish orderflow middle line broken at index {orderflow.Index}");
                    continue; // Skip sweep check if already broken through
                }

                // ii. SweepOrderflowMiddleLine
                // If candle's close is below Mid and candle's high is above Mid, set Swept=true and swingPoint.SweptOrderflow=true
                if (!orderflow.Swept && candle.Close < mid && candle.High > mid)
                {
                    orderflow.Swept = true;
                    swingPoint.SweptOrderflow = true;
                    _logger($"Bullish orderflow middle line swept at index {orderflow.Index}");

                    // If ShowSweptOrderflow is true, extend the midline to the sweeping candle
                    // When ShowMacros is true, only show swept orderflow when swing point is inside Macro time
                    bool shouldShowSwept = _settings.Patterns.ShowSweptOrderflow && _chart != null && candle.Index.HasValue;
                    if (shouldShowSwept)
                    {
                        bool macroConditionMet = !_settings.Time.ShowMacros || swingPoint.InsideMacro;
                        if (macroConditionMet)
                        {
                            ExtendOrderflowMidline(orderflow, candle.Index.Value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extends the orderflow midline to the sweeping candle
        /// </summary>
        private void ExtendOrderflowMidline(Level orderflow, int sweepingCandleIndex)
        {
            if (_chart == null)
                return;

            // Get the end index of the original orderflow rectangle
            int originalEndIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);

            // Draw extended midline from original end to sweeping candle
            string extendedMidlineId = $"of-{orderflow.Direction}-{orderflow.Index}-{orderflow.IndexHigh}-{orderflow.IndexLow}-swept-midline";

            var color = orderflow.Direction == Direction.Up ? Color.Green : Color.Pink;

            _chart.DrawTrendLine(
                extendedMidlineId,
                originalEndIndex,
                orderflow.Mid,
                sweepingCandleIndex,
                orderflow.Mid,
                color,
                1,
                LineStyle.Dots);

            _logger($"Extended orderflow midline to index {sweepingCandleIndex}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnsubscribeFromEvents();
                _disposed = true;
            }
        }
    }
}
