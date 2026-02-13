using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Services.Time;

public class RTOGManager
{
    private readonly CandleManager _candleManager;
    private readonly Chart _chart;
    private readonly bool _showRTOG;
    private readonly int _utcOffset;
    private readonly List<TimeGapLevel> _rtogs = new();

    private bool _set930 = false;
    private double? _previousDayClosePrice;
    private DateTime? _previousDayCloseTime;
    private int? _previousDayCloseIndex;

    private bool _set1614 = false;

    private const int LifespanDays = 20;

    public RTOGManager(
        CandleManager candleManager,
        Chart chart,
        bool showRTOG,
        int utcOffset)
    {
        _candleManager = candleManager;
        _chart = chart;
        _showRTOG = showRTOG;
        _utcOffset = utcOffset;
    }

    public void ProcessBar(int currentIndex, DateTime marketTime, bool isNewDay)
    {
        if (!_showRTOG || currentIndex >= _candleManager.Count) return;

        var currentCandle = _candleManager.GetCandle(currentIndex);
        if (currentCandle == null) return;

        if (isNewDay)
        {
            _set930 = false;
            _set1614 = false;
        }

        // Track the 16:14 candle close (RTH close)
        if (marketTime.Hour == 16 && marketTime.Minute >= 14 && marketTime.Minute < 15 && !_set1614)
        {
            _previousDayClosePrice = currentCandle.Close;
            _previousDayCloseTime = currentCandle.Time;
            _previousDayCloseIndex = currentCandle.Index;
            _set1614 = true;
        }

        // At 9:30, create the RTOG gap
        if (marketTime.Hour == 9 && marketTime.Minute >= 30 && marketTime.Minute < 31 && !_set930)
        {
            _set930 = true;

            if (_previousDayClosePrice.HasValue)
            {
                CreateRTOG(currentCandle, _previousDayClosePrice.Value, _previousDayCloseTime.Value, _previousDayCloseIndex.Value);
            }
        }

        ProcessLifespan(marketTime);
    }

    private void CreateRTOG(Candle openCandle, double previousClose, DateTime previousCloseTime, int previousCloseIndex)
    {
        if (openCandle?.Index == null) return;

        double openPrice = openCandle.Open;

        if (Math.Abs(openPrice - previousClose) < double.Epsilon) return;

        double high = Math.Max(openPrice, previousClose);
        double low = Math.Min(openPrice, previousClose);

        Direction direction = openPrice > previousClose ? Direction.Up : Direction.Down;

        DateTime highTime, lowTime;
        int indexHigh, indexLow;

        if (openPrice > previousClose)
        {
            highTime = openCandle.Time;
            lowTime = previousCloseTime;
            indexHigh = openCandle.Index.Value;
            indexLow = previousCloseIndex;
        }
        else
        {
            highTime = previousCloseTime;
            lowTime = openCandle.Time;
            indexHigh = previousCloseIndex;
            indexLow = openCandle.Index.Value;
        }

        var level = new Level(
            LevelType.RTOG,
            low,
            high,
            lowTime,
            highTime,
            direction: direction,
            index: openCandle.Index.Value,
            indexHigh: indexHigh,
            indexLow: indexLow
        );

        level.InitializeQuadrants();

        var rtog = new TimeGapLevel
        {
            Level = level,
            CreatedDate = openCandle.Time,
            IsActive = true
        };

        _rtogs.Add(rtog);
        DrawRTOG(rtog);
    }

    private void DrawRTOG(TimeGapLevel rtog)
    {
        if (_chart == null || rtog?.Level == null) return;

        var level = rtog.Level;
        rtog.ChartId = $"rtog-{level.HighTime.Ticks}-{level.LowTime.Ticks}";

        Color color = level.Direction == Direction.Up ? Color.Aquamarine : Color.Teal;
        DateTime startTime = rtog.CreatedDate;

        TimeGapDrawingHelper.DrawTimeGap(_chart, rtog, "RTOG", color, startTime, LifespanDays);
    }

    private void ProcessLifespan(DateTime currentTime)
    {
        var expired = _rtogs.Where(r => r.IsActive && (currentTime - r.CreatedDate).TotalDays >= LifespanDays).ToList();

        foreach (var rtog in expired)
        {
            rtog.IsActive = false;
            TimeGapDrawingHelper.RemoveTimeGap(_chart, rtog);
        }

        _rtogs.RemoveAll(r => !r.IsActive);
    }
}
