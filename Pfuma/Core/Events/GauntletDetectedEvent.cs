using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a Gauntlet is detected
/// </summary>
public class GauntletDetectedEvent : PatternEventBase
{
    public Level Gauntlet { get; }
    public Direction Direction { get; }
        
    public GauntletDetectedEvent(Level gauntlet, Direction direction) : base(gauntlet.Index)
    {
        Gauntlet = gauntlet;
        Direction = direction;
    }
}