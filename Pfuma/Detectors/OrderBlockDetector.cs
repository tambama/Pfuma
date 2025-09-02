using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors.Base;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors;

/// <summary>
/// Detects Order Blocks based on swing point analysis
/// 
/// Bullish Order Block Detection:
/// - Triggered when a new bullish swing point is created
/// - Order bullish swing points by index descending: first=current high, second=previous high
/// - Order bearish swing points by index descending: first=current low, second=previous low
/// - Check: current high > previous high AND current low < previous low
/// - Check: current high swing point's candle closes above the high of previous swing high
/// - Create bullish order block from previous high to current low
/// 
/// Bearish Order Block Detection:
/// - Triggered when a new bearish swing point is created
/// - Order bearish swing points by index descending: first=current low, second=previous low
/// - Order bullish swing points by index descending: first=current high, second=previous high
/// - Check: current low < previous low AND current high > previous high
/// - Check: current low swing point's candle closes below the low of previous swing low
/// - Create bearish order block from previous low to current high
/// </summary>
public class OrderBlockDetector : BasePatternDetector<Level>
{
    private readonly IVisualization<Level> _visualizer;
    private readonly SwingPointManager _swingPointManager;
    private readonly HashSet<string> _detectedOrderBlockSignatures;
        
    public OrderBlockDetector(
        Chart chart,
        CandleManager candleManager,
        IEventAggregator eventAggregator,
        IRepository<Level> repository,
        IVisualization<Level> visualizer,
        SwingPointManager swingPointManager,
        IndicatorSettings settings,
        Action<string> logger = null)
        : base(chart, candleManager, eventAggregator, repository, settings, logger)
    {
        _visualizer = visualizer;
        _swingPointManager = swingPointManager;
        _detectedOrderBlockSignatures = new HashSet<string>();
    }
        
    protected override int GetMinimumBarsRequired()
    {
        return Constants.Patterns.OrderBlockLookback;
    }
        
    protected override List<Level> PerformDetection(int currentIndex)
    {
        var detectedOrderBlocks = new List<Level>();
        
        // Order block detection is now event-driven via swing point events
        // This method is kept for interface compliance but detection happens in event handlers
        
        return detectedOrderBlocks;
    }

    /// <summary>
    /// Checks for bearish order block when a new bearish swing point is created
    /// Logic:
    /// - Order bearish swing points by index descending: first=current low, second=previous low
    /// - Order bullish swing points by index descending: first=current high, second=previous high
    /// - Check: current low < previous low AND current high > previous high
    /// - Check: current low swing point's candle closes below the low of previous swing low
    /// - Create bearish order block from previous low to current high
    /// </summary>
    private Level CheckForBearishOrderBlock()
    {
        try
        {
            if (!Settings.Patterns.ShowOrderBlock)
                return null;

            // Get all swing points ordered by index descending
            var allBearishSwingPoints = _swingPointManager.GetSwingLows()
                .OrderByDescending(sp => sp.Index)
                .ToList();
            
            var allBullishSwingPoints = _swingPointManager.GetSwingHighs()
                .OrderByDescending(sp => sp.Index)
                .ToList();

            // Need at least 2 bearish and 2 bullish swing points
            if (allBearishSwingPoints.Count < 2 || allBullishSwingPoints.Count < 2)
                return null;

            // Order swing points by index descending
            var currentLow = allBearishSwingPoints[0]; // First = current low
            var previousLow = allBearishSwingPoints[1]; // Second = previous low
            
            var currentHigh = allBullishSwingPoints[0]; // First = current high
            var previousHigh = allBullishSwingPoints[1]; // Second = previous high
            
            // Create a unique signature for this order block configuration
            string orderBlockSignature = $"BEARISH_{previousLow.Index}_{currentHigh.Index}";
            
            // Check if we've already detected this order block
            if (_detectedOrderBlockSignatures.Contains(orderBlockSignature))
                return null;

            // Check conditions:
            // 1. Current low < previous low
            bool condition1 = currentLow.Price < previousLow.Price;
            
            // 2. Current high > previous high
            bool condition2 = currentHigh.Price > previousHigh.Price;
            
            if (!condition1 || !condition2)
                return null;

            // 3. Check if current low swing point's candle closes below the low of previous swing low
            var currentLowCandle = CandleManager.GetCandle(currentLow.Index);
            if (currentLowCandle == null)
                return null;
                
            bool closeBelowPreviousLow = currentLowCandle.Close < previousLow.Price;

            if (closeBelowPreviousLow)
            {
                // Check if the order block has already been swept
                if (IsOrderBlockSwept(previousLow.Price, currentHigh.Price, 
                    previousLow.Index, currentHigh.Index, Direction.Down))
                {
                    Logger?.Invoke($"Bearish Order Block already swept, skipping detection");
                    return null;
                }
                
                // Create bearish order block from previous low to current high
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    previousLow.Price,
                    currentHigh.Price,
                    previousLow.Time,
                    currentHigh.Time,
                    null,
                    Direction.Down, // Bearish order block
                    previousLow.Index,
                    currentHigh.Index,
                    previousLow.Index,
                    0,
                    Zone.Equilibrium,
                    3, // High score for breaking structure
                    null,
                    true // isConfirmed
                );

                // Mark this configuration as detected
                _detectedOrderBlockSignatures.Add(orderBlockSignature);
                
                Logger?.Invoke($"Bearish Order Block detected: High={orderBlock.High:F5}, Low={orderBlock.Low:F5} from previous low at index {previousLow.Index} to current high at index {currentHigh.Index}");
                return orderBlock;
            }
        }
        catch (Exception ex)
        {
            Logger?.Invoke($"Error checking bearish order block: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Checks for bullish order block when a new bullish swing point is created
    /// Logic:
    /// - Order bullish swing points by index descending: first=current high, second=previous high
    /// - Order bearish swing points by index descending: first=current low, second=previous low
    /// - Check: current high > previous high AND current low < previous low
    /// - Check: current high swing point's candle closes above the high of previous swing high
    /// - Create bullish order block from previous high to current low
    /// </summary>
    private Level CheckForBullishOrderBlock()
    {
        try
        {
            if (!Settings.Patterns.ShowOrderBlock)
                return null;

            // Get all swing points ordered by index descending
            var allBullishSwingPoints = _swingPointManager.GetSwingHighs()
                .OrderByDescending(sp => sp.Index)
                .ToList();
            
            var allBearishSwingPoints = _swingPointManager.GetSwingLows()
                .OrderByDescending(sp => sp.Index)
                .ToList();

            // Need at least 2 bullish and 2 bearish swing points
            if (allBullishSwingPoints.Count < 2 || allBearishSwingPoints.Count < 2)
                return null;

            // Order swing points by index descending
            var currentHigh = allBullishSwingPoints[0]; // First = current high
            var previousHigh = allBullishSwingPoints[1]; // Second = previous high
            
            var currentLow = allBearishSwingPoints[0]; // First = current low
            var previousLow = allBearishSwingPoints[1]; // Second = previous low
            
            // Create a unique signature for this order block configuration
            string orderBlockSignature = $"BULLISH_{previousHigh.Index}_{currentLow.Index}";
            
            // Check if we've already detected this order block
            if (_detectedOrderBlockSignatures.Contains(orderBlockSignature))
                return null;

            // Check conditions:
            // 1. Current high > previous high
            bool condition1 = currentHigh.Price > previousHigh.Price;
            
            // 2. Current low < previous low
            bool condition2 = currentLow.Price < previousLow.Price;
            
            if (!condition1 || !condition2)
                return null;

            // 3. Check if current high swing point's candle closes above the high of previous swing high
            var currentHighCandle = CandleManager.GetCandle(currentHigh.Index);
            if (currentHighCandle == null)
                return null;
                
            bool closeAbovePreviousHigh = currentHighCandle.Close > previousHigh.Price;

            if (closeAbovePreviousHigh)
            {
                // Check if the order block has already been swept
                if (IsOrderBlockSwept(currentLow.Price, previousHigh.Price, 
                    previousHigh.Index, currentLow.Index, Direction.Up))
                {
                    Logger?.Invoke($"Bullish Order Block already swept, skipping detection");
                    return null;
                }
                
                // Create bullish order block from previous high to current low
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    currentLow.Price,
                    previousHigh.Price,
                    currentLow.Time,
                    previousHigh.Time,
                    null,
                    Direction.Up, // Bullish order block
                    previousHigh.Index,
                    previousHigh.Index,
                    currentLow.Index,
                    0,
                    Zone.Equilibrium,
                    3, // High score for breaking structure
                    null,
                    true // isConfirmed
                );

                // Mark this configuration as detected
                _detectedOrderBlockSignatures.Add(orderBlockSignature);
                
                Logger?.Invoke($"Bullish Order Block detected: High={orderBlock.High:F5}, Low={orderBlock.Low:F5} from previous high at index {previousHigh.Index} to current low at index {currentLow.Index}");
                return orderBlock;
            }
        }
        catch (Exception ex)
        {
            Logger?.Invoke($"Error checking bullish order block: {ex.Message}");
        }

        return null;
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Subscribe to swing point detection events to trigger order block detection
        EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
    }

    private void OnSwingPointDetected(SwingPointDetectedEvent evt)
    {
        if (evt?.SwingPoint == null)
            return;

        Level orderBlock = null;

        if (evt.SwingPoint.SwingType == SwingType.H || evt.SwingPoint.SwingType == SwingType.HH) // New bullish swing point created
        {
            // Check for bullish order block
            orderBlock = CheckForBullishOrderBlock();
        }
        else if (evt.SwingPoint.SwingType == SwingType.L || evt.SwingPoint.SwingType == SwingType.LL) // New bearish swing point created
        {
            // Check for bearish order block
            orderBlock = CheckForBearishOrderBlock();
        }
        
        if (orderBlock != null)
        {
            Repository.Add(orderBlock);
            PublishDetectionEvent(orderBlock, evt.SwingPoint.Index);
            _visualizer?.Draw(orderBlock);
        }
    }
        
    protected override void PublishDetectionEvent(Level detectedLevel, int currentIndex)
    {
        EventAggregator.Publish(new OrderBlockDetectedEvent(detectedLevel));
    }
        
    public override List<Level> GetByDirection(Direction direction)
    {
        return Repository.Find(l => l.LevelType == LevelType.OrderBlock && l.Direction == direction);
    }
        
    public override bool IsValid(Level level, int currentIndex)
    {
        return level?.LevelType == LevelType.OrderBlock;
    }

    /// <summary>
    /// Checks if an order block has been swept by price action
    /// </summary>
    private bool IsOrderBlockSwept(double low, double high, int startIndex, int endIndex, Direction direction)
    {
        try
        {
            // Get the current price index
            int currentIndex = CandleManager.Count - 1;
            
            // Check from the end of the order block to current candle
            int checkStart = Math.Max(startIndex, endIndex) + 1;
            
            for (int i = checkStart; i <= currentIndex; i++)
            {
                var candle = CandleManager.GetCandle(i);
                if (candle == null)
                    continue;
                    
                if (direction == Direction.Up)
                {
                    // Bullish order block is swept if price goes below its low
                    if (candle.Low < low)
                    {
                        Logger?.Invoke($"Bullish Order Block swept at index {i}, candle low {candle.Low:F5} < OB low {low:F5}");
                        return true;
                    }
                }
                else // Direction.Down
                {
                    // Bearish order block is swept if price goes above its high
                    if (candle.High > high)
                    {
                        Logger?.Invoke($"Bearish Order Block swept at index {i}, candle high {candle.High:F5} > OB high {high:F5}");
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Logger?.Invoke($"Error checking if order block is swept: {ex.Message}");
            return false;
        }
    }
    
    public override void Dispose()
    {
        _detectedOrderBlockSignatures?.Clear();
        base.Dispose();
    }
}