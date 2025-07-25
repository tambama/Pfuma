using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when liquidity is swept
/// </summary>
public class LiquiditySweptEvent : PatternEventBase
{
    public SwingPoint SweptPoint { get; }
    public int SweepingCandleIndex { get; }
    public Candle SweepingCandle { get; }
        
    public LiquiditySweptEvent(SwingPoint sweptPoint, int sweepingCandleIndex, Candle sweepingCandle) 
        : base(sweepingCandleIndex)
    {
        SweptPoint = sweptPoint;
        SweepingCandleIndex = sweepingCandleIndex;
        SweepingCandle = sweepingCandle;
    }
}