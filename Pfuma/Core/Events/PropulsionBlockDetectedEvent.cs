using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Propulsion Block is detected
    /// </summary>
    public class PropulsionBlockDetectedEvent : PatternEventBase
    {
        public Level PropulsionBlock { get; }
        public Direction Direction { get; }
        
        public PropulsionBlockDetectedEvent(Level propulsionBlock) : base(propulsionBlock.Index)
        {
            PropulsionBlock = propulsionBlock;
            Direction = propulsionBlock.Direction;
        }
    }
}