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
/// Detects Order Blocks based on orderflow analysis
/// 
/// Bullish Order Block Detection:
/// - Triggered when a new bullish orderflow is created
/// - High of current bullish OF > high of most recent bearish OF
/// - Low of current bullish OF < low of previous bullish OF  
/// - Must have bullish candle closing above bearish OF high
/// - The most recent bearish orderflow becomes a bullish order block
/// 
/// Bearish Order Block Detection:
/// - Triggered when a new bearish orderflow is created
/// - Low of current bearish OF < low of most recent bullish OF
/// - High of current bearish OF > high of previous bearish OF
/// - Must have bearish candle closing below bullish OF low
/// - The most recent bullish orderflow becomes a bearish order block
/// </summary>
public class OrderBlockDetector : BasePatternDetector<Level>
{
    private readonly IVisualization<Level> _visualizer;
    private readonly SwingPointDetector _swingPointDetector;
    private readonly HashSet<int> _processedIndices;
        
    public OrderBlockDetector(
        Chart chart,
        CandleManager candleManager,
        IEventAggregator eventAggregator,
        IRepository<Level> repository,
        IVisualization<Level> visualizer,
        SwingPointDetector swingPointDetector,
        IndicatorSettings settings,
        Action<string> logger = null)
        : base(chart, candleManager, eventAggregator, repository, settings, logger)
    {
        _visualizer = visualizer;
        _swingPointDetector = swingPointDetector;
        _processedIndices = new HashSet<int>();
    }
        
    protected override int GetMinimumBarsRequired()
    {
        return Constants.Patterns.OrderBlockLookback;
    }
        
    protected override List<Level> PerformDetection(int currentIndex)
    {
        var detectedOrderBlocks = new List<Level>();
        
        if (!Settings.Patterns.ShowOrderBlock || _processedIndices.Contains(currentIndex))
            return detectedOrderBlocks;

        try
        {
            // Get all orderflows (bullish and bearish)
            var allOrderFlows = Repository.Find(l => l.LevelType == LevelType.Orderflow)
                .OrderBy(of => of.Index)
                .ToList();

            if (allOrderFlows.Count < 3) // Need at least 3 orderflows to detect pattern
                return detectedOrderBlocks;

            // Check for new bullish orderflows that might trigger bullish order block detection
            var recentBullishOrderFlows = allOrderFlows
                .Where(of => of.Direction == Direction.Up && of.Index >= currentIndex - 10)
                .OrderByDescending(of => of.Index)
                .ToList();

            foreach (var newBullishOrderFlow in recentBullishOrderFlows)
            {
                var bullishOrderBlock = CheckForBullishOrderBlockFromOrderFlow(newBullishOrderFlow, allOrderFlows);
                if (bullishOrderBlock != null)
                {
                    detectedOrderBlocks.Add(bullishOrderBlock);
                    _processedIndices.Add(currentIndex);
                }
            }

            // Check for new bearish orderflows that might trigger bearish order block detection
            var recentBearishOrderFlows = allOrderFlows
                .Where(of => of.Direction == Direction.Down && of.Index >= currentIndex - 10)
                .OrderByDescending(of => of.Index)
                .ToList();

            foreach (var newBearishOrderFlow in recentBearishOrderFlows)
            {
                var bearishOrderBlock = CheckForBearishOrderBlockFromOrderFlow(newBearishOrderFlow, allOrderFlows);
                if (bearishOrderBlock != null)
                {
                    detectedOrderBlocks.Add(bearishOrderBlock);
                    _processedIndices.Add(currentIndex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.Invoke($"Error in OrderBlock detection: {ex.Message}");
        }

        return detectedOrderBlocks;
    }

    /// <summary>
    /// Checks if a new bullish orderflow creates a bullish order block pattern
    /// For bullish order blocks:
    /// - A new bullish orderflow is created (current bullish OF)
    /// - Get the most recent bearish orderflow (current bearish OF)
    /// - Get the second most recent bullish orderflow (previous bullish OF)
    /// - Check: High of current bullish OF > high of current bearish OF
    /// - Check: Low of current bullish OF < low of previous bullish OF
    /// - Check: There's a bullish candle in current bullish OF that closes above current bearish OF's high
    /// - Then the current bearish orderflow becomes a bullish order block
    /// </summary>
    private Level CheckForBullishOrderBlockFromOrderFlow(Level newBullishOrderFlow, List<Level> allOrderFlows)
    {
        try
        {
            // Current bullish orderflow is the new one passed in
            var currentBullishOrderFlow = newBullishOrderFlow;
            
            // Get all bearish orderflows ordered by index descending and get the first one (most recent)
            var currentBearishOrderFlow = allOrderFlows
                .Where(of => of.Direction == Direction.Down)
                .OrderByDescending(of => of.Index)
                .FirstOrDefault();

            if (currentBearishOrderFlow == null)
                return null;

            // Get all bullish orderflows ordered by index descending and get the second one (previous bullish)
            var previousBullishOrderFlow = allOrderFlows
                .Where(of => of.Direction == Direction.Up)
                .OrderByDescending(of => of.Index)
                .Skip(1) // Skip the first (current) to get the second (previous)
                .FirstOrDefault();

            if (previousBullishOrderFlow == null)
                return null;

            // Check bullish order block conditions:
            // 1. High of current bullish orderflow > high of current bearish orderflow
            // 2. Low of current bullish orderflow < low of previous bullish orderflow
            // 3. There must be a bullish candle that closes above the high of the current bearish orderflow
            bool condition1 = currentBullishOrderFlow.High > currentBearishOrderFlow.High;
            bool condition2 = currentBullishOrderFlow.Low < previousBullishOrderFlow.Low;
            
            // Check for bullish candle closure above current bearish orderflow high
            // For bullish orderflow with directional indexing: Index is at the low, IndexHigh is at the high
            bool hasBullishCandleClosureAbove = false;
            int startIndex = currentBullishOrderFlow.Index;      // Starting point of bullish move (at the low)
            int endIndex = currentBullishOrderFlow.IndexHigh;    // End point of bullish move (at the high)
            
            // Iterate through the range (startIndex should be < endIndex for bullish)
            for (int i = Math.Min(startIndex, endIndex); i <= Math.Max(startIndex, endIndex); i++)
            {
                var candle = CandleManager.GetCandle(i);
                if (candle != null && candle.Close > candle.Open && candle.Close > currentBearishOrderFlow.High)
                {
                    hasBullishCandleClosureAbove = true;
                    break;
                }
            }

            if (condition1 && condition2 && hasBullishCandleClosureAbove)
            {
                // The current bearish orderflow becomes a bullish order block
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    currentBearishOrderFlow.Low,
                    currentBearishOrderFlow.High,
                    currentBearishOrderFlow.LowTime,
                    currentBearishOrderFlow.HighTime,
                    currentBearishOrderFlow.MidTime,
                    Direction.Up, // Bullish order block
                    currentBearishOrderFlow.Index,
                    currentBearishOrderFlow.IndexHigh,
                    currentBearishOrderFlow.IndexLow,
                    currentBearishOrderFlow.IndexMid,
                    currentBearishOrderFlow.Zone,
                    currentBearishOrderFlow.Score,
                    currentBearishOrderFlow.StretchTo,
                    true // isConfirmed
                );

                Logger?.Invoke($"Bullish Order Block detected: High={orderBlock.High:F5}, Low={orderBlock.Low:F5} at index {orderBlock.Index}");
                return orderBlock;
            }
        }
        catch (Exception ex)
        {
            Logger?.Invoke($"Error checking bullish order block from orderflow: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Checks if a new bearish orderflow creates a bearish order block pattern
    /// For bearish order blocks:
    /// - A new bearish orderflow is created (current bearish OF)
    /// - Get the most recent bullish orderflow (current bullish OF)
    /// - Get the second most recent bearish orderflow (previous bearish OF)
    /// - Check: Low of current bearish OF < low of current bullish OF
    /// - Check: High of current bearish OF > high of previous bearish OF
    /// - Check: There's a bearish candle in current bearish OF that closes below current bullish OF's low
    /// - Then the current bullish orderflow becomes a bearish order block
    /// </summary>
    private Level CheckForBearishOrderBlockFromOrderFlow(Level newBearishOrderFlow, List<Level> allOrderFlows)
    {
        try
        {
            // Current bearish orderflow is the new one passed in
            var currentBearishOrderFlow = newBearishOrderFlow;
            
            // Get all bullish orderflows ordered by index descending and get the first one (most recent)
            var currentBullishOrderFlow = allOrderFlows
                .Where(of => of.Direction == Direction.Up)
                .OrderByDescending(of => of.Index)
                .FirstOrDefault();

            if (currentBullishOrderFlow == null)
                return null;

            // Get all bearish orderflows ordered by index descending and get the second one (previous bearish)
            var previousBearishOrderFlow = allOrderFlows
                .Where(of => of.Direction == Direction.Down)
                .OrderByDescending(of => of.Index)
                .Skip(1) // Skip the first (current) to get the second (previous)
                .FirstOrDefault();

            if (previousBearishOrderFlow == null)
                return null;

            // Check bearish order block conditions:
            // 1. Low of current bearish orderflow < low of current bullish orderflow
            // 2. High of current bearish orderflow > high of previous bearish orderflow
            // 3. There must be a bearish candle that closes below the low of the current bullish orderflow
            bool condition1 = currentBearishOrderFlow.Low < currentBullishOrderFlow.Low;
            bool condition2 = currentBearishOrderFlow.High > previousBearishOrderFlow.High;
            
            // Check for bearish candle closure below current bullish orderflow low
            // For bearish orderflow with directional indexing: Index is at the high, IndexLow is at the low
            bool hasBearishCandleClosureBelow = false;
            int startIndex = currentBearishOrderFlow.Index;      // Starting point of bearish move (at the high)
            int endIndex = currentBearishOrderFlow.IndexLow;     // End point of bearish move (at the low)
            
            // Since bearish moves from high to low, startIndex > endIndex, so we iterate downwards
            for (int i = Math.Min(startIndex, endIndex); i <= Math.Max(startIndex, endIndex); i++)
            {
                var candle = CandleManager.GetCandle(i);
                if (candle != null && candle.Close < candle.Open && candle.Close < currentBullishOrderFlow.Low)
                {
                    hasBearishCandleClosureBelow = true;
                    break;
                }
            }

            if (condition1 && condition2 && hasBearishCandleClosureBelow)
            {
                // The current bullish orderflow becomes a bearish order block
                var orderBlock = new Level(
                    LevelType.OrderBlock,
                    currentBullishOrderFlow.Low,
                    currentBullishOrderFlow.High,
                    currentBullishOrderFlow.LowTime,
                    currentBullishOrderFlow.HighTime,
                    currentBullishOrderFlow.MidTime,
                    Direction.Down, // Bearish order block
                    currentBullishOrderFlow.Index,
                    currentBullishOrderFlow.IndexHigh,
                    currentBullishOrderFlow.IndexLow,
                    currentBullishOrderFlow.IndexMid,
                    currentBullishOrderFlow.Zone,
                    currentBullishOrderFlow.Score,
                    currentBullishOrderFlow.StretchTo,
                    true // isConfirmed
                );

                Logger?.Invoke($"Bearish Order Block detected: High={orderBlock.High:F5}, Low={orderBlock.Low:F5} at index {orderBlock.Index}");
                return orderBlock;
            }
        }
        catch (Exception ex)
        {
            Logger?.Invoke($"Error checking bearish order block from orderflow: {ex.Message}");
        }

        return null;
    }

    public override void Initialize()
    {
        base.Initialize();
        
        // Subscribe to orderflow detection events to detect order blocks
        EventAggregator.Subscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
    }

    private void OnOrderFlowDetected(OrderFlowDetectedEvent evt)
    {
        var allOrderFlows = Repository.Find(l => l.LevelType == LevelType.Orderflow).OrderBy(of => of.Index).ToList();
        Level orderBlock = null;

        if (evt?.OrderFlow?.Direction == Direction.Up) // Process bullish orderflows for bullish order blocks
        {
            // Trigger detection when a new bullish orderflow is detected
            orderBlock = CheckForBullishOrderBlockFromOrderFlow(evt.OrderFlow, allOrderFlows);
        }
        else if (evt?.OrderFlow?.Direction == Direction.Down) // Process bearish orderflows for bearish order blocks
        {
            // Trigger detection when a new bearish orderflow is detected
            orderBlock = CheckForBearishOrderBlockFromOrderFlow(evt.OrderFlow, allOrderFlows);
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

    public override void Dispose()
    {
        _processedIndices?.Clear();
        base.Dispose();
    }
}