using System;

namespace Pfuma.Models
{
    public class Signal
    {
        public Signal(string id, double entry, double stop, double takeProfit, Direction direction, DateTime timestamp, int barIndex)
        {
            Id = id;
            Entry = entry;
            Stop = stop;
            TakeProfit = takeProfit;
            Direction = direction;
            Status = SignalStatus.Ready;
            Result = SignalResult.None;
            Timestamp = timestamp;
            BarIndex = barIndex;
        }

        public string Id { get; set; }
        public double Entry { get; set; }
        public double Stop { get; set; }
        public double TakeProfit { get; set; }
        public Direction Direction { get; set; }
        public SignalStatus Status { get; set; }
        public SignalResult Result { get; set; }
        public DateTime Timestamp { get; set; }
        public int BarIndex { get; set; }
        public DateTime? OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public int? OpenBarIndex { get; set; }
        public int? CloseBarIndex { get; set; }
    }

    public enum SignalStatus
    {
        Ready,
        Open,
        Closed
    }

    public enum SignalResult
    {
        None,
        TP,  // Take Profit
        BE,  // Break Even
        L    // Loss
    }
}