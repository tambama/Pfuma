using System;
using System.Collections.Generic;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    public interface IFibonacciSweepDetector
    {
        void ProcessSwingPoint(SwingPoint swingPoint, Candle candle);
    }
    
    public class FibonacciSweepDetector : IFibonacciSweepDetector
    {
        private readonly IFibonacciService _fibonacciService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Action<string> _log;
        
        public FibonacciSweepDetector(IFibonacciService fibonacciService, IEventAggregator eventAggregator, Action<string> log = null)
        {
            _fibonacciService = fibonacciService;
            _eventAggregator = eventAggregator;
            _log = log;
            
            // Subscribe to swing point detection events
            _eventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent swingEvent)
        {
            if (swingEvent?.SwingPoint == null || swingEvent.SwingPoint.Bar == null) return;
            
            ProcessSwingPoint(swingEvent.SwingPoint, swingEvent.SwingPoint.Bar);
        }
        
        public void ProcessSwingPoint(SwingPoint swingPoint, Candle candle)
        {
            if (swingPoint == null || candle == null) return;
            
            var fibLevels = _fibonacciService.GetAllFibonacciLevels();
            if (fibLevels == null || fibLevels.Count == 0) return;
            
            // Determine if bullish or bearish based on SwingType
            // HH and H are swing highs (bullish), LL and L are swing lows (bearish)
            bool isBullish = swingPoint.Direction == Direction.Up;
            
            foreach (var fibLevel in fibLevels)
            {
                if (isBullish)
                {
                    ProcessBullishSwingPoint(swingPoint, candle, fibLevel);
                }
                else
                {
                    ProcessBearishSwingPoint(swingPoint, candle, fibLevel);
                }
            }
        }
        
        private void ProcessBullishSwingPoint(SwingPoint swingPoint, Candle candle, FibonacciLevel fibLevel)
        {
            var sweptLevels = new List<(double ratio, double price, bool isSweep, bool isBreak, string id, FibType fibType)>();
            
            foreach (var level in fibLevel.Levels)
            {
                double ratio = level.Key;
                double price = level.Value;
                
                // Skip if this level has already been swept or broken
                if (fibLevel.SweptLevelLineIds.ContainsKey(ratio))
                {
                    string status = fibLevel.SweptLevelLineIds[ratio];
                    if (status == "BROKEN" || status == "SWEPT")
                    {
                        continue; // Already processed, skip
                    }
                }
                
                // Also check the SweptLevels dictionary
                if (fibLevel.SweptLevels.ContainsKey(ratio) && fibLevel.SweptLevels[ratio])
                {
                    continue; // Already swept, skip
                }
                
                // Bullish sweep: opens below level, high above level, closes below level
                bool isSweep = candle.Open < price && candle.High > price && candle.Close < price;
                
                // Bullish break: opens below level, closes above level
                bool isBreak = candle.Open < price && candle.Close > price;
                
                if (isSweep || isBreak)
                {
                    sweptLevels.Add((ratio, price, isSweep, isBreak, fibLevel.Id, fibLevel.FibType));
                }
            }
            
            // Process swept/broken levels
            foreach (var (ratio, price, isSweep, isBreak, id, type) in sweptLevels)
            {
                if (isSweep)
                {
                    // Mark as swept
                    swingPoint.SweptFib = true;
                    swingPoint.SweptFibLevels.Add((swingPoint.Index, price, ratio, id, type));
                    
                    // Update the FibonacciLevel tracking
                    fibLevel.SweptLevelLineIds[ratio] = "SWEPT";
                    fibLevel.SweptLevels[ratio] = true;  // Mark as swept to prevent multiple sweeps
                    
                    // Publish event for visualization update
                    PublishFibonacciSweepEvent(fibLevel, ratio, swingPoint.Index, price, true, false);
                }
                else if (isBreak)
                {
                    // Mark as broken (remove from chart)
                    fibLevel.SweptLevelLineIds[ratio] = "BROKEN";
                    fibLevel.SweptLevels[ratio] = true;  // Mark as processed to prevent reprocessing
                    
                    // Publish event for visualization update (remove line)
                    PublishFibonacciSweepEvent(fibLevel, ratio, swingPoint.Index, price, false, true);
                }
            }
        }
        
        private void ProcessBearishSwingPoint(SwingPoint swingPoint, Candle candle, FibonacciLevel fibLevel)
        {
            var sweptLevels = new List<(double ratio, double price, bool isSweep, bool isBreak, string id, FibType type)>();
            
            foreach (var level in fibLevel.Levels)
            {
                double ratio = level.Key;
                double price = level.Value;
                
                // Skip if this level has already been swept or broken
                if (fibLevel.SweptLevelLineIds.ContainsKey(ratio))
                {
                    string status = fibLevel.SweptLevelLineIds[ratio];
                    if (status == "BROKEN" || status == "SWEPT")
                    {
                        continue; // Already processed, skip
                    }
                }
                
                // Also check the SweptLevels dictionary
                if (fibLevel.SweptLevels.ContainsKey(ratio) && fibLevel.SweptLevels[ratio])
                {
                    continue; // Already swept, skip
                }
                
                // Bearish sweep: opens above level, low below level, closes above level
                bool isSweep = candle.Open > price && candle.Low < price && candle.Close > price;
                
                // Bearish break: opens above level, closes below level
                bool isBreak = candle.Open > price && candle.Close < price;
                
                if (isSweep || isBreak)
                {
                    sweptLevels.Add((ratio, price, isSweep, isBreak, fibLevel.Id, fibLevel.FibType));
                }
            }
            
            // Process swept/broken levels
            foreach (var (ratio, price, isSweep, isBreak, id, type) in sweptLevels)
            {
                if (isSweep)
                {
                    // Mark as swept
                    swingPoint.SweptFib = true;
                    swingPoint.SweptFibLevels.Add((swingPoint.Index, price, ratio, id, type));
                    
                    // Update the FibonacciLevel tracking
                    fibLevel.SweptLevelLineIds[ratio] = "SWEPT";
                    fibLevel.SweptLevels[ratio] = true;  // Mark as swept to prevent multiple sweeps
                    
                    // Publish event for visualization update
                    PublishFibonacciSweepEvent(fibLevel, ratio, swingPoint.Index, price, true, false);
                }
                else if (isBreak)
                {
                    // Mark as broken (remove from chart)
                    fibLevel.SweptLevelLineIds[ratio] = "BROKEN";
                    fibLevel.SweptLevels[ratio] = true;  // Mark as processed to prevent reprocessing
                    
                    // Publish event for visualization update (remove line)
                    PublishFibonacciSweepEvent(fibLevel, ratio, swingPoint.Index, price, false, true);
                }
            }
        }
        
        private void PublishFibonacciSweepEvent(FibonacciLevel fibLevel, double ratio, int sweepIndex, double price, bool isSweep, bool isBreak)
        {
            var sweepEvent = new FibonacciLevelSweptEvent
            {
                FibonacciLevel = fibLevel,
                SweptRatio = ratio,
                SweptPrice = price,
                SweepIndex = sweepIndex,
                IsSweep = isSweep,
                IsBreak = isBreak
            };
            
            _eventAggregator?.Publish(sweepEvent);
        }
    }
    
    // Event for Fibonacci level sweep/break
    public class FibonacciLevelSweptEvent : PatternEventBase
    {
        public FibonacciLevel FibonacciLevel { get; set; }
        public double SweptRatio { get; set; }
        public double SweptPrice { get; set; }
        public int SweepIndex { get; set; }
        public bool IsSweep { get; set; }
        public bool IsBreak { get; set; }
        
        public FibonacciLevelSweptEvent() : base(0) { }
    }
}