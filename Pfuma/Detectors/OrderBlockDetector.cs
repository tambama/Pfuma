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
/// Bearish Order Block Detection:
/// - Triggered when a new bearish orderflow is created
/// - Order bearish swing points by index descending: first=current, second=previous
/// - Order bullish swing points by index descending: second=current, third=previous
/// - Check: current bearish swing is less than previous bearish swing
/// - Check: current bullish swing is greater than previous bullish swing
/// - Check: bearish candle between current bullish and current bearish closes below previous bearish
/// - Mark orderflow from previous bearish to current bullish as bearish order block
/// 
/// Bullish Order Block Detection:
/// - Triggered when a new bullish orderflow is created
/// - Order bullish swing points by index descending: first=current, second=previous
/// - Order bearish swing points by index descending: second=current, third=previous
/// - Check: current bullish swing is greater than previous bullish swing
/// - Check: current bearish swing is less than previous bearish swing
/// - Check: bullish candle between current bearish and current bullish closes above previous bullish
/// - Mark orderflow from previous bullish to current bearish as bullish order block
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
    /// Checks for bearish order block when a new bearish orderflow is created
    /// Logic:
    /// - Order bearish swing points by index descending: first=current, second=previous
    /// - Order bullish swing points by index descending: second=current, third=previous
    /// - Check: current bearish swing is less than previous bearish swing
    /// - Check: current bullish swing is greater than previous bullish swing
    /// - Check: bearish candle between current bullish and current bearish closes below previous bearish
    /// - Mark orderflow from previous bearish to current bullish as bearish order block
    /// </summary>
    private Level CheckForBearishOrderBlock()
    {
        try
        {
            if (!Settings.Patterns.ShowOrderBlock)
                return null;

            // Get all swing points
            var allBearishSwingPoints = _swingPointManager.GetSwingLows()
                .OrderByDescending(sp => sp.Index)
                .ToList();
            
            var allBullishSwingPoints = _swingPointManager.GetSwingHighs()
                .OrderByDescending(sp => sp.Index)
                .ToList();

            // Need at least 2 bearish and 3 bullish swing points
            if (allBearishSwingPoints.Count < 2 || allBullishSwingPoints.Count < 3)
                return null;

            // Order bearish swing points by index descending
            var currentBearishSwing = allBearishSwingPoints[0]; // First = current
            var previousBearishSwing = allBearishSwingPoints[1]; // Second = previous
            
            // Order bullish swing points by index descending
            var currentBullishSwing = allBullishSwingPoints[1]; // Second = current
            var previousBullishSwing = allBullishSwingPoints[2]; // Third = previous
            
            // Create a unique signature for this order block configuration
            string orderBlockSignature = $"BEARISH_{previousBearishSwing.Index}_{currentBullishSwing.Index}";
            
            // Check if we've already detected this order block
            if (_detectedOrderBlockSignatures.Contains(orderBlockSignature))
                return null;

            // Check conditions:
            // 1. Current bearish swing < previous bearish swing
            bool condition1 = currentBearishSwing.Price < previousBearishSwing.Price;
            
            // 2. Current bullish swing > previous bullish swing
            bool condition2 = currentBullishSwing.Price > previousBullishSwing.Price;
            
            if (!condition1 || !condition2)
                return null;

            // 3. Check for bearish candle between current bullish and current bearish that closes below previous bearish
            bool hasBearishCandleClosureBelowPreviousBearish = false;
            int startIndex = Math.Min(currentBullishSwing.Index, currentBearishSwing.Index);
            int endIndex = Math.Max(currentBullishSwing.Index, currentBearishSwing.Index);
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = CandleManager.GetCandle(i);
                if (candle != null && candle.Close < candle.Open && candle.Close < previousBearishSwing.Price)
                {
                    hasBearishCandleClosureBelowPreviousBearish = true;
                    break;
                }
            }

            if (hasBearishCandleClosureBelowPreviousBearish)
            {
                // Check if the order block has already been swept
                if (IsOrderBlockSwept(previousBearishSwing.Price, currentBullishSwing.Price, 
                    previousBearishSwing.Index, currentBullishSwing.Index, Direction.Down))
                {
                    Logger?.Invoke($"Bearish Order Block already swept, skipping detection");
                    return null;
                }
                
                // Create bearish order block from previous bearish swing to current bullish swing
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    previousBearishSwing.Price,
                    currentBullishSwing.Price,
                    previousBearishSwing.Time,
                    currentBullishSwing.Time,
                    null,
                    Direction.Down, // Bearish order block
                    previousBearishSwing.Index,
                    currentBullishSwing.Index,
                    previousBearishSwing.Index,
                    0,
                    Zone.Equilibrium,
                    3, // High score for breaking structure
                    null,
                    true // isConfirmed
                );

                // Mark this configuration as detected
                _detectedOrderBlockSignatures.Add(orderBlockSignature);
                
                Logger?.Invoke($"Bearish Order Block detected: High={orderBlock.High:F5}, Low={orderBlock.Low:F5} from previous bearish at index {previousBearishSwing.Index} to current bullish at index {currentBullishSwing.Index}");
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
    /// Checks for bullish order block when a new bullish orderflow is created
    /// Logic:
    /// - Order bullish swing points by index descending: first=current, second=previous
    /// - Order bearish swing points by index descending: second=current, third=previous
    /// - Check: current bullish swing is greater than previous bullish swing
    /// - Check: current bearish swing is less than previous bearish swing
    /// - Check: bullish candle between current bearish and current bullish closes above previous bullish
    /// - Mark orderflow from previous bullish to current bearish as bullish order block
    /// </summary>
    private Level CheckForBullishOrderBlock()
    {
        try
        {
            if (!Settings.Patterns.ShowOrderBlock)
                return null;

            // Get all swing points
            var allBullishSwingPoints = _swingPointManager.GetSwingHighs()
                .OrderByDescending(sp => sp.Index)
                .ToList();
            
            var allBearishSwingPoints = _swingPointManager.GetSwingLows()
                .OrderByDescending(sp => sp.Index)
                .ToList();

            // Need at least 2 bullish and 3 bearish swing points
            if (allBullishSwingPoints.Count < 2 || allBearishSwingPoints.Count < 3)
                return null;

            // Order bullish swing points by index descending
            var currentBullishSwing = allBullishSwingPoints[0]; // First = current
            var previousBullishSwing = allBullishSwingPoints[1]; // Second = previous
            
            // Order bearish swing points by index descending
            var currentBearishSwing = allBearishSwingPoints[1]; // Second = current
            var previousBearishSwing = allBearishSwingPoints[2]; // Third = previous
            
            // Create a unique signature for this order block configuration
            string orderBlockSignature = $"BULLISH_{previousBullishSwing.Index}_{currentBearishSwing.Index}";
            
            // Check if we've already detected this order block
            if (_detectedOrderBlockSignatures.Contains(orderBlockSignature))
                return null;

            // Check conditions:
            // 1. Current bullish swing > previous bullish swing
            bool condition1 = currentBullishSwing.Price > previousBullishSwing.Price;
            
            // 2. Current bearish swing < previous bearish swing
            bool condition2 = currentBearishSwing.Price < previousBearishSwing.Price;
            
            if (!condition1 || !condition2)
                return null;

            // 3. Check for bullish candle between current bearish and current bullish that closes above previous bullish
            bool hasBullishCandleClosureAbovePreviousBullish = false;
            int startIndex = Math.Min(currentBearishSwing.Index, currentBullishSwing.Index);
            int endIndex = Math.Max(currentBearishSwing.Index, currentBullishSwing.Index);
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = CandleManager.GetCandle(i);
                if (candle != null && candle.Close > candle.Open && candle.Close > previousBullishSwing.Price)
                {
                    hasBullishCandleClosureAbovePreviousBullish = true;
                    break;
                }
            }

            if (hasBullishCandleClosureAbovePreviousBullish)
            {
                // Check if the order block has already been swept
                if (IsOrderBlockSwept(currentBearishSwing.Price, previousBullishSwing.Price, 
                    previousBullishSwing.Index, currentBearishSwing.Index, Direction.Up))
                {
                    Logger?.Invoke($"Bullish Order Block already swept, skipping detection");
                    return null;
                }
                
                // Create bullish order block from previous bullish swing to current bearish swing
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    currentBearishSwing.Price,
                    previousBullishSwing.Price,
                    currentBearishSwing.Time,
                    previousBullishSwing.Time,
                    null,
                    Direction.Up, // Bullish order block
                    previousBullishSwing.Index,
                    previousBullishSwing.Index,
                    currentBearishSwing.Index,
                    0,
                    Zone.Equilibrium,
                    3, // High score for breaking structure
                    null,
                    true // isConfirmed
                );

                // Mark this configuration as detected
                _detectedOrderBlockSignatures.Add(orderBlockSignature);
                
                Logger?.Invoke($"Bullish Order Block detected: High={orderBlock.High:F5}, Low={orderBlock.Low:F5} from previous bullish at index {previousBullishSwing.Index} to current bearish at index {currentBearishSwing.Index}");
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
        
        // Subscribe to orderflow detection events to trigger order block detection
        EventAggregator.Subscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
    }

    private void OnOrderFlowDetected(OrderFlowDetectedEvent evt)
    {
        if (evt?.OrderFlow == null)
            return;

        Level orderBlock = null;

        if (evt.OrderFlow.Direction == Direction.Up) // New bullish orderflow created
        {
            // Check for bullish order block
            orderBlock = CheckForBullishOrderBlock();
        }
        else if (evt.OrderFlow.Direction == Direction.Down) // New bearish orderflow created
        {
            // Check for bearish order block
            orderBlock = CheckForBearishOrderBlock();
        }
        
        if (orderBlock != null)
        {
            Repository.Add(orderBlock);
            PublishDetectionEvent(orderBlock, evt.OrderFlow.Index);
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