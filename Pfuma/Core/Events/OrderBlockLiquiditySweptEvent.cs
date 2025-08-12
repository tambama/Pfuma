using Pfuma.Models;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event published when an order block's liquidity is swept by a swing point
    /// </summary>
    public class OrderBlockLiquiditySweptEvent : PatternEventBase
    {
        /// <summary>
        /// The order block that had its liquidity swept
        /// </summary>
        public Level OrderBlock { get; }
        
        /// <summary>
        /// The swing point that swept the order block liquidity
        /// </summary>
        public SwingPoint SwingPoint { get; }

        /// <summary>
        /// The index at which the liquidity sweep occurred
        /// </summary>
        public int SweepIndex { get; }

        public OrderBlockLiquiditySweptEvent(Level orderBlock, SwingPoint swingPoint, int sweepIndex)
            : base(orderBlock?.Index ?? 0)
        {
            OrderBlock = orderBlock;
            SwingPoint = swingPoint;
            SweepIndex = sweepIndex;
        }
    }
}