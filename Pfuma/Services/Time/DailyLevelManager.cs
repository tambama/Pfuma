using System;
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
    private readonly Bars _bars;
    private readonly SwingPointDetector _swingPointDetector;
    private readonly Chart _chart;
    private readonly bool _showDailyLevels;
    private DateTime _lastDayStartTime = DateTime.MinValue;
    private bool _processingDailyLevels = false;

    public DailyLevelManager(
        Bars bars,
        SwingPointDetector swingPointDetector,
        Chart chart,
        bool showDailyLevels = true)
    {
        _bars = bars;
        _swingPointDetector = swingPointDetector;
        _chart = chart;
        _showDailyLevels = showDailyLevels;
    }

    public void ProcessDailyBoundary(int currentIndex)
    {
        if (_processingDailyLevels || currentIndex >= _bars.Count) return;

        _processingDailyLevels = true;

        try
        {
            if (_lastDayStartTime != DateTime.MinValue)
            {
                ProcessPreviousDayLevels(currentIndex);
            }

            _lastDayStartTime = _bars[currentIndex].OpenTime;
        }
        finally
        {
            _processingDailyLevels = false;
        }
    }


    private void ProcessPreviousDayLevels(int currentIndex)
    {
        DateTime dayStart = _lastDayStartTime;
        DateTime dayEnd = _bars[currentIndex].OpenTime;

        var minMax = _bars.GetMinMax(dayStart, dayEnd);

        var highCandle = new Candle(_bars[minMax.maxIndex], minMax.maxIndex);
        var lowCandle = new Candle(_bars[minMax.minIndex], minMax.minIndex);

        CreateOrUpdateSpecialSwingPoint(
            minMax.maxIndex,
            minMax.max,
            minMax.maxTime,
            highCandle,
            SwingType.H,
            LiquidityType.PDH,
            Direction.Up,
            LiquidityName.PDH,
            dayEnd);

        CreateOrUpdateSpecialSwingPoint(
            minMax.minIndex,
            minMax.min,
            minMax.minTime,
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