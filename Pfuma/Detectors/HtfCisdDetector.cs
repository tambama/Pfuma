using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors.Base;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects Higher Timeframe CISD (Change in State of Delivery) patterns.
    /// CISD is detected from HTF order flows that sweep liquidity.
    /// </summary>
    public class HtfCisdDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly Dictionary<TimeFrame, List<Level>> _htfCisds;
        private readonly int _maxCisdsPerDirection;

        public HtfCisdDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _htfCisds = new Dictionary<TimeFrame, List<Level>>();
            _maxCisdsPerDirection = settings.Patterns.MaxCisdsPerDirection;

            // Initialize dictionaries for each higher timeframe
            foreach (var htf in candleManager.GetHigherTimeframes())
            {
                _htfCisds[htf] = new List<Level>();
            }
        }

        protected override List<Level> PerformDetection(int currentIndex)
        {
            // HTF CISD detection is triggered by HTF order flow events
            return new List<Level>();
        }

        /// <summary>
        /// Detects CISD from an HTF orderflow that swept liquidity
        /// </summary>
        public void DetectCisdFromHtfOrderFlow(Level orderflow, TimeFrame timeframe)
        {
            // Only detect CISD if the orderflow swept liquidity (same as regular CISD)
            if (orderflow == null || timeframe == null || orderflow.SweptSwingPoint == null)
                return;

            // Get HTF candles for this timeframe
            var htfCandles = CandleManager.GetHigherTimeframeCandles(timeframe);
            if (htfCandles == null || htfCandles.Count < 2)
                return;

            // Find HTF candles within the orderflow range
            var orderflowCandles = htfCandles
                .Where(c => c.Index.HasValue &&
                           c.Index.Value >= Math.Min(orderflow.IndexLow, orderflow.IndexHigh) &&
                           c.Index.Value <= Math.Max(orderflow.IndexLow, orderflow.IndexHigh))
                .OrderBy(c => c.Index)
                .ToList();

            if (orderflowCandles.Count == 0)
            {
                // If no exact match, find candles that overlap with the orderflow time range
                orderflowCandles = htfCandles
                    .Where(c => c.Time >= orderflow.LowTime && c.Time <= orderflow.HighTime)
                    .OrderBy(c => c.Time)
                    .ToList();
            }

            if (orderflowCandles.Count == 0)
                return;

            Level cisdLevel = null;

            if (orderflow.Direction == Direction.Up)
            {
                // Bullish orderflow → detect Bearish CISD
                cisdLevel = DetectBearishHtfCisd(orderflow, orderflowCandles, timeframe);
            }
            else
            {
                // Bearish orderflow → detect Bullish CISD
                cisdLevel = DetectBullishHtfCisd(orderflow, orderflowCandles, timeframe);
            }

            if (cisdLevel != null)
            {
                // Associate with orderflow
                cisdLevel.OrderFlowId = orderflow.Id;
                cisdLevel.TimeFrame = timeframe;

                // Initialize if needed
                if (!_htfCisds.ContainsKey(timeframe))
                    _htfCisds[timeframe] = new List<Level>();

                // Manage max CISDs before adding
                ManageMaxHtfCisdCount(timeframe, cisdLevel.Direction);

                // Store
                _htfCisds[timeframe].Add(cisdLevel);
                Repository.Add(cisdLevel);

                // Publish and visualize
                PublishDetectionEvent(cisdLevel, cisdLevel.Index);
                LogDetection(cisdLevel, cisdLevel.Index);
            }
        }

        /// <summary>
        /// Detect Bearish HTF CISD from bullish orderflow
        /// Find consecutive bullish HTF candles within the orderflow
        /// </summary>
        private Level DetectBearishHtfCisd(Level orderflow, List<Candle> htfCandles, TimeFrame timeframe)
        {
            // Find all sets of consecutive bullish candles
            List<List<Candle>> bullishSets = new List<List<Candle>>();
            List<Candle> currentSet = new List<Candle>();

            for (int i = 0; i < htfCandles.Count; i++)
            {
                var candle = htfCandles[i];
                var direction = candle.Direction;

                if (direction == Direction.Up)
                {
                    currentSet.Add(candle);
                }
                else if (currentSet.Count > 0)
                {
                    // If this is the last candle and it's bearish, add it to the current set
                    if (i == htfCandles.Count - 1)
                    {
                        currentSet.Add(candle);
                    }
                    bullishSets.Add(new List<Candle>(currentSet));
                    currentSet.Clear();
                }
            }

            if (currentSet.Count > 0)
            {
                bullishSets.Add(new List<Candle>(currentSet));
            }

            if (bullishSets.Count == 0)
                return null;

            // Use the last set of consecutive bullish candles
            var lastBullishSet = bullishSets[bullishSets.Count - 1];

            if (lastBullishSet.Count == 0)
                return null;

            var firstBullishCandle = lastBullishSet.First();
            var lastBullishCandle = lastBullishSet.Last();

            int firstIndex = firstBullishCandle.Index ?? 0;
            int lastIndex = lastBullishCandle.Index ?? 0;

            // Create a BEARISH CISD level
            var cisdLevel = new Level(
                LevelType.CISD,
                firstBullishCandle.Open,           // low: first bullish candle's open
                lastBullishCandle.High,            // high: last bullish candle's high
                firstBullishCandle.Time,           // lowTime
                lastBullishCandle.Time,            // highTime
                null,                              // midTime
                Direction.Down,                    // direction (bearish CISD)
                firstIndex,                        // index
                lastIndex,                         // indexHigh
                firstIndex                         // indexLow
            );

            cisdLevel.TimeFrame = timeframe;
            cisdLevel.InitializeQuadrants();

            return cisdLevel;
        }

        /// <summary>
        /// Detect Bullish HTF CISD from bearish orderflow
        /// Find consecutive bearish HTF candles within the orderflow
        /// </summary>
        private Level DetectBullishHtfCisd(Level orderflow, List<Candle> htfCandles, TimeFrame timeframe)
        {
            // Find all sets of consecutive bearish candles
            List<List<Candle>> bearishSets = new List<List<Candle>>();
            List<Candle> currentSet = new List<Candle>();

            for (int i = 0; i < htfCandles.Count; i++)
            {
                var candle = htfCandles[i];
                var direction = candle.Direction;

                if (direction == Direction.Down)
                {
                    currentSet.Add(candle);
                }
                else if (currentSet.Count > 0)
                {
                    // If this is the last candle and it's bullish, add it to the current set
                    if (i == htfCandles.Count - 1)
                    {
                        currentSet.Add(candle);
                    }
                    bearishSets.Add(new List<Candle>(currentSet));
                    currentSet.Clear();
                }
            }

            if (currentSet.Count > 0)
            {
                bearishSets.Add(new List<Candle>(currentSet));
            }

            if (bearishSets.Count == 0)
                return null;

            // Use the last set of consecutive bearish candles
            var lastBearishSet = bearishSets[bearishSets.Count - 1];

            if (lastBearishSet.Count == 0)
                return null;

            var firstBearishCandle = lastBearishSet.First();
            var lastBearishCandle = lastBearishSet.Last();

            int firstIndex = firstBearishCandle.Index ?? 0;
            int lastIndex = lastBearishCandle.Index ?? 0;

            // Create a BULLISH CISD level
            var cisdLevel = new Level(
                LevelType.CISD,
                lastBearishCandle.Low,             // low: last bearish candle's low
                firstBearishCandle.Open,           // high: first bearish candle's open
                lastBearishCandle.Time,            // lowTime
                firstBearishCandle.Time,           // highTime
                null,                              // midTime
                Direction.Up,                      // direction (bullish CISD)
                firstIndex,                        // index
                firstIndex,                        // indexHigh
                lastIndex                          // indexLow
            );

            cisdLevel.TimeFrame = timeframe;
            cisdLevel.InitializeQuadrants();

            return cisdLevel;
        }

        /// <summary>
        /// Manage max CISD count per direction for a timeframe
        /// </summary>
        private void ManageMaxHtfCisdCount(TimeFrame timeframe, Direction direction)
        {
            if (!_htfCisds.ContainsKey(timeframe))
                return;

            var unconfirmedCisds = _htfCisds[timeframe]
                .Where(cisd => cisd.Direction == direction && !cisd.IsConfirmed)
                .OrderBy(cisd => cisd.Index)
                .ToList();

            while (unconfirmedCisds.Count >= _maxCisdsPerDirection && unconfirmedCisds.Count > 0)
            {
                var oldestCisd = unconfirmedCisds.First();
                _htfCisds[timeframe].Remove(oldestCisd);
                Repository.Remove(oldestCisd);
                _visualizer?.Remove(oldestCisd);
                unconfirmedCisds.Remove(oldestCisd);
            }
        }

        /// <summary>
        /// Check for HTF CISD confirmation when a new HTF candle is created
        /// </summary>
        public void CheckHtfCisdConfirmation(Candle htfCandle, TimeFrame timeframe)
        {
            if (htfCandle == null || !_htfCisds.ContainsKey(timeframe))
                return;

            var pendingCisds = _htfCisds[timeframe]
                .Where(cisd => !cisd.IsConfirmed)
                .ToList();

            foreach (var cisd in pendingCisds)
            {
                bool isConfirmed = false;

                if (cisd.Direction == Direction.Up) // Bullish CISD
                {
                    // Confirmed when a bullish candle closes above the CISD high
                    if (htfCandle.Direction == Direction.Up &&
                        htfCandle.Open < cisd.High &&
                        htfCandle.Close > cisd.High)
                    {
                        isConfirmed = true;
                    }
                }
                else // Bearish CISD
                {
                    // Confirmed when a bearish candle closes below the CISD low
                    if (htfCandle.Direction == Direction.Down &&
                        htfCandle.Open > cisd.Low &&
                        htfCandle.Close < cisd.Low)
                    {
                        isConfirmed = true;
                    }
                }

                if (isConfirmed)
                {
                    cisd.IsConfirmed = true;
                    cisd.IndexOfConfirmingCandle = htfCandle.Index ?? 0;

                    // Publish confirmation event
                    EventAggregator.Publish(new HtfCisdConfirmedEvent(cisd, timeframe, cisd.Direction));

                    // Draw the CISD only when confirmed
                    if (Settings.Patterns.ShowHtfCisd && _visualizer != null)
                    {
                        _visualizer.Draw(cisd);
                    }

                    Logger?.Invoke($"HTF CISD confirmed: {timeframe.GetShortName()} {cisd.Direction} at index {cisd.IndexOfConfirmingCandle}");
                }
            }
        }

        protected override void PublishDetectionEvent(Level cisd, int currentIndex)
        {
            EventAggregator.Publish(new HtfCisdDetectedEvent(cisd, cisd.TimeFrame));

            // Only draw when confirmed (drawing is handled in CheckHtfCisdConfirmation)
        }

        protected override void LogDetection(Level cisd, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"HTF CISD detected: {cisd.TimeFrame?.GetShortName()} {cisd.Direction} at index {currentIndex}, " +
                       $"Range: {cisd.Low:F5} - {cisd.High:F5}");
            }
        }

        public override List<Level> GetByDirection(Direction direction)
        {
            var result = new List<Level>();

            foreach (var htfList in _htfCisds.Values)
            {
                result.AddRange(htfList.Where(cisd => cisd.Direction == direction));
            }

            return result;
        }

        public List<Level> GetByTimeFrame(TimeFrame timeframe)
        {
            if (_htfCisds.ContainsKey(timeframe))
                return new List<Level>(_htfCisds[timeframe]);

            return new List<Level>();
        }

        public override bool IsValid(Level cisd, int currentIndex)
        {
            return cisd != null &&
                   cisd.LevelType == LevelType.CISD &&
                   cisd.TimeFrame != null &&
                   (!cisd.Activated || cisd.Index < currentIndex);
        }

        protected override void SubscribeToEvents()
        {
            // Subscribe to HTF order flow events
            EventAggregator.Subscribe<HtfOrderFlowDetectedEvent>(OnHtfOrderFlowDetected);
            // Subscribe to HTF candle events for confirmation checking
            EventAggregator.Subscribe<HtfCandleCreatedEvent>(OnHtfCandleCreated);
        }

        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<HtfOrderFlowDetectedEvent>(OnHtfOrderFlowDetected);
            EventAggregator.Unsubscribe<HtfCandleCreatedEvent>(OnHtfCandleCreated);
        }

        private void OnHtfOrderFlowDetected(HtfOrderFlowDetectedEvent evt)
        {
            if (evt.OrderFlow != null && evt.TimeFrame != null)
            {
                DetectCisdFromHtfOrderFlow(evt.OrderFlow, evt.TimeFrame);
            }
        }

        private void OnHtfCandleCreated(HtfCandleCreatedEvent evt)
        {
            if (evt.HtfCandle != null && evt.TimeFrame != null)
            {
                CheckHtfCisdConfirmation(evt.HtfCandle, evt.TimeFrame);
            }
        }

        /// <summary>
        /// Get all HTF CISDs across all timeframes
        /// </summary>
        public List<Level> GetAllHtfCisds()
        {
            var result = new List<Level>();

            foreach (var htfList in _htfCisds.Values)
            {
                result.AddRange(htfList);
            }

            return result;
        }

        /// <summary>
        /// Get count of HTF CISDs for a specific timeframe
        /// </summary>
        public int GetTimeFrameCisdCount(TimeFrame timeframe)
        {
            if (_htfCisds.ContainsKey(timeframe))
                return _htfCisds[timeframe].Count;

            return 0;
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            _htfCisds.Clear();
        }
    }
}
