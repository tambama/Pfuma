using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a CISD is detected
/// </summary>
public class CisdDetectedEvent : PatternEventBase
{
    public Level CisdLevel { get; }
        
    public CisdDetectedEvent(Level cisdLevel) : base(cisdLevel.Index)
    {
        CisdLevel = cisdLevel;
    }
}