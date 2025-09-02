using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Interfaces;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Services;
using FibonacciLevel = Pfuma.Models.FibonacciLevel;

namespace Pfuma.Visualization
{
    public class FibonacciVisualizer
    {
        private readonly Chart _chart;
        private readonly IFibonacciService _fibonacciService;
        private readonly IEventAggregator _eventAggregator;
        private bool _showFibonacciLevels;
        private readonly Dictionary<string, List<string>> _drawnObjects;
        private readonly List<string> _extendedLineIds; // Separate tracking for extended lines
        private readonly CandleManager _candleManager;
        private readonly Action<string> _log;
        
        public bool ShowFibonacciLevels 
        { 
            get => _showFibonacciLevels;
            set => _showFibonacciLevels = value;
        }
        
        public FibonacciVisualizer(Chart chart, IFibonacciService fibonacciService, IEventAggregator eventAggregator, CandleManager candleManager, bool showFibonacciLevels, Action<string> log = null)
        {
            _chart = chart;
            _fibonacciService = fibonacciService;
            _eventAggregator = eventAggregator;
            _candleManager = candleManager;
            _showFibonacciLevels = showFibonacciLevels;
            _log = log;
            _drawnObjects = new Dictionary<string, List<string>>();
            _extendedLineIds = new List<string>();
            
            
            // Subscribe to level removal events
            if (_fibonacciService != null)
            {
                _fibonacciService.LevelRemoved += OnFibonacciLevelRemoved;
            }
            
            // Subscribe to sweep/break events
            if (_eventAggregator != null)
            {
                _eventAggregator.Subscribe<Services.FibonacciLevelSweptEvent>(OnFibonacciLevelSwept);
            }
        }
        
        private void OnFibonacciLevelRemoved(FibonacciLevel removedLevel)
        {
            if (removedLevel != null && !string.IsNullOrEmpty(removedLevel.FibonacciId))
            {
                // Always remove all regular drawings for this level
                // Extended lines are tracked separately and will persist
                RemoveAllFibonacciDrawings(removedLevel);
            }
        }
        
        private void OnFibonacciLevelSwept(Services.FibonacciLevelSweptEvent sweepEvent)
        {
            if (sweepEvent == null || sweepEvent.FibonacciLevel == null) 
                return;
            
            // Check if visualizer is enabled
            if (!_showFibonacciLevels) 
                return;
            
            var fibLevel = sweepEvent.FibonacciLevel;
            string lineId = $"{fibLevel.FibonacciId}_line_{sweepEvent.SweptRatio:F3}";
            string labelId = $"{lineId}-label";
            
            if (sweepEvent.IsBreak)
            {
                // Remove the line and label for broken levels
                RemoveSpecificLine(lineId, labelId);
            }
            else if (sweepEvent.IsSweep)
            {
                // Extend the line to the sweep candle
                ExtendLineToSweep(fibLevel, lineId, labelId, sweepEvent.SweptRatio, sweepEvent.SweptPrice, sweepEvent.SweepIndex);
            }
        }
        
        private void RemoveSpecificLine(string lineId, string labelId)
        {
            if (_chart == null) 
                return;
            
            try
            {
                // Try to remove the original line (might not exist if already extended)
                _chart.RemoveObject(lineId);
                _chart.RemoveObject(labelId);
                
                // Also try to remove any extended version that might exist
                string extendedLineId = lineId + "-extended";
                string extendedLabelId = extendedLineId + "-label";
                
                _chart.RemoveObject(extendedLineId);
                _chart.RemoveObject(extendedLabelId);
                
                // Remove from regular tracking
                foreach (var kvp in _drawnObjects)
                {
                    kvp.Value.Remove(lineId);
                    kvp.Value.Remove(labelId);
                }
                
                // Remove from extended tracking
                _extendedLineIds.Remove(extendedLineId);
                _extendedLineIds.Remove(extendedLabelId);
            }
            catch
            {
                // Silently handle removal errors
            }
        }
        
        private void ExtendLineToSweep(FibonacciLevel fibLevel, string lineId, string labelId, double ratio, double price, int sweepIndex)
        {
            if (_chart == null) 
                return;
            
            try
            {
                // Redraw with extended endpoint and force remove existing
                string labelText = GetFibonacciLabelText(ratio);
                
                string extendedLineId = lineId + "-extended";
                string extendedLabelId = extendedLineId + "-label";
                
                // Use removeExisting=true to force removal of any conflicting objects
                _chart.DrawStraightLine(
                    extendedLineId,
                    fibLevel.StartIndex,
                    price,
                    sweepIndex,  // Extend to the sweep candle
                    price,
                    labelText,
                    LineStyle.Solid,
                    Color.Orange,  // Change color to indicate swept level
                    true,
                    true  // removeExisting = true to force cleanup
                );
                
                // Now explicitly remove the original line to ensure it's gone
                _chart.RemoveObject(lineId);
                _chart.RemoveObject(labelId);
                
                // Remove old IDs from regular tracking
                if (_drawnObjects.ContainsKey(fibLevel.FibonacciId))
                {
                    var objectList = _drawnObjects[fibLevel.FibonacciId];
                    objectList.Remove(lineId);
                    objectList.Remove(labelId);
                }
                
                // Add extended IDs to separate tracking to prevent deletion during cleanup
                if (!_extendedLineIds.Contains(extendedLineId))
                {
                    _extendedLineIds.Add(extendedLineId);
                }
                if (!_extendedLineIds.Contains(extendedLabelId))
                {
                    _extendedLineIds.Add(extendedLabelId);
                }
            }
            catch
            {
                // Silently handle extension errors
            }
        }
        
        public void DrawFibonacciLevels()
        {
            if (!_showFibonacciLevels || _chart == null) return;
            
            var levels = _fibonacciService.GetFibonacciLevels();
            
            foreach (var fibLevel in levels)
            {
                DrawFibonacciLevel(fibLevel);
            }
        }
        
        private void DrawFibonacciLevel(FibonacciLevel fibLevel)
        {
            if (fibLevel == null || string.IsNullOrEmpty(fibLevel.FibonacciId)) return;
            
            // Clean up old drawings for this Fibonacci level if they exist
            RemoveFibonacciDrawings(fibLevel.FibonacciId);
            
            var objectIds = new List<string>();
            
            // Draw each Fibonacci ratio level
            foreach (var kvp in fibLevel.Levels)
            {
                double ratio = kvp.Key;
                double price = kvp.Value;
                
                // Generate unique ID for this line
                string lineId = $"{fibLevel.FibonacciId}_line_{ratio:F3}";
                string labelText = GetFibonacciLabelText(ratio);
                
                // Draw the horizontal line with label
                _chart.DrawStraightLine(
                    lineId,
                    fibLevel.StartIndex,
                    price,
                    fibLevel.EndIndex,
                    price,
                    labelText,
                    LineStyle.Solid,
                    Color.Gray,
                    true,  // hasLabel = true
                    false  // removeExisting = false
                );
                
                // Add both the line ID and the label ID (created by DrawStraightLine)
                objectIds.Add(lineId);
                objectIds.Add($"{lineId}-label");  // DrawStraightLine creates label with this ID
            }
            
            // Store the object IDs for later cleanup
            _drawnObjects[fibLevel.FibonacciId] = objectIds;
        }
        
        private string GetFibonacciLabelText(double ratio)
        {
            // Format common Fibonacci ratios with their standard names
            return ratio switch
            {
                -2.0 => "-200%",
                -1.5 => "-150%",
                -1.0 => "-100%",
                -0.5 => "-50%",
                -0.25 => "-25%",
                0.0 => "0%",
                0.114 => "11.4%",
                0.886 => "88.6%",
                1.0 => "100%",
                _ => $"{ratio * 100:F1}%"
            };
        }
        
        private void RemoveNonSweptFibonacciDrawings(FibonacciLevel fibLevel)
        {
            if (_chart == null || fibLevel == null) return;
            
            if (_drawnObjects.ContainsKey(fibLevel.FibonacciId))
            {
                var objectIds = _drawnObjects[fibLevel.FibonacciId];
                var objectsToRemove = new List<string>();
                
                // Identify which objects correspond to non-swept ratios
                foreach (var kvp in fibLevel.Levels)
                {
                    double ratio = kvp.Key;
                    
                    // If this ratio was not swept, remove its visual elements
                    if (!fibLevel.SweptLevels.ContainsKey(ratio) || !fibLevel.SweptLevels[ratio])
                    {
                        string lineId = $"{fibLevel.FibonacciId}_line_{ratio:F3}";
                        string labelId = $"{lineId}-label";
                        
                        objectsToRemove.Add(lineId);
                        objectsToRemove.Add(labelId);
                    }
                }
                
                // Remove the non-swept objects from chart and tracking
                foreach (var objectId in objectsToRemove)
                {
                    try
                    {
                        _chart.RemoveObject(objectId);
                        objectIds.Remove(objectId);
                    }
                    catch
                    {
                        // Silently handle if object doesn't exist
                    }
                }
                
                // If no objects remain for this Fibonacci ID, remove the key entirely
                if (objectIds.Count == 0)
                {
                    _drawnObjects.Remove(fibLevel.FibonacciId);
                }
                
            }
        }
        
        private void RemoveAllFibonacciDrawings(FibonacciLevel fibLevel)
        {
            if (_chart == null || fibLevel == null) return;
            
            // Remove all objects that are tracked in _drawnObjects for this fibonacci level
            if (_drawnObjects.ContainsKey(fibLevel.FibonacciId))
            {
                var trackedObjects = _drawnObjects[fibLevel.FibonacciId].ToList();
                foreach (var objectId in trackedObjects)
                {
                    try
                    {
                        _chart.RemoveObject(objectId);
                    }
                    catch
                    {
                        // Silently handle if object doesn't exist
                    }
                }
                _drawnObjects.Remove(fibLevel.FibonacciId);
            }
            
            // Also attempt to remove all possible line IDs for this fibonacci level
            // This catches any lines that might not be in tracking (e.g., due to extended line handling)
            foreach (var kvp in fibLevel.Levels)
            {
                double ratio = kvp.Key;
                string lineId = $"{fibLevel.FibonacciId}_line_{ratio:F3}";
                string labelId = $"{lineId}-label";
                
                try
                {
                    _chart.RemoveObject(lineId);
                    _chart.RemoveObject(labelId);
                }
                catch
                {
                    // Silently handle if object doesn't exist
                }
            }
            
            // Also remove extended lines that belong to this FibonacciLevel
            var extendedLinesToRemove = new List<string>();
            foreach (var extendedId in _extendedLineIds)
            {
                if (extendedId.Contains(fibLevel.FibonacciId))
                {
                    extendedLinesToRemove.Add(extendedId);
                    try
                    {
                        _chart.RemoveObject(extendedId);
                    }
                    catch
                    {
                        // Silently handle if object doesn't exist
                    }
                }
            }
            
            // Remove from extended tracking
            foreach (var extendedId in extendedLinesToRemove)
            {
                _extendedLineIds.Remove(extendedId);
            }
        }
        
        private void RemoveFibonacciDrawings(string fibonacciId)
        {
            if (_chart == null) return;
            
            if (_drawnObjects.ContainsKey(fibonacciId))
            {
                foreach (var objectId in _drawnObjects[fibonacciId])
                {
                    try
                    {
                        _chart.RemoveObject(objectId);
                    }
                    catch
                    {
                        // Silently handle if object doesn't exist
                    }
                }
                _drawnObjects.Remove(fibonacciId);
            }
        }
        
        // Extended lines should never be automatically removed - they persist permanently

        public void ClearAllDrawings()
        {
            // Create a copy of keys to avoid collection modification during iteration
            var keys = new List<string>(_drawnObjects.Keys);
            foreach (var fibId in keys)
            {
                RemoveFibonacciDrawings(fibId);
            }
            _drawnObjects.Clear();
            
            // NOTE: Extended lines are NOT cleared here - they persist permanently
            // Only clear extended line tracking on disposal
        }
        
        public void Dispose()
        {
            // Unsubscribe from events
            if (_fibonacciService != null)
            {
                _fibonacciService.LevelRemoved -= OnFibonacciLevelRemoved;
            }
            
            if (_eventAggregator != null)
            {
                _eventAggregator.Unsubscribe<Services.FibonacciLevelSweptEvent>(OnFibonacciLevelSwept);
            }
            
            // Clear all regular drawings
            ClearAllDrawings();
            
            // Clear extended lines only on disposal
            foreach (var extendedId in _extendedLineIds)
            {
                try
                {
                    _chart.RemoveObject(extendedId);
                }
                catch
                {
                    // Silently handle if object doesn't exist
                }
            }
            _extendedLineIds.Clear();
        }
    }
}