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
    private readonly List<RTOGLevel> _rtogs = new();

    private DateTime _lastProcessedDate = DateTime.MinValue;
    private bool _set930 = false;
    private double? _previousDayClosePrice;
    private DateTime? _previousDayCloseTime;
    private int? _previousDayCloseIndex;

    // Track the 16:14 close each day so it's available for the next day's 9:30
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

    public void ProcessBar(int currentIndex, DateTime marketTime)
    {
        if (!_showRTOG || currentIndex >= _candleManager.Count) return;

        var currentCandle = _candleManager.GetCandle(currentIndex);
        if (currentCandle == null) return;

        // Reset trackers when we cross into a new trading day (at 18:00 market time)
        DateTime currentDay = marketTime.Hour >= 18 ? marketTime.Date : marketTime.Date.AddDays(-1);
        if (_lastProcessedDate != DateTime.MinValue && currentDay > _lastProcessedDate)
        {
            _set930 = false;
            _set1614 = false;
        }
        _lastProcessedDate = currentDay;

        // Track the 16:14 candle close (RTH close) - capture at 16:14 or the last candle before 16:15
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

        // Process lifespan - remove expired RTOGs
        ProcessLifespan(marketTime);
    }

    private void CreateRTOG(Candle openCandle, double previousClose, DateTime previousCloseTime, int previousCloseIndex)
    {
        if (openCandle?.Index == null) return;

        double openPrice = openCandle.Open;

        // No gap if prices are equal
        if (Math.Abs(openPrice - previousClose) < double.Epsilon) return;

        double high = Math.Max(openPrice, previousClose);
        double low = Math.Min(openPrice, previousClose);

        // Direction: Up if open > previous close (gap up), Down if open < previous close (gap down)
        Direction direction = openPrice > previousClose ? Direction.Up : Direction.Down;

        DateTime highTime, lowTime;
        int indexHigh, indexLow;

        if (openPrice > previousClose)
        {
            // Gap up: high is the open, low is the previous close
            highTime = openCandle.Time;
            lowTime = previousCloseTime;
            indexHigh = openCandle.Index.Value;
            indexLow = previousCloseIndex;
        }
        else
        {
            // Gap down: high is the previous close, low is the open
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

        // Initialize quadrants
        level.InitializeQuadrants();

        var rtog = new RTOGLevel
        {
            Level = level,
            CreatedDate = openCandle.Time,
            IsActive = true
        };

        _rtogs.Add(rtog);

        DrawRTOG(rtog);
    }

    private void DrawRTOG(RTOGLevel rtog)
    {
        if (_chart == null || rtog?.Level == null) return;

        var level = rtog.Level;
        string id = $"rtog-{level.HighTime.Ticks}-{level.LowTime.Ticks}";
        rtog.ChartId = id;

        Color color = level.Direction == Direction.Up ? Color.Aquamarine : Color.Teal;
        Color fillColor = Color.FromArgb(10, color);

        // Always start drawing from the 9:30 candle time (CreatedDate)
        DateTime startTime = rtog.CreatedDate;
        DateTime rectEndTime = startTime.AddDays(LifespanDays);

        // Draw extended rectangle
        string rectId = $"{id}-rect";
        var rect = _chart.DrawRectangle(
            rectId,
            startTime,
            level.High,
            rectEndTime,
            level.Low,
            fillColor,
            1
        );
        rect.IsFilled = true;

        // Draw dotted midline
        string midId = $"{id}-mid";
        _chart.DrawTrendLine(
            midId,
            startTime,
            level.Mid,
            rectEndTime,
            level.Mid,
            Color.FromArgb(100, color),
            1,
            LineStyle.Dots
        );

        // Draw quadrant lines
        if (level.Quadrants != null && level.Quadrants.Count > 0)
        {
            DrawQuadrants(rtog, color);
        }

        // Draw label
        string labelId = $"{id}-label";
        var text = _chart.DrawText(
            labelId,
            "RTOG",
            startTime,
            level.High,
            color
        );
        text.VerticalAlignment = VerticalAlignment.Top;
        text.HorizontalAlignment = HorizontalAlignment.Left;
        text.FontSize = 8;
    }

    private void DrawQuadrants(RTOGLevel rtog, Color color)
    {
        var level = rtog.Level;
        string id = rtog.ChartId;
        DateTime startTime = rtog.CreatedDate;
        DateTime endTime = startTime.AddDays(LifespanDays);

        foreach (var quadrant in level.Quadrants)
        {
            // Skip 0% and 100% (those are the rectangle edges) and 50% (that's the midline)
            if (quadrant.Percent == 0 || quadrant.Percent == 100 || quadrant.Percent == 50)
                continue;

            string quadId = $"{id}-quad-{quadrant.Percent}";
            _chart.DrawTrendLine(
                quadId,
                startTime,
                quadrant.Price,
                endTime,
                quadrant.Price,
                Color.FromArgb(90, color),
                1,
                LineStyle.DotsRare
            );
        }
    }

    private void ProcessLifespan(DateTime currentTime)
    {
        var expired = _rtogs.Where(r => r.IsActive && (currentTime - r.CreatedDate).TotalDays >= LifespanDays).ToList();

        foreach (var rtog in expired)
        {
            rtog.IsActive = false;
            RemoveRTOG(rtog);
        }

        _rtogs.RemoveAll(r => !r.IsActive);
    }

    private void RemoveRTOG(RTOGLevel rtog)
    {
        if (_chart == null || rtog?.ChartId == null) return;

        string id = rtog.ChartId;

        _chart.RemoveObject($"{id}-rect");
        _chart.RemoveObject($"{id}-mid");
        _chart.RemoveObject($"{id}-label");

        if (rtog.Level?.Quadrants != null)
        {
            foreach (var quadrant in rtog.Level.Quadrants)
            {
                if (quadrant.Percent == 0 || quadrant.Percent == 100 || quadrant.Percent == 50)
                    continue;
                _chart.RemoveObject($"{id}-quad-{quadrant.Percent}");
            }
        }
    }

    private class RTOGLevel
    {
        public Level Level { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
        public string ChartId { get; set; }
    }
}
