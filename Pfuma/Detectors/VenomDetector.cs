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
    /// Detects Venom patterns (FVGs where candle2 or candle3 swept liquidity or Fibonacci levels)
    /// </summary>
    public class VenomDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        
        public VenomDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IVisualization<Level> visualizer,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            // Venom detection is triggered by FVG detection events
            return new List<Level>();
        }
        
        /// <summary>
        /// Checks if an FVG qualifies as a Venom pattern
        /// </summary>
        public void CheckForVenom(Level fvg)
        {
            if (fvg == null || fvg.LevelType != LevelType.FairValueGap)
                return;
            
            // Get the FVG candles
            var candle2 = CandleManager.GetCandle(fvg.IndexMid);
            var candle3 = fvg.Direction == Direction.Up ? 
                CandleManager.GetCandle(fvg.IndexHigh) : 
                CandleManager.GetCandle(fvg.IndexLow);
            
            if (candle2 == null || candle3 == null)
                return;
            
            // Check if either candle2 or candle3 swept liquidity/fibonacci
            bool candle2HasSweep = candle2.SweptLiquidity > 0 || candle2.SweptFibonacci > 0;
            bool candle3HasSweep = candle3.SweptLiquidity > 0 || candle3.SweptFibonacci > 0;
            
            if (candle2HasSweep || candle3HasSweep)
            {
                // Remove any existing venom in the target direction first
                Direction venomDirection = fvg.Direction == Direction.Up ? Direction.Down : Direction.Up;
                RemovePreviousVenom(venomDirection);
                
                // Convert FVG to Venom with opposite direction
                fvg.LevelType = LevelType.Venom;
                fvg.Direction = venomDirection;  // Bearish FVG → Bullish Venom, Bullish FVG → Bearish Venom
                fvg.IsConfirmed = false;
                
                // Store and publish the venom
                PublishDetectionEvent(fvg, fvg.Index);
                
                Logger?.Invoke($"Venom detected: {fvg.Direction} at index {fvg.Index}");
            }
        }
        
        /// <summary>
        /// Removes any existing venom in the specified direction
        /// </summary>
        private void RemovePreviousVenom(Direction direction)
        {
            var existingVenoms = Repository
                .Find(v => v.LevelType == LevelType.Venom && v.Direction == direction)
                .ToList();
            
            foreach (var venom in existingVenoms)
            {
                Repository.Remove(venom);
                // Don't remove visualization as per requirements
            }
        }
        
        /// <summary>
        /// Checks for venom confirmation when swing points are detected
        /// </summary>
        public void CheckVenomConfirmation(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
            
            var unconfirmedVenoms = Repository
                .Find(v => v.LevelType == LevelType.Venom && !v.IsConfirmed)
                .ToList();
            
            foreach (var venom in unconfirmedVenoms)
            {
                bool shouldConfirm = false;
                
                if (venom.Direction == Direction.Up && swingPoint.Direction == Direction.Up)
                {
                    // Bullish venom confirmed when bullish swing point candle closes above venom high
                    shouldConfirm = swingPoint.Bar.Close > venom.High;
                }
                else if (venom.Direction == Direction.Down && swingPoint.Direction == Direction.Down)
                {
                    // Bearish venom confirmed when bearish swing point candle closes below venom low
                    shouldConfirm = swingPoint.Bar.Close < venom.Low;
                }
                
                if (shouldConfirm)
                {
                    venom.IsConfirmed = true;
                    
                    // Publish confirmation event
                    EventAggregator.Publish(new VenomConfirmedEvent(venom));
                    
                    // Update visualization
                    _visualizer?.Update(venom);
                    
                    Logger?.Invoke($"Venom confirmed: {venom.Direction} at swing point index {swingPoint.Index}");
                }
            }
        }
        
        /// <summary>
        /// Checks for venom invalidation when swing points are detected
        /// </summary>
        public void CheckVenomInvalidation(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
            
            var unconfirmedVenoms = Repository
                .Find(v => v.LevelType == LevelType.Venom && !v.IsConfirmed)
                .ToList();
            
            foreach (var venom in unconfirmedVenoms)
            {
                bool shouldInvalidate = false;
                
                if (venom.Direction == Direction.Up && swingPoint.Direction == Direction.Down)
                {
                    // Bullish venom invalidated when bearish swing point candle closes below IndexLowPrice
                    shouldInvalidate = swingPoint.Bar.Close < venom.IndexLowPrice;
                }
                else if (venom.Direction == Direction.Down && swingPoint.Direction == Direction.Up)
                {
                    // Bearish venom invalidated when bullish swing point candle closes above IndexHighPrice
                    shouldInvalidate = swingPoint.Bar.Close > venom.IndexHighPrice;
                }
                
                if (shouldInvalidate)
                {
                    // Remove venom from repository and delete visualization
                    Repository.Remove(venom);
                    _visualizer?.Remove(venom);
                    
                    // Publish invalidation event
                    EventAggregator.Publish(new VenomInvalidatedEvent(venom));
                    
                    Logger?.Invoke($"Venom invalidated: {venom.Direction} at swing point index {swingPoint.Index}");
                }
            }
        }
        
        protected override void PublishDetectionEvent(Level venom, int currentIndex)
        {
            EventAggregator.Publish(new VenomDetectedEvent(venom));
            
            if (Settings.Patterns.ShowVenom && _visualizer != null)
            {
                _visualizer.Draw(venom);
            }
        }
        
        protected override void LogDetection(Level venom, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Venom detected: {venom.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(v => 
                v.LevelType == LevelType.Venom && 
                v.Direction == direction);
        }
        
        public override bool IsValid(Level venom, int currentIndex)
        {
            return venom != null && 
                   venom.LevelType == LevelType.Venom;
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<FvgDetectedEvent>(OnFvgDetected);
            EventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<FvgDetectedEvent>(OnFvgDetected);
            EventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        private void OnFvgDetected(FvgDetectedEvent evt)
        {
            if (evt.FvgLevel != null)
            {
                CheckForVenom(evt.FvgLevel);
            }
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            if (evt.SwingPoint != null)
            {
                // Check for confirmation first, then invalidation
                CheckVenomConfirmation(evt.SwingPoint);
                CheckVenomInvalidation(evt.SwingPoint);
            }
        }
    }
}