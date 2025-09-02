using System;

namespace Pfuma.Models
{
    public class TimeCycle
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public DateTime Date { get; set; }
        
        public double? HighPrice { get; set; }
        public double? LowPrice { get; set; }
        public int? HighIndex { get; set; }
        public int? LowIndex { get; set; }
        public DateTime? HighTime { get; set; }
        public DateTime? LowTime { get; set; }
        
        public bool IsComplete { get; set; }
        
        public TimeCycle(TimeSpan startTime, TimeSpan endTime, DateTime date)
        {
            StartTime = startTime;
            EndTime = endTime;
            Date = date;
            IsComplete = false;
        }
        
        public void UpdateHigh(double price, int index, DateTime time)
        {
            if (!HighPrice.HasValue || price > HighPrice.Value)
            {
                HighPrice = price;
                HighIndex = index;
                HighTime = time;
            }
        }
        
        public void UpdateLow(double price, int index, DateTime time)
        {
            if (!LowPrice.HasValue || price < LowPrice.Value)
            {
                LowPrice = price;
                LowIndex = index;
                LowTime = time;
            }
        }
        
        public bool HasData()
        {
            return HighPrice.HasValue && LowPrice.HasValue;
        }
    }
}