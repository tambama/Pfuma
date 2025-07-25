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
        Bars bars,
        IEventAggregator eventAggregator,
        IRepository<Level> repository,
        IVisualization<Level> visualizer,
        SwingPointDetector swingPointDetector,
        IndicatorSettings settings,
        Action<string> logger = null)
        : base(chart, bars, eventAggregator, repository, settings, logger)
    {
        _visualizer = visualizer;
        _swingPointDetector = swingPointDetector;
        _processedIndices = new HashSet<int>();
    }
        
    protected override int GetMinimumBarsRequired()
    {
        return Constants.Patterns.OrderBlockLookback;
    }
        
    protected override List<Level> PerformDetection(Bars bars, int currentIndex)
    {
        var detectedOrderBlocks = new List<Level>();
            
        // Need swing point detector and sufficient bars
        if (_swingPointDetector == null || currentIndex < Constants.Patterns.OrderBlockLookback)
            return detectedOrderBlocks;
            
        // Check if there's an FVG at the current position (would come from event)
        // For now, we'll check the previous bars for order block candidates
            
        // Look for potential order blocks in the last few bars
        for (int i = Math.Max(0, currentIndex - Constants.Patterns.OrderBlockLookback); i < currentIndex; i++)
        {
            // Skip if we've already processed this index
            if (_processedIndices.Contains(i))
                continue;
                
            var orderBlock = CheckForOrderBlock(bars, i, currentIndex);
            if (orderBlock != null)
            {
                detectedOrderBlocks.Add(orderBlock);
                _processedIndices.Add(i);
            }
        }
            
        return detectedOrderBlocks;
    }
        
    private Level CheckForOrderBlock(Bars bars, int candidateIndex, int currentIndex)
    {
        // Get swing point at the candidate index
        var swingPoint = _swingPointDetector.GetSwingPointAtIndex(candidateIndex);
        if (swingPoint == null)
            return null;
            
        var candidateBar = bars[candidateIndex];
            
        // For bullish order block, we need a swing low
        if (swingPoint.Direction == Direction.Down)
        {
            // Check if this swing low meets order block criteria
            if (IsValidBullishOrderBlock(swingPoint, candidateIndex, currentIndex))
            {
                return CreateOrderBlock(candidateBar, candidateIndex, Direction.Up);
            }
        }
        // For bearish order block, we need a swing high
        else if (swingPoint.Direction == Direction.Up)
        {
            // Check if this swing high meets order block criteria
            if (IsValidBearishOrderBlock(swingPoint, candidateIndex, currentIndex))
            {
                return CreateOrderBlock(candidateBar, candidateIndex, Direction.Down);
            }
        }
            
        return null;
    }
        
    private bool IsValidBullishOrderBlock(SwingPoint swingLow, int candidateIndex, int currentIndex)
    {
        // Get previous swing points
        var previousSwingPoints = _swingPointDetector.GetAllSwingPoints()
            .Where(sp => sp.Index < candidateIndex)
            .OrderByDescending(sp => sp.Index)
            .ToList();
            
        if (previousSwingPoints.Count == 0)
            return false;
            
        // Find the most recent swing high before this low
        var lastSwingHigh = previousSwingPoints
            .FirstOrDefault(sp => sp.Direction == Direction.Up);
            
        if (lastSwingHigh == null)
            return false;
            
        // Check if we've swept a previous swing low
        var previousSwingLow = previousSwingPoints
            .FirstOrDefault(sp => sp.Direction == Direction.Down);
            
        if (previousSwingLow != null)
        {
            var candidateBar = Bars[candidateIndex];
            bool sweptPreviousLow = candidateBar.Low <= previousSwingLow.Price &&
                                    candidateBar.Close > previousSwingLow.Price;
                
            // Valid if we swept a previous low OR came after a swing high
            return sweptPreviousLow || lastSwingHigh.Index < swingLow.Index;
        }
            
        // If no previous low, just check if we're after a high
        return lastSwingHigh.Index < swingLow.Index;
    }
        
    private bool IsValidBearishOrderBlock(SwingPoint swingHigh, int candidateIndex, int currentIndex)
    {
        // Get previous swing points
        var previousSwingPoints = _swingPointDetector.GetAllSwingPoints()
            .Where(sp => sp.Index < candidateIndex)
            .OrderByDescending(sp => sp.Index)
            .ToList();
            
        if (previousSwingPoints.Count == 0)
            return false;
            
        // Find the most recent swing low before this high
        var lastSwingLow = previousSwingPoints
            .FirstOrDefault(sp => sp.Direction == Direction.Down);
            
        if (lastSwingLow == null)
            return false;
            
        // Check if we've swept a previous swing high
        var previousSwingHigh = previousSwingPoints
            .FirstOrDefault(sp => sp.Direction == Direction.Up);
            
        if (previousSwingHigh != null)
        {
            var candidateBar = Bars[candidateIndex];
            bool sweptPreviousHigh = candidateBar.High >= previousSwingHigh.Price &&
                                     candidateBar.Close < previousSwingHigh.Price;
                
            // Valid if we swept a previous high OR came after a swing low
            return sweptPreviousHigh || lastSwingLow.Index < swingHigh.Index;
        }
            
        // If no previous high, just check if we're after a low
        return lastSwingLow.Index < swingHigh.Index;
    }
        
    private Level CreateOrderBlock(Bar bar, int index, Direction direction)
    {
        var orderBlock = new Level(
            LevelType.OrderBlock,
            bar.Low,  // Use the full candle low
            bar.High, // Use the full candle high
            bar.OpenTime,
            bar.OpenTime.AddMinutes(Constants.Time.LevelExtensionMinutes),
            bar.OpenTime,
            direction,
            index,
            index,
            index
        );
            
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
        if (currentIndex >= orderBlock.Index && currentIndex < Bars.Count)
        {
            for (int i = orderBlock.Index + 1; i <= currentIndex; i++)
            {
                var bar = Bars[i];
                    
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
        // When an FVG is detected, check if the first candle is an order block
        // This would trigger additional order block detection logic
        if (evt.FvgLevel != null && IsValidBarIndex(evt.FvgLevel.Index))
        {
            var orderBlock = CheckForOrderBlock(Bars, evt.FvgLevel.Index, evt.Index);
            if (orderBlock != null && PostDetectionValidation(orderBlock, evt.Index))
            {
                Repository.Add(orderBlock);
                PublishDetectionEvent(orderBlock, evt.Index);
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