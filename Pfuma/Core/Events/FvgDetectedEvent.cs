using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when an FVG is detected
/// </summary>
public class FvgDetectedEvent : PatternEventBase
{
    public Level FvgLevel { get; }
        
    public FvgDetectedEvent(Level fvgLevel) : base(fvgLevel.Index)
    {
        FvgLevel = fvgLevel;
    }
}