using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Extensions;
using Pfuma.Models;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Visualizes Higher Timeframe candles with body, wicks, and high/low markers.
    /// Subscribes to HtfCandleCreatedEvent and draws candles when HTF candles are created.
    /// </summary>
    public class HtfCandleVisualizer : IInitializable
    {
        private readonly Chart _chart;
        private readonly IEventAggregator _eventAggregator;
        private readonly IndicatorSettings _settings;

        // Colors for candle visualization
        private readonly Color _bullishColor = Color.FromArgb(180, 0, 200, 0);
        private readonly Color _bearishColor = Color.FromArgb(180, 200, 0, 0);
        private readonly Color _bullishWickColor = Color.FromArgb(255, 0, 150, 0);
        private readonly Color _bearishWickColor = Color.FromArgb(255, 150, 0, 0);
        private readonly Color _labelColor = Color.White;

        // Counter for HTF candles per timeframe
        private readonly Dictionary<string, int> _htfCandleCount = new Dictionary<string, int>();

        public HtfCandleVisualizer(
            Chart chart,
            IEventAggregator eventAggregator,
            IndicatorSettings settings)
        {
            _chart = chart;
            _eventAggregator = eventAggregator;
            _settings = settings;
        }

        public void Initialize()
        {
            // Subscribe to HTF candle created events
            _eventAggregator.Subscribe<HtfCandleCreatedEvent>(OnHtfCandleCreated);
        }

        public void Dispose()
        {
            // Unsubscribe from events
            _eventAggregator.Unsubscribe<HtfCandleCreatedEvent>(OnHtfCandleCreated);
        }

        /// <summary>
        /// Event handler for when a new HTF candle is created
        /// </summary>
        private void OnHtfCandleCreated(HtfCandleCreatedEvent evt)
        {
            if (!_settings.Patterns.ShowHighTimeframeCandle)
                return;

            DrawHtfCandle(evt.HtfCandle);
        }

        /// <summary>
        /// Draw a complete HTF candle visualization (body, wicks, high/low dots, and count label)
        /// </summary>
        public void DrawHtfCandle(Candle htfCandle)
        {
            if (_chart == null || htfCandle == null)
                return;

            var tfShortName = htfCandle.TimeFrame?.GetShortName() ?? "HTF";
            var baseId = $"htf_candle_{tfShortName}_{htfCandle.Time:yyyyMMddHHmm}";

            // Increment candle count for this timeframe
            if (!_htfCandleCount.ContainsKey(tfShortName))
                _htfCandleCount[tfShortName] = 0;
            _htfCandleCount[tfShortName]++;
            int candleNumber = _htfCandleCount[tfShortName];

            // Get candle indices
            int startIndex = htfCandle.Index ?? 0;
            int endIndex = htfCandle.IndexEnd ?? startIndex;

            // Determine if bullish or bearish
            bool isBullish = htfCandle.Close >= htfCandle.Open;
            var bodyColor = isBullish ? _bullishColor : _bearishColor;
            var wickColor = isBullish ? _bullishWickColor : _bearishWickColor;

            // Calculate body boundaries
            double bodyTop = Math.Max(htfCandle.Open, htfCandle.Close);
            double bodyBottom = Math.Min(htfCandle.Open, htfCandle.Close);

            // Draw candle body (rectangle from Open to Close)
            DrawCandleBody(baseId, startIndex, endIndex, bodyTop, bodyBottom, bodyColor);

            // Draw upper wick (line from body top to High)
            DrawUpperWick(baseId, htfCandle, bodyTop, wickColor);

            // Draw lower wick (line from body bottom to Low)
            DrawLowerWick(baseId, htfCandle, bodyBottom, wickColor);

            // Draw high/low markers (dots)
            DrawHighLowMarkers(htfCandle, tfShortName);

            // Draw candle number label inside the candle
            DrawCandleLabel(baseId, htfCandle, candleNumber);
        }

        /// <summary>
        /// Draw the candle body as a rectangle
        /// </summary>
        private void DrawCandleBody(string baseId, int startIndex, int endIndex, double bodyTop, double bodyBottom, Color color)
        {
            var bodyId = $"{baseId}_body";

            var body = _chart.DrawRectangle(bodyId, startIndex, bodyTop, endIndex, bodyBottom, color);
            body.IsFilled = true;
            body.Color = color;
        }

        /// <summary>
        /// Draw the upper wick from body top to high
        /// </summary>
        private void DrawUpperWick(string baseId, Candle htfCandle, double bodyTop, Color color)
        {
            // Only draw if there's a wick (high > body top)
            if (htfCandle.High <= bodyTop)
                return;

            var wickId = $"{baseId}_upper_wick";

            // Draw wick at the index where the high occurred
            int wickIndex = htfCandle.IndexOfHigh ?? htfCandle.Index ?? 0;

            var wick = _chart.DrawTrendLine(wickId, wickIndex, bodyTop, wickIndex, htfCandle.High, color, 2, LineStyle.Solid);
            wick.IsInteractive = false;
        }

        /// <summary>
        /// Draw the lower wick from body bottom to low
        /// </summary>
        private void DrawLowerWick(string baseId, Candle htfCandle, double bodyBottom, Color color)
        {
            // Only draw if there's a wick (low < body bottom)
            if (htfCandle.Low >= bodyBottom)
                return;

            var wickId = $"{baseId}_lower_wick";

            // Draw wick at the index where the low occurred
            int wickIndex = htfCandle.IndexOfLow ?? htfCandle.Index ?? 0;

            var wick = _chart.DrawTrendLine(wickId, wickIndex, bodyBottom, wickIndex, htfCandle.Low, color, 2, LineStyle.Solid);
            wick.IsInteractive = false;
        }

        /// <summary>
        /// Draw dots at the high and low points
        /// </summary>
        private void DrawHighLowMarkers(Candle htfCandle, string tfShortName)
        {
            // Draw red dot for low at the exact bar index where the low occurred
            if (htfCandle.IndexOfLow.HasValue)
            {
                var lowIconName = $"htf_low_{tfShortName}_{htfCandle.IndexOfLow}_{htfCandle.Time:yyyyMMddHHmm}";
                _chart.DrawIcon(lowIconName, ChartIconType.Circle, htfCandle.IndexOfLow.Value, htfCandle.Low, Color.Red);
            }

            // Draw green dot for high at the exact bar index where the high occurred
            if (htfCandle.IndexOfHigh.HasValue)
            {
                var highIconName = $"htf_high_{tfShortName}_{htfCandle.IndexOfHigh}_{htfCandle.Time:yyyyMMddHHmm}";
                _chart.DrawIcon(highIconName, ChartIconType.Circle, htfCandle.IndexOfHigh.Value, htfCandle.High, Color.Green);
            }
        }

        /// <summary>
        /// Draw a label inside the candle showing the candle number
        /// </summary>
        private void DrawCandleLabel(string baseId, Candle htfCandle, int candleNumber)
        {
            var labelId = $"{baseId}_label";

            // Calculate the center of the candle
            int startIndex = htfCandle.Index ?? 0;
            int endIndex = htfCandle.IndexEnd ?? startIndex;
            int midIndex = (startIndex + endIndex) / 2;

            // Position label at the middle of the candle body
            double midPrice = (htfCandle.Open + htfCandle.Close) / 2;

            // Draw the candle number label
            var label = _chart.DrawText(labelId, candleNumber.ToString(), midIndex, midPrice, _labelColor);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.FontSize = 10;
        }

        /// <summary>
        /// Remove HTF candle visualization from chart
        /// </summary>
        public void RemoveHtfCandle(Candle htfCandle)
        {
            if (_chart == null || htfCandle == null)
                return;

            var tfShortName = htfCandle.TimeFrame?.GetShortName() ?? "HTF";
            var baseId = $"htf_candle_{tfShortName}_{htfCandle.Time:yyyyMMddHHmm}";

            // Remove body
            _chart.RemoveObject($"{baseId}_body");

            // Remove wicks
            _chart.RemoveObject($"{baseId}_upper_wick");
            _chart.RemoveObject($"{baseId}_lower_wick");

            // Remove label
            _chart.RemoveObject($"{baseId}_label");

            // Remove high/low dots
            if (htfCandle.IndexOfLow.HasValue)
            {
                var lowIconName = $"htf_low_{tfShortName}_{htfCandle.IndexOfLow}_{htfCandle.Time:yyyyMMddHHmm}";
                _chart.RemoveObject(lowIconName);
            }

            if (htfCandle.IndexOfHigh.HasValue)
            {
                var highIconName = $"htf_high_{tfShortName}_{htfCandle.IndexOfHigh}_{htfCandle.Time:yyyyMMddHHmm}";
                _chart.RemoveObject(highIconName);
            }
        }
    }
}
