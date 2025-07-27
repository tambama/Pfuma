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

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects CISD (Consecutive Identical Sequential Direction) patterns
    /// </summary>
    public class CisdDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly int _maxCisdsPerDirection;
        
        public CisdDetector(
            Chart chart,
            Bars bars,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, bars, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _maxCisdsPerDirection = settings.Patterns.MaxCisdsPerDirection;
        }
        
        protected override List<Level> PerformDetection(Bars bars, int currentIndex)
        {
            // CISD detection is triggered by order flow events
            return new List<Level>();
        }
        
        /// <summary>
        /// Detects CISD from an orderflow that swept liquidity
        /// </summary>
        public void DetectCisdFromOrderFlow(Level orderflow)
        {
            if (orderflow == null || orderflow.SweptSwingPoint == null)
                return;
            
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);
            
            if (startIndex < 0 || endIndex < 0 || startIndex >= Bars.Count || endIndex >= Bars.Count)
                return;
            
            Level cisdLevel = null;
            
            if (orderflow.Direction == Direction.Up)
            {
                cisdLevel = DetectBearishCisd(orderflow, startIndex, endIndex);
            }
            else
            {
                cisdLevel = DetectBullishCisd(orderflow, startIndex, endIndex);
            }
            
            if (cisdLevel != null)
            {
                // Associate with orderflow
                orderflow.CISDLevel = cisdLevel;
                
                // Manage max CISDs before adding
                ManageMaxCisdCount(cisdLevel.Direction);
                
                // Store and publish
                Repository.Add(cisdLevel);
                PublishDetectionEvent(cisdLevel, endIndex);
            }
        }
        
        private Level DetectBearishCisd(Level orderflow, int startIndex, int endIndex)
        {
            // Find all sets of consecutive bullish candles within the orderflow
            List<List<int>> bullishSets = new List<List<int>>();
            List<int> currentSet = new List<int>();
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                var bar = Bars[i];
                var direction = bar.GetCandleDirection();
                
                if (direction == Direction.Up)
                {
                    currentSet.Add(i);
                }
                else if (currentSet.Count > 0)
                {
                    bullishSets.Add(new List<int>(currentSet));
                    currentSet.Clear();
                }
            }
            
            if (currentSet.Count > 0)
            {
                bullishSets.Add(new List<int>(currentSet));
            }
            
            if (bullishSets.Count == 0)
                return null;
            
            // Use the last set of consecutive bullish candles
            var lastBullishSet = bullishSets[bullishSets.Count - 1];
            
            if (lastBullishSet.Count == 0)
                return null;
            
            int firstBullishIndex = lastBullishSet.Min();
            int lastBullishIndex = lastBullishSet.Max();
            
            // Create a BEARISH CISD level
            var cisdLevel = new Level(
                LevelType.CISD,
                Bars[firstBullishIndex].Open,
                Bars[lastBullishIndex].Close,
                Bars[firstBullishIndex].OpenTime,
                Bars[lastBullishIndex].OpenTime,
                null,
                Direction.Down,
                firstBullishIndex,
                lastBullishIndex,
                firstBullishIndex
            );
            
            return cisdLevel;
        }
        
        private Level DetectBullishCisd(Level orderflow, int startIndex, int endIndex)
        {
            // Find all sets of consecutive bearish candles within the orderflow
            List<List<int>> bearishSets = new List<List<int>>();
            List<int> currentSet = new List<int>();
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                var bar = Bars[i];
                var direction = bar.GetCandleDirection();
                
                if (direction == Direction.Down)
                {
                    currentSet.Add(i);
                }
                else if (currentSet.Count > 0)
                {
                    bearishSets.Add(new List<int>(currentSet));
                    currentSet.Clear();
                }
            }
            
            if (currentSet.Count > 0)
            {
                bearishSets.Add(new List<int>(currentSet));
            }
            
            if (bearishSets.Count == 0)
                return null;
            
            // Use the last set of consecutive bearish candles
            var lastBearishSet = bearishSets[bearishSets.Count - 1];
            
            if (lastBearishSet.Count == 0)
                return null;
            
            int firstBearishIndex = lastBearishSet.Min();
            int lastBearishIndex = lastBearishSet.Max();
            
            // Create a BULLISH CISD level
            var cisdLevel = new Level(
                LevelType.CISD,
                Bars[lastBearishIndex].Close,
                Bars[firstBearishIndex].Open,
                Bars[lastBearishIndex].OpenTime,
                Bars[firstBearishIndex].OpenTime,
                null,
                Direction.Up,
                firstBearishIndex,
                firstBearishIndex,
                lastBearishIndex
            );
            
            return cisdLevel;
        }
        
        /// <summary>
        /// Checks for CISD confirmation on a swing point
        /// </summary>
        public void CheckCisdConfirmation(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
            
            var pendingCisdLevels = Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && !cisd.IsConfirmed);
            
            foreach (var cisd in pendingCisdLevels)
            {
                bool isConfirmed = false;
                
                if (cisd.Direction == Direction.Up) // Bullish CISD
                {
                    // Bullish CISD is confirmed when a bullish candle closes above the CISD high
                    if (swingPoint.CandleDirection == Direction.Up && 
                        swingPoint.Bar.Open < cisd.High &&
                        swingPoint.Bar.Close > cisd.High)
                    {
                        isConfirmed = true;
                    }
                }
                else // Bearish CISD
                {
                    // Bearish CISD is confirmed when a bearish candle closes below the CISD low
                    if (swingPoint.CandleDirection == Direction.Down && 
                        swingPoint.Bar.Open > cisd.Low &&
                        swingPoint.Bar.Close < cisd.Low)
                    {
                        isConfirmed = true;
                    }
                }
                
                if (isConfirmed)
                {
                    cisd.IsConfirmed = true;
                    cisd.IndexOfConfirmingCandle = swingPoint.Index;
                    
                    // Publish confirmation event
                    EventAggregator.Publish(new CisdConfirmedEvent(cisd, cisd.Direction));
                    
                    // Update visualization
                    _visualizer?.Update(cisd);
                }
            }
        }
        
        /// <summary>
        /// Checks for CISD activation on a bar
        /// </summary>
        public void CheckCisdActivation(Bar bar, int barIndex)
        {
            if (bar == null)
                return;
            
            var confirmedCisdLevels = Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && cisd.IsConfirmed && !cisd.Activated);
            
            foreach (var cisd in confirmedCisdLevels)
            {
                bool isActivated = false;
                
                if (cisd.Direction == Direction.Up) // Bullish CISD
                {
                    // Activated when price moves below the CISD level
                    if (bar.Open > cisd.High && bar.Low < cisd.High)
                    {
                        isActivated = true;
                    }
                }
                else // Bearish CISD
                {
                    // Activated when price moves above the CISD level
                    if (bar.Open < cisd.Low && bar.High > cisd.Low)
                    {
                        isActivated = true;
                    }
                }
                
                if (isActivated)
                {
                    cisd.Activated = true;
                    
                    // Publish activation event
                    EventAggregator.Publish(new CisdActivatedEvent(cisd, barIndex));
                    
                    // Update visualization
                    _visualizer?.Update(cisd);
                }
            }
        }
        
        private void ManageMaxCisdCount(Direction direction)
        {
            var unconfirmedCisds = Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && 
                cisd.Direction == direction && 
                !cisd.IsConfirmed)
                .OrderBy(cisd => cisd.Index)
                .ToList();
            
            while (unconfirmedCisds.Count >= _maxCisdsPerDirection && unconfirmedCisds.Count > 0)
            {
                var oldestCisd = unconfirmedCisds.First();
                Repository.Remove(oldestCisd);
                _visualizer?.Remove(oldestCisd);
                unconfirmedCisds.Remove(oldestCisd);
            }
        }
        
        protected override void PublishDetectionEvent(Level cisd, int currentIndex)
        {
            EventAggregator.Publish(new CisdDetectedEvent(cisd));
            
            if (Settings.Patterns.ShowCISD && _visualizer != null)
            {
                _visualizer.Draw(cisd);
            }
        }
        
        protected override void LogDetection(Level cisd, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"CISD detected: {cisd.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(cisd => 
                cisd.LevelType == LevelType.CISD && 
                cisd.Direction == direction);
        }
        
        public override bool IsValid(Level cisd, int currentIndex)
        {
            return cisd != null && 
                   cisd.LevelType == LevelType.CISD && 
                   (!cisd.Activated || cisd.Index < currentIndex);
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
            EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
            EventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        private void OnOrderFlowDetected(OrderFlowDetectedEvent evt)
        {
            if (evt.OrderFlow != null && evt.OrderFlow.SweptSwingPoint != null)
            {
                DetectCisdFromOrderFlow(evt.OrderFlow);
            }
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            CheckCisdConfirmation(evt.SwingPoint);
        }
    }
}