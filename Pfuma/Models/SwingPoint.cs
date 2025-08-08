using System;
using cAlgo.API;

namespace Pfuma.Models;

public class SwingPoint
{
    public SwingType SwingType { get; set; }
    public int Index { get; set; }
    public int Number { get; set; }
    public int PreviousIndex { get; set; }
    public int NextIndex { get; set; }
    public int IndexThatBrokeSwing { get; set; }

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

    public int PassCount { get; set; }
    public int BrokenCount { get; set; }

    public bool IsRejectionBlock { get; set; }
    public bool IsInversion { get; set; }

    public bool IsPotentialChoCh { get; set; }
    
    // Track activated FVGs and Order Blocks
    public bool ActivatedFVG { get; set; }
    public bool ActivatedRejectionBlock { get; set; }
    public bool IsInFVG { get; set; }
    public bool IsInRejectionBlock { get; set; }
    public bool ActivatedStdv { get; set; }
    public bool SweptLiquidity { get; set; }
    public bool ActivatedUnicorn { get; set; }
    
    // Simple boolean flag for quadrant sweeping
    public bool InsideKeyLevel { get; set; }
    public Level SweptKeyLevel { get; set; }
    
    // References to the activated levels
    public Level ActivatedFVGLevel { get; set; }
    public Level ActivatedRejectionBlockLevel { get; set; }
    public bool ActivatedCISD { get; set; }
    public Level ActivatedCISDLevel { get; set; }
    
    // Standard Deviations
    public StandardDeviation SweptDeviation { get; set; }
    public bool SweptMinusTwo { get; set; }  // To track which level was swept (MinusTwo or MinusFour)
    
    
    
    // Special liquidity tracking  
    public bool IsSpecialLiquidity { get; set; }
    
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

    public SwingPoint()
    {
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