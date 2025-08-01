using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a Change of Character occurs
/// </summary>
public class ChangeOfCharacterEvent : PatternEventBase
{
    public SwingPoint ChochPoint { get; }
    public Direction NewBias { get; }
        
    public ChangeOfCharacterEvent(SwingPoint chochPoint, Direction newBias) 
        : base(chochPoint.Index)
    {
        ChochPoint = chochPoint;
        NewBias = newBias;
    }
}