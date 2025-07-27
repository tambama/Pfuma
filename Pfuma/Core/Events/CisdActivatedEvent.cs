using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a CISD is activated
/// </summary>
public class CisdActivatedEvent : PatternEventBase
{
    public Level CisdLevel { get; }
        
    public CisdActivatedEvent(Level cisdLevel, int activationIndex) : base(activationIndex)
    {
        CisdLevel = cisdLevel;
    }
}