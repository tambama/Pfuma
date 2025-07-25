using System;
using System.Collections.Generic;
using System.Linq;
using Pfuma.Core.Interfaces;

namespace Pfuma.Services;

/// <summary>
/// Implementation of the event aggregator pattern for decoupled communication
/// </summary>
public class EventAggregator : IEventAggregator
{
    private readonly Dictionary<Type, List<Delegate>> _eventHandlers;
    private readonly object _lockObject = new object();
    private readonly Action<string> _logger;
        
    public EventAggregator(Action<string> logger = null)
    {
        _eventHandlers = new Dictionary<Type, List<Delegate>>();
        _logger = logger ?? (_ => { });
    }
        
    public void Publish<TEvent>(TEvent eventData) where TEvent : class
    {
        if (eventData == null)
            return;
            
        List<Delegate> handlers;
            
        lock (_lockObject)
        {
            var eventType = typeof(TEvent);
                
            if (!_eventHandlers.TryGetValue(eventType, out handlers))
                return;
                
            // Create a copy to avoid collection modified exceptions
            handlers = handlers.ToList();
        }
            
        // Invoke handlers outside of lock to prevent deadlocks
        foreach (var handler in handlers)
        {
            try
            {
                (handler as Action<TEvent>)?.Invoke(eventData);
            }
            catch (Exception ex)
            {
                _logger($"Error in event handler for {typeof(TEvent).Name}: {ex.Message}");
            }
        }
    }
        
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
            
        lock (_lockObject)
        {
            var eventType = typeof(TEvent);
                
            if (!_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _eventHandlers[eventType] = handlers;
            }
                
            // Avoid duplicate subscriptions
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }
    }
        
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        if (handler == null)
            return;
            
        lock (_lockObject)
        {
            var eventType = typeof(TEvent);
                
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                    
                // Clean up empty handler lists
                if (handlers.Count == 0)
                {
                    _eventHandlers.Remove(eventType);
                }
            }
        }
    }
        
    /// <summary>
    /// Clears all event subscriptions
    /// </summary>
    public void Clear()
    {
        lock (_lockObject)
        {
            _eventHandlers.Clear();
        }
    }
        
    /// <summary>
    /// Gets the number of subscribers for a specific event type
    /// </summary>
    public int GetSubscriberCount<TEvent>() where TEvent : class
    {
        lock (_lockObject)
        {
            var eventType = typeof(TEvent);
                
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                return handlers.Count;
            }
                
            return 0;
        }
    }
        
    /// <summary>
    /// Gets the total number of event subscriptions
    /// </summary>
    public int GetTotalSubscriptions()
    {
        lock (_lockObject)
        {
            return _eventHandlers.Sum(kvp => kvp.Value.Count);
        }
    }
}
    
/// <summary>
/// Extension methods for event aggregator
/// </summary>
public static class EventAggregatorExtensions
{
    /// <summary>
    /// Publishes multiple events in sequence
    /// </summary>
    public static void PublishMany<TEvent>(this IEventAggregator eventAggregator, 
        IEnumerable<TEvent> events) where TEvent : class
    {
        foreach (var evt in events)
        {
            eventAggregator.Publish(evt);
        }
    }
        
    /// <summary>
    /// Subscribes to an event with a weak reference to prevent memory leaks
    /// </summary>
    public static void SubscribeWeak<TEvent>(this IEventAggregator eventAggregator,
        Action<TEvent> handler) where TEvent : class
    {
        var weakHandler = new WeakEventHandler<TEvent>(handler);
        eventAggregator.Subscribe<TEvent>(weakHandler.Handle);
    }
}
    
/// <summary>
/// Weak event handler to prevent memory leaks
/// </summary>
internal class WeakEventHandler<TEvent> where TEvent : class
{
    private readonly WeakReference _weakReference;
    private readonly Action<TEvent> _handler;
        
    public WeakEventHandler(Action<TEvent> handler)
    {
        _weakReference = new WeakReference(handler.Target);
        _handler = handler;
    }
        
    public void Handle(TEvent eventData)
    {
        if (_weakReference.IsAlive)
        {
            _handler(eventData);
        }
    }
}