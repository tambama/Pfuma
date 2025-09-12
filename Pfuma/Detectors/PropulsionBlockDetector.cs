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
    /// Detects Propulsion Block patterns when CISD is confirmed
    /// </summary>
    public class PropulsionBlockDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly IRepository<SwingPoint> _swingPointRepository;
        
        public PropulsionBlockDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            IRepository<SwingPoint> swingPointRepository,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _swingPointRepository = swingPointRepository;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            // Propulsion block detection is triggered by CISD confirmation events
            return new List<Level>();
        }
        
        /// <summary>
        /// Detects propulsion block when CISD is confirmed
        /// </summary>
        private void DetectPropulsionBlock(Level cisd)
        {
            if (cisd == null || !cisd.IsConfirmed)
                return;
            
            Level propulsionBlock = null;
            
            if (cisd.Direction == Direction.Up) // Bullish CISD
            {
                propulsionBlock = DetectBullishPropulsionBlock(cisd);
            }
            else // Bearish CISD
            {
                propulsionBlock = DetectBearishPropulsionBlock(cisd);
            }
            
            if (propulsionBlock != null)
            {
                // Mark the CISD as having a propulsion block
                cisd.HasPropulsionBlock = true;
                
                // The propulsion block is already in the repository as an order flow
                // We just changed its type, so publish the event and update visualization
                PublishDetectionEvent(propulsionBlock, cisd.IndexOfConfirmingCandle);
                
                Logger?.Invoke($"Propulsion Block detected for {cisd.Direction} CISD at index {cisd.IndexOfConfirmingCandle}");
            }
        }
        
        private Level DetectBullishPropulsionBlock(Level cisd)
        {
            int currentIndex = cisd.IndexOfConfirmingCandle;
            int lowIndex = cisd.IndexLow;
            
            // Get all swing points between low index and current index (exclusive)
            var swingPointsBetween = _swingPointRepository
                .Find(sp => sp.Index > lowIndex && sp.Index < currentIndex)
                .ToList();
            
            // Need at least 2 swing points
            if (swingPointsBetween.Count < 2)
                return null;
            
            // Get bullish swing points (swing highs) and order by index descending
            var bullishSwingPoints = swingPointsBetween
                .Where(sp => sp.Direction == Direction.Up)
                .OrderByDescending(sp => sp.Index)
                .ToList();
            
            if (!bullishSwingPoints.Any())
                return null;
            
            // Take the first (most recent) bullish swing point
            var previousHigh = bullishSwingPoints.First();
            
            // Find the bearish order flow at this swing point's index
            var bearishOrderFlow = Repository
                .Find(level => 
                    level.LevelType == LevelType.Orderflow && 
                    level.Direction == Direction.Down &&
                    (level.IndexHigh == previousHigh.Index || level.IndexLow == previousHigh.Index ||
                     level.Index == previousHigh.Index))
                .FirstOrDefault();
            
            if (bearishOrderFlow == null)
                return null;
            
            // Mark this bearish order flow as a bullish propulsion block
            // (per requirements: "Call this bearish order flow a bullish propulsion block")
            bearishOrderFlow.LevelType = LevelType.PropulsionBlock;
            bearishOrderFlow.Direction = Direction.Up;
            
            return bearishOrderFlow;
        }
        
        private Level DetectBearishPropulsionBlock(Level cisd)
        {
            int currentIndex = cisd.IndexOfConfirmingCandle;
            int highIndex = cisd.IndexHigh;
            
            // Get all swing points between high index and current index (exclusive)
            var swingPointsBetween = _swingPointRepository
                .Find(sp => sp.Index > highIndex && sp.Index < currentIndex)
                .ToList();
            
            // Need at least 2 swing points
            if (swingPointsBetween.Count < 2)
                return null;
            
            // Get bearish swing points (swing lows) and order by index descending
            var bearishSwingPoints = swingPointsBetween
                .Where(sp => sp.Direction == Direction.Down)
                .OrderByDescending(sp => sp.Index)
                .ToList();
            
            if (!bearishSwingPoints.Any())
                return null;
            
            // Take the first (most recent) bearish swing point
            var previousLow = bearishSwingPoints.First();
            
            // Find the bullish order flow at this swing point's index
            var bullishOrderFlow = Repository
                .Find(level => 
                    level.LevelType == LevelType.Orderflow && 
                    level.Direction == Direction.Up &&
                    (level.IndexHigh == previousLow.Index || level.IndexLow == previousLow.Index ||
                     level.Index == previousLow.Index))
                .FirstOrDefault();
            
            if (bullishOrderFlow == null)
                return null;
            
            // Mark this bullish order flow as a bearish propulsion block
            // (per requirements: "Call this bullish order flow a bearish propulsion block")
            bullishOrderFlow.LevelType = LevelType.PropulsionBlock;
            bullishOrderFlow.Direction = Direction.Down;
            
            return bullishOrderFlow;
        }
        
        protected override void PublishDetectionEvent(Level propulsionBlock, int currentIndex)
        {
            EventAggregator.Publish(new PropulsionBlockDetectedEvent(propulsionBlock));
            
            if (Settings.Patterns.ShowPropulsionBlock && _visualizer != null)
            {
                _visualizer.Draw(propulsionBlock);
            }
        }
        
        protected override void LogDetection(Level propulsionBlock, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Propulsion Block detected: {propulsionBlock.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(pb => 
                pb.LevelType == LevelType.PropulsionBlock && 
                pb.Direction == direction);
        }
        
        public override bool IsValid(Level propulsionBlock, int currentIndex)
        {
            return propulsionBlock != null && 
                   propulsionBlock.LevelType == LevelType.PropulsionBlock && 
                   propulsionBlock.Index < currentIndex;
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
            if (evt.CisdLevel != null && evt.CisdLevel.IsConfirmed)
            {
                DetectPropulsionBlock(evt.CisdLevel);
            }
        }
    }
}