using System.Collections.Generic;
using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when FVGs are found within an Order Flow pattern
    /// </summary>
    public class OrderFlowFvgsDetectedEvent : PatternEventBase
    {
        public Level OrderFlow { get; }
        public List<Level> FVGs { get; }

        public OrderFlowFvgsDetectedEvent(Level orderFlow, List<Level> fvgs) : base(orderFlow.Index)
        {
            OrderFlow = orderFlow;
            FVGs = fvgs ?? new List<Level>();
        }
    }
}
