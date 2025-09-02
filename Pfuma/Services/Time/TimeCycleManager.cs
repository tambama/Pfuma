using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Services.Time
{
    public interface ITimeCycleManager
    {
        void ProcessBar(int index, DateTime marketTime);
        TimeCycle GetCurrentCycle();
        List<TimeCycle> GetCompletedCycles();
    }
    
    public class TimeCycleManager : ITimeCycleManager
    {
        private readonly CandleManager _candleManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly List<TimeCycle> _completedCycles;
        private TimeCycle _currentCycle;
        private readonly List<(TimeSpan start, TimeSpan end)> _cycleDefinitions;
        
        public TimeCycleManager(CandleManager candleManager, IEventAggregator eventAggregator)
        {
            _candleManager = candleManager;
            _eventAggregator = eventAggregator;
            _completedCycles = new List<TimeCycle>();
            
            // Define all time cycles
            _cycleDefinitions = new List<(TimeSpan start, TimeSpan end)>
            {
                (new TimeSpan(0, 0, 0), new TimeSpan(1, 30, 0)),
                (new TimeSpan(1, 30, 0), new TimeSpan(3, 0, 0)),
                (new TimeSpan(3, 0, 0), new TimeSpan(4, 30, 0)),
                (new TimeSpan(4, 30, 0), new TimeSpan(6, 0, 0)),
                (new TimeSpan(6, 0, 0), new TimeSpan(7, 30, 0)),
                (new TimeSpan(7, 30, 0), new TimeSpan(9, 0, 0)),
                (new TimeSpan(9, 0, 0), new TimeSpan(10, 30, 0)),
                (new TimeSpan(10, 30, 0), new TimeSpan(12, 0, 0)),
                (new TimeSpan(12, 0, 0), new TimeSpan(13, 30, 0)),
                (new TimeSpan(13, 30, 0), new TimeSpan(15, 0, 0)),
                (new TimeSpan(15, 0, 0), new TimeSpan(16, 30, 0)),
                (new TimeSpan(16, 30, 0), new TimeSpan(18, 0, 0)),
                (new TimeSpan(18, 0, 0), new TimeSpan(19, 30, 0)),
                (new TimeSpan(19, 30, 0), new TimeSpan(21, 0, 0)),
                (new TimeSpan(21, 0, 0), new TimeSpan(22, 30, 0)),
                (new TimeSpan(22, 30, 0), new TimeSpan(0, 0, 0)) // This represents 22:30 to midnight (next day)
            };
        }
        
        public void ProcessBar(int index, DateTime marketTime)
        {
            if (index >= _candleManager.Count) return;
            
            var candle = _candleManager.GetCandle(index);
            if (candle == null) return;
            
            TimeSpan currentTimeOfDay = marketTime.TimeOfDay;
            
            // Special handling for midnight (00:00) - complete the 22:30-00:00 cycle
            if (currentTimeOfDay == TimeSpan.Zero && _currentCycle != null && 
                _currentCycle.StartTime == new TimeSpan(22, 30, 0) && _currentCycle.EndTime == TimeSpan.Zero)
            {
                // Complete the 22:30-00:00 cycle at midnight
                if (_currentCycle.HasData())
                {
                    CompleteCycle(_currentCycle);
                }
                _currentCycle = null;
            }
            
            var currentCycleDefinition = GetCycleDefinition(currentTimeOfDay);
            
            // Check if we need to start a new cycle
            if (_currentCycle == null || !IsInSameCycle(_currentCycle, currentTimeOfDay, marketTime.Date))
            {
                // Complete the previous cycle if it exists and hasn't been completed already
                if (_currentCycle != null && _currentCycle.HasData())
                {
                    CompleteCycle(_currentCycle);
                }
                
                // Start new cycle
                _currentCycle = new TimeCycle(currentCycleDefinition.start, currentCycleDefinition.end, marketTime.Date);
            }
            
            // Update current cycle with bar data
            if (_currentCycle != null)
            {
                _currentCycle.UpdateHigh(candle.High, index, marketTime);
                _currentCycle.UpdateLow(candle.Low, index, marketTime);
            }
        }
        
        private (TimeSpan start, TimeSpan end) GetCycleDefinition(TimeSpan timeOfDay)
        {
            foreach (var cycle in _cycleDefinitions)
            {
                if (cycle.start == new TimeSpan(22, 30, 0) && cycle.end == TimeSpan.Zero) // Handle 22:30-00:00 cycle
                {
                    if (timeOfDay >= cycle.start) // 22:30 onwards on same day
                    {
                        return cycle;
                    }
                }
                else if (timeOfDay >= cycle.start && timeOfDay < cycle.end)
                {
                    return cycle;
                }
            }
            
            // Default to first cycle if no match (shouldn't happen)
            return _cycleDefinitions[0];
        }
        
        private bool IsInSameCycle(TimeCycle cycle, TimeSpan currentTimeOfDay, DateTime currentDate)
        {
            // Handle the 22:30-00:00 cycle that spans midnight
            if (cycle.StartTime == new TimeSpan(22, 30, 0) && cycle.EndTime == TimeSpan.Zero)
            {
                // If we're on the same date and time is >= 22:30
                if (cycle.Date.Date == currentDate.Date && currentTimeOfDay >= cycle.StartTime)
                {
                    return true;
                }
                // If we're on the next date and time is < 00:00 (which is always true for next day)
                // But we only want to continue until exactly 00:00
                if (cycle.Date.Date.AddDays(1) == currentDate.Date && currentTimeOfDay == TimeSpan.Zero)
                {
                    return false; // 00:00 starts the new cycle
                }
                return false;
            }
            
            // Regular cycles within same day
            if (cycle.Date.Date != currentDate.Date)
            {
                return false;
            }
            
            return currentTimeOfDay >= cycle.StartTime && currentTimeOfDay < cycle.EndTime;
        }
        
        private void CompleteCycle(TimeCycle cycle)
        {
            if (!cycle.HasData()) return;
            
            cycle.IsComplete = true;
            _completedCycles.Add(cycle);
            
            // Publish event for completed cycle
            _eventAggregator?.Publish(new TimeCycleDetectedEvent(cycle));
        }
        
        public TimeCycle GetCurrentCycle()
        {
            return _currentCycle;
        }
        
        public List<TimeCycle> GetCompletedCycles()
        {
            return new List<TimeCycle>(_completedCycles);
        }
    }
}