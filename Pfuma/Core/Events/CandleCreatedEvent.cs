using Pfuma.Models;
using cAlgo.API;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a regular timeframe candle is created/finalized
    /// </summary>
    public class CandleCreatedEvent : PatternEventBase
    {
        public Candle Candle { get; }
        public TimeFrame TimeFrame { get; }

        public CandleCreatedEvent(Candle candle, TimeFrame timeFrame) : base(candle.Index ?? 0)
        {
            Candle = candle;
            TimeFrame = timeFrame;
        }
    }
}
