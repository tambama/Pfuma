using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when an SMT line should be removed from visualization
/// </summary>
public class SMTLineRemovedEvent : PatternEventBase
{
    /// <summary>
    /// The swing point whose SMT line should be removed
    /// </summary>
    public SwingPoint SwingPoint { get; }

    public SMTLineRemovedEvent(SwingPoint swingPoint) : base(swingPoint.Index)
    {
        SwingPoint = swingPoint;
    }
}