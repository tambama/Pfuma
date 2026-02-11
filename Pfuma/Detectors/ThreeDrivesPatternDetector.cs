using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors;

public class ThreeDrivesPatternDetector : IDisposable
{
    private readonly IEventAggregator _eventAggregator;
    private readonly NextArrayManager _nextArrayManager;
    private readonly IndicatorSettings _settings;
    private readonly IndicatorDataSeries _buySeries;
    private readonly IndicatorDataSeries _sellSeries;
    private readonly List<Candle> _recentCandles = new List<Candle>();
    private const int MaxCandles = 4;
    private Candle _pendingCandle;
    private bool _disposed;

    public ThreeDrivesPatternDetector(IEventAggregator eventAggregator, NextArrayManager nextArrayManager, IndicatorSettings settings, IndicatorDataSeries buySeries, IndicatorDataSeries sellSeries)
    {
        _eventAggregator = eventAggregator;
        _nextArrayManager = nextArrayManager;
        _settings = settings;
        _buySeries = buySeries;
        _sellSeries = sellSeries;

        _eventAggregator.Subscribe<CandleCreatedEvent>(OnCandleCreated);
    }

    private void OnCandleCreated(CandleCreatedEvent evt)
    {
        if (!_settings.Patterns.Show3DrivesPattern)
            return;

        var candle = evt.Candle;
        if (candle == null)
            return;

        // When a new candle arrives, the previous pending candle is now confirmed closed
        if (_pendingCandle != null)
        {
            ProcessClosedCandle(_pendingCandle);
        }

        // Current candle becomes pending (not yet closed)
        _pendingCandle = candle;
    }

    private void ProcessClosedCandle(Candle candle)
    {
        // Check if candle is inside bearish next PD array
        var bearishArray = _nextArrayManager?.NextBearishArray;
        if (bearishArray != null &&
            candle.High >= bearishArray.Low &&
            candle.Close <= bearishArray.High)
        {
            candle.InsideBearishPda = true;
        }

        // Check if candle is inside bullish next PD array
        var bullishArray = _nextArrayManager?.NextBullishArray;
        if (bullishArray != null &&
            candle.Low <= bullishArray.High &&
            candle.Close >= bullishArray.Low)
        {
            candle.InsideBullishPda = true;
        }

        // Add candle to the collection
        _recentCandles.Add(candle);
        while (_recentCandles.Count > MaxCandles)
            _recentCandles.RemoveAt(0);

        if (_recentCandles.Count < MaxCandles)
            return;

        // Candle zero = current, candle one = previous, etc.
        var candleZero = _recentCandles[3];
        var candleOne = _recentCandles[2];
        var candleTwo = _recentCandles[1];
        var candleThree = _recentCandles[0];

        if (candleZero.Direction == Direction.Up)
        {
            // Bearish 3 drives: candle one bearish, candle two bullish, candle three bullish
            if (candleOne.Direction == Direction.Down &&
                candleTwo.Direction == Direction.Up &&
                candleThree.Direction == Direction.Up)
            {
                if (candleZero.InsideBearishPda || candleOne.InsideBearishPda ||
                    candleTwo.InsideBearishPda || candleThree.InsideBearishPda)
                {
                    candleZero.Is3Drives = true;
                    _sellSeries[candleZero.Index.Value] = candleZero.High;
                }
            }
        }
        else if (candleZero.Direction == Direction.Down)
        {
            // Bullish 3 drives: candle one bullish, candle two bearish, candle three bearish
            if (candleOne.Direction == Direction.Up &&
                candleTwo.Direction == Direction.Down &&
                candleThree.Direction == Direction.Down)
            {
                if (candleZero.InsideBullishPda || candleOne.InsideBullishPda ||
                    candleTwo.InsideBullishPda || candleThree.InsideBullishPda)
                {
                    candleZero.Is3Drives = true;
                    _buySeries[candleZero.Index.Value] = candleZero.Low;
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _eventAggregator.Unsubscribe<CandleCreatedEvent>(OnCandleCreated);
            _disposed = true;
        }
    }
}
