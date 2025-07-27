using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when a Rejection Block is detected
/// </summary>
public class RejectionBlockDetectedEvent : PatternEventBase
{
    public Level RejectionBlock { get; }
        
    public RejectionBlockDetectedEvent(Level rejectionBlock) : base(rejectionBlock.Index)
    {
        RejectionBlock = rejectionBlock;
    }
}