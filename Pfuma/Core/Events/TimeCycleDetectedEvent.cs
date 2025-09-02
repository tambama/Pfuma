using System;
using Pfuma.Models;

namespace Pfuma.Core.Events
{
    public class TimeCycleDetectedEvent : PatternEventBase
    {
        public TimeCycle CompletedCycle { get; set; }
        public double HighPrice { get; set; }
        public double LowPrice { get; set; }
        public int HighIndex { get; set; }
        public int LowIndex { get; set; }
        public DateTime HighTime { get; set; }
        public DateTime LowTime { get; set; }
        
        public TimeCycleDetectedEvent(TimeCycle cycle) : base(cycle.HighIndex.Value)
        {
            CompletedCycle = cycle;
            HighPrice = cycle.HighPrice.Value;
            LowPrice = cycle.LowPrice.Value;
            HighIndex = cycle.HighIndex.Value;
            LowIndex = cycle.LowIndex.Value;
            HighTime = cycle.HighTime.Value;
            LowTime = cycle.LowTime.Value;
        }
    }
}