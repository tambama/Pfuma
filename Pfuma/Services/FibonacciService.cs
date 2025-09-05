using System;
using System.Collections.Generic;
using System.Linq;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;

namespace Pfuma.Services
{
    public interface IFibonacciService
    {
        List<FibonacciLevel> GetFibonacciLevels();
        FibonacciLevel GetLatestLevel();
        FibonacciLevel RemoveOldestLevel();
        void ClearOldLevels(int maxLevels = 2);
        void CheckAndRemoveFullySweptLevels();
        event Action<FibonacciLevel> LevelRemoved;
        event Action<FibonacciLevel> LevelFullySwept;
    }
    
    public class FibonacciService : IFibonacciService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly List<FibonacciLevel> _fibonacciLevels;
        private const int MaxLevels = 2; // Only keep 2 levels at most
        
        public event Action<FibonacciLevel> LevelRemoved;
        public event Action<FibonacciLevel> LevelFullySwept;
        
        public FibonacciService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _fibonacciLevels = new List<FibonacciLevel>();
            
            // Subscribe to time cycle events
            _eventAggregator.Subscribe<TimeCycleDetectedEvent>(OnTimeCycleDetected);
        }
        
        private void OnTimeCycleDetected(TimeCycleDetectedEvent cycleEvent)
        {
            if (cycleEvent?.CompletedCycle == null) return;
            
            var cycle = cycleEvent.CompletedCycle;
            
            // Ensure we have valid data
            if (!cycle.HighPrice.HasValue || !cycle.LowPrice.HasValue ||
                !cycle.HighIndex.HasValue || !cycle.LowIndex.HasValue ||
                !cycle.HighTime.HasValue || !cycle.LowTime.HasValue)
            {
                return;
            }
            
            // Determine the order based on indices
            int startIndex, endIndex;
            double startPrice, endPrice;
            DateTime startTime, endTime;
            
            if (cycle.HighIndex.Value > cycle.LowIndex.Value)
            {
                // High comes after low - uptrend
                startIndex = cycle.LowIndex.Value;
                endIndex = cycle.HighIndex.Value;
                startPrice = cycle.LowPrice.Value;
                endPrice = cycle.HighPrice.Value;
                startTime = cycle.LowTime.Value;
                endTime = cycle.HighTime.Value;
            }
            else
            {
                // Low comes after high - downtrend
                startIndex = cycle.HighIndex.Value;
                endIndex = cycle.LowIndex.Value;
                startPrice = cycle.HighPrice.Value;
                endPrice = cycle.LowPrice.Value;
                startTime = cycle.HighTime.Value;
                endTime = cycle.LowTime.Value;
            }
            
            // Create Fibonacci level for Cycle
            var fibLevel = new FibonacciLevel(
                startIndex,
                endIndex,
                startPrice,
                endPrice,
                startTime,
                endTime,
                FibType.Cycle
            );
            
            // Generate unique ID for this Fibonacci level
            fibLevel.FibonacciId = $"cycle_fib_{startTime.Ticks}_{endTime.Ticks}";
            
            // Check if we need to remove old levels (keep only 2)
            if (_fibonacciLevels.Count >= MaxLevels)
            {
                // Remove the oldest level
                var oldestLevel = RemoveOldestLevel();
                if (oldestLevel != null)
                {
                    // Notify that a level was removed
                    LevelRemoved?.Invoke(oldestLevel);
                }
            }
            
            // Add the new level
            _fibonacciLevels.Add(fibLevel);
        }
        
        public List<FibonacciLevel> GetFibonacciLevels()
        {
            return new List<FibonacciLevel>(_fibonacciLevels);
        }
        
        public FibonacciLevel GetLatestLevel()
        {
            return _fibonacciLevels.LastOrDefault();
        }
        
        public FibonacciLevel RemoveOldestLevel()
        {
            if (_fibonacciLevels.Count > 0)
            {
                // Find the oldest level that doesn't have any swept levels
                FibonacciLevel levelToRemove = null;
                int indexToRemove = -1;
                
                for (int i = 0; i < _fibonacciLevels.Count; i++)
                {
                    var level = _fibonacciLevels[i];
                    
                    // Check if this level has any swept ratios
                    bool hasSweptLevels = level.SweptLevels.Values.Any(swept => swept);
                    
                    if (!hasSweptLevels)
                    {
                        // This level has no swept ratios, safe to remove
                        levelToRemove = level;
                        indexToRemove = i;
                        break;
                    }
                }
                
                // If no level without swept ratios found, remove the oldest anyway
                // but only remove the non-swept ratios from it
                if (levelToRemove == null)
                {
                    levelToRemove = _fibonacciLevels[0];
                    indexToRemove = 0;
                }
                
                _fibonacciLevels.RemoveAt(indexToRemove);
                return levelToRemove;
            }
            return null;
        }
        
        public void ClearOldLevels(int maxLevels = 2)
        {
            while (_fibonacciLevels.Count > maxLevels)
            {
                var removedLevel = RemoveOldestLevel();
                if (removedLevel != null)
                {
                    LevelRemoved?.Invoke(removedLevel);
                }
            }
        }
        
        public void CheckAndRemoveFullySweptLevels()
        {
            var levelsToRemove = new List<FibonacciLevel>();
            
            foreach (var level in _fibonacciLevels)
            {
                if (level.AreAllLevelsSwept())
                {
                    levelsToRemove.Add(level);
                }
            }
            
            foreach (var levelToRemove in levelsToRemove)
            {
                _fibonacciLevels.Remove(levelToRemove);
                LevelFullySwept?.Invoke(levelToRemove);
            }
        }
    }
}