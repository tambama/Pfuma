using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Venom pattern is invalidated
    /// </summary>
    public class VenomInvalidatedEvent : PatternEventBase
    {
        public Level Venom { get; }
        public Direction Direction { get; }
        
        public VenomInvalidatedEvent(Level venom) : base(venom.Index)
        {
            Venom = venom;
            Direction = venom.Direction;
        }
    }
}