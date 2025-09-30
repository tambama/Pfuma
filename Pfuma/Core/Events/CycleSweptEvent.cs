using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a cycle high or low is swept by a swing point
/// </summary>
public class CycleSweptEvent : PatternEventBase
{
    /// <summary>
    /// The cycle swing point that was swept (cycle high or low)
    /// </summary>
    public SwingPoint SweptCyclePoint { get; }

    /// <summary>
    /// The swing point that swept the cycle (the sweeping swing point)
    /// </summary>
    public SwingPoint SweepingSwingPoint { get; }

    public CycleSweptEvent(SwingPoint sweptCyclePoint, SwingPoint sweepingSwingPoint)
        : base(sweepingSwingPoint.Index)
    {
        SweptCyclePoint = sweptCyclePoint;
        SweepingSwingPoint = sweepingSwingPoint;
    }
}