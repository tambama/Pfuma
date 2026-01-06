using Pfuma.Models;
using cAlgo.API;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Event fired when a Higher Timeframe candle is created/finalized
    /// </summary>
    public class HtfCandleCreatedEvent : PatternEventBase
    {
        public Candle HtfCandle { get; }
        public TimeFrame TimeFrame { get; }

        public HtfCandleCreatedEvent(Candle htfCandle, TimeFrame timeFrame) : base(htfCandle.Index ?? 0)
        {
            HtfCandle = htfCandle;
            TimeFrame = timeFrame;
        }
    }
}
