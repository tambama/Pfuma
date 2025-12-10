using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Handles the detection of swing points based on ICT methodology
    /// </summary>
    public class SwingPointDetector
    {
        private int _lastSwingHighIndex = -1;
        private int _lastSwingLowIndex = -1;
        private double _lastSwingHighValue = double.MinValue;
        private double _lastSwingLowValue = double.MaxValue;
        private bool _lastSwingWasHigh = false;
        private bool _lastSwingWasLow = false;

        // Add counter to track swing point numbers
        private int _currentSwingPointNumber = 0;

        private readonly SwingPointManager _swingPointManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly CandleManager _candleManager;
        private readonly TimeManager _timeManager;

        // Reference to the last high and low swing points
        private SwingPoint _lastHighSwingPoint;
        private SwingPoint _lastLowSwingPoint;

        // Define a delegate for the swing point removed event
        public delegate void SwingPointRemovedEventHandler(SwingPoint removedPoint);

        // Define the event
        public event SwingPointRemovedEventHandler SwingPointRemoved;
        
        // Define delegate and event for swept liquidity
        public delegate void LiquiditySweptEventHandler(SwingPoint sweptPoint, int sweepingCandleIndex,
            Candle sweepingCandle);

        public event LiquiditySweptEventHandler LiquiditySwept;

        // Define delegate for SMT re-evaluation
        public delegate void SMTReEvaluationHandler(SwingPoint oldSwingPoint, SwingPoint newSwingPoint);

        private readonly SMTReEvaluationHandler _smtReEvaluationHandler;

        public SwingPointDetector(SwingPointManager swingPointManager,
            CandleManager candleManager, IEventAggregator eventAggregator = null, TimeManager timeManager = null,
            SMTReEvaluationHandler smtReEvaluationHandler = null)
        {
            _swingPointManager = swingPointManager;
            _candleManager = candleManager;
            _eventAggregator = eventAggregator;
            _timeManager = timeManager;
            _smtReEvaluationHandler = smtReEvaluationHandler;
        }
        
        /// <summary>
        /// Publishes swing point detected event
        /// </summary>
        private void PublishSwingPointDetected(SwingPoint swingPoint)
        {
            _eventAggregator?.Publish(new SwingPointDetectedEvent(swingPoint));
        }
        
        /// <summary>
        /// Sets the InsideMacro property for a swing point based on its time
        /// </summary>
        private void SetInsideMacroStatus(SwingPoint swingPoint)
        {
            if (swingPoint == null || _timeManager == null)
                return;
                
            swingPoint.InsideMacro = _timeManager.IsInsideMacroTime(swingPoint.Time);
        }
        
        /// <summary>
        /// Adds swing point to collection and publishes event
        /// </summary>
        private void AddSwingPointAndPublish(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;

            // Set the InsideMacro property based on whether the swing point is in macro time
            SetInsideMacroStatus(swingPoint);

            // Only publish event if swing point was actually added (not a duplicate)
            bool wasAdded = _swingPointManager.AddSwingPoint(swingPoint);
            if (!wasAdded)
                return;

            PublishSwingPointDetected(swingPoint);

            // Update last swing point references
            if (swingPoint.SwingType == SwingType.H)
            {
                _lastHighSwingPoint = swingPoint;
            }
            else
            {
                _lastLowSwingPoint = swingPoint;
            }
        }

        public void ProcessBar(int index, Candle bar)
        {
            // Need at least 1 bar to calculate
            if (index <= 0 || bar == null)
                return;

            var close = bar.Close;
            var open = bar.Open;
            var low = bar.Low;
            var high = bar.High;
            var time = bar.Time;

            bool isDownCandle = close < open;
            bool isUpCandle = close > open;

            // Handle the special case where previous candle has a swing low
            if (_lastSwingWasLow && _lastSwingLowIndex >= 0 && _lastLowSwingPoint != null)
            {
                double prevSwingLowValue = _lastSwingLowValue;
                double prevSwingLowCandleHigh = _lastLowSwingPoint.Bar.High;

                if (low < prevSwingLowValue && high > prevSwingLowCandleHigh)
                {
                    if (isDownCandle)
                    {
                        // Set current high as swing high first
                        _swingPointManager.SetSwingHigh(index, high);
                        _lastSwingHighIndex = index;
                        _lastSwingHighValue = high;
                        _lastSwingWasHigh = true;
                        _lastSwingWasLow = false;

                        // Create new high swing point
                        var highSwingPoint = new SwingPoint(
                            index,
                            high,
                            time,
                            bar,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;

                        // Then set current low as swing low
                        _swingPointManager.SetSwingLow(index, low);
                        _lastSwingLowIndex = index;
                        _lastSwingLowValue = low;
                        _lastSwingWasLow = true;
                        _lastSwingWasHigh = false;

                        // Create new low swing point
                        var lowSwingPoint = new SwingPoint(
                            index,
                            low,
                            time,
                            bar,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;

                        return; // Finished processing this candle
                    }
                    else if (isUpCandle)
                    {
                        // Move the swing low to current candle
                        _swingPointManager.ClearSwingLow(_lastSwingLowIndex);
                        _swingPointManager.SetSwingLow(index, low);
                        _lastSwingLowIndex = index;
                        _lastSwingLowValue = low;

                        // Remove the old swing point and add a new one
                        var removedPoint = _lastLowSwingPoint;
                        _swingPointManager.RemoveSwingPoint(removedPoint);
                        // Trigger the event to notify listeners
                        SwingPointRemoved?.Invoke(removedPoint);

                        // Create new low swing point
                        var lowSwingPoint = new SwingPoint(
                            index,
                            low,
                            time,
                            bar,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        
                        // Check conditions for copying SweptLiquidity and SweptFib properties
                        if (removedPoint != null)
                        {
                            bool shouldCopyProperties = false;
                            
                            // Original condition: current candle closes above the low of the removed swing point
                            if (close > removedPoint.Bar.Low)
                            {
                                shouldCopyProperties = true;
                            }
                            // Additional condition: if candle closes below the bearish swing point but its high 
                            // is above the swept liquidity price or swept fib price
                            else if (close < removedPoint.Price && 
                                    ((removedPoint.SweptLiquidityPrice.HasValue && high > removedPoint.SweptLiquidityPrice.Value) ||
                                     (removedPoint.SweptFibPrice.HasValue && high > removedPoint.SweptFibPrice.Value)))
                            {
                                shouldCopyProperties = true;
                            }
                            
                            if (shouldCopyProperties)
                            {
                                // Copy SweptLiquidity and SweptFib properties from the removed swing point
                                lowSwingPoint.SweptLiquidity = removedPoint.SweptLiquidity;
                                lowSwingPoint.SweptFib = removedPoint.SweptFib;

                                // Handle SMT re-evaluation for replaced swing point
                                _smtReEvaluationHandler?.Invoke(removedPoint, lowSwingPoint);
                            }
                        }
                        
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;

                        // Then set current high as swing high
                        _swingPointManager.SetSwingHigh(index, high);
                        _lastSwingHighIndex = index;
                        _lastSwingHighValue = high;
                        _lastSwingWasHigh = true;
                        _lastSwingWasLow = false;

                        // Create new high swing point
                        var highSwingPoint = new SwingPoint(
                            index,
                            high,
                            time,
                            bar,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;

                        return; // Finished processing this candle
                    }
                }
            }

            // Handle the special case where previous candle has a swing high
            if (_lastSwingWasHigh && _lastSwingHighIndex >= 0 && _lastHighSwingPoint != null)
            {
                double prevSwingHighValue = _lastSwingHighValue;
                double prevSwingHighCandleLow = _lastHighSwingPoint.Bar.Low;

                if (high > prevSwingHighValue && low < prevSwingHighCandleLow)
                {
                    if (isUpCandle)
                    {
                        // Set current low as swing low first
                        _swingPointManager.SetSwingLow(index, low);
                        _lastSwingLowIndex = index;
                        _lastSwingLowValue = low;
                        _lastSwingWasLow = true;
                        _lastSwingWasHigh = false;

                        // Create new low swing point
                        var lowSwingPoint = new SwingPoint(
                            index,
                            low,
                            time,
                            bar,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;

                        // Then set current high as swing high
                        _swingPointManager.SetSwingHigh(index, high);
                        _lastSwingHighIndex = index;
                        _lastSwingHighValue = high;
                        _lastSwingWasHigh = true;
                        _lastSwingWasLow = false;

                        // Create new high swing point
                        var highSwingPoint = new SwingPoint(
                            index,
                            high,
                            time,
                            bar,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;

                        return; // Finished processing this candle
                    }
                    else if (isDownCandle)
                    {
                        // Move the swing high to current candle
                        _swingPointManager.ClearSwingHigh(_lastSwingHighIndex);
                        _swingPointManager.SetSwingHigh(index, high);
                        _lastSwingHighIndex = index;
                        _lastSwingHighValue = high;

                        // Remove the old swing point and add a new one
                        var removedPoint = _lastHighSwingPoint;
                        _swingPointManager.RemoveSwingPoint(removedPoint);
                        // Trigger the event to notify listeners
                        SwingPointRemoved?.Invoke(removedPoint);

                        // Create new high swing point
                        var highSwingPoint = new SwingPoint(
                            index,
                            high,
                            time,
                            bar,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        
                        // Check if current candle closes below the high of the removed swing point
                        if (removedPoint != null && close < removedPoint.Price)
                        {
                            // Copy SweptLiquidity and SweptFib properties from the removed swing point
                            highSwingPoint.SweptLiquidity = removedPoint.SweptLiquidity;
                            highSwingPoint.SweptFib = removedPoint.SweptFib;

                            // Handle SMT re-evaluation for replaced swing point
                            _smtReEvaluationHandler?.Invoke(removedPoint, highSwingPoint);
                        }
                        
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;

                        // Then set current low as swing low
                        _swingPointManager.SetSwingLow(index, low);
                        _lastSwingLowIndex = index;
                        _lastSwingLowValue = low;
                        _lastSwingWasLow = true;
                        _lastSwingWasHigh = false;

                        // Create new low swing point
                        var lowSwingPoint = new SwingPoint(
                            index,
                            low,
                            time,
                            bar,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;

                        return; // Finished processing this candle
                    }
                }
            }

            // Normal swing high detection logic
            if (_lastSwingWasLow || (!_lastSwingWasHigh && !_lastSwingWasLow))
            {
                // If last swing was a low or no swing yet
                if (high > _lastSwingHighValue)
                {
                    // New swing high
                    _swingPointManager.SetSwingHigh(index, high);
                    _lastSwingHighIndex = index;
                    _lastSwingHighValue = high;
                    _lastSwingWasHigh = true;
                    _lastSwingWasLow = false;

                    // Create new high swing point
                    var highSwingPoint = new SwingPoint(
                        index,
                        high,
                        time,
                        bar,
                        SwingType.H,
                        LiquidityType.Normal,
                        Direction.Up
                    );
                    highSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(highSwingPoint);
                    _lastHighSwingPoint = highSwingPoint;

                    return; // Finished processing this candle
                }
            }
            else if (_lastSwingWasHigh && _lastSwingHighIndex >= 0 && _lastHighSwingPoint != null)
            {
                // If last swing was a high
                if (high > _lastSwingHighValue)
                {
                    // Move swing high to current candle
                    _swingPointManager.ClearSwingHigh(_lastSwingHighIndex);
                    _swingPointManager.SetSwingHigh(index, high);
                    _lastSwingHighIndex = index;
                    _lastSwingHighValue = high;

                    // Remove the old swing point and add a new one
                    var removedPoint = _lastHighSwingPoint;
                    _swingPointManager.RemoveSwingPoint(removedPoint);
                    // Trigger the event to notify listeners
                    SwingPointRemoved?.Invoke(removedPoint);

                    // Create new high swing point
                    var highSwingPoint = new SwingPoint(
                        index,
                        high,
                        time,
                        bar,
                        SwingType.H,
                        LiquidityType.Normal,
                        Direction.Up
                    );
                    
                    // Check if current candle closes below the high of the removed swing point
                    if (removedPoint != null && close < removedPoint.Price)
                    {
                        // Copy SweptLiquidity and SweptFib properties from the removed swing point
                        highSwingPoint.SweptLiquidity = removedPoint.SweptLiquidity;
                        highSwingPoint.SweptFib = removedPoint.SweptFib;
                    }
                    
                    highSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(highSwingPoint);
                    _lastHighSwingPoint = highSwingPoint;

                    return; // Finished processing this candle
                }

                // Check if we should create a new swing low
                if (low < _lastHighSwingPoint.Bar.Low)
                {
                    // New swing low after a swing high
                    _swingPointManager.SetSwingLow(index, low);
                    _lastSwingLowIndex = index;
                    _lastSwingLowValue = low;
                    _lastSwingWasLow = true;
                    _lastSwingWasHigh = false;

                    // Create new low swing point
                    var lowSwingPoint = new SwingPoint(
                        index,
                        low,
                        time,
                        bar,
                        SwingType.L,
                        LiquidityType.Normal,
                        Direction.Down
                    );
                    lowSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(lowSwingPoint);
                    _lastLowSwingPoint = lowSwingPoint;

                    return; // Finished processing this candle
                }
            }

            // Normal swing low detection logic
            if (_lastSwingWasHigh || (!_lastSwingWasHigh && !_lastSwingWasLow))
            {
                // If last swing was a high or no swing yet
                if (low < _lastSwingLowValue)
                {
                    // New swing low
                    _swingPointManager.SetSwingLow(index, low);
                    _lastSwingLowIndex = index;
                    _lastSwingLowValue = low;
                    _lastSwingWasLow = true;
                    _lastSwingWasHigh = false;

                    // Create new low swing point
                    var lowSwingPoint = new SwingPoint(
                        index,
                        low,
                        time,
                        bar,
                        SwingType.L,
                        LiquidityType.Normal,
                        Direction.Down
                    );
                    lowSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(lowSwingPoint);
                    _lastLowSwingPoint = lowSwingPoint;

                    return; // Finished processing this candle
                }
            }
            else if (_lastSwingWasLow && _lastSwingLowIndex >= 0 && _lastLowSwingPoint != null)
            {
                // If last swing was a low
                if (low < _lastSwingLowValue)
                {
                    // Move swing low to current candle
                    _swingPointManager.ClearSwingLow(_lastSwingLowIndex);
                    _swingPointManager.SetSwingLow(index, low);
                    _lastSwingLowIndex = index;
                    _lastSwingLowValue = low;

                    // Remove the old swing point and add a new one
                    var removedPoint = _lastLowSwingPoint;
                    _swingPointManager.RemoveSwingPoint(removedPoint);
                    // Trigger the event to notify listeners
                    SwingPointRemoved?.Invoke(removedPoint);

                    // Create new low swing point
                    var lowSwingPoint = new SwingPoint(
                        index,
                        low,
                        time,
                        bar,
                        SwingType.L,
                        LiquidityType.Normal,
                        Direction.Down
                    );
                    
                    // Check conditions for copying SweptLiquidity and SweptFib properties
                    if (removedPoint != null)
                    {
                        bool shouldCopyProperties = false;
                        
                        // Original condition: current candle closes above the low of the removed swing point
                        if (close > removedPoint.Bar.Low)
                        {
                            shouldCopyProperties = true;
                        }
                        // Additional condition: if candle closes below the bearish swing point but its high 
                        // is above the swept liquidity price or swept fib price
                        else if (close < removedPoint.Price && 
                                ((removedPoint.SweptLiquidityPrice.HasValue && high > removedPoint.SweptLiquidityPrice.Value) ||
                                 (removedPoint.SweptFibPrice.HasValue && high > removedPoint.SweptFibPrice.Value)))
                        {
                            shouldCopyProperties = true;
                        }
                        
                        if (shouldCopyProperties)
                        {
                            // Copy SweptLiquidity and SweptFib properties from the removed swing point
                            lowSwingPoint.SweptLiquidity = removedPoint.SweptLiquidity;
                            lowSwingPoint.SweptFib = removedPoint.SweptFib;
                        }
                    }
                    
                    lowSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(lowSwingPoint);
                    _lastLowSwingPoint = lowSwingPoint;

                    return; // Finished processing this candle
                }

                // Check if we should create a new swing high
                if (high > _lastLowSwingPoint.Bar.High)
                {
                    // New swing high after a swing low
                    _swingPointManager.SetSwingHigh(index, high);
                    _lastSwingHighIndex = index;
                    _lastSwingHighValue = high;
                    _lastSwingWasHigh = true;
                    _lastSwingWasLow = false;

                    // Create new high swing point
                    var highSwingPoint = new SwingPoint(
                        index,
                        high,
                        time,
                        bar,
                        SwingType.H,
                        LiquidityType.Normal,
                        Direction.Up
                    );
                    highSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(highSwingPoint);
                    _lastHighSwingPoint = highSwingPoint;

                    return; // Finished processing this candle
                }
            }
        }

        public void AddSpecialSwingPoint(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;

            // Check if a swing point already exists at the same index
            var existingPoint = _swingPointManager.GetSwingPointAtIndex(swingPoint.Index);
            
            if (existingPoint != null)
            {
                // Preserve the Number from the existing swing point
                swingPoint.Number = existingPoint.Number;
                
                // Remove the existing swing point from the collection
                _swingPointManager.RemoveSwingPoint(existingPoint);
            }
            else
            {
                // Assign a new number only if there's no existing swing point
                swingPoint.Number = ++_currentSwingPointNumber;
            }

            // Add to collection
            AddSwingPointAndPublish(swingPoint);
        }

        /// <summary>
        /// Checks if the new bar sweeps any important liquidity points (PDH, PDL)
        /// </summary>
        public void CheckForSweptLiquidity(Candle currentBar, int currentIndex)
        {
            var sweptPoints = _swingPointManager.CheckForSweptLiquidity(currentBar, currentIndex);
            
            // Notify via the SweptLiquidity event for each swept point
            foreach (var point in sweptPoints)
            {
                OnLiquiditySwept(point, currentIndex, currentBar);
            }
        }

        // Method to trigger the event
        protected virtual void OnLiquiditySwept(SwingPoint sweptPoint, int sweepingCandleIndex, Candle sweepingCandle)
        {
            LiquiditySwept?.Invoke(sweptPoint, sweepingCandleIndex, sweepingCandle);
        }

        // Methods to retrieve swing points
        public List<SwingPoint> GetAllSwingPoints()
        {
            return _swingPointManager.GetAllSwingPoints();
        }

        public List<SwingPoint> GetSwingHighs()
        {
            return _swingPointManager.GetSwingHighs();
        }

        public List<SwingPoint> GetSwingLows()
        {
            return _swingPointManager.GetSwingLows();
        }

        public SwingPoint GetLastSwingHigh()
        {
            return _swingPointManager.GetLastSwingHigh();
        }

        public SwingPoint GetLastSwingLow()
        {
            return _swingPointManager.GetLastSwingLow();
        }

        // Get swing point at specific index
        public SwingPoint GetSwingPointAtIndex(int index)
        {
            return _swingPointManager.GetSwingPointAtIndex(index);
        }

        public List<SwingPoint> GetSwingPointsAtIndex(int index)
        {
            return _swingPointManager.GetSwingPointsAtIndex(index);
        }

        // Check if there's a swing point at specific index
        public bool HasSwingPointAtIndex(int index)
        {
            return _swingPointManager.HasSwingPointAtIndex(index);
        }

        // Get previous and next swing points
        public SwingPoint GetPreviousSwingPoint(SwingPoint currentPoint)
        {
            return _swingPointManager.GetPreviousSwingPoint(currentPoint);
        }

        public SwingPoint GetNextSwingPoint(SwingPoint currentPoint)
        {
            return _swingPointManager.GetNextSwingPoint(currentPoint);
        }

        // Update previous and next pointers for all swing points
        public void UpdateSwingPointRelationships()
        {
            _swingPointManager.UpdateSwingPointRelationships();
        }
    }
}