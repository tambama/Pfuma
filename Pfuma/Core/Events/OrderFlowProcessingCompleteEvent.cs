using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired after orderflow detection processing is complete for a swing point.
    /// This ensures orderflow confirmation/deactivation happens after new orderflows are created.
    /// </summary>
    public class OrderFlowProcessingCompleteEvent : PatternEventBase
    {
        public SwingPoint SwingPoint { get; }
        public TimeFrame TimeFrame { get; }

        public OrderFlowProcessingCompleteEvent(SwingPoint swingPoint) : base(swingPoint.Index)
        {
            SwingPoint = swingPoint;
            TimeFrame = swingPoint.TimeFrame;
        }
    }
}
