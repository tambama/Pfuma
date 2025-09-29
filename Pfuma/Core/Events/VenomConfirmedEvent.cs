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
        public int ConfirmingSwingPointIndex { get; }

        public VenomConfirmedEvent(Level venom, int confirmingSwingPointIndex) : base(confirmingSwingPointIndex)
        {
            Venom = venom;
            Direction = venom.Direction;
            ConfirmingSwingPointIndex = confirmingSwingPointIndex;
        }
    }
}