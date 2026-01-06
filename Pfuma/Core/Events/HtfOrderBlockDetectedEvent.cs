using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Higher Timeframe Order Block is detected
    /// </summary>
    public class HtfOrderBlockDetectedEvent : PatternEventBase
    {
        public Level OrderBlock { get; }
        public TimeFrame TimeFrame { get; }

        public HtfOrderBlockDetectedEvent(Level orderBlock, TimeFrame timeFrame) : base(orderBlock.Index)
        {
            OrderBlock = orderBlock;
            TimeFrame = timeFrame;
        }
    }
}
