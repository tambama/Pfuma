using System;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Manages the "Next Array" feature - displays the nearest unbroken bullish and bearish
    /// rejection blocks when a new swing point is detected
    /// </summary>
    public class NextArrayManager : IDisposable
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IRepository<Level> _levelRepository;
        private readonly Chart _chart;
        private readonly IndicatorSettings _settings;
        private readonly Action<string> _logger;
        private readonly int _extensionOpacity;
        private bool _disposed;

        // Tracking for current next arrays
        private Level _nextBullishArray;
        private Level _nextBearishArray;

        public Level NextBullishArray => _nextBullishArray;
        public Level NextBearishArray => _nextBearishArray;

        // Object IDs for chart elements
        private const string NextBullishArrayId = "next-array-bullish";
        private const string NextBearishArrayId = "next-array-bearish";

        public NextArrayManager(
            IEventAggregator eventAggregator,
            IRepository<Level> levelRepository,
            Chart chart,
            IndicatorSettings settings,
            int extensionOpacity,
            Action<string> logger = null)
        {
            _eventAggregator = eventAggregator;
            _levelRepository = levelRepository;
            _chart = chart;
            _settings = settings;
            _extensionOpacity = extensionOpacity;
            _logger = logger ?? (_ => { });

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }

        private void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }

        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            if (evt?.SwingPoint == null)
                return;

            UpdateNextArrays(evt.SwingPoint);
        }

        private void UpdateNextArrays(SwingPoint swingPoint)
        {
            // Get all unbroken rejection blocks
            var rejectionBlocks = _levelRepository.Find(l =>
                l.LevelType == LevelType.RejectionBlock &&
                l.IsActive &&
                !l.IsBrokenThrough);

            double currentPrice = swingPoint.Price;

            // Find nearest bullish rejection block (below current price)
            var nearestBullish = rejectionBlocks
                .Where(rb => rb.Direction == Direction.Up)
                .OrderByDescending(rb => rb.High)
                .FirstOrDefault();

            // Find nearest bearish rejection block (above current price)
            var nearestBearish = rejectionBlocks
                .Where(rb => rb.Direction == Direction.Down)
                .OrderBy(rb => rb.Low)
                .FirstOrDefault();

            // Update bullish next array
            if (nearestBullish != null && nearestBullish != _nextBullishArray)
            {
                // Remove old rectangle if exists
                RemoveNextArrayVisuals(NextBullishArrayId);

                _nextBullishArray = nearestBullish;
                DrawNextArrayRectangle(_nextBullishArray, NextBullishArrayId);
                _logger($"Next bullish array updated: {_nextBullishArray.High:F5}");
            }
            // Note: Don't remove the current next array just because no new candidate was found
            // The current one may still be valid (price inside it). It will be removed by CheckNextArraysBroken when broken.

            // Update bearish next array
            if (nearestBearish != null && nearestBearish != _nextBearishArray)
            {
                // Remove old rectangle if exists
                RemoveNextArrayVisuals(NextBearishArrayId);

                _nextBearishArray = nearestBearish;
                DrawNextArrayRectangle(_nextBearishArray, NextBearishArrayId);
                _logger($"Next bearish array updated: {_nextBearishArray.Low:F5}");
            }
            // Note: Don't remove the current next array just because no new candidate was found
            // The current one may still be valid (price inside it). It will be removed by CheckNextArraysBroken when broken.
        }

        private void DrawNextArrayRectangle(Level rejectionBlock, string objectId)
        {
            if (rejectionBlock == null || _chart == null)
                return;

            var baseColor = rejectionBlock.Direction == Direction.Up ? Color.Green : Color.Red;

            // Draw extended rectangle with far future end time
            var rectangle = _chart.DrawRectangle(
                objectId,
                rejectionBlock.LowTime,
                rejectionBlock.Low,
                rejectionBlock.HighTime.AddYears(10), // Extend far into the future
                rejectionBlock.High,
                baseColor);

            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(_extensionOpacity * 255 / 100, baseColor.R, baseColor.G, baseColor.B);

            // Draw midpoint line
            _chart.DrawTrendLine(
                $"{objectId}-mid",
                rejectionBlock.LowTime,
                rejectionBlock.Mid,
                rejectionBlock.HighTime.AddYears(10),
                rejectionBlock.Mid,
                Color.White,
                1,
                LineStyle.Dots);
        }

        private void RemoveNextArrayVisuals(string objectId)
        {
            _chart?.RemoveObject(objectId);
            _chart?.RemoveObject($"{objectId}-mid");
        }

        /// <summary>
        /// Checks if the next arrays have been broken and removes them if so.
        /// Should be called on each bar from the main Calculate method.
        /// </summary>
        public void CheckNextArraysBroken(Candle currentCandle)
        {
            if (currentCandle == null)
                return;

            // Check if bullish next array is broken (price closed below low)
            if (_nextBullishArray != null)
            {
                if (currentCandle.Close < _nextBullishArray.Low)
                {
                    _nextBullishArray.IsBrokenThrough = true;
                    RemoveNextArrayVisuals(NextBullishArrayId);
                    _logger($"Next bullish array broken at price {currentCandle.Close:F5}");
                    _nextBullishArray = null;
                }
            }

            // Check if bearish next array is broken (price closed above high)
            if (_nextBearishArray != null)
            {
                if (currentCandle.Close > _nextBearishArray.High)
                {
                    _nextBearishArray.IsBrokenThrough = true;
                    RemoveNextArrayVisuals(NextBearishArrayId);
                    _logger($"Next bearish array broken at price {currentCandle.Close:F5}");
                    _nextBearishArray = null;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnsubscribeFromEvents();
                RemoveNextArrayVisuals(NextBullishArrayId);
                RemoveNextArrayVisuals(NextBearishArrayId);
                _nextBullishArray = null;
                _nextBearishArray = null;
                _disposed = true;
            }
        }
    }
}
