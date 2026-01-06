using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Higher Timeframe CISD is detected
    /// </summary>
    public class HtfCisdDetectedEvent : PatternEventBase
    {
        public Level CisdLevel { get; }
        public TimeFrame TimeFrame { get; }

        public HtfCisdDetectedEvent(Level cisdLevel, TimeFrame timeFrame) : base(cisdLevel.Index)
        {
            CisdLevel = cisdLevel;
            TimeFrame = timeFrame;
        }
    }

    /// <summary>
    /// Event fired when a Higher Timeframe CISD is confirmed
    /// </summary>
    public class HtfCisdConfirmedEvent : PatternEventBase
    {
        public Level CisdLevel { get; }
        public TimeFrame TimeFrame { get; }
        public Direction Direction { get; }

        public HtfCisdConfirmedEvent(Level cisdLevel, TimeFrame timeFrame, Direction direction) : base(cisdLevel.Index)
        {
            CisdLevel = cisdLevel;
            TimeFrame = timeFrame;
            Direction = direction;
        }
    }
}
