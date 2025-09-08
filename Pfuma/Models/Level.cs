using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace Pfuma.Models
{
    public class Level
    {
        public Level(LevelType levelType, double low, double high, DateTime lowTime, DateTime highTime, DateTime? midTime = null, Direction direction = Direction.Up, int index = 0, int indexHigh = 0, int indexLow = 0, int indexMid = 0, Zone zone = Zone.Equilibrium, int score = 1, DateTime? stretchTo = null, bool isConfirmed = false, double? entry = 0)
        {
            Id = Guid.NewGuid().ToString();
            LevelType = levelType;
            Low = low;
            High = high;
            LowTime = lowTime;
            HighTime = highTime;
            MidTime = midTime ?? highTime;
            Direction = direction;
            Index = index;
            IndexHigh = indexHigh;
            IndexLow = indexLow;
            IndexMid = indexMid;
            Zone = zone;
            Score = score;
            StretchTo = stretchTo;
            IsConfirmed = isConfirmed;
            Entry = entry;
            SweptSwingPoints = new List<SwingPoint>();
        }

        public string Id { get; private set; }
        public Zone Zone { get; set; }
        public LevelType LevelType { get; set; }
        public Direction Direction { get; set; }
        public double Low { get; set; }
        public DateTime LowTime { get; set; }
        public double High { get; set; }
        public DateTime HighTime { get; set; }
        public TimeFrame TimeFrame { get; set; }
        public double Mid => (High + Low) / 2;
        public DateTime MidTime { get; set; }
        public int Index { get; set; }
        public int IndexHigh { get; set; }
        public int IndexLow { get; set; }
        public int IndexMid { get; set; } // Added to track the middle candle
        public int Score { get; set; }
        public bool Activated { get; set; }
        public int ActivationIndex { get; set; }
        public bool IsInverted { get; set; }
        public int PassCount { get; set; }
        public bool IsConfirmed { get; set; }
        public DateTime? StretchTo { get; set; }
        public double? Entry { get; set; }
        public int IndexOfConfirmingCandle { get; set; }
        public Level BreakerBlock { get; set; }
        public Level RejectionBlock { get; set; }
        public string OrderFlowId { get; set; }
    
    
        // Properties for liquidity sweep tracking
        public SwingPoint SweptSwingPoint { get; set; } // The extreme swept swing point
        public List<SwingPoint> SweptSwingPoints { get; set; } = new List<SwingPoint>(); // All swept swing points
        public int IndexOfSweepingCandle { get; set; }
        public int SweptCount => SweptSwingPoints?.Count ?? 0;
    
    
        // CISD
        public Level CISDLevel { get; set; }
    
        // Quadrant tracking
        public List<Quadrant> Quadrants { get; set; } = new List<Quadrant>();

        // Whether this level has been liquidity swept (independent of quadrants)
        public bool IsLiquiditySwept { get; set; } = false;
        
        // Whether this level has been broken through (candle closed through the level)
        public bool IsBrokenThrough { get; set; } = false;
        
        // Index at which liquidity sweep occurred
        public int SweptIndex { get; set; }
        
        // Whether this level has been extended (rectangle extended to include swing point candle)
        public bool IsExtended { get; set; } = false;

        // Whether this level is active (not liquidity swept and, if has quadrants, at least one quadrant is not swept)
        public bool IsActive => !IsLiquiditySwept && (Quadrants.Count == 0 || Quadrants.Any(q => !q.IsSwept));

        // Initialize quadrants
        public void InitializeQuadrants()
        {
            // Clear any existing quadrants
            Quadrants.Clear();
    
            // Calculate price levels for the quadrants
            double range = High - Low;
    
            // For bullish PD arrays, 0% is low and 100% is high
            if (Direction == Direction.Up)
            {
                Quadrants.Add(new Quadrant(0, Low));
                Quadrants.Add(new Quadrant(25, Low + (range * 0.25)));
                Quadrants.Add(new Quadrant(50, Low + (range * 0.5))); // Mid
                Quadrants.Add(new Quadrant(75, Low + (range * 0.75)));
                Quadrants.Add(new Quadrant(100, High));
            }
            // For bearish PD arrays, 0% is high and 100% is low
            else
            {
                Quadrants.Add(new Quadrant(0, High));
                Quadrants.Add(new Quadrant(25, High - (range * 0.25)));
                Quadrants.Add(new Quadrant(50, High - (range * 0.5))); // Mid
                Quadrants.Add(new Quadrant(75, High - (range * 0.75)));
                Quadrants.Add(new Quadrant(100, Low));
            }
        }

        // Check if a swing point sweeps any quadrants
        public List<Quadrant> CheckForSweptQuadrants(SwingPoint swingPoint)
        {
            var sweptQuadrants = new List<Quadrant>();
    
            // Only check if the swing point direction is opposite to the PD Array direction
            if (swingPoint.Direction == Direction)
                return sweptQuadrants; // Return empty list if same direction
    
            foreach (var quadrant in Quadrants)
            {
                // Skip already swept quadrants
                if (quadrant.IsSwept)
                    continue;
            
                // For bullish PD arrays, check if bearish swing point bar opened above and went below
                if (Direction == Direction.Up)
                {
                    if (swingPoint.Bar.Open > quadrant.Price && swingPoint.Bar.Low <= quadrant.Price)
                    {
                        quadrant.IsSwept = true;
                        quadrant.SweptByIndex = swingPoint.Index;
                        sweptQuadrants.Add(quadrant);
                    }
                }
                // For bearish PD arrays, check if bullish swing point bar opened below and went above
                else
                {
                    if (swingPoint.Bar.Open < quadrant.Price && swingPoint.Bar.High >= quadrant.Price)
                    {
                        quadrant.IsSwept = true;
                        quadrant.SweptByIndex = swingPoint.Index;
                        sweptQuadrants.Add(quadrant);
                    }
                }
            }
    
            return sweptQuadrants;
        }
    }
}