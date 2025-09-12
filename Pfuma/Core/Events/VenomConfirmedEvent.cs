using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Venom pattern is confirmed
    /// </summary>
    public class VenomConfirmedEvent : PatternEventBase
    {
        public Level Venom { get; }
        public Direction Direction { get; }
        
        public VenomConfirmedEvent(Level venom) : base(venom.Index)
        {
            Venom = venom;
            Direction = venom.Direction;
        }
    }
}