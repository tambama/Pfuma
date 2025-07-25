namespace Pfuma.Core.Interfaces;

/// <summary>
/// Interface for event aggregator
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// Publishes an event to all subscribers
    /// </summary>
    void Publish<TEvent>(TEvent eventData) where TEvent : class;
        
    /// <summary>
    /// Subscribes to an event type
    /// </summary>
    void Subscribe<TEvent>(System.Action<TEvent> handler) where TEvent : class;
        
    /// <summary>
    /// Unsubscribes from an event type
    /// </summary>
    void Unsubscribe<TEvent>(System.Action<TEvent> handler) where TEvent : class;
        
    /// <summary>
    /// Clears all event subscriptions
    /// </summary>
    void Clear();
}