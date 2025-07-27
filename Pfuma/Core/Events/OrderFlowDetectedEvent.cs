using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when an Order Flow pattern is detected
    /// </summary>
    public class OrderFlowDetectedEvent : PatternEventBase
    {
        public Level OrderFlow { get; }
        
        public OrderFlowDetectedEvent(Level orderFlow) : base(orderFlow.Index)
        {
            OrderFlow = orderFlow;
        }
    }
}