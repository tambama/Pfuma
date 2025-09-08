using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors;
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
        private bool _showCycleFibLevels;
        private bool _showCISDFibLevels;
        private bool _showExtendedFib;
        private readonly Dictionary<string, List<string>> _drawnObjects;
        private readonly List<string> _extendedLineIds; // Separate tracking for extended lines
        private readonly CandleManager _candleManager;
        private readonly Action<string> _log;
        
        public bool ShowCycleFibLevels 
        { 
            get => _showCycleFibLevels;
            set => _showCycleFibLevels = value;
        }
        
        public bool ShowCISDFibLevels
        {
            get => _showCISDFibLevels;
            set => _showCISDFibLevels = value;
        }
        
        public bool ShowExtendedFib
        {
            get => _showExtendedFib;
            set => _showExtendedFib = value;
        }
        
        public FibonacciVisualizer(Chart chart, IFibonacciService fibonacciService, IEventAggregator eventAggregator, CandleManager candleManager, bool showCycleFibLevels, bool showCISDFibLevels, bool showExtendedFib, Action<string> log = null)
        {
            _chart = chart;
            _fibonacciService = fibonacciService;
            _eventAggregator = eventAggregator;
            _candleManager = candleManager;
            _showCycleFibLevels = showCycleFibLevels;
            _showCISDFibLevels = showCISDFibLevels;
            _showExtendedFib = showExtendedFib;
            _log = log;
            _drawnObjects = new Dictionary<string, List<string>>();
            _extendedLineIds = new List<string>();
            
            
            // Subscribe to level removal events
            if (_fibonacciService != null)
            {
                _fibonacciService.LevelRemoved += OnFibonacciLevelRemoved;
                _fibonacciService.LevelFullySwept += OnFibonacciLevelFullySwept;
            }
            
            // Subscribe to sweep/break events
            if (_eventAggregator != null)
            {
                _eventAggregator.Subscribe<FibonacciLevelSweptEvent>(OnFibonacciLevelSwept);
            }
        }
        
        private void OnFibonacciLevelRemoved(FibonacciLevel removedLevel)
        {
            if (removedLevel != null && !string.IsNullOrEmpty(removedLevel.Id))
            {
                RemoveFibonacciDrawings(removedLevel.Id);
            }
        }
        
        private void OnFibonacciLevelFullySwept(FibonacciLevel sweptLevel)
        {
            if (sweptLevel != null && !string.IsNullOrEmpty(sweptLevel.Id))
            {
                // Remove all drawings for fully swept level (especially important for CISD levels)
                RemoveFibonacciDrawings(sweptLevel.Id);
            }
        }
        
        private void OnFibonacciLevelSwept(FibonacciLevelSweptEvent sweepEvent)
        {
            if (sweepEvent == null || sweepEvent.FibonacciLevel == null) 
                return;
            
            var fibLevel = sweepEvent.FibonacciLevel;
            string lineId = $"{fibLevel.Id}_line_{sweepEvent.SweptRatio:F3}";
            string labelId = $"{lineId}-label";
            
            if (sweepEvent.IsBreak)
            {
                // Always remove broken levels regardless of display settings
                RemoveSpecificLine(lineId, labelId);
            }
            else if (sweepEvent.IsSweep && _showExtendedFib)
            {
                // Only extend the line if showExtendedFib is true
                ExtendLineToSweep(fibLevel, lineId, labelId, sweepEvent.SweptRatio, sweepEvent.SweptPrice, sweepEvent.SweepIndex);
            }
            else if (sweepEvent.IsSweep && !_showExtendedFib && (_showCycleFibLevels || _showCISDFibLevels))
            {
                // If not showing extended but showing regular fibs, just remove the swept line
                RemoveSpecificLine(lineId, labelId);
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
                // Now explicitly remove the original line to ensure it's gone
                _chart.RemoveObject(lineId);
                _chart.RemoveObject(labelId);
                
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
                    LineStyle.Dots,
                    Color.Wheat,  // Change color to indicate swept level
                    true,
                    true  // removeExisting = true to force cleanup
                );
                
                // Remove old IDs from regular tracking
                if (_drawnObjects.ContainsKey(fibLevel.Id))
                {
                    var objectList = _drawnObjects[fibLevel.Id];
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
            if (_chart == null) return;
            
            // Draw Cycle Fibonacci levels if enabled
            if (_showCycleFibLevels)
            {
                var cycleLevels = _fibonacciService.GetFibonacciLevels();
                foreach (var fibLevel in cycleLevels)
                {
                    DrawFibonacciLevel(fibLevel);
                }
            }
            else
            {
                // Clear Cycle Fibonacci drawings if disabled
                ClearCycleFibonacciDrawings();
            }
            
            // Draw CISD Fibonacci levels if enabled
            if (_showCISDFibLevels)
            {
                var cisdLevels = _fibonacciService.GetCisdFibonacciLevels();
                foreach (var fibLevel in cisdLevels)
                {
                    DrawFibonacciLevel(fibLevel);
                }
            }
            else
            {
                // Clear CISD Fibonacci drawings if disabled
                ClearCisdFibonacciDrawings();
            }
        }
        
        private void DrawFibonacciLevel(FibonacciLevel fibLevel)
        {
            if (fibLevel == null || string.IsNullOrEmpty(fibLevel.Id)) return;
            
            // Don't clear all drawings - only draw what needs to be drawn
            // This preserves extended lines that have already been drawn for swept levels
            
            var objectIds = new List<string>();
            
            // Get existing tracked objects for this fibonacci level
            if (_drawnObjects.ContainsKey(fibLevel.Id))
            {
                objectIds = _drawnObjects[fibLevel.Id];
            }
            else
            {
                _drawnObjects[fibLevel.Id] = objectIds;
            }
            
            // Draw each Fibonacci ratio level that hasn't been swept
            foreach (var kvp in fibLevel.Levels)
            {
                double ratio = kvp.Key;
                double price = kvp.Value;
                
                // Check if this ratio has been swept - if so, skip drawing it
                if (fibLevel.SweptLevels.ContainsKey(ratio) && fibLevel.SweptLevels[ratio])
                {
                    // This ratio has been swept - it should either be extended or removed
                    // Don't draw the original line
                    continue;
                }
                
                // Generate unique ID for this line
                string lineId = $"{fibLevel.Id}_line_{ratio:F3}";
                string labelId = $"{lineId}-label";
                
                // Check if an extended version exists - if so, skip drawing the original
                string extendedLineId = lineId + "-extended";
                if (_extendedLineIds.Contains(extendedLineId))
                {
                    // Extended line exists, don't draw the original
                    continue;
                }
                
                // Check if this line is already drawn
                if (objectIds.Contains(lineId))
                {
                    // Line already exists, skip redrawing
                    continue;
                }
                
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
                
                // Add both the line ID and the label ID to tracking
                if (!objectIds.Contains(lineId))
                {
                    objectIds.Add(lineId);
                }
                if (!objectIds.Contains(labelId))
                {
                    objectIds.Add(labelId);
                }
            }
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
            
            if (_drawnObjects.ContainsKey(fibLevel.Id))
            {
                var objectIds = _drawnObjects[fibLevel.Id];
                var objectsToRemove = new List<string>();
                
                // Identify which objects correspond to non-swept ratios
                foreach (var kvp in fibLevel.Levels)
                {
                    double ratio = kvp.Key;
                    
                    // If this ratio was not swept, remove its visual elements
                    if (!fibLevel.SweptLevels.ContainsKey(ratio) || !fibLevel.SweptLevels[ratio])
                    {
                        string lineId = $"{fibLevel.Id}_line_{ratio:F3}";
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
                    _drawnObjects.Remove(fibLevel.Id);
                }
                
            }
        }
        
        private void RemoveAllFibonacciDrawings(FibonacciLevel fibLevel)
        {
            if (_chart == null || fibLevel == null) return;
            
            // Remove all objects that are tracked in _drawnObjects for this fibonacci level
            if (_drawnObjects.ContainsKey(fibLevel.Id))
            {
                var trackedObjects = _drawnObjects[fibLevel.Id].ToList();
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
                _drawnObjects.Remove(fibLevel.Id);
            }
            
            // Also attempt to remove all possible line IDs for this fibonacci level
            // This catches any lines that might not be in tracking (e.g., due to extended line handling)
            foreach (var kvp in fibLevel.Levels)
            {
                double ratio = kvp.Key;
                string lineId = $"{fibLevel.Id}_line_{ratio:F3}";
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
                if (extendedId.Contains(fibLevel.Id))
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
        
        private void ClearCycleFibonacciDrawings()
        {
            var cycleLevels = _fibonacciService.GetFibonacciLevels();
            foreach (var fibLevel in cycleLevels)
            {
                if (!string.IsNullOrEmpty(fibLevel.Id))
                {
                    RemoveFibonacciDrawings(fibLevel.Id);
                }
            }
        }
        
        private void ClearCisdFibonacciDrawings()
        {
            var cisdLevels = _fibonacciService.GetCisdFibonacciLevels();
            foreach (var fibLevel in cisdLevels)
            {
                if (!string.IsNullOrEmpty(fibLevel.Id))
                {
                    RemoveFibonacciDrawings(fibLevel.Id);
                }
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
                _fibonacciService.LevelFullySwept -= OnFibonacciLevelFullySwept;
            }
            
            if (_eventAggregator != null)
            {
                _eventAggregator.Unsubscribe<FibonacciLevelSweptEvent>(OnFibonacciLevelSwept);
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