using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Interfaces;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors.Base;

/// <summary>
/// Base class for all pattern detection components
/// </summary>
public abstract class BasePatternDetector<T> : IPatternDetector<T>, IInitializable where T : class
{
    protected readonly Chart Chart;
    protected readonly CandleManager CandleManager;
    protected readonly IEventAggregator EventAggregator;
    protected readonly IRepository<T> Repository;
    protected readonly IndicatorSettings Settings;
    protected readonly Action<string> Logger;
        
    protected BasePatternDetector(
        Chart chart,
        CandleManager candleManager,
        IEventAggregator eventAggregator,
        IRepository<T> repository,
        IndicatorSettings settings,
        Action<string> logger = null)
    {
        Chart = chart;
        CandleManager = candleManager;
        EventAggregator = eventAggregator;
        Repository = repository;
        Settings = settings;
        Logger = logger ?? (_ => { });
    }
        
    /// <summary>
    /// Template method for pattern detection
    /// </summary>
    public virtual void Detect(int currentIndex)
    {
        try
        {
            // Pre-detection validation
            if (!PreDetectionValidation(currentIndex))
                return;
                
            // Perform the actual detection
            var detectedPatterns = PerformDetection(currentIndex);
                
            // Post-process detected patterns
            foreach (var pattern in detectedPatterns)
            {
                if (PostDetectionValidation(pattern, currentIndex))
                {
                    // Store the pattern
                    Repository.Add(pattern);
                        
                    // Publish detection event
                    PublishDetectionEvent(pattern, currentIndex);
                        
                    // Log detection if needed
                    LogDetection(pattern, currentIndex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger($"Error in {GetType().Name}.Detect: {ex.Message}");
        }
    }
        
    /// <summary>
    /// Override to implement pre-detection validation
    /// </summary>
    protected virtual bool PreDetectionValidation(int currentIndex)
    {
        return currentIndex >= GetMinimumBarsRequired() && currentIndex < CandleManager.Count;
    }
        
    /// <summary>
    /// Override to implement the actual pattern detection logic
    /// </summary>
    protected abstract List<T> PerformDetection(int currentIndex);
        
    /// <summary>
    /// Override to implement post-detection validation
    /// </summary>
    protected virtual bool PostDetectionValidation(T pattern, int currentIndex)
    {
        return pattern != null;
    }
        
    /// <summary>
    /// Override to publish pattern-specific events
    /// </summary>
    protected abstract void PublishDetectionEvent(T pattern, int currentIndex);
        
    /// <summary>
    /// Override to implement pattern-specific logging
    /// </summary>
    protected virtual void LogDetection(T pattern, int currentIndex)
    {
        Logger($"{GetType().Name}: Pattern detected at index {currentIndex}");
    }
        
    /// <summary>
    /// Override to specify minimum bars required for detection
    /// </summary>
    protected virtual int GetMinimumBarsRequired()
    {
        return Constants.Calculations.MinimumBarsRequired;
    }
        
    // IPatternDetector implementation
    public virtual List<T> GetAll()
    {
        return Repository.GetAll();
    }
        
    public abstract List<T> GetByDirection(Direction direction);
        
    public virtual void Clear()
    {
        Repository.Clear();
    }
        
    public abstract bool IsValid(T pattern, int currentIndex);
        
    // IInitializable implementation
    public virtual void Initialize()
    {
        // Subscribe to relevant events
        SubscribeToEvents();
            
        // Perform any detector-specific initialization
        OnInitialize();
    }
        
    public virtual void Dispose()
    {
        // Unsubscribe from events
        UnsubscribeFromEvents();
            
        // Clear repository
        Repository.Clear();
            
        // Perform any detector-specific cleanup
        OnDispose();
    }
        
    /// <summary>
    /// Override to subscribe to specific events
    /// </summary>
    protected virtual void SubscribeToEvents()
    {
        // Default implementation - override in derived classes
    }
        
    /// <summary>
    /// Override to unsubscribe from specific events
    /// </summary>
    protected virtual void UnsubscribeFromEvents()
    {
        // Default implementation - override in derived classes
    }
        
    /// <summary>
    /// Override for detector-specific initialization
    /// </summary>
    protected virtual void OnInitialize()
    {
        // Default implementation - override in derived classes
    }
        
    /// <summary>
    /// Override for detector-specific cleanup
    /// </summary>
    protected virtual void OnDispose()
    {
        // Default implementation - override in derived classes
    }
        
    /// <summary>
    /// Helper method to check if a bar index is within valid range
    /// </summary>
    protected bool IsValidBarIndex(int index)
    {
        return index >= 0 && index < CandleManager.Count;
    }
        
    /// <summary>
    /// Helper method to get a bar safely
    /// </summary>
    protected Candle GetBarSafely(int index)
    {
        return IsValidBarIndex(index) ? CandleManager.GetCandle(index) : default;
    }
}