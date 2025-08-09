using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when an Order Block is detected
/// </summary>
public class OrderBlockDetectedEvent : PatternEventBase
{
    public Level OrderBlock { get; }
        
    public OrderBlockDetectedEvent(Level orderBlock) : base(orderBlock.Index)
    {
        OrderBlock = orderBlock;
    }
}