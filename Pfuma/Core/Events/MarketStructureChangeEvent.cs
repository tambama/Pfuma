using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when market structure changes
/// </summary>
public class MarketStructureChangeEvent : PatternEventBase
{
    public Direction NewBias { get; }
    public Direction OldBias { get; }
    public bool IsChoch { get; }
        
    public MarketStructureChangeEvent(Direction newBias, Direction oldBias, bool isChoch, int index) 
        : base(index)
    {
        NewBias = newBias;
        OldBias = oldBias;
        IsChoch = isChoch;
    }
}