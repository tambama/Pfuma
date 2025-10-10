using System;
using System.Collections.Generic;

namespace Pfuma.Models;

public enum FibType
{
    Cycle,
    CISD,
    Ote
}

public class FibonacciLevel
{
    public FibonacciLevel(int startIndex, int endIndex, double startPrice, double endPrice, DateTime startTime, DateTime endTime, FibType fibType = FibType.Cycle)
    {
        // Generate unique ID for this Fibonacci level
        Id = Guid.NewGuid().ToString();
        
        // For drawing: always left to right (chronological)
        StartIndex = startIndex;
        EndIndex = endIndex;
        StartTime = startTime;
        EndTime = endTime;
        FibType = fibType;
        
        // Set tracked ratios based on FibType
        if (fibType == FibType.Cycle)
        {
            TrackedRatios = new double[] 
            { 
                -4.0, -3.5, -2.0, -1.5, -1.0, -0.5, -0.25, 0.0, 0.5, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 
            };
        }
        else if (fibType == FibType.CISD)
        {
            TrackedRatios = new double[] { -2.0, -3.75, -4.0 };
        }
        else if (fibType == FibType.Ote)
        {
            TrackedRatios = new double[] { 0.0, 1.0, -1, -1.5, -2.0, -4.0 };
        }
        
        // For calculation: always low to high (price order)
        LowPrice = Math.Min(startPrice, endPrice);
        HighPrice = Math.Max(startPrice, endPrice);
        
        // If start price is higher than end price, we're in a downtrend
        Direction = startPrice > endPrice ? Direction.Down : Direction.Up;
        
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
    public string Id { get; set; } // ID of the Fibonacci object on the chart
    public FibType FibType { get; set; } // Type of Fibonacci level (Cycle or CISD)

    // Levels dictionary - key is the ratio, value is the calculated price level
    public Dictionary<double, double> Levels { get; private set; } = new Dictionary<double, double>();
    
    // Tracked ratios for detecting sweeps - now instance property set in constructor based on FibType
    public double[] TrackedRatios { get; private set; }
    
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
            if (FibType == FibType.Cycle)
            {
                double level;
            
                // Base calculation ensures price levels are in same order as ratios
                level = LowPrice + (ratio * range);
            
                Levels[ratio] = level;
                SweptLevels[ratio] = false;
            }
            else
            {
                double level;
                level = Direction == Direction.Up ? HighPrice - (ratio * range) : LowPrice + (ratio * range);
                Levels[ratio] = level;
                SweptLevels[ratio] = false;
            }
        }
    }
    
    // Check if all levels for this Fibonacci retracement have been swept
    public bool AreAllLevelsSwept()
    {
        if (FibType == FibType.CISD)
        {
            // For CISD levels, only need to check if the 0.0 level (LowPrice for bullish, HighPrice for bearish) is swept
            // Bullish CISD: When LowPrice (0.0 ratio) is swept, the entire level is considered fully swept
            // Bearish CISD: When HighPrice (0.0 ratio) is swept, the entire level is considered fully swept
            if (SweptLevels.ContainsKey(0.0) && SweptLevels[0.0])
            {
                return true;
            }
            return false;
        }
        else if (FibType == FibType.Ote)
        {
            // For OTE levels, check if the starting point (1.0 level) is swept
            // Bullish OTE (Direction.Up): LowPrice corresponds to ratio 1.0 (starting point)
            // Bearish OTE (Direction.Down): HighPrice corresponds to ratio 1.0 (starting point)
            if (SweptLevels.ContainsKey(1.0) && SweptLevels[1.0])
            {
                return true;
            }
            return false;
        }
        else
        {
            // For Cycle levels, all ratios must be swept
            foreach (double ratio in TrackedRatios)
            {
                if (!SweptLevels.ContainsKey(ratio) || !SweptLevels[ratio])
                    return false;
            }
            return true;
        }
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