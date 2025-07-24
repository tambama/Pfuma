using System;
using System.Collections.Generic;

namespace Pfuma.Models;

public class FibonacciLevel
{
    public FibonacciLevel(int startIndex, int endIndex, double startPrice, double endPrice, DateTime startTime, DateTime endTime, SessionType sessionType)
    {
        // For drawing: always left to right (chronological)
        StartIndex = startIndex;
        EndIndex = endIndex;
        StartTime = startTime;
        EndTime = endTime;
        
        // For calculation: always low to high (price order)
        LowPrice = Math.Min(startPrice, endPrice);
        HighPrice = Math.Max(startPrice, endPrice);
        
        // If start price is higher than end price, we're in a downtrend
        Direction = startPrice > endPrice ? Direction.Down : Direction.Up;
        
        SessionType = sessionType;
        
        // Calculate all the levels based on low-to-high range
        CalculateLevels();
    }

    // Drawing properties (chronological order)
    public int StartIndex { get; set; }  // Index of the starting point (left)
    public int EndIndex { get; set; }    // Index of the end point (right)
    public DateTime StartTime { get; set; } // Time of the starting point (left)
    public DateTime EndTime { get; set; }   // Time of the end point (right)
    
    // Calculation properties (price order)
    public double LowPrice { get; set; }   // Lower price point
    public double HighPrice { get; set; }  // Higher price point
    
    public SessionType SessionType { get; set; } // Session this level belongs to
    public Direction Direction { get; set; } // Direction (up or down)
    public string FibonacciId { get; set; } // ID of the Fibonacci object on the chart

    // Levels dictionary - key is the ratio, value is the calculated price level
    public Dictionary<double, double> Levels { get; private set; } = new Dictionary<double, double>();
    
    // Tracked ratios for detecting sweeps - ensure they are in ascending order for visualization
    public static readonly double[] TrackedRatios = new double[] 
    { 
        -2.0, -1.5, -1.0, -0.5, -0.25, 0.0, 0.114, 0.886, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 
    };
    
    // Swept levels tracking
    public Dictionary<double, bool> SweptLevels { get; private set; } = new Dictionary<double, bool>();
    public Dictionary<double, string> SweptLevelLineIds { get; private set; } = new Dictionary<double, string>();

    // Calculate all Fibonacci levels
    private void CalculateLevels()
    {
        // Always calculate from low to high
        double range = HighPrice - LowPrice;
        
        // Calculate levels in ascending order by ratio
        foreach (double ratio in TrackedRatios)
        {
            double level;
            
            // Base calculation ensures price levels are in same order as ratios
            level = LowPrice + (ratio * range);
            
            Levels[ratio] = level;
            SweptLevels[ratio] = false;
        }
    }
    
    // Check if all levels for this Fibonacci retracement have been swept
    public bool AreAllLevelsSwept()
    {
        foreach (double ratio in TrackedRatios)
        {
            if (!SweptLevels.ContainsKey(ratio) || !SweptLevels[ratio])
                return false;
        }
        return true;
    }
    
    // Get zone based on ratio
    public Zone GetZone(double ratio)
    {
        if (ratio <= 0.114)
            return Zone.Discount;
        else if (ratio >= 0.886)
            return Zone.Premium;
        else
            return Zone.Equilibrium;
    }
}