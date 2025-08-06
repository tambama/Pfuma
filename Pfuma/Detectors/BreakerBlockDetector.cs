using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors.Base;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects Breaker Blocks associated with confirmed CISD patterns
    /// </summary>
    public class BreakerBlockDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly IRepository<Level> _orderFlowRepository;
        
        public BreakerBlockDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IRepository<Level> orderFlowRepository,
            IVisualization<Level> visualizer,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _orderFlowRepository = orderFlowRepository;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            // Breaker block detection is triggered by CISD confirmation events
            return new List<Level>();
        }
        
        /// <summary>
        /// Finds and creates a breaker block for a confirmed CISD
        /// </summary>
        public void FindBreakerBlockForCisd(Level cisd)
        {
            if (cisd == null || !cisd.IsConfirmed || cisd.BreakerBlock != null)
                return;
            
            Level breakerBlock = null;
            
            if (cisd.Direction == Direction.Up) // Bullish CISD
            {
                breakerBlock = FindBullishBreakerBlock(cisd);
            }
            else // Bearish CISD
            {
                breakerBlock = FindBearishBreakerBlock(cisd);
            }
            
            if (breakerBlock != null && ValidateBreakerBlock(breakerBlock, cisd))
            {
                cisd.BreakerBlock = breakerBlock;
                
                // Store and publish
                Repository.Add(breakerBlock);
                PublishDetectionEvent(breakerBlock, cisd.IndexOfConfirmingCandle);
            }
        }
        
        private Level FindBullishBreakerBlock(Level cisd)
        {
            // Find the previous bullish orderflow
            var previousBullishOrderflow = _orderFlowRepository
                .Find(p => p.LevelType == LevelType.Orderflow &&
                           p.Direction == Direction.Up && 
                           p.Index < cisd.Index)
                .OrderByDescending(p => p.Index)
                .FirstOrDefault();
            
            if (previousBullishOrderflow == null)
                return null;
            
            // Find the last set of consecutive bullish candles in this orderflow
            var lastConsecutiveBullishCandles = FindLastConsecutiveCandlesInOrderflow(
                previousBullishOrderflow, Direction.Up);
            
            if (lastConsecutiveBullishCandles.Count == 0)
                return null;
            
            int firstBullishCandleIndex = lastConsecutiveBullishCandles.First();
            int lastBullishCandleIndex = lastConsecutiveBullishCandles.Last();
            
            Candle firstBullishCandle = CandleManager.GetCandle(firstBullishCandleIndex);
            Candle lastBullishCandle = CandleManager.GetCandle(lastBullishCandleIndex);
            
            // Create a bullish breaker block
            var breakerBlock = new Level(
                LevelType.BreakerBlock,
                firstBullishCandle.Low,
                lastBullishCandle.High,
                firstBullishCandle.Time,
                lastBullishCandle.Time,
                null,
                Direction.Up,
                firstBullishCandleIndex,
                lastBullishCandleIndex,
                firstBullishCandleIndex
            );
            
            // Set TimeFrame from candle
            breakerBlock.TimeFrame = firstBullishCandle.TimeFrame;
            
            return breakerBlock;
        }
        
        private Level FindBearishBreakerBlock(Level cisd)
        {
            // Find the previous bearish orderflow
            var previousBearishOrderflow = _orderFlowRepository
                .Find(p => p.LevelType == LevelType.Orderflow &&
                           p.Direction == Direction.Down && 
                           p.Index < cisd.Index)
                .OrderByDescending(p => p.Index)
                .FirstOrDefault();
            
            if (previousBearishOrderflow == null)
                return null;
            
            // Find the last set of consecutive bearish candles in this orderflow
            var lastConsecutiveBearishCandles = FindLastConsecutiveCandlesInOrderflow(
                previousBearishOrderflow, Direction.Down);
            
            if (lastConsecutiveBearishCandles.Count == 0)
                return null;
            
            int firstBearishCandleIndex = lastConsecutiveBearishCandles.First();
            int lastBearishCandleIndex = lastConsecutiveBearishCandles.Last();
            
            Candle firstBearishCandle = CandleManager.GetCandle(firstBearishCandleIndex);
            Candle lastBearishCandle = CandleManager.GetCandle(lastBearishCandleIndex);
            
            // Create a bearish breaker block
            var breakerBlock = new Level(
                LevelType.BreakerBlock,
                lastBearishCandle.Low,
                firstBearishCandle.High,
                lastBearishCandle.Time,
                firstBearishCandle.Time,
                null,
                Direction.Down,
                firstBearishCandleIndex,
                firstBearishCandleIndex,
                lastBearishCandleIndex
            );
            
            // Set TimeFrame from candle
            breakerBlock.TimeFrame = firstBearishCandle.TimeFrame;
            
            return breakerBlock;
        }
        
        private List<int> FindLastConsecutiveCandlesInOrderflow(Level orderflow, Direction direction)
        {
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);
            
            if (!IsValidBarIndex(startIndex) || !IsValidBarIndex(endIndex))
                return new List<int>();
            
            List<int> lastConsecutiveCandles = new List<int>();
            bool foundFirstMatchingCandle = false;
            
            // Start from the end and work backward
            for (int i = endIndex; i >= startIndex; i--)
            {
                var bar = CandleManager.GetCandle(i);
                var barDirection = bar.GetCandleDirection();
                
                if (barDirection == direction)
                {
                    lastConsecutiveCandles.Insert(0, i);
                    foundFirstMatchingCandle = true;
                }
                else
                {
                    if (!foundFirstMatchingCandle)
                        continue;
                    
                    // Once we hit a candle of the opposite direction AFTER finding matching candles, we break
                    break;
                }
            }
            
            return lastConsecutiveCandles;
        }
        
        private bool ValidateBreakerBlock(Level breakerBlock, Level cisd)
        {
            if (breakerBlock == null || cisd == null)
                return false;
            
            // For bullish breaker blocks, ensure the low is not higher than the CISD price
            if (breakerBlock.Direction == Direction.Up)
            {
                return breakerBlock.Low <= cisd.High;
            }
            
            // For bearish breaker blocks, ensure the high is not lower than the CISD price
            if (breakerBlock.Direction == Direction.Down)
            {
                return breakerBlock.High >= cisd.Low;
            }
            
            return false;
        }
        
        protected override void PublishDetectionEvent(Level breakerBlock, int currentIndex)
        {
            EventAggregator.Publish(new BreakerBlockDetectedEvent(breakerBlock));
            
            if (Settings.Patterns.ShowBreakerBlock && _visualizer != null)
            {
                _visualizer.Draw(breakerBlock);
            }
        }
        
        protected override void LogDetection(Level breakerBlock, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Breaker Block detected: {breakerBlock.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(bb => 
                bb.LevelType == LevelType.BreakerBlock && 
                bb.Direction == direction);
        }
        
        public override bool IsValid(Level breakerBlock, int currentIndex)
        {
            return breakerBlock != null && 
                   breakerBlock.LevelType == LevelType.BreakerBlock;
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<CisdConfirmedEvent>(OnCisdConfirmed);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<CisdConfirmedEvent>(OnCisdConfirmed);
        }
        
        private void OnCisdConfirmed(CisdConfirmedEvent evt)
        {
            FindBreakerBlockForCisd(evt.CisdLevel);
        }
    }
}