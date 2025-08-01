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

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects Gauntlet patterns (FVG where the sweeping candle is part of the FVG)
    /// </summary>
    public class GauntletDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly IRepository<Level> _fvgRepository;
        private readonly TimeManager _timeManager;
        
        public GauntletDetector(
            Chart chart,
            Bars bars,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IRepository<Level> fvgRepository,
            IVisualization<Level> visualizer,
            TimeManager timeManager,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, bars, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _fvgRepository = fvgRepository;
            _timeManager = timeManager;
        }
        
        protected override List<Level> PerformDetection(Bars bars, int currentIndex)
        {
            // Gauntlet detection is triggered by order flow events
            return new List<Level>();
        }
        
        /// <summary>
        /// Checks if an order flow with swept liquidity forms a Gauntlet pattern
        /// </summary>
        public void CheckForGauntlet(Level orderflow, int sweepingCandleIndex)
        {
            // Skip if index is invalid
            if (sweepingCandleIndex < 1 || sweepingCandleIndex >= Bars.Count)
                return;
            
            // Skip if the orderflow doesn't have swept liquidity
            if (orderflow.SweptSwingPoint == null)
                return;
            
            // Get all FVGs
            var fvgs = _fvgRepository.Find(l => l.LevelType == LevelType.FairValueGap);
            if (!fvgs.Any())
                return;
            
            // Get the time of the sweeping candle for macro time check
            DateTime sweepingCandleTime = Bars[sweepingCandleIndex].OpenTime;
            
            // Check if we're filtering by macro time and if this candle is in a macro period
            bool isInMacro = !Settings.Time.MacroFilter || 
                           (_timeManager != null && _timeManager.IsInMacroTime(sweepingCandleTime));
            
            // Skip processing if we're filtering by macro and not in a macro time
            if (Settings.Time.MacroFilter && !isInMacro)
                return;
            
            // Find the last FVG within the orderflow
            var lastFvgInOrderflow = FindLastFVGInOrderflow(orderflow, fvgs);
            
            // If we found a matching FVG, check if its second or third candle is the sweeping candle
            if (lastFvgInOrderflow != null)
            {
                bool isSweepingCandlePartOfFVG = false;
                
                if (orderflow.Direction == Direction.Up)
                {
                    // For bullish FVGs, check if the sweeping candle matches either the middle or high candle
                    isSweepingCandlePartOfFVG =
                        (lastFvgInOrderflow.IndexMid == sweepingCandleIndex) ||
                        (lastFvgInOrderflow.IndexHigh == sweepingCandleIndex);
                }
                else
                {
                    // For bearish FVGs, check if the sweeping candle matches either the middle or low candle
                    isSweepingCandlePartOfFVG =
                        (lastFvgInOrderflow.IndexMid == sweepingCandleIndex) ||
                        (lastFvgInOrderflow.IndexLow == sweepingCandleIndex);
                }
                
                // If the sweeping candle is part of the FVG, this is a Gauntlet
                if (isSweepingCandlePartOfFVG)
                {
                    // Create a Gauntlet level
                    var gauntlet = new Level(
                        LevelType.Gauntlet,
                        lastFvgInOrderflow.Low,
                        lastFvgInOrderflow.High,
                        lastFvgInOrderflow.LowTime,
                        lastFvgInOrderflow.HighTime,
                        null,
                        orderflow.Direction,
                        lastFvgInOrderflow.IndexLow,
                        lastFvgInOrderflow.IndexHigh, 
                        lastFvgInOrderflow.IndexLow,
                        lastFvgInOrderflow.IndexMid
                    )
                    {
                        SweptSwingPoint = orderflow.SweptSwingPoint,
                        IndexOfSweepingCandle = sweepingCandleIndex
                    };
                    
                    // Store and publish
                    Repository.Add(gauntlet);
                    PublishDetectionEvent(gauntlet, sweepingCandleIndex);
                }
            }
        }
        
        /// <summary>
        /// Finds the last FVG within an orderflow's range
        /// </summary>
        private Level FindLastFVGInOrderflow(Level orderflow, List<Level> fvgs)
        {
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);
            
            // Find FVGs within the orderflow range
            var fvgsInRange = fvgs.Where(fvg =>
            {
                int fvgStartIndex = Math.Min(fvg.IndexLow, fvg.IndexHigh);
                int fvgEndIndex = Math.Max(fvg.IndexLow, fvg.IndexHigh);
                
                // Check if FVG is within orderflow range
                return fvgStartIndex >= startIndex && fvgEndIndex <= endIndex;
            }).ToList();
            
            if (!fvgsInRange.Any())
                return null;
            
            // Return the last FVG in the orderflow based on direction
            if (orderflow.Direction == Direction.Up)
            {
                return fvgsInRange.OrderByDescending(f => f.IndexHigh).FirstOrDefault();
            }
            else
            {
                return fvgsInRange.OrderBy(f => f.IndexLow).FirstOrDefault();
            }
        }
        
        protected override void PublishDetectionEvent(Level gauntlet, int currentIndex)
        {
            EventAggregator.Publish(new GauntletDetectedEvent(gauntlet, gauntlet.Direction));
            
            if (Settings.Patterns.ShowGauntlet && _visualizer != null)
            {
                _visualizer.Draw(gauntlet);
            }
        }
        
        protected override void LogDetection(Level gauntlet, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Gauntlet detected: {gauntlet.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(g => 
                g.LevelType == LevelType.Gauntlet && 
                g.Direction == direction);
        }
        
        public override bool IsValid(Level gauntlet, int currentIndex)
        {
            return gauntlet != null && 
                   gauntlet.LevelType == LevelType.Gauntlet;
        }
        
        public Level GetLastGauntlet(Direction direction)
        {
            return GetByDirection(direction)
                .OrderByDescending(g => g.Index)
                .FirstOrDefault();
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
        }
        
        private void OnOrderFlowDetected(OrderFlowDetectedEvent evt)
        {
            // When an order flow is detected, check if it forms a gauntlet
            if (evt.OrderFlow != null && evt.OrderFlow.IndexOfSweepingCandle > 0)
            {
                CheckForGauntlet(evt.OrderFlow, evt.OrderFlow.IndexOfSweepingCandle);
            }
        }
    }
}