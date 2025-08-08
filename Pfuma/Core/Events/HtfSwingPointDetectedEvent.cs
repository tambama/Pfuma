using Pfuma.Models;
using cAlgo.API;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Higher Timeframe swing point is detected
    /// </summary>
    public class HtfSwingPointDetectedEvent : PatternEventBase
    {
        public SwingPoint SwingPoint { get; }
        public TimeFrame TimeFrame { get; }
        
        public HtfSwingPointDetectedEvent(SwingPoint swingPoint, TimeFrame timeFrame) : base(swingPoint.Index)
        {
            SwingPoint = swingPoint;
            TimeFrame = timeFrame;
        }
    }
}