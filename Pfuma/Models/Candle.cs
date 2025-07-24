using System;
using cAlgo.API;

namespace Pfuma.Models;

public class Candle
{
    public Candle(Bar bar, int index, TimeFrame timeFrame = null)
    {
        Index = index;
        Time = bar.OpenTime;
        Open = bar.Open;
        High = bar.High;
        Low = bar.Low;
        Close = bar.Close;
        TimeFrame = timeFrame;
    }

    public int? Index { get; set; }
    public DateTime Time { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    
    // Timeframe tracking
    public TimeFrame TimeFrame { get; set; }
    
    // High Timeframe
    public bool IsHighTimeframe { get; set; }
    public int? IndexOfHigh { get; set; }
    public int? IndexOfLow { get; set; }
    public DateTime? TimeOfHigh { get; set; }
    public DateTime? TimeOfLow { get; set; }
    public DateTime? TimeOfClose { get; set; }
}