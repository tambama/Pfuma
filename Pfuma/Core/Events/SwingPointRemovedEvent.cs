using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a swing point is removed
/// </summary>
public class SwingPointRemovedEvent : PatternEventBase
{
    public SwingPoint SwingPoint { get; }
        
    public SwingPointRemovedEvent(SwingPoint swingPoint) : base(swingPoint.Index)
    {
        SwingPoint = swingPoint;
    }
}