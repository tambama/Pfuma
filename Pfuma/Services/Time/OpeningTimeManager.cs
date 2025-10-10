using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Detectors;
using Pfuma.Models;
using Pfuma.Extensions;

namespace Pfuma.Services.Time;

/// <summary>
/// Manages opening time levels at 18:00 and 00:00
/// </summary>
public interface IOpeningTimeManager
{
    void ProcessBar(int currentIndex, DateTime marketTime);
    void UpdateSweptLevel(SwingPoint openLevel, SwingPoint sweepingPoint);
}

public class OpeningTimeManager : IOpeningTimeManager
{
    private readonly CandleManager _candleManager;
    private readonly SwingPointDetector _swingPointDetector;
    private readonly Chart _chart;
    private readonly bool _showOpeningTimes;
    private readonly int _utcOffset;
    private readonly Dictionary<string, OpeningLevel> _openingLevels;

    // Trackers to ensure each opening is only set once per day
    private bool _set18 = false;
    private bool _set00 = false;
    private bool _set930 = false;
    private DateTime _lastProcessedDate = DateTime.MinValue;

    public OpeningTimeManager(
        CandleManager candleManager,
        SwingPointDetector swingPointDetector,
        Chart chart,
        bool showOpeningTimes = true,
        int utcOffset = -4)
    {
        _candleManager = candleManager;
        _swingPointDetector = swingPointDetector;
        _chart = chart;
        _showOpeningTimes = showOpeningTimes;
        _utcOffset = utcOffset;
        _openingLevels = new Dictionary<string, OpeningLevel>();
    }

    public void ProcessBar(int currentIndex, DateTime marketTime)
    {
        if (!_showOpeningTimes || currentIndex >= _candleManager.Count) return;

        var currentCandle = _candleManager.GetCandle(currentIndex);
        if (currentCandle == null) return;

        // Reset trackers when we cross into a new day (at 18:00 market time, which marks the new trading day)
        DateTime currentDay = marketTime.Hour >= 18 ? marketTime.Date : marketTime.Date.AddDays(-1);
        if (_lastProcessedDate != DateTime.MinValue && currentDay > _lastProcessedDate)
        {
            _set18 = false;
            _set00 = false;
            _set930 = false;
        }
        _lastProcessedDate = currentDay;

        // Check if current candle's hour is 18 and 18:00 line hasn't been set yet for this day
        if (marketTime.Hour == 18 && !_set18)
        {
            CreateOpeningLevel(currentCandle, "18:00", currentCandle.Time);
            _set18 = true;
        }

        // Check if current candle's hour is 0 and 00:00 line hasn't been set yet for this day
        if (marketTime.Hour == 0 && !_set00)
        {
            CreateOpeningLevel(currentCandle, "00:00", currentCandle.Time);
            _set00 = true;
        }

        // Check if current candle's hour is 9 and 9:30 line hasn't been set yet for this day
        if (marketTime.Hour == 9 && marketTime.Minute >= 30 && !_set930)
        {
            CreateOpeningLevel(currentCandle, "9:30", currentCandle.Time);
            _set930 = true;
        }

        // Process lifespan for all opening levels
        ProcessOpeningLevelLifespan(marketTime);
    }

    private void CreateOpeningLevel(Candle candle, string label, DateTime time)
    {
        if (candle?.Index == null) return;

        string levelKey = $"{label}-{time:yyyy-MM-dd}";

        // Avoid creating duplicate levels
        if (_openingLevels.ContainsKey(levelKey))
            return;

        // Create swing point for opening level
        var swingPoint = new SwingPoint(
            candle.Index.Value,
            candle.Open,
            time,
            candle,
            SwingType.H, // Neutral type
            LiquidityType.Open,
            Direction.Up, // Neutral direction
            LiquidityName.N
        );

        _swingPointDetector.AddSpecialSwingPoint(swingPoint);

        // Create opening level tracking
        var openingLevel = new OpeningLevel
        {
            Key = levelKey,
            Price = candle.Open,
            Time = time,
            Label = label,
            SwingPoint = swingPoint,
            IsActive = true
        };

        _openingLevels[levelKey] = openingLevel;

        // Draw the level on chart
        if (_chart != null)
        {
            DrawOpeningLevel(openingLevel);
        }
    }

    private void DrawOpeningLevel(OpeningLevel level, DateTime? endTime = null)
    {
        if (_chart == null || level == null) return;

        string lineId = $"open-{level.Key}";

        // If swept, draw from opening time to sweeping candle time
        // Otherwise, extend to infinity
        bool isSwept = endTime.HasValue;
        DateTime lineEndTime = isSwept ? endTime.Value : level.Time.AddDays(1);

        // Draw white dotted horizontal line from the exact candle at that time
        _chart.DrawStraightLine(
            lineId,
            level.Time,                    // Start from the exact candle time
            level.Price,
            lineEndTime,                   // End at sweeping candle or extend
            level.Price,
            level.Label,
            LineStyle.Dots,
            Color.White,
            false,                          // hasLabel - show label
            true,                          // removeExisting - redraw if already exists
            true,                      // extended - extend to infinity only if not swept
            false,                         // editable
            true                           // labelOnRight - float label to right of window
        );
    }

    public void UpdateSweptLevel(SwingPoint openLevel, SwingPoint sweepingPoint)
    {
        if (openLevel == null || sweepingPoint == null) return;

        // Find the opening level in our tracking dictionary
        foreach (var kvp in _openingLevels)
        {
            var level = kvp.Value;
            if (level.SwingPoint == openLevel)
            {
                // Redraw the line from opening time to sweeping candle time
                DrawOpeningLevel(level, sweepingPoint.Time);
                break;
            }
        }
    }

    private void ProcessOpeningLevelLifespan(DateTime currentTime)
    {
        var levelsToRemove = new List<string>();

        foreach (var kvp in _openingLevels)
        {
            var level = kvp.Value;

            if (!level.IsActive)
                continue;

            // Check if level has been swept
            if (level.SwingPoint != null && level.SwingPoint.Swept)
            {
                // Mark first swept date if not already set
                if (!level.FirstSweptDate.HasValue)
                {
                    level.FirstSweptDate = currentTime.Date;
                    level.DaysRemaining = 3;
                }
                else
                {
                    // Calculate days elapsed since first sweep
                    int daysElapsed = (currentTime.Date - level.FirstSweptDate.Value).Days;
                    level.DaysRemaining = Math.Max(0, 3 - daysElapsed);

                    // If lifespan expired, remove the level
                    if (level.DaysRemaining == 0)
                    {
                        level.IsActive = false;
                        levelsToRemove.Add(kvp.Key);

                        // Remove from chart
                        RemoveOpeningLevel(level);

                        // Mark swing point as not swept so it won't be processed anymore
                        if (level.SwingPoint != null)
                        {
                            level.SwingPoint.Swept = false;
                            level.SwingPoint.FirstSweptDate = null;
                        }
                    }
                }
            }
        }

        // Remove expired levels from tracking
        foreach (var key in levelsToRemove)
        {
            _openingLevels.Remove(key);
        }
    }

    private void RemoveOpeningLevel(OpeningLevel level)
    {
        if (_chart == null || level == null) return;

        string lineId = $"open-{level.Key}";
        _chart.RemoveObject(lineId);
    }

    private class OpeningLevel
    {
        public string Key { get; set; }
        public double Price { get; set; }
        public DateTime Time { get; set; }
        public string Label { get; set; }
        public SwingPoint SwingPoint { get; set; }
        public bool IsActive { get; set; }
        public DateTime? FirstSweptDate { get; set; }
        public int DaysRemaining { get; set; }
    }
}
