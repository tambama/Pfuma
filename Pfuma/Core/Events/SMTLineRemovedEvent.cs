using Pfuma.Models;

namespace Pfuma.Core.Events;

public class SMTLineRemovedEvent : PatternEventBase
{
    public SwingPoint SwingPoint { get; }

    public SMTLineRemovedEvent(SwingPoint swingPoint)
        : base(swingPoint.Index)
    {
        SwingPoint = swingPoint;
    }
}