using Pfuma.Models;
using cAlgo.API;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Higher Timeframe Order Flow pattern is detected
    /// </summary>
    public class HtfOrderFlowDetectedEvent : PatternEventBase
    {
        public Level OrderFlow { get; }
        public TimeFrame TimeFrame { get; }
        
        public HtfOrderFlowDetectedEvent(Level orderFlow, TimeFrame timeFrame) : base(orderFlow.Index)
        {
            OrderFlow = orderFlow;
            TimeFrame = timeFrame;
        }
    }
}