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
    private readonly List<TimeGapLevel> _fpfvgs = new();

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

    public void ProcessBar(int currentIndex, DateTime marketTime, bool isNewDay)
    {
        if (!_showFpfvg || currentIndex >= _candleManager.Count) return;

        var currentCandle = _candleManager.GetCandle(currentIndex);
        if (currentCandle == null) return;

        _currentPrice = currentCandle.Close;

        if (isNewDay)
        {
            _past930 = false;
            _index930 = -1;
        }

        // At 9:30, reset capturedToday and start accepting FVGs (only between 9:30 and 18:00)
        if (!_past930 && marketTime.Hour < 18 &&
            ((marketTime.Hour == 9 && marketTime.Minute >= 30) || (marketTime.Hour > 9)))
        {
            _past930 = true;
            _capturedToday = false;
            _index930 = currentIndex;
        }

        ProcessLifespan(marketTime);
        UpdateDrawnFpfvgs();
    }

    private void OnFvgDetected(FvgDetectedEvent evt)
    {
        if (!_showFpfvg || !_past930 || _capturedToday) return;
        if (evt?.FvgLevel == null) return;

        var fvg = evt.FvgLevel;

        if (_index930 >= 0 && fvg.Index < _index930)
            return;

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

        var fpfvg = new TimeGapLevel
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
            TimeGapDrawingHelper.RemoveTimeGap(_chart, oldest);
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

    private void DrawFpfvg(TimeGapLevel fpfvg)
    {
        if (_chart == null || fpfvg?.Level == null) return;

        var level = fpfvg.Level;
        fpfvg.ChartId = $"fpfvg-{level.Index}-{level.Direction}";

        Color color = Color.Pink;
        DateTime startTime = level.MidTime;

        TimeGapDrawingHelper.DrawTimeGap(_chart, fpfvg, "FPFVG", color, startTime, LifespanDays);
    }

    private void ProcessLifespan(DateTime currentTime)
    {
        var expired = _fpfvgs.Where(f => f.IsActive && (currentTime - f.CreatedDate).TotalDays >= LifespanDays).ToList();

        foreach (var fpfvg in expired)
        {
            fpfvg.IsActive = false;
            TimeGapDrawingHelper.RemoveTimeGap(_chart, fpfvg);
        }

        _fpfvgs.RemoveAll(f => !f.IsActive);
    }
}
