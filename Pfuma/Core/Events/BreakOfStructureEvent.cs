using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a Break of Structure occurs
/// </summary>
public class BreakOfStructureEvent : PatternEventBase
{
    public SwingPoint StructurePoint { get; }
    public Direction Direction { get; }
        
    public BreakOfStructureEvent(SwingPoint structurePoint, Direction direction) 
        : base(structurePoint.Index)
    {
        StructurePoint = structurePoint;
        Direction = direction;
    }
}