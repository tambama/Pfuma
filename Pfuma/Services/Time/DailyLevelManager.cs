using System;
using System.Linq;
using cAlgo.API;
using Pfuma.Detectors;
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

        // Check if high and low occur at the same index
        if (highCandle.Index == lowCandle.Index)
        {
            // Special handling when PDH and PDL are at the same candle
            // We need to draw both lines at their correct prices
            HandleSameIndexDailyLevels(
                highCandle,
                lowCandle,
                dayEnd);
        }
        else
        {
            // Normal case - high and low are at different indices
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

        // Check if there's already a swing point at this index with the same direction
        var existingPointsAtIndex = _swingPointDetector.GetSwingPointsAtIndex(index);
        var existingPointWithSameDirection = existingPointsAtIndex?.FirstOrDefault(sp => sp.Direction == direction);

        if (existingPointWithSameDirection != null)
        {
            // If daily level is being set at same index as session level, replace it completely
            if ((liquidityType == LiquidityType.PDH || liquidityType == LiquidityType.PDL) &&
                IsSessionLevelType(existingPointWithSameDirection.LiquidityType))
            {
                // Remove existing session level visualization
                RemoveExistingSessionLabels(time, price);
                
                // Update the existing point to daily level
                existingPointWithSameDirection.LiquidityType = liquidityType;
                existingPointWithSameDirection.LiquidityName = liquidityName;
                existingPointWithSameDirection.Price = price; // Ensure correct price
            }
            else if ((liquidityType == LiquidityType.PDH || liquidityType == LiquidityType.PDL) &&
                existingPointWithSameDirection.LiquidityType != LiquidityType.PDH &&
                existingPointWithSameDirection.LiquidityType != LiquidityType.PDL)
            {
                RemoveExistingSessionLabels(time, price);
                existingPointWithSameDirection.LiquidityType = liquidityType;
                existingPointWithSameDirection.LiquidityName = liquidityName;
                existingPointWithSameDirection.Price = price; // Ensure correct price
            }
            else
            {
                existingPointWithSameDirection.LiquidityType = liquidityType;
                existingPointWithSameDirection.LiquidityName = liquidityName;
                existingPointWithSameDirection.Price = price; // Ensure correct price
            }
        }
        else
        {
            // Create new swing point (either no existing point, or existing point has different direction)
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

    /// <summary>
    /// Handle the special case when PDH and PDL occur at the same index
    /// </summary>
    private void HandleSameIndexDailyLevels(Candle candle, Candle sameCandle, DateTime endTime)
    {
        if (_swingPointDetector == null || candle == null) return;
        
        int index = candle.Index ?? 0;
        
        // Remove any existing session levels at this index
        RemoveExistingSessionLabels(candle.Time, candle.High);
        RemoveExistingSessionLabels(candle.Time, candle.Low);
        
        // Check for existing swing points at this index
        var existingPointsAtIndex = _swingPointDetector.GetSwingPointsAtIndex(index);
        var existingUpwardPoint = existingPointsAtIndex?.FirstOrDefault(sp => sp.Direction == Direction.Up);
        var existingDownwardPoint = existingPointsAtIndex?.FirstOrDefault(sp => sp.Direction == Direction.Down);
        
        // Handle or create PDH (upward direction)
        if (existingUpwardPoint != null)
        {
            // Update existing upward point to PDH
            existingUpwardPoint.LiquidityType = LiquidityType.PDH;
            existingUpwardPoint.LiquidityName = LiquidityName.PDH;
            existingUpwardPoint.Price = candle.High; // Ensure high price
        }
        else
        {
            // Create a new swing point for PDH (at the high price)
            var pdhSwingPoint = new SwingPoint(
                index,
                candle.High,
                candle.Time,
                candle,
                SwingType.H,
                LiquidityType.PDH,
                Direction.Up,
                LiquidityName.PDH
            );
            
            _swingPointDetector.AddSpecialSwingPoint(pdhSwingPoint);
        }
        
        // Handle or create PDL (downward direction)
        if (existingDownwardPoint != null)
        {
            // Update existing downward point to PDL
            existingDownwardPoint.LiquidityType = LiquidityType.PDL;
            existingDownwardPoint.LiquidityName = LiquidityName.PDL;
            existingDownwardPoint.Price = candle.Low; // Ensure low price
        }
        else
        {
            // Create a new swing point for PDL (at the low price)
            var pdlSwingPoint = new SwingPoint(
                index,
                candle.Low,
                candle.Time,
                candle,
                SwingType.L,
                LiquidityType.PDL,
                Direction.Down,
                LiquidityName.PDL
            );
            
            _swingPointDetector.AddSpecialSwingPoint(pdlSwingPoint);
        }
        
        // Draw both PDH and PDL lines at their correct prices
        if (_chart != null && _showDailyLevels)
        {
            // Draw PDH line at the high price
            string pdhId = $"pdh-{candle.Time.Ticks}";
            _chart.DrawStraightLine(
                pdhId,
                candle.Time,
                candle.High,
                endTime,
                candle.High,
                "PDH",
                LineStyle.Solid,
                Color.Wheat,
                true,
                true
            );
            
            // Draw PDL line at the low price
            string pdlId = $"pdl-{candle.Time.Ticks}";
            _chart.DrawStraightLine(
                pdlId,
                candle.Time,
                candle.Low,
                endTime,
                candle.Low,
                "PDL",
                LineStyle.Solid,
                Color.Wheat,
                true,
                true
            );
        }
    }

    /// <summary>
    /// Check if the liquidity type represents a session level that should be replaced by daily levels
    /// </summary>
    private bool IsSessionLevelType(LiquidityType liquidityType)
    {
        return liquidityType == LiquidityType.PSH || 
               liquidityType == LiquidityType.PSL;
    }

    private void RemoveExistingSessionLabels(DateTime time, double price)
    {
        if (_chart == null)
            return;

        // Remove session level drawings that might exist at this time/price level
        string[] sessionPrefixes =
        {
            "ah", "al", "lh", "ll", "lph", "lpl", "llh", "lll",
            "nyph", "nypl", "nyamh", "nyaml", "nylh", "nyll",
            "nypph", "nyppl", "nypmh", "nypml", "sh", "sl"
        };

        foreach (var prefix in sessionPrefixes)
        {
            // Try different possible ID patterns that might have been used for session levels
            string lineId = $"{prefix}-{time.Ticks}";
            string labelId = $"{lineId}-label";

            _chart.RemoveObject(lineId);
            _chart.RemoveObject(labelId);
            
            // Also try removing with simplified naming patterns
            string simplifiedId = prefix.ToLower();
            _chart.RemoveObject(simplifiedId);
            _chart.RemoveObject($"{simplifiedId}-label");
        }
    }
}