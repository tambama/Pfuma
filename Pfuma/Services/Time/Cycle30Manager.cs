using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Services.Time
{
    /// <summary>
    /// Manages 30-minute cycle tracking and high/low detection
    /// </summary>
    public class Cycle30Manager
    {
        private readonly CandleManager _candleManager;
        private readonly Chart _chart;
        private readonly int _utcOffset;
        private readonly Action<string> _logger;

        // Current cycle tracking
        private DateTime _currentCycleStart = DateTime.MinValue;
        private int _currentCycleStartIndex = -1;
        private double _currentCycleHigh = double.MinValue;
        private double _currentCycleLow = double.MaxValue;
        private int _currentCycleHighIndex = -1;
        private int _currentCycleLowIndex = -1;

        // Cycle swing points collection
        public List<SwingPoint> Cycles30 { get; private set; } = new List<SwingPoint>();

        public Cycle30Manager(
            CandleManager candleManager,
            Chart chart,
            int utcOffset = 0,
            Action<string> logger = null)
        {
            _candleManager = candleManager;
            _chart = chart;
            _utcOffset = utcOffset;
            _logger = logger ?? (_ => { });
        }

        /// <summary>
        /// Process a bar for 30-minute cycle tracking
        /// </summary>
        public void ProcessBar(int index, DateTime utcTime)
        {
            var candle = _candleManager.GetCandle(index);
            if (candle == null) return;

            // Convert to local time
            DateTime localTime = utcTime.AddHours(_utcOffset);

            // Check if we're starting a new 30-minute cycle
            DateTime cycleStart = GetCycleStart(localTime);

            if (_currentCycleStart == DateTime.MinValue)
            {
                // First cycle initialization
                InitializeNewCycle(cycleStart, index);
            }
            else if (cycleStart != _currentCycleStart)
            {
                // New cycle detected - process previous cycle
                ProcessPreviousCycle();
                InitializeNewCycle(cycleStart, index);
            }

            // Update current cycle high/low
            UpdateCurrentCycleExtremes(candle, index);
        }

        /// <summary>
        /// Get the start time of the 30-minute cycle containing the given time
        /// </summary>
        private DateTime GetCycleStart(DateTime localTime)
        {
            int minutes = localTime.Minute < 30 ? 0 : 30;
            return new DateTime(localTime.Year, localTime.Month, localTime.Day, localTime.Hour, minutes, 0);
        }

        /// <summary>
        /// Determine if a cycle starts at minute 00 (green) or 30 (pink)
        /// </summary>
        public bool IsCycleStartingAtMinute00(DateTime cycleStart)
        {
            return cycleStart.Minute == 0;
        }

        /// <summary>
        /// Initialize a new cycle
        /// </summary>
        private void InitializeNewCycle(DateTime cycleStart, int startIndex)
        {
            _currentCycleStart = cycleStart;
            _currentCycleStartIndex = startIndex;
            _currentCycleHigh = double.MinValue;
            _currentCycleLow = double.MaxValue;
            _currentCycleHighIndex = -1;
            _currentCycleLowIndex = -1;

            _logger($"New 30-minute cycle started at {cycleStart:HH:mm} (index {startIndex})");
        }

        /// <summary>
        /// Update current cycle extremes
        /// </summary>
        private void UpdateCurrentCycleExtremes(Candle candle, int index)
        {
            if (candle.High > _currentCycleHigh)
            {
                _currentCycleHigh = candle.High;
                _currentCycleHighIndex = index;
            }

            if (candle.Low < _currentCycleLow)
            {
                _currentCycleLow = candle.Low;
                _currentCycleLowIndex = index;
            }
        }

        /// <summary>
        /// Process the completed previous cycle - create swing points for high/low
        /// </summary>
        private void ProcessPreviousCycle()
        {
            if (_currentCycleHighIndex == -1 || _currentCycleLowIndex == -1)
                return;

            // Create cycle high swing point
            var cycleHighCandle = _candleManager.GetCandle(_currentCycleHighIndex);
            if (cycleHighCandle != null)
            {
                var cycleHigh = new SwingPoint(
                    _currentCycleHighIndex,
                    _currentCycleHigh,
                    cycleHighCandle.Time,
                    cycleHighCandle,
                    SwingType.H,
                    LiquidityType.Cycle,
                    Direction.Up
                );
                cycleHigh.Number = Cycles30.Count + 1;
                Cycles30.Add(cycleHigh);

                _logger($"Cycle30 High created: {_currentCycleHigh:F5} at index {_currentCycleHighIndex}");
            }

            // Create cycle low swing point
            var cycleLowCandle = _candleManager.GetCandle(_currentCycleLowIndex);
            if (cycleLowCandle != null)
            {
                var cycleLow = new SwingPoint(
                    _currentCycleLowIndex,
                    _currentCycleLow,
                    cycleLowCandle.Time,
                    cycleLowCandle,
                    SwingType.L,
                    LiquidityType.Cycle,
                    Direction.Down
                );
                cycleLow.Number = Cycles30.Count + 1;
                Cycles30.Add(cycleLow);

                _logger($"Cycle30 Low created: {_currentCycleLow:F5} at index {_currentCycleLowIndex}");
            }
        }

        /// <summary>
        /// Get all cycle highs
        /// </summary>
        public IEnumerable<SwingPoint> GetCycleHighs()
        {
            return Cycles30.Where(sp => sp.SwingType == SwingType.H);
        }

        /// <summary>
        /// Get all cycle lows
        /// </summary>
        public IEnumerable<SwingPoint> GetCycleLows()
        {
            return Cycles30.Where(sp => sp.SwingType == SwingType.L);
        }

        /// <summary>
        /// Get current cycle start time for visualization
        /// </summary>
        public DateTime GetCurrentCycleStart()
        {
            return _currentCycleStart;
        }

        /// <summary>
        /// Get current cycle start index for visualization
        /// </summary>
        public int GetCurrentCycleStartIndex()
        {
            return _currentCycleStartIndex;
        }

        /// <summary>
        /// Remove a swept cycle point from the collection to prevent duplicate sweeps
        /// </summary>
        public void RemoveSweptCyclePoint(SwingPoint sweptPoint)
        {
            if (sweptPoint?.LiquidityType == LiquidityType.Cycle)
            {
                Cycles30.Remove(sweptPoint);
                _logger($"Removed swept cycle point: {sweptPoint.SwingType} at {sweptPoint.Price:F5} (index {sweptPoint.Index})");
            }
        }
    }
}