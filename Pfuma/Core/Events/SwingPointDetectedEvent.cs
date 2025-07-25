using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a new swing point is detected
/// </summary>
public class SwingPointDetectedEvent : PatternEventBase
{
    public SwingPoint SwingPoint { get; }
        
    public SwingPointDetectedEvent(SwingPoint swingPoint) : base(swingPoint.Index)
    {
        SwingPoint = swingPoint;
    }
}