using System;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;
using Pfuma.Services;
using Pfuma.Extensions;

namespace Pfuma.Services.Time;

/// <summary>
/// Manages daily high/low level processing
/// </summary>
public class DailyLevelManager : IDailyLevelManager
{
    private readonly CandleManager _candleManager;
    private readonly SwingPointDetector _swingPointDetector;
    private readonly Chart _chart;
    private readonly bool _showDailyLevels;
    private DateTime _lastDayStartTime = DateTime.MinValue;
    private bool _processingDailyLevels = false;

    public DailyLevelManager(
        CandleManager candleManager,
        SwingPointDetector swingPointDetector,
        Chart chart,
        bool showDailyLevels = true)
    {
        _candleManager = candleManager;
        _swingPointDetector = swingPointDetector;
        _chart = chart;
        _showDailyLevels = showDailyLevels;
    }

    public void ProcessDailyBoundary(int currentIndex)
    {
        if (_processingDailyLevels || currentIndex >= _candleManager.Count) return;

        _processingDailyLevels = true;

        try
        {
            if (_lastDayStartTime != DateTime.MinValue)
            {
                ProcessPreviousDayLevels(currentIndex);
            }

            var currentCandle = _candleManager.GetCandle(currentIndex);
            if (currentCandle != null)
            {
                _lastDayStartTime = currentCandle.Time;
            }
        }
        finally
        {
            _processingDailyLevels = false;
        }
    }


    private void ProcessPreviousDayLevels(int currentIndex)
    {
        DateTime dayStart = _lastDayStartTime;
        var currentCandle = _candleManager.GetCandle(currentIndex);
        if (currentCandle == null) return;
        
        DateTime dayEnd = currentCandle.Time;

        // Find high and low candles between start and end times
        var candlesInRange = _candleManager.GetCandlesBetween(dayStart, dayEnd);
        if (candlesInRange.Count == 0) return;

        var highCandle = candlesInRange.OrderByDescending(c => c.High).First();
        var lowCandle = candlesInRange.OrderBy(c => c.Low).First();

        CreateOrUpdateSpecialSwingPoint(
            highCandle.Index ?? 0,
            highCandle.High,
            highCandle.Time,
            highCandle,
            SwingType.H,
            LiquidityType.PDH,
            Direction.Up,
            LiquidityName.PDH,
            dayEnd);

        CreateOrUpdateSpecialSwingPoint(
            lowCandle.Index ?? 0,
            lowCandle.Low,
            lowCandle.Time,
            lowCandle,
            SwingType.L,
            LiquidityType.PDL,
            Direction.Down,
            LiquidityName.PDL,
            dayEnd);
    }

    private void CreateOrUpdateSpecialSwingPoint(
        int index,
        double price,
        DateTime time,
        Candle candle,
        SwingType swingType,
        LiquidityType liquidityType,
        Direction direction,
        LiquidityName liquidityName,
        DateTime endTime)
    {
        if (_swingPointDetector == null) return;

        var existingPoint = _swingPointDetector.GetSwingPointAtIndex(index);

        if (existingPoint != null)
        {
            if ((liquidityType == LiquidityType.PDH || liquidityType == LiquidityType.PDL) &&
                existingPoint.LiquidityType != LiquidityType.PDH &&
                existingPoint.LiquidityType != LiquidityType.PDL)
            {
                RemoveExistingSessionLabels(time, price);
            }

            existingPoint.LiquidityType = liquidityType;
            existingPoint.LiquidityName = liquidityName;
        }
        else
        {
            var swingPoint = new SwingPoint(
                index,
                price,
                time,
                candle,
                swingType,
                liquidityType,
                direction,
                liquidityName
            );

            _swingPointDetector.AddSpecialSwingPoint(swingPoint);
        }

        if (_chart != null && _showDailyLevels)
        {
            string id = $"{liquidityName.ToString().ToLower()}-{time.Ticks}";

            _chart.DrawStraightLine(
                id,
                time,
                price,
                endTime,
                price,
                liquidityName.ToString(),
                LineStyle.Solid,
                Color.Wheat,
                true,
                true
            );
        }
    }

    private void RemoveExistingSessionLabels(DateTime time, double price)
    {
        if (_chart == null)
            return;

        string[] sessionPrefixes =
        {
            "ah", "al", "lh", "ll", "lph", "lpl", "llh", "lll",
            "nyph", "nypl", "nyamh", "nyaml", "nylh", "nyll",
            "nypph", "nyppl", "nypmh", "nypml", "sh", "sl"
        };

        foreach (var prefix in sessionPrefixes)
        {
            string lineId = $"{prefix}-{time.Ticks}";
            string labelId = $"{lineId}-label";

            _chart.RemoveObject(lineId);
            _chart.RemoveObject(labelId);
        }
    }
}