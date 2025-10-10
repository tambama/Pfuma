using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when an OTE is detected
/// </summary>
public class OteDetectedEvent : PatternEventBase
{
    public Level OteLevel { get; }
    public SwingPoint OteHigh { get; }
    public SwingPoint OteLow { get; }

    public OteDetectedEvent(Level oteLevel, SwingPoint oteHigh, SwingPoint oteLow) : base(oteLevel.Index)
    {
        OteLevel = oteLevel;
        OteHigh = oteHigh;
        OteLow = oteLow;
    }
}
