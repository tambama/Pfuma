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
/// Detects Rejection Blocks based on swing point analysis
/// </summary>
public class RejectionBlockDetector : BasePatternDetector<Level>
{
    private readonly IVisualization<Level> _visualizer;
    private readonly SwingPointDetector _swingPointDetector;
    private readonly HashSet<int> _processedIndices;
        
    public RejectionBlockDetector(
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
        return Constants.Patterns.RejectionBlockLookback;
    }
        
    protected override List<Level> PerformDetection(int currentIndex)
    {
        // Rejection blocks are only detected through FVG events, not through regular scanning
        // This method returns empty as detection happens in OnFvgDetected event handler
        return new List<Level>();
    }
        
    private Level CheckForRejectionBlockFromFvg(Level fvg, int currentIndex)
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
        // For bullish FVG, the first candle should be a swing low (becomes bullish rejection block)
        // For bearish FVG, the first candle should be a swing high (becomes bearish rejection block)
        Direction rejectionBlockDirection;
        
        if (fvg.Direction == Direction.Up && swingPoint.Direction == Direction.Down)
        {
            // Bullish FVG with swing low at first candle = Bullish Rejection Block
            rejectionBlockDirection = Direction.Up;
        }
        else if (fvg.Direction == Direction.Down && swingPoint.Direction == Direction.Up)
        {
            // Bearish FVG with swing high at first candle = Bearish Rejection Block
            rejectionBlockDirection = Direction.Down;
        }
        else
        {
            // Direction mismatch - not a valid rejection block
            return null;
        }
            
        // Create the rejection block from the first candle
        var rejectionBlock = CreateRejectionBlock(firstCandleBar, firstCandleIndex, rejectionBlockDirection);
        
        // Mark this index as processed
        _processedIndices.Add(firstCandleIndex);
        
        return rejectionBlock;
    }
        
    private Level CreateRejectionBlock(Candle candle, int index, Direction direction)
    {
        var rejectionBlock = new Level(
            LevelType.RejectionBlock,
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
        rejectionBlock.TimeFrame = candle.TimeFrame;
            
        // Initialize quadrants
        rejectionBlock.InitializeQuadrants();
            
        return rejectionBlock;
    }
        
    protected override bool PostDetectionValidation(Level rejectionBlock, int currentIndex)
    {
        if (!base.PostDetectionValidation(rejectionBlock, currentIndex))
            return false;
            
        // Check if we already have this rejection block
        bool isDuplicate = Repository.Any(existing =>
            existing.Index == rejectionBlock.Index &&
            existing.Direction == rejectionBlock.Direction &&
            existing.LevelType == LevelType.RejectionBlock);
            
        return !isDuplicate;
    }
        
    protected override void PublishDetectionEvent(Level rejectionBlock, int currentIndex)
    {
        // Publish rejection block detected event
        EventAggregator.Publish(new RejectionBlockDetectedEvent(rejectionBlock));
            
        // Draw the rejection block if visualization is enabled
        if (Settings.Patterns.ShowRejectionBlock && _visualizer != null)
        {
            _visualizer.Draw(rejectionBlock);
        }
    }
        
    protected override void LogDetection(Level rejectionBlock, int currentIndex)
    {
        if (Settings.Notifications.EnableLog)
        {
            Logger($"Rejection Block detected: {rejectionBlock.Direction} at index {currentIndex}, " +
                   $"Range: {rejectionBlock.Low:F5} - {rejectionBlock.High:F5}");
        }
    }
        
    public override List<Level> GetByDirection(Direction direction)
    {
        return Repository.Find(ob => 
            ob.Direction == direction && 
            ob.LevelType == LevelType.RejectionBlock);
    }
        
    public override bool IsValid(Level rejectionBlock, int currentIndex)
    {
        // A rejection block is valid until it's been mitigated
        // Check if price has moved through the rejection block
        if (currentIndex >= rejectionBlock.Index && currentIndex < CandleManager.Count)
        {
            for (int i = rejectionBlock.Index + 1; i <= currentIndex; i++)
            {
                var bar = CandleManager.GetCandle(i);
                    
                // For bullish rejection block, check if price has moved below and closed below
                if (rejectionBlock.Direction == Direction.Up)
                {
                    if (bar.Close < rejectionBlock.Low)
                    {
                        return false; // Rejection block has been mitigated
                    }
                }
                // For bearish rejection block, check if price has moved above and closed above
                else if (rejectionBlock.Direction == Direction.Down)
                {
                    if (bar.Close > rejectionBlock.High)
                    {
                        return false; // Rejection block has been mitigated
                    }
                }
            }
        }
            
        return true;
    }
        
    protected override void SubscribeToEvents()
    {
        // Subscribe to FVG events as rejection blocks often form with FVGs
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
        // If it is, create a rejection block from that candle
        if (evt.FvgLevel != null && IsValidBarIndex(evt.FvgLevel.Index))
        {
            var rejectionBlock = CheckForRejectionBlockFromFvg(evt.FvgLevel, evt.Index);
            if (rejectionBlock != null && PostDetectionValidation(rejectionBlock, evt.Index))
            {
                Repository.Add(rejectionBlock);
                PublishDetectionEvent(rejectionBlock, evt.Index);
                LogDetection(rejectionBlock, evt.Index);
            }
        }
    }
        
    private void OnSwingPointDetected(SwingPointDetectedEvent evt)
    {
        // Could trigger rejection block detection around new swing points
    }
        
    private void OnSwingPointRemoved(SwingPointRemovedEvent evt)
    {
        // If a swing point is removed, we might need to invalidate related rejection blocks
        var affectedRejectionBlocks = Repository.Find(ob => 
            ob.Index == evt.SwingPoint.Index);
            
        foreach (var rejectionBlock in affectedRejectionBlocks)
        {
            Repository.Remove(rejectionBlock);
            _visualizer?.Remove(rejectionBlock);
            _processedIndices.Remove(rejectionBlock.Index);
        }
    }
        
    protected override void OnDispose()
    {
        base.OnDispose();
        _processedIndices.Clear();
    }
}