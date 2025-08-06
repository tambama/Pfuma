using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Interfaces;
using Pfuma.Extensions;
using Pfuma.Models;

namespace Pfuma.Visualization.Base
{
    /// <summary>
    /// Base class for all visualization components
    /// </summary>
    public abstract class BaseVisualizer<T> : IVisualization<T> where T : class
    {
        protected readonly Chart Chart;
        protected readonly VisualizationSettings Settings;
        protected readonly Dictionary<string, List<string>> ObjectRegistry;
        protected readonly Action<string> Logger;
        
        protected BaseVisualizer(
            Chart chart,
            VisualizationSettings settings,
            Action<string> logger = null)
        {
            Chart = chart;
            Settings = settings;
            ObjectRegistry = new Dictionary<string, List<string>>();
            Logger = logger ?? (_ => { });
        }
        
        /// <summary>
        /// Template method for drawing patterns
        /// </summary>
        public virtual void Draw(T pattern)
        {
            try
            {
                if (!ShouldDraw(pattern))
                    return;
                
                // Get unique ID for this pattern
                string patternId = GetPatternId(pattern);
                
                // Remove any existing visualization
                RemoveExistingObjects(patternId);
                
                // Get all object IDs that will be created
                var objectIds = new List<string>();
                
                // Perform the actual drawing
                PerformDraw(pattern, patternId, objectIds);
                
                // Register the created objects
                RegisterObjects(patternId, objectIds);
                
                // Log if needed
                LogDraw(pattern, patternId);
            }
            catch (Exception ex)
            {
                Logger($"Error in {GetType().Name}.Draw: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Template method for updating patterns
        /// </summary>
        public virtual void Update(T pattern)
        {
            try
            {
                // Default implementation: remove and redraw
                Remove(pattern);
                Draw(pattern);
            }
            catch (Exception ex)
            {
                Logger($"Error in {GetType().Name}.Update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Template method for removing pattern visualization
        /// </summary>
        public virtual void Remove(T pattern)
        {
            try
            {
                string patternId = GetPatternId(pattern);
                RemoveExistingObjects(patternId);
                UnregisterObjects(patternId);
                
                LogRemove(pattern, patternId);
            }
            catch (Exception ex)
            {
                Logger($"Error in {GetType().Name}.Remove: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clears all visualizations
        /// </summary>
        public virtual void Clear()
        {
            foreach (var patternId in ObjectRegistry.Keys)
            {
                RemoveExistingObjects(patternId);
            }
            ObjectRegistry.Clear();
        }
        
        /// <summary>
        /// Override to determine if pattern should be drawn
        /// </summary>
        protected virtual bool ShouldDraw(T pattern)
        {
            return pattern != null && Chart != null;
        }
        
        /// <summary>
        /// Override to generate unique pattern ID
        /// </summary>
        protected abstract string GetPatternId(T pattern);
        
        /// <summary>
        /// Override to implement actual drawing logic
        /// </summary>
        protected abstract void PerformDraw(T pattern, string patternId, List<string> objectIds);
        
        /// <summary>
        /// Override to implement pattern-specific logging
        /// </summary>
        protected virtual void LogDraw(T pattern, string patternId)
        {
            // Default implementation - override in derived classes
        }
        
        /// <summary>
        /// Override to implement pattern-specific logging
        /// </summary>
        protected virtual void LogRemove(T pattern, string patternId)
        {
            // Default implementation - override in derived classes
        }
        
        /// <summary>
        /// Registers chart objects for a pattern
        /// </summary>
        protected void RegisterObjects(string patternId, List<string> objectIds)
        {
            if (!ObjectRegistry.ContainsKey(patternId))
                ObjectRegistry[patternId] = new List<string>();
            
            ObjectRegistry[patternId].AddRange(objectIds);
        }
        
        /// <summary>
        /// Unregisters chart objects for a pattern
        /// </summary>
        protected void UnregisterObjects(string patternId)
        {
            if (ObjectRegistry.ContainsKey(patternId))
                ObjectRegistry.Remove(patternId);
        }
        
        /// <summary>
        /// Removes existing chart objects for a pattern
        /// </summary>
        protected void RemoveExistingObjects(string patternId)
        {
            if (ObjectRegistry.TryGetValue(patternId, out var objectIds))
            {
                foreach (var objectId in objectIds)
                {
                    Chart.RemoveObject(objectId);
                }
            }
        }
        
        /// <summary>
        /// Helper method to get color based on direction
        /// </summary>
        protected Color GetDirectionalColor(Direction direction)
        {
            return direction == Direction.Up
                ? GetColorFromString(Settings.Colors.BullishColor)
                : GetColorFromString(Settings.Colors.BearishColor);
        }
        
        /// <summary>
        /// Helper method to convert string color name to Color
        /// </summary>
        protected Color GetColorFromString(string colorName)
        {
            return colorName.ToLower() switch
            {
                "green" => Color.Green,
                "red" => Color.Red,
                "pink" => Color.Pink,
                "wheat" => Color.Wheat,
                "yellow" => Color.Yellow,
                "white" => Color.White,
                "gray" => Color.Gray,
                "grey" => Color.Gray,
                _ => Color.Gray
            };
        }
        
        /// <summary>
        /// Helper method to apply opacity to a color
        /// </summary>
        protected Color ApplyOpacity(Color color, int opacity)
        {
            return Color.FromArgb(opacity, color);
        }
        
        /// <summary>
        /// Checks if timeframe labels should be shown for the given timeframe
        /// </summary>
        protected bool ShouldShowTimeframeLabel(TimeFrame timeFrame)
        {
            if (string.IsNullOrWhiteSpace(Settings.SeeTimeframe))
                return false;
                
            var shortName = timeFrame.GetShortName();
            var timeframes = Settings.SeeTimeframe.Split(',');
            
            foreach (var tf in timeframes)
            {
                if (string.Equals(tf.Trim(), shortName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            
            return false;
        }
    }
}