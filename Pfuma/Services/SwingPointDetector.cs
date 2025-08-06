using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;
using Pfuma.Core.Interfaces;
using Pfuma.Core.Events;

namespace Pfuma.Services
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

        private readonly IndicatorDataSeries _swingHighs;
        private readonly IndicatorDataSeries _swingLows;
        private readonly IEventAggregator _eventAggregator;

        // Collection to store all swing points
        private readonly List<SwingPoint> _swingPoints = new();

        // Reference to the last high and low swing points
        private SwingPoint _lastHighSwingPoint;
        private SwingPoint _lastLowSwingPoint;

        // Define a delegate for the swing point removed event
        public delegate void SwingPointRemovedEventHandler(SwingPoint removedPoint);

        // Define the event
        public event SwingPointRemovedEventHandler SwingPointRemoved;

        public SwingPointDetector(IndicatorDataSeries swingHighs, IndicatorDataSeries swingLows, IEventAggregator eventAggregator = null)
        {
            _swingHighs = swingHighs;
            _swingLows = swingLows;
            _eventAggregator = eventAggregator;
        }
        
        /// <summary>
        /// Publishes swing point detected event
        /// </summary>
        private void PublishSwingPointDetected(SwingPoint swingPoint)
        {
            _eventAggregator?.Publish(new SwingPointDetectedEvent(swingPoint));
        }
        
        /// <summary>
        /// Adds swing point to collection and publishes event
        /// </summary>
        private void AddSwingPointAndPublish(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
                
            _swingPoints.Add(swingPoint);
            PublishSwingPointDetected(swingPoint);
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
                        _swingHighs[index] = high;
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
                        _swingLows[index] = low;
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
                        _swingLows[_lastSwingLowIndex] = double.NaN;
                        _swingLows[index] = low;
                        _lastSwingLowIndex = index;
                        _lastSwingLowValue = low;

                        // Remove the old swing point and add a new one
                        var removedPoint = _lastLowSwingPoint;
                        _swingPoints.Remove(removedPoint);
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
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;

                        // Then set current high as swing high
                        _swingHighs[index] = high;
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
                        _swingLows[index] = low;
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
                        _swingHighs[index] = high;
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
                        _swingHighs[_lastSwingHighIndex] = double.NaN;
                        _swingHighs[index] = high;
                        _lastSwingHighIndex = index;
                        _lastSwingHighValue = high;

                        // Remove the old swing point and add a new one
                        var removedPoint = _lastHighSwingPoint;
                        _swingPoints.Remove(removedPoint);
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
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;

                        // Then set current low as swing low
                        _swingLows[index] = low;
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
                    _swingHighs[index] = high;
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
                    _swingHighs[_lastSwingHighIndex] = double.NaN;
                    _swingHighs[index] = high;
                    _lastSwingHighIndex = index;
                    _lastSwingHighValue = high;

                    // Remove the old swing point and add a new one
                    var removedPoint = _lastHighSwingPoint;
                    _swingPoints.Remove(removedPoint);
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
                    highSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(highSwingPoint);
                    _lastHighSwingPoint = highSwingPoint;

                    return; // Finished processing this candle
                }

                // Check if we should create a new swing low
                if (low < _lastHighSwingPoint.Bar.Low)
                {
                    // New swing low after a swing high
                    _swingLows[index] = low;
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
                    _swingLows[index] = low;
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
                    _swingLows[_lastSwingLowIndex] = double.NaN;
                    _swingLows[index] = low;
                    _lastSwingLowIndex = index;
                    _lastSwingLowValue = low;

                    // Remove the old swing point and add a new one
                    var removedPoint = _lastLowSwingPoint;
                    _swingPoints.Remove(removedPoint);
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
                    lowSwingPoint.Number = ++_currentSwingPointNumber;
                    AddSwingPointAndPublish(lowSwingPoint);
                    _lastLowSwingPoint = lowSwingPoint;

                    return; // Finished processing this candle
                }

                // Check if we should create a new swing high
                if (high > _lastLowSwingPoint.Bar.High)
                {
                    // New swing high after a swing low
                    _swingHighs[index] = high;
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
            var existingPoint = _swingPoints.Find(sp => sp.Index == swingPoint.Index);
            
            if (existingPoint != null)
            {
                // Preserve the Number from the existing swing point
                swingPoint.Number = existingPoint.Number;
                
                // Remove the existing swing point from the collection
                _swingPoints.Remove(existingPoint);
            }
            else
            {
                // Assign a new number only if there's no existing swing point
                swingPoint.Number = ++_currentSwingPointNumber;
            }

            // Add to collection
            AddSwingPointAndPublish(swingPoint);

            // Set indicator data series value
            // if (swingPoint.SwingType == SwingType.H)
            // {
            //     _swingHighs[swingPoint.Index] = swingPoint.Price;
            //     _lastHighSwingPoint = swingPoint;
            // }
            // else
            // {
            //     _swingLows[swingPoint.Index] = swingPoint.Price;
            //     _lastLowSwingPoint = swingPoint;
            // }
        }

        public void ProcessHighTimeframeBar(Candle htfCandle)
        {
            // Skip if we don't have valid data
            if (htfCandle == null || !htfCandle.IsHighTimeframe ||
                !htfCandle.IndexOfHigh.HasValue || !htfCandle.IndexOfLow.HasValue)
                return;

            var close = htfCandle.Close;
            var open = htfCandle.Open;
            var low = htfCandle.Low;
            var high = htfCandle.High;
            var time = htfCandle.Time;
            if (htfCandle.Index != null)
            {
                var index = htfCandle.Index.Value;
            }

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
                        _swingHighs[htfCandle.IndexOfHigh.Value] = high;
                        _lastSwingHighIndex = htfCandle.IndexOfHigh.Value;
                        _lastSwingHighValue = high;
                        _lastSwingWasHigh = true;
                        _lastSwingWasLow = false;

                        // Create new high swing point
                        if (htfCandle.TimeOfHigh != null)
                        {
                            var highSwingPoint = new SwingPoint(
                                htfCandle.IndexOfHigh.Value,
                                high,
                                htfCandle.TimeOfHigh.Value,
                                htfCandle,
                                SwingType.H,
                                LiquidityType.Normal,
                                Direction.Up
                            );
                            highSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(highSwingPoint);
                            _lastHighSwingPoint = highSwingPoint;
                        }

                        // Then set current low as swing low
                        _swingLows[htfCandle.IndexOfLow.Value] = low;
                        _lastSwingLowIndex = htfCandle.IndexOfLow.Value;
                        _lastSwingLowValue = low;
                        _lastSwingWasLow = true;
                        _lastSwingWasHigh = false;

                        // Create new low swing point
                        if (htfCandle.TimeOfLow != null)
                        {
                            var lowSwingPoint = new SwingPoint(
                                htfCandle.IndexOfLow.Value,
                                low,
                                htfCandle.TimeOfLow.Value,
                                htfCandle,
                                SwingType.L,
                                LiquidityType.Normal,
                                Direction.Down
                            );
                            lowSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(lowSwingPoint);
                            _lastLowSwingPoint = lowSwingPoint;
                        }

                        return; // Finished processing this candle
                    }
                    else if (isUpCandle)
                    {
                        // Move the swing low to current candle
                        _swingLows[_lastSwingLowIndex] = double.NaN;
                        _swingLows[htfCandle.IndexOfLow.Value] = low;
                        _lastSwingLowIndex = htfCandle.IndexOfLow.Value;
                        _lastSwingLowValue = low;

                        // Remove the old swing point and add a new one
                        var removedPoint = _lastLowSwingPoint;
                        _swingPoints.Remove(removedPoint);
                        // Trigger the event to notify listeners
                        SwingPointRemoved?.Invoke(removedPoint);

                        // Create new low swing point
                        if (htfCandle.TimeOfLow != null)
                        {
                            var lowSwingPoint = new SwingPoint(
                                htfCandle.IndexOfLow.Value,
                                low,
                                htfCandle.TimeOfLow.Value,
                                htfCandle,
                                SwingType.L,
                                LiquidityType.Normal,
                                Direction.Down
                            );
                            lowSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(lowSwingPoint);
                            _lastLowSwingPoint = lowSwingPoint;
                        }

                        // Then set current high as swing high
                        _swingHighs[htfCandle.IndexOfHigh.Value] = high;
                        _lastSwingHighIndex = htfCandle.IndexOfHigh.Value;
                        _lastSwingHighValue = high;
                        _lastSwingWasHigh = true;
                        _lastSwingWasLow = false;

                        // Create new high swing point
                        if (htfCandle.TimeOfHigh != null)
                        {
                            var highSwingPoint = new SwingPoint(
                                htfCandle.IndexOfHigh.Value,
                                high,
                                htfCandle.TimeOfHigh.Value,
                                htfCandle,
                                SwingType.H,
                                LiquidityType.Normal,
                                Direction.Up
                            );
                            highSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(highSwingPoint);
                            _lastHighSwingPoint = highSwingPoint;
                        }

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
                        _swingLows[htfCandle.IndexOfLow.Value] = low;
                        _lastSwingLowIndex = htfCandle.IndexOfLow.Value;
                        _lastSwingLowValue = low;
                        _lastSwingWasLow = true;
                        _lastSwingWasHigh = false;

                        // Create new low swing point
                        if (htfCandle.TimeOfLow != null)
                        {
                            var lowSwingPoint = new SwingPoint(
                                htfCandle.IndexOfLow.Value,
                                low,
                                htfCandle.TimeOfLow.Value,
                                htfCandle,
                                SwingType.L,
                                LiquidityType.Normal,
                                Direction.Down
                            );
                            lowSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(lowSwingPoint);
                            _lastLowSwingPoint = lowSwingPoint;
                        }

                        // Then set current high as swing high
                        _swingHighs[htfCandle.IndexOfHigh.Value] = high;
                        _lastSwingHighIndex = htfCandle.IndexOfHigh.Value;
                        _lastSwingHighValue = high;
                        _lastSwingWasHigh = true;
                        _lastSwingWasLow = false;

                        // Create new high swing point
                        if (htfCandle.TimeOfHigh != null)
                        {
                            var highSwingPoint = new SwingPoint(
                                htfCandle.IndexOfHigh.Value,
                                high,
                                htfCandle.TimeOfHigh.Value,
                                htfCandle,
                                SwingType.H,
                                LiquidityType.Normal,
                                Direction.Up
                            );
                            highSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(highSwingPoint);
                            _lastHighSwingPoint = highSwingPoint;
                        }

                        return; // Finished processing this candle
                    }
                    else if (isDownCandle)
                    {
                        // Move the swing high to current candle
                        _swingHighs[_lastSwingHighIndex] = double.NaN;
                        _swingHighs[htfCandle.IndexOfHigh.Value] = high;
                        _lastSwingHighIndex = htfCandle.IndexOfHigh.Value;
                        _lastSwingHighValue = high;

                        // Remove the old swing point and add a new one
                        var removedPoint = _lastHighSwingPoint;
                        _swingPoints.Remove(removedPoint);
                        // Trigger the event to notify listeners
                        SwingPointRemoved?.Invoke(removedPoint);

                        // Create new high swing point
                        if (htfCandle.TimeOfHigh != null)
                        {
                            var highSwingPoint = new SwingPoint(
                                htfCandle.IndexOfHigh.Value,
                                high,
                                htfCandle.TimeOfHigh.Value,
                                htfCandle,
                                SwingType.H,
                                LiquidityType.Normal,
                                Direction.Up
                            );
                            highSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(highSwingPoint);
                            _lastHighSwingPoint = highSwingPoint;
                        }

                        // Then set current low as swing low
                        _swingLows[htfCandle.IndexOfLow.Value] = low;
                        _lastSwingLowIndex = htfCandle.IndexOfLow.Value;
                        _lastSwingLowValue = low;
                        _lastSwingWasLow = true;
                        _lastSwingWasHigh = false;

                        // Create new low swing point
                        if (htfCandle.TimeOfLow != null)
                        {
                            var lowSwingPoint = new SwingPoint(
                                htfCandle.IndexOfLow.Value,
                                low,
                                htfCandle.TimeOfLow.Value,
                                htfCandle,
                                SwingType.L,
                                LiquidityType.Normal,
                                Direction.Down
                            );
                            lowSwingPoint.Number = ++_currentSwingPointNumber;
                            AddSwingPointAndPublish(lowSwingPoint);
                            _lastLowSwingPoint = lowSwingPoint;
                        }

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
                    _swingHighs[htfCandle.IndexOfHigh.Value] = high;
                    _lastSwingHighIndex = htfCandle.IndexOfHigh.Value;
                    _lastSwingHighValue = high;
                    _lastSwingWasHigh = true;
                    _lastSwingWasLow = false;

                    // Create new high swing point
                    if (htfCandle.TimeOfHigh != null)
                    {
                        var highSwingPoint = new SwingPoint(
                            htfCandle.IndexOfHigh.Value,
                            high,
                            htfCandle.TimeOfHigh.Value,
                            htfCandle,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;
                    }

                    return; // Finished processing this candle
                }
            }
            else if (_lastSwingWasHigh && _lastSwingHighIndex >= 0 && _lastHighSwingPoint != null)
            {
                // If last swing was a high
                if (high > _lastSwingHighValue)
                {
                    // Move swing high to current candle
                    _swingHighs[_lastSwingHighIndex] = double.NaN;
                    _swingHighs[htfCandle.IndexOfHigh.Value] = high;
                    _lastSwingHighIndex = htfCandle.IndexOfHigh.Value;
                    _lastSwingHighValue = high;

                    // Remove the old swing point and add a new one
                    var removedPoint = _lastHighSwingPoint;
                    _swingPoints.Remove(removedPoint);
                    // Trigger the event to notify listeners
                    SwingPointRemoved?.Invoke(removedPoint);

                    // Create new high swing point
                    if (htfCandle.TimeOfHigh != null)
                    {
                        var highSwingPoint = new SwingPoint(
                            htfCandle.IndexOfHigh.Value,
                            high,
                            htfCandle.TimeOfHigh.Value,
                            htfCandle,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;
                    }

                    return; // Finished processing this candle
                }

                // Check if we should create a new swing low
                if (low < _lastHighSwingPoint.Bar.Low)
                {
                    // New swing low after a swing high
                    _swingLows[htfCandle.IndexOfLow.Value] = low;
                    _lastSwingLowIndex = htfCandle.IndexOfLow.Value;
                    _lastSwingLowValue = low;
                    _lastSwingWasLow = true;
                    _lastSwingWasHigh = false;

                    // Create new low swing point
                    if (htfCandle.TimeOfLow != null)
                    {
                        var lowSwingPoint = new SwingPoint(
                            htfCandle.IndexOfLow.Value,
                            low,
                            htfCandle.TimeOfLow.Value,
                            htfCandle,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;
                    }

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
                    _swingLows[htfCandle.IndexOfLow.Value] = low;
                    _lastSwingLowIndex = htfCandle.IndexOfLow.Value;
                    _lastSwingLowValue = low;
                    _lastSwingWasLow = true;
                    _lastSwingWasHigh = false;

                    // Create new low swing point
                    if (htfCandle.TimeOfLow != null)
                    {
                        var lowSwingPoint = new SwingPoint(
                            htfCandle.IndexOfLow.Value,
                            low,
                            htfCandle.TimeOfLow.Value,
                            htfCandle,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;
                    }

                    return; // Finished processing this candle
                }
            }
            else if (_lastSwingWasLow && _lastSwingLowIndex >= 0 && _lastLowSwingPoint != null)
            {
                // If last swing was a low
                if (low < _lastSwingLowValue)
                {
                    // Move swing low to current candle
                    _swingLows[_lastSwingLowIndex] = double.NaN;
                    _swingLows[htfCandle.IndexOfLow.Value] = low;
                    _lastSwingLowIndex = htfCandle.IndexOfLow.Value;
                    _lastSwingLowValue = low;

                    // Remove the old swing point and add a new one
                    var removedPoint = _lastLowSwingPoint;
                    _swingPoints.Remove(removedPoint);
                    // Trigger the event to notify listeners
                    SwingPointRemoved?.Invoke(removedPoint);

                    // Create new low swing point
                    if (htfCandle.TimeOfLow != null)
                    {
                        var lowSwingPoint = new SwingPoint(
                            htfCandle.IndexOfLow.Value,
                            low,
                            htfCandle.TimeOfLow.Value,
                            htfCandle,
                            SwingType.L,
                            LiquidityType.Normal,
                            Direction.Down
                        );
                        lowSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(lowSwingPoint);
                        _lastLowSwingPoint = lowSwingPoint;
                    }

                    return; // Finished processing this candle
                }

                // Check if we should create a new swing high
                if (high > _lastLowSwingPoint.Bar.High)
                {
                    // New swing high after a swing low
                    _swingHighs[htfCandle.IndexOfHigh.Value] = high;
                    _lastSwingHighIndex = htfCandle.IndexOfHigh.Value;
                    _lastSwingHighValue = high;
                    _lastSwingWasHigh = true;
                    _lastSwingWasLow = false;

                    // Create new high swing point
                    if (htfCandle.TimeOfHigh != null)
                    {
                        var highSwingPoint = new SwingPoint(
                            htfCandle.IndexOfHigh.Value,
                            high,
                            htfCandle.TimeOfHigh.Value,
                            htfCandle,
                            SwingType.H,
                            LiquidityType.Normal,
                            Direction.Up
                        );
                        highSwingPoint.Number = ++_currentSwingPointNumber;
                        AddSwingPointAndPublish(highSwingPoint);
                        _lastHighSwingPoint = highSwingPoint;
                    }

                    return; // Finished processing this candle
                }
            }
        }

        /// <summary>
        /// Checks if the new bar sweeps any important liquidity points (PDH, PDL)
        /// </summary>
        public void CheckForSweptLiquidity(Candle currentBar, int currentIndex)
        {
            // Get all swing points that could be swept
            var liquidityPoints = _swingPoints.Where(sp =>
                (sp.LiquidityType == LiquidityType.PDH ||
                sp.LiquidityType == LiquidityType.PDL) && !sp.Swept).ToList();

            foreach (var point in liquidityPoints)
            {
                // Skip already swept points
                if (point.Swept)
                    continue;

                bool wasSwept = false;

                // Check for swept highs (PDH)
                if (point.LiquidityType == LiquidityType.PDH &&
                    currentBar.High >= point.Price && point.Index < currentIndex)
                {
                    wasSwept = true;
                }
                // Check for swept lows (PDL)
                else if (point.LiquidityType == LiquidityType.PDL &&
                         currentBar.Low <= point.Price && point.Index < currentIndex)
                {
                    wasSwept = true;
                }

                // If the point was swept, handle it
                if (wasSwept)
                {
                    point.Swept = true;
                    point.IndexOfSweepingCandle = currentIndex;

                    // Notify via the SweptLiquidity event
                    OnLiquiditySwept(point, currentIndex, currentBar);
                }
            }
        }

// Define delegate and event for swept liquidity
        public delegate void LiquiditySweptEventHandler(SwingPoint sweptPoint, int sweepingCandleIndex,
            Candle sweepingCandle);

        public event LiquiditySweptEventHandler LiquiditySwept;

// Method to trigger the event
        protected virtual void OnLiquiditySwept(SwingPoint sweptPoint, int sweepingCandleIndex, Candle sweepingCandle)
        {
            LiquiditySwept?.Invoke(sweptPoint, sweepingCandleIndex, sweepingCandle);
        }

        // Methods to retrieve swing points
        public List<SwingPoint> GetAllSwingPoints()
        {
            return _swingPoints;
        }

        public List<SwingPoint> GetSwingHighs()
        {
            return _swingPoints.FindAll(sp => sp.SwingType == SwingType.H);
        }

        public List<SwingPoint> GetSwingLows()
        {
            return _swingPoints.FindAll(sp => sp.SwingType == SwingType.L);
        }

        public SwingPoint GetLastSwingHigh()
        {
            return _lastHighSwingPoint;
        }

        public SwingPoint GetLastSwingLow()
        {
            return _lastLowSwingPoint;
        }

        // Get swing point at specific index
        public SwingPoint GetSwingPointAtIndex(int index)
        {
            return _swingPoints.Find(sp => sp.Index == index);
        }

        public List<SwingPoint> GetSwingPointsAtIndex(int index)
        {
            return _swingPoints.FindAll(sp => sp.Index == index);
        }

        // Check if there's a swing point at specific index
        public bool HasSwingPointAtIndex(int index)
        {
            return _swingPoints.Exists(sp => sp.Index == index);
        }

        // Get previous and next swing points
        public SwingPoint GetPreviousSwingPoint(SwingPoint currentPoint)
        {
            if (currentPoint == null) return null;

            return _swingPoints
                .FindLast(sp => sp.Index < currentPoint.Index && sp.SwingType == currentPoint.SwingType);
        }

        public SwingPoint GetNextSwingPoint(SwingPoint currentPoint)
        {
            if (currentPoint == null) return null;

            return _swingPoints
                .Find(sp => sp.Index > currentPoint.Index && sp.SwingType == currentPoint.SwingType);
        }

        // Update previous and next pointers for all swing points
        public void UpdateSwingPointRelationships()
        {
            // Sort by index to ensure proper order
            _swingPoints.Sort((a, b) => a.Index.CompareTo(b.Index));

            // Process high swing points
            var highPoints = GetSwingHighs();
            highPoints.Sort((a, b) => a.Index.CompareTo(b.Index));

            for (int i = 0; i < highPoints.Count; i++)
            {
                if (i > 0)
                {
                    highPoints[i].PreviousIndex = highPoints[i - 1].Index;
                }

                if (i < highPoints.Count - 1)
                {
                    highPoints[i].NextIndex = highPoints[i + 1].Index;
                }
            }

            // Process low swing points
            var lowPoints = GetSwingLows();
            lowPoints.Sort((a, b) => a.Index.CompareTo(b.Index));

            for (int i = 0; i < lowPoints.Count; i++)
            {
                if (i > 0)
                {
                    lowPoints[i].PreviousIndex = lowPoints[i - 1].Index;
                }

                if (i < lowPoints.Count - 1)
                {
                    lowPoints[i].NextIndex = lowPoints[i + 1].Index;
                }
            }
        }
    }
}