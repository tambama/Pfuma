using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a CISD is liquidity swept
/// </summary>
public class CisdLiquiditySweptEvent : PatternEventBase
{
    public Level CisdLevel { get; }
    public SwingPoint SweptBySwingPoint { get; }
    
    public CisdLiquiditySweptEvent(Level cisdLevel, SwingPoint sweptBySwingPoint, int index) : base(index)
    {
        CisdLevel = cisdLevel;
        SweptBySwingPoint = sweptBySwingPoint;
    }
}