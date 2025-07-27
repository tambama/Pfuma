using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a Unicorn pattern is detected
/// </summary>
public class UnicornDetectedEvent : PatternEventBase
{
    public Level Unicorn { get; }
        
    public UnicornDetectedEvent(Level unicorn) : base(unicorn.Index)
    {
        Unicorn = unicorn;
    }
}