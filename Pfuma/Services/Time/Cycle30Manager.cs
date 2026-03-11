using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Services.Time
{
    public class Cycle30Manager
    {
        private readonly CandleManager _candleManager;
        private readonly Chart _chart;
        private readonly int _utcOffset;
        private readonly Action<string> _logger;

        private DateTime _currentCycleStart = DateTime.MinValue;
        private int _currentCycleStartIndex = -1;
        private double _currentCycleHigh = double.MinValue;
        private double _currentCycleLow = double.MaxValue;
        private int _currentCycleHighIndex = -1;
        private int _currentCycleLowIndex = -1;

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

        public void ProcessBar(int index, DateTime utcTime)
        {
            var candle = _candleManager.GetCandle(index);
            if (candle == null) return;

            DateTime localTime = utcTime.AddHours(_utcOffset);
            DateTime cycleStart = GetCycleStart(localTime);

            if (_currentCycleStart == DateTime.MinValue)
            {
                InitializeNewCycle(cycleStart, index);
            }
            else if (cycleStart != _currentCycleStart)
            {
                ProcessPreviousCycle();
                InitializeNewCycle(cycleStart, index);
            }

            UpdateCurrentCycleExtremes(candle, index);
        }

        private DateTime GetCycleStart(DateTime localTime)
        {
            int minutes = localTime.Minute < 30 ? 0 : 30;
            return new DateTime(localTime.Year, localTime.Month, localTime.Day, localTime.Hour, minutes, 0);
        }

        public bool IsCycleStartingAtMinute00(DateTime cycleStart)
        {
            return cycleStart.Minute == 0;
        }

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

        private void ProcessPreviousCycle()
        {
            if (_currentCycleHighIndex == -1 || _currentCycleLowIndex == -1)
                return;

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

        public IEnumerable<SwingPoint> GetCycleHighs()
        {
            return Cycles30.Where(sp => sp.SwingType == SwingType.H);
        }

        public IEnumerable<SwingPoint> GetCycleLows()
        {
            return Cycles30.Where(sp => sp.SwingType == SwingType.L);
        }

        public DateTime GetCurrentCycleStart()
        {
            return _currentCycleStart;
        }

        public int GetCurrentCycleStartIndex()
        {
            return _currentCycleStartIndex;
        }

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