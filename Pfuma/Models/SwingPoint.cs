using System;
using System.Collections.Generic;
using cAlgo.API;

namespace Pfuma.Models;

public class SwingPoint
{
    public SwingType SwingType { get; set; }
    public int Index { get; set; }
    public int Number { get; set; }
    public int PreviousIndex { get; set; }
    public int NextIndex { get; set; }

    public double Price { get; set; }
    public DateTime Time { get; set; }
    public Direction Direction { get; set; }
    public Candle Bar { get; set; }
    public Direction CandleDirection { get; set; }
    public bool Swept { get; set; }
    
    // TimeFrame tracking
    public TimeFrame TimeFrame { get; set; }
    
    // Added property to track which candle swept this swing point
    public int IndexOfSweepingCandle { get; set; }

    // ICT Concepts
    public LiquidityType LiquidityType { get; set; }
    public LiquidityName LiquidityName { get; set; }
    
    // Track activated FVGs and Order Blocks
    public bool ActivatedFVG { get; set; }
    public bool ActivatedRejectionBlock { get; set; }
    public bool ActivatedStdv { get; set; }
    public bool SweptLiquidity { get; set; }
    public bool ActivatedUnicorn { get; set; }
    
    // Track if swing point was created during macro time
    public bool InsideMacro { get; set; }
    
    // Simple boolean flag for quadrant sweeping
    public bool InsidePda { get; set; }
    public Level Pda { get; set; }
    
    // Fibonacci sweep tracking
    public bool SweptFib { get; set; }
    public List<(int Index, double Price, double Ratio, string Id, FibType Type)> SweptFibLevels { get; set; } = new List<(int, double, double, string, FibType)>();
    
    // Swept price tracking for property copying conditions
    public double? SweptLiquidityPrice { get; set; }
    public double? SweptFibPrice { get; set; }

    // 369 time pattern properties
    public bool Has369 { get; set; }
    public int? Number369 { get; set; }
    public bool Drawn369 { get; set; }

    // 30-minute cycle properties
    public bool SweptCycle { get; set; }

    // Score
    public int Score
    {
        get
        {
            var score = 0;

            if (ActivatedFVG)
                score += 1;
            
            if (ActivatedRejectionBlock)
                score += 1;
            
            if (ActivatedStdv)
                score += 1;
            
            
            if (SweptLiquidity)
                score += 1;
            
            if (ActivatedUnicorn)
                score += 3;
            
            
            return score;
        }
    }

    public SwingPoint(int index, double price, DateTime time, Candle bar, SwingType swingType,
        LiquidityType liquidityType = LiquidityType.Normal, Direction direction = Direction.Up,
        LiquidityName liquidityName = LiquidityName.N)
    {
        Index = index;
        Price = price;
        Time = time;
        Direction = direction;
        CandleDirection = bar.Close > bar.Open ? Direction.Up : Direction.Down;
        Bar = bar;
        SwingType = swingType;
        LiquidityType = liquidityType;
        LiquidityName = liquidityName;
        TimeFrame = bar?.TimeFrame; // Get TimeFrame from the Candle
    }
}