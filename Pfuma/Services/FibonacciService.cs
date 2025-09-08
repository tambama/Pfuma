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
        List<FibonacciLevel> GetCisdFibonacciLevels();
        FibonacciLevel GetLatestLevel();
        FibonacciLevel RemoveOldestLevel();
        void AddCisdFibonacciLevel(FibonacciLevel level);
        void ClearOldLevels(int maxLevels = 2);
        void ClearOldCisdLevels(int maxLevels = 5);
        void CheckAndRemoveFullySweptLevels();
        event Action<FibonacciLevel> LevelRemoved;
        event Action<FibonacciLevel> LevelFullySwept;
    }
    
    public class FibonacciService : IFibonacciService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly List<FibonacciLevel> _fibonacciLevels;
        private readonly List<FibonacciLevel> _cisdFibonacciLevels;
        private const int MaxLevels = 2; // Only keep 2 levels at most
        private const int MaxCisdLevels = 5; // Keep up to 5 CISD Fibonacci levels
        
        public event Action<FibonacciLevel> LevelRemoved;
        public event Action<FibonacciLevel> LevelFullySwept;
        
        public FibonacciService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _fibonacciLevels = new List<FibonacciLevel>();
            _cisdFibonacciLevels = new List<FibonacciLevel>();
            
            // Subscribe to time cycle events
            _eventAggregator.Subscribe<TimeCycleDetectedEvent>(OnTimeCycleDetected);
            
            // Subscribe to swing point creation events for CISD level sweeping
            _eventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
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
            // Id is already set in the FibonacciLevel constructor
            
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
        
        private void OnSwingPointDetected(SwingPointDetectedEvent swingPointEvent)
        {
            if (swingPointEvent?.SwingPoint == null) return;
            
            var swingPoint = swingPointEvent.SwingPoint;
            
            // Check if this swing point sweeps any CISD Fibonacci levels
            CheckCisdFibonacciSweep(swingPoint);
        }
        
        private void CheckCisdFibonacciSweep(SwingPoint swingPoint)
        {
            var levelsToRemove = new List<FibonacciLevel>();
            
            foreach (var cisdLevel in _cisdFibonacciLevels)
            {
                bool isSwept = false;
                
                // Bearish swing point checks bullish CISD levels (sweeps LowPrice)
                if (swingPoint.Direction == Direction.Down && cisdLevel.Direction == Direction.Up)
                {
                    // Check if bearish swing point breaks below the LowPrice of bullish CISD
                    if (swingPoint.Price < cisdLevel.LowPrice)
                    {
                        // Mark the 0.0 level as swept
                        cisdLevel.SweptLevels[0.0] = true;
                        isSwept = true;
                    }
                }
                // Bullish swing point checks bearish CISD levels (sweeps HighPrice)
                else if (swingPoint.Direction == Direction.Up && cisdLevel.Direction == Direction.Down)
                {
                    // Check if bullish swing point breaks above the HighPrice of bearish CISD
                    if (swingPoint.Price > cisdLevel.HighPrice)
                    {
                        // Mark the 0.0 level as swept
                        cisdLevel.SweptLevels[0.0] = true;
                        isSwept = true;
                    }
                }
                
                if (isSwept)
                {
                    levelsToRemove.Add(cisdLevel);
                }
            }
            
            // Remove swept levels and notify
            foreach (var levelToRemove in levelsToRemove)
            {
                _cisdFibonacciLevels.Remove(levelToRemove);
                LevelFullySwept?.Invoke(levelToRemove);
            }
        }
        
        public List<FibonacciLevel> GetFibonacciLevels()
        {
            return new List<FibonacciLevel>(_fibonacciLevels);
        }
        
        public List<FibonacciLevel> GetCisdFibonacciLevels()
        {
            return new List<FibonacciLevel>(_cisdFibonacciLevels);
        }
        
        public void AddCisdFibonacciLevel(FibonacciLevel level)
        {
            if (level == null || level.FibType != FibType.CISD) return;
            
            // Check if we need to remove old CISD levels (keep only MaxCisdLevels)
            if (_cisdFibonacciLevels.Count >= MaxCisdLevels)
            {
                // Remove the oldest CISD level
                var oldestLevel = _cisdFibonacciLevels[0];
                _cisdFibonacciLevels.RemoveAt(0);
                
                // Notify that a level was removed
                LevelRemoved?.Invoke(oldestLevel);
            }
            
            // Add the new CISD Fibonacci level
            _cisdFibonacciLevels.Add(level);
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
        
        public void ClearOldCisdLevels(int maxLevels = 5)
        {
            while (_cisdFibonacciLevels.Count > maxLevels)
            {
                var oldestLevel = _cisdFibonacciLevels[0];
                _cisdFibonacciLevels.RemoveAt(0);
                LevelRemoved?.Invoke(oldestLevel);
            }
        }
        
        public void CheckAndRemoveFullySweptLevels()
        {
            // Check and remove fully swept Cycle Fibonacci levels
            var cycleLevelsToRemove = new List<FibonacciLevel>();
            
            foreach (var level in _fibonacciLevels)
            {
                if (level.AreAllLevelsSwept())
                {
                    cycleLevelsToRemove.Add(level);
                }
            }
            
            foreach (var levelToRemove in cycleLevelsToRemove)
            {
                _fibonacciLevels.Remove(levelToRemove);
                LevelFullySwept?.Invoke(levelToRemove);
            }
            
            // Check and remove fully swept CISD Fibonacci levels
            var cisdLevelsToRemove = new List<FibonacciLevel>();
            
            foreach (var level in _cisdFibonacciLevels)
            {
                if (level.AreAllLevelsSwept())
                {
                    cisdLevelsToRemove.Add(level);
                }
            }
            
            foreach (var levelToRemove in cisdLevelsToRemove)
            {
                _cisdFibonacciLevels.Remove(levelToRemove);
                LevelFullySwept?.Invoke(levelToRemove);
            }
        }
    }
}