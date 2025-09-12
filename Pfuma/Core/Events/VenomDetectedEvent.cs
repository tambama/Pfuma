using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Venom pattern is detected
    /// </summary>
    public class VenomDetectedEvent : PatternEventBase
    {
        public Level Venom { get; }
        public Direction Direction { get; }
        
        public VenomDetectedEvent(Level venom) : base(venom.Index)
        {
            Venom = venom;
            Direction = venom.Direction;
        }
    }
}