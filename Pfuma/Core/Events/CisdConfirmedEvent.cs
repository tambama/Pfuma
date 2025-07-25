using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when CISD is confirmed
/// </summary>
public class CisdConfirmedEvent : PatternEventBase
{
    public Level CisdLevel { get; }
    public Direction Direction { get; }
        
    public CisdConfirmedEvent(Level cisdLevel, Direction direction) : base(cisdLevel.Index)
    {
        CisdLevel = cisdLevel;
        Direction = direction;
    }
}