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
        // Order blocks are only detected through FVG events, not through regular scanning
        // This method returns empty as detection happens in OnFvgDetected event handler
        return new List<Level>();
    }
        
    private Level CheckForOrderBlockFromFvg(Level fvg, int currentIndex)
    {
        if (fvg == null || _swingPointDetector == null)
            return null;
            
        // Get the first candle index of the FVG (bar1 in the 3-candle pattern)
        int firstCandleIndex = fvg.Index; // This should be the index of bar1
        
        // Check if the first candle is a swing point
        var swingPoint = _swingPointDetector.GetSwingPointAtIndex(firstCandleIndex);
        if (swingPoint == null)
            return null;
            
        // Skip if we've already processed this index
        if (_processedIndices.Contains(firstCandleIndex))
            return null;
            
        var firstCandleBar = CandleManager.GetCandle(firstCandleIndex);
        
        // Determine order block direction based on FVG direction
        // For bullish FVG, the first candle should be a swing low (becomes bullish order block)
        // For bearish FVG, the first candle should be a swing high (becomes bearish order block)
        Direction orderBlockDirection;
        
        if (fvg.Direction == Direction.Up && swingPoint.Direction == Direction.Down)
        {
            // Bullish FVG with swing low at first candle = Bullish Order Block
            orderBlockDirection = Direction.Up;
        }
        else if (fvg.Direction == Direction.Down && swingPoint.Direction == Direction.Up)
        {
            // Bearish FVG with swing high at first candle = Bearish Order Block
            orderBlockDirection = Direction.Down;
        }
        else
        {
            // Direction mismatch - not a valid order block
            return null;
        }
            
        // Create the order block from the first candle
        var orderBlock = CreateOrderBlock(firstCandleBar, firstCandleIndex, orderBlockDirection);
        
        // Mark this index as processed
        _processedIndices.Add(firstCandleIndex);
        
        return orderBlock;
    }
        
        
        
    private Level CreateOrderBlock(Candle candle, int index, Direction direction)
    {
        var orderBlock = new Level(
            LevelType.OrderBlock,
            candle.Low,  // Use the full candle low
            candle.High, // Use the full candle high
            candle.Time,
            candle.Time.AddMinutes(Constants.Time.LevelExtensionMinutes),
            candle.Time,
            direction,
            index,
            index,
            index
        );
        
        // Set TimeFrame from candle
        orderBlock.TimeFrame = candle.TimeFrame;
            
        // Initialize quadrants
        orderBlock.InitializeQuadrants();
            
        return orderBlock;
    }
        
    protected override bool PostDetectionValidation(Level orderBlock, int currentIndex)
    {
        if (!base.PostDetectionValidation(orderBlock, currentIndex))
            return false;
            
        // Check if we already have this order block
        bool isDuplicate = Repository.Any(existing =>
            existing.Index == orderBlock.Index &&
            existing.Direction == orderBlock.Direction &&
            existing.LevelType == LevelType.OrderBlock);
            
        return !isDuplicate;
    }
        
    protected override void PublishDetectionEvent(Level orderBlock, int currentIndex)
    {
        // Publish order block detected event
        EventAggregator.Publish(new OrderBlockDetectedEvent(orderBlock));
            
        // Draw the order block if visualization is enabled
        if (Settings.Patterns.ShowOrderBlock && _visualizer != null)
        {
            _visualizer.Draw(orderBlock);
        }
    }
        
    protected override void LogDetection(Level orderBlock, int currentIndex)
    {
        if (Settings.Notifications.EnableLog)
        {
            Logger($"Order Block detected: {orderBlock.Direction} at index {currentIndex}, " +
                   $"Range: {orderBlock.Low:F5} - {orderBlock.High:F5}");
        }
    }
        
    public override List<Level> GetByDirection(Direction direction)
    {
        return Repository.Find(ob => 
            ob.Direction == direction && 
            ob.LevelType == LevelType.OrderBlock);
    }
        
    public override bool IsValid(Level orderBlock, int currentIndex)
    {
        // An order block is valid until it's been mitigated
        // Check if price has moved through the order block
        if (currentIndex >= orderBlock.Index && currentIndex < CandleManager.Count)
        {
            for (int i = orderBlock.Index + 1; i <= currentIndex; i++)
            {
                var bar = CandleManager.GetCandle(i);
                    
                // For bullish order block, check if price has moved below and closed below
                if (orderBlock.Direction == Direction.Up)
                {
                    if (bar.Close < orderBlock.Low)
                    {
                        return false; // Order block has been mitigated
                    }
                }
                // For bearish order block, check if price has moved above and closed above
                else if (orderBlock.Direction == Direction.Down)
                {
                    if (bar.Close > orderBlock.High)
                    {
                        return false; // Order block has been mitigated
                    }
                }
            }
        }
            
        return true;
    }
        
    protected override void SubscribeToEvents()
    {
        // Subscribe to FVG events as order blocks often form with FVGs
        EventAggregator.Subscribe<FvgDetectedEvent>(OnFvgDetected);
            
        // Subscribe to swing point events
        EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        EventAggregator.Subscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
    }
        
    protected override void UnsubscribeFromEvents()
    {
        EventAggregator.Unsubscribe<FvgDetectedEvent>(OnFvgDetected);
        EventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        EventAggregator.Unsubscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
    }
        
    private void OnFvgDetected(FvgDetectedEvent evt)
    {
        // When an FVG is detected, check if the first candle is a swing point
        // If it is, create an order block from that candle
        if (evt.FvgLevel != null && IsValidBarIndex(evt.FvgLevel.Index))
        {
            var orderBlock = CheckForOrderBlockFromFvg(evt.FvgLevel, evt.Index);
            if (orderBlock != null && PostDetectionValidation(orderBlock, evt.Index))
            {
                Repository.Add(orderBlock);
                PublishDetectionEvent(orderBlock, evt.Index);
                LogDetection(orderBlock, evt.Index);
            }
        }
    }
        
    private void OnSwingPointDetected(SwingPointDetectedEvent evt)
    {
        // Could trigger order block detection around new swing points
    }
        
    private void OnSwingPointRemoved(SwingPointRemovedEvent evt)
    {
        // If a swing point is removed, we might need to invalidate related order blocks
        var affectedOrderBlocks = Repository.Find(ob => 
            ob.Index == evt.SwingPoint.Index);
            
        foreach (var orderBlock in affectedOrderBlocks)
        {
            Repository.Remove(orderBlock);
            _visualizer?.Remove(orderBlock);
            _processedIndices.Remove(orderBlock.Index);
        }
    }
        
    protected override void OnDispose()
    {
        base.OnDispose();
        _processedIndices.Clear();
    }
}