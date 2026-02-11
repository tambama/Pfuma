using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;

namespace Pfuma.Services.Time;

public class FpfvgManager
{
    private readonly CandleManager _candleManager;
    private readonly Chart _chart;
    private readonly IEventAggregator _eventAggregator;
    private readonly bool _showFpfvg;
    private readonly int _utcOffset;
    private readonly List<FpfvgLevel> _fpfvgs = new();

    private DateTime _lastProcessedDate = DateTime.MinValue;
    private bool _past930;
    private bool _capturedToday;
    private double _currentPrice;
    private int _index930 = -1;

    private const int LifespanDays = 20;
    private const int MaxCollection = 10;
    private const int MaxDrawn = 3;

    public FpfvgManager(
        CandleManager candleManager,
        Chart chart,
        IEventAggregator eventAggregator,
        bool showFpfvg,
        int utcOffset)
    {
        _candleManager = candleManager;
        _chart = chart;
        _eventAggregator = eventAggregator;
        _showFpfvg = showFpfvg;
        _utcOffset = utcOffset;

        if (_showFpfvg)
        {
            _eventAggregator.Subscribe<FvgDetectedEvent>(OnFvgDetected);
        }
    }

    public void ProcessBar(int currentIndex, DateTime marketTime)
    {
        if (!_showFpfvg || currentIndex >= _candleManager.Count) return;

        var currentCandle = _candleManager.GetCandle(currentIndex);
        if (currentCandle == null) return;

        _currentPrice = currentCandle.Close;

        // Reset trackers at 9:30 each day
        DateTime currentDay = marketTime.Hour >= 18 ? marketTime.Date : marketTime.Date.AddDays(-1);
        if (_lastProcessedDate != DateTime.MinValue && currentDay > _lastProcessedDate)
        {
            // New trading day started at 18:00 — clear the 9:30 gate so we wait for the next 9:30
            _past930 = false;
            _index930 = -1;
        }
        _lastProcessedDate = currentDay;

        // At 9:30, reset capturedToday and start accepting FVGs (only between 9:30 and 18:00)
        if (!_past930 && marketTime.Hour < 18 &&
            ((marketTime.Hour == 9 && marketTime.Minute >= 30) || (marketTime.Hour > 9)))
        {
            _past930 = true;
            _capturedToday = false;
            _index930 = currentIndex;
        }

        // Process lifespan - remove expired FPFVGs
        ProcessLifespan(marketTime);

        // Redraw to keep the closest 3 to current price
        UpdateDrawnFpfvgs();
    }

    private void OnFvgDetected(FvgDetectedEvent evt)
    {
        if (!_showFpfvg || !_past930 || _capturedToday) return;
        if (evt?.FvgLevel == null) return;

        var fvg = evt.FvgLevel;

        // The FVG's first candle must be at or after the 9:30 bar
        // fvg.Index is candle1 (the first candle of the 3-candle FVG pattern)
        // This ensures the FVG formed after 9:30, not before
        if (_index930 >= 0 && fvg.Index < _index930)
            return;

        // Clone the FVG data into an FPFVG level
        var fpfvgLevel = new Level(
            LevelType.FPFVG,
            fvg.Low,
            fvg.High,
            fvg.LowTime,
            fvg.HighTime,
            fvg.MidTime,
            fvg.Direction,
            fvg.Index,
            fvg.IndexHigh,
            fvg.IndexLow,
            fvg.IndexMid
        );

        fpfvgLevel.InitializeQuadrants();

        var fpfvg = new FpfvgLevel
        {
            Level = fpfvgLevel,
            CreatedDate = fvg.MidTime,
            IsActive = true
        };

        _fpfvgs.Add(fpfvg);
        _capturedToday = true;

        // Trim collection to max size (remove oldest)
        while (_fpfvgs.Count > MaxCollection)
        {
            var oldest = _fpfvgs[0];
            RemoveFpfvg(oldest);
            _fpfvgs.RemoveAt(0);
        }

        UpdateDrawnFpfvgs();
    }

    private void UpdateDrawnFpfvgs()
    {
        if (_chart == null) return;

        foreach (var fpfvg in _fpfvgs.Where(f => f.IsActive && !f.IsDrawn))
        {
            DrawFpfvg(fpfvg);
            fpfvg.IsDrawn = true;
        }
    }

    private void DrawFpfvg(FpfvgLevel fpfvg)
    {
        if (_chart == null || fpfvg?.Level == null) return;

        var level = fpfvg.Level;
        string id = $"fpfvg-{level.Index}-{level.Direction}";
        fpfvg.ChartId = id;

        Color color = Color.Pink;
        Color fillColor = Color.FromArgb(10, color);

        DateTime startTime = level.MidTime;
        DateTime endTime = fpfvg.CreatedDate.AddDays(LifespanDays);

        // Draw extended rectangle
        string rectId = $"{id}-rect";
        var rect = _chart.DrawRectangle(
            rectId,
            startTime,
            level.High,
            endTime,
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
            endTime,
            level.Mid,
            Color.FromArgb(60, color),
            1,
            LineStyle.Dots
        );

        // Draw quadrant lines
        if (level.Quadrants != null && level.Quadrants.Count > 0)
        {
            DrawQuadrants(fpfvg, color);
        }

        // Draw label
        string labelId = $"{id}-label";
        var text = _chart.DrawText(
            labelId,
            "FPFVG",
            startTime,
            level.High,
            color
        );
        text.VerticalAlignment = VerticalAlignment.Top;
        text.HorizontalAlignment = HorizontalAlignment.Left;
        text.FontSize = 8;
    }

    private void DrawQuadrants(FpfvgLevel fpfvg, Color color)
    {
        var level = fpfvg.Level;
        string id = fpfvg.ChartId;
        DateTime startTime = level.MidTime;
        DateTime endTime = fpfvg.CreatedDate.AddDays(LifespanDays);

        foreach (var quadrant in level.Quadrants)
        {
            if (quadrant.Percent == 0 || quadrant.Percent == 100 || quadrant.Percent == 50)
                continue;

            string quadId = $"{id}-quad-{quadrant.Percent}";
            _chart.DrawTrendLine(
                quadId,
                startTime,
                quadrant.Price,
                endTime,
                quadrant.Price,
                Color.FromArgb(40, color),
                1,
                LineStyle.DotsRare
            );
        }
    }

    private void ProcessLifespan(DateTime currentTime)
    {
        var expired = _fpfvgs.Where(f => f.IsActive && (currentTime - f.CreatedDate).TotalDays >= LifespanDays).ToList();

        foreach (var fpfvg in expired)
        {
            fpfvg.IsActive = false;
            RemoveFpfvg(fpfvg);
        }

        _fpfvgs.RemoveAll(f => !f.IsActive);
    }

    private void RemoveFpfvg(FpfvgLevel fpfvg)
    {
        if (_chart == null || fpfvg?.ChartId == null) return;

        string id = fpfvg.ChartId;

        _chart.RemoveObject($"{id}-rect");
        _chart.RemoveObject($"{id}-mid");
        _chart.RemoveObject($"{id}-label");

        if (fpfvg.Level?.Quadrants != null)
        {
            foreach (var quadrant in fpfvg.Level.Quadrants)
            {
                if (quadrant.Percent == 0 || quadrant.Percent == 100 || quadrant.Percent == 50)
                    continue;
                _chart.RemoveObject($"{id}-quad-{quadrant.Percent}");
            }
        }
    }

    private class FpfvgLevel
    {
        public Level Level { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
        public bool IsDrawn { get; set; }
        public string ChartId { get; set; }
    }
}
