using System;
using cAlgo.API;

namespace Pfuma.Models;

public class Candle
{
    public Candle()
    {
        // Parameterless constructor
    }
    
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
    
    // Indices for tracking exact high/low positions in HTF candles
    public int? IndexOfHigh { get; set; }
    public int? IndexOfLow { get; set; }
    
    // Timeframe tracking
    public TimeFrame TimeFrame { get; set; }
    
    public int PositionInFvg { get; set; }
    public int SweptLiquidity { get; set; }
    public int SweptFibonacci { get; set; }
    public bool InsidePda { get; set; }
    public bool SweptCycle { get; set; }
    public bool HasSMT { get; set; }

    // Direction
    public Direction Direction => Close > Open ? Direction.Up : Direction.Down;
}