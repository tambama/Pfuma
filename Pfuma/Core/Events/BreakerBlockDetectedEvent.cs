using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a Breaker Block is detected
/// </summary>
public class BreakerBlockDetectedEvent : PatternEventBase
{
    public Level BreakerBlock { get; }
        
    public BreakerBlockDetectedEvent(Level breakerBlock) : base(breakerBlock.Index)
    {
        BreakerBlock = breakerBlock;
    }
}