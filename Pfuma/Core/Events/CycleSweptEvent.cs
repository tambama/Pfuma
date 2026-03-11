using Pfuma.Models;

namespace Pfuma.Core.Events;

public class CycleSweptEvent : PatternEventBase
{
    public SwingPoint SweptCyclePoint { get; }
    public SwingPoint SweepingSwingPoint { get; }

    public CycleSweptEvent(SwingPoint sweptCyclePoint, SwingPoint sweepingSwingPoint)
        : base(sweepingSwingPoint.Index)
    {
        SweptCyclePoint = sweptCyclePoint;
        SweepingSwingPoint = sweepingSwingPoint;
    }
}