using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Manages IndicatorDataSeries for swing points and provides data access methods
    /// </summary>
    public class SwingPointManager
    {
        private readonly IndicatorDataSeries _swingHighs;
        private readonly IndicatorDataSeries _swingLows;
        private readonly List<SwingPoint> _swingPoints = new();
        
        private SwingPoint _lastHighSwingPoint;
        private SwingPoint _lastLowSwingPoint;
        
        public SwingPointManager(IndicatorDataSeries swingHighs, IndicatorDataSeries swingLows)
        {
            _swingHighs = swingHighs;
            _swingLows = swingLows;
        }
        
        /// <summary>
        /// Updates the IndicatorDataSeries for a swing high
        /// </summary>
        public void SetSwingHigh(int index, double value)
        {
            _swingHighs[index] = value;
        }
        
        /// <summary>
        /// Updates the IndicatorDataSeries for a swing low
        /// </summary>
        public void SetSwingLow(int index, double value)
        {
            _swingLows[index] = value;
        }
        
        /// <summary>
        /// Clears a swing high at the specified index
        /// </summary>
        public void ClearSwingHigh(int index)
        {
            _swingHighs[index] = double.NaN;
        }
        
        /// <summary>
        /// Clears a swing low at the specified index
        /// </summary>
        public void ClearSwingLow(int index)
        {
            _swingLows[index] = double.NaN;
        }
        
        /// <summary>
        /// Adds a swing point to the collection
        /// </summary>
        /// <returns>True if the swing point was added, false if it was a duplicate</returns>
        public bool AddSwingPoint(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return false;

            // Check for duplicate swing point at same index and direction
            bool isDuplicate = _swingPoints.Any(sp =>
                sp.Index == swingPoint.Index &&
                sp.SwingType == swingPoint.SwingType &&
                sp.Direction == swingPoint.Direction);

            if (isDuplicate)
                return false;

            _swingPoints.Add(swingPoint);

            // Update last swing point references
            if (swingPoint.SwingType == SwingType.H)
            {
                _lastHighSwingPoint = swingPoint;
            }
            else
            {
                _lastLowSwingPoint = swingPoint;
            }

            return true;
        }
        
        /// <summary>
        /// Removes a swing point from the collection
        /// </summary>
        public void RemoveSwingPoint(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
                
            _swingPoints.Remove(swingPoint);
        }
        
        /// <summary>
        /// Gets all swing points
        /// </summary>
        public List<SwingPoint> GetAllSwingPoints()
        {
            return _swingPoints;
        }
        
        /// <summary>
        /// Gets all swing highs
        /// </summary>
        public List<SwingPoint> GetSwingHighs()
        {
            return _swingPoints.FindAll(sp => sp.SwingType == SwingType.H);
        }
        
        /// <summary>
        /// Gets all swing lows
        /// </summary>
        public List<SwingPoint> GetSwingLows()
        {
            return _swingPoints.FindAll(sp => sp.SwingType == SwingType.L);
        }
        
        /// <summary>
        /// Gets the last swing high
        /// </summary>
        public SwingPoint GetLastSwingHigh()
        {
            return _lastHighSwingPoint;
        }
        
        /// <summary>
        /// Gets the last swing low
        /// </summary>
        public SwingPoint GetLastSwingLow()
        {
            return _lastLowSwingPoint;
        }
        
        /// <summary>
        /// Gets swing point at specific index
        /// </summary>
        public SwingPoint GetSwingPointAtIndex(int index)
        {
            return _swingPoints.Find(sp => sp.Index == index);
        }
        
        /// <summary>
        /// Gets all swing points at specific index
        /// </summary>
        public List<SwingPoint> GetSwingPointsAtIndex(int index)
        {
            return _swingPoints.FindAll(sp => sp.Index == index);
        }
        
        /// <summary>
        /// Checks if there's a swing point at specific index
        /// </summary>
        public bool HasSwingPointAtIndex(int index)
        {
            return _swingPoints.Exists(sp => sp.Index == index);
        }
        
        /// <summary>
        /// Gets the previous swing point of the same type
        /// </summary>
        public SwingPoint GetPreviousSwingPoint(SwingPoint currentPoint)
        {
            if (currentPoint == null) return null;
            
            return _swingPoints
                .FindLast(sp => sp.Index < currentPoint.Index && sp.SwingType == currentPoint.SwingType);
        }
        
        /// <summary>
        /// Gets the next swing point of the same type
        /// </summary>
        public SwingPoint GetNextSwingPoint(SwingPoint currentPoint)
        {
            if (currentPoint == null) return null;
            
            return _swingPoints
                .Find(sp => sp.Index > currentPoint.Index && sp.SwingType == currentPoint.SwingType);
        }
        
        /// <summary>
        /// Updates previous and next pointers for all swing points
        /// </summary>
        public void UpdateSwingPointRelationships()
        {
            // Sort by index to ensure proper order
            _swingPoints.Sort((a, b) => a.Index.CompareTo(b.Index));
            
            // Process high swing points
            var highPoints = GetSwingHighs();
            highPoints.Sort((a, b) => a.Index.CompareTo(b.Index));
            
            for (int i = 0; i < highPoints.Count; i++)
            {
                if (i > 0)
                {
                    highPoints[i].PreviousIndex = highPoints[i - 1].Index;
                }
                
                if (i < highPoints.Count - 1)
                {
                    highPoints[i].NextIndex = highPoints[i + 1].Index;
                }
            }
            
            // Process low swing points
            var lowPoints = GetSwingLows();
            lowPoints.Sort((a, b) => a.Index.CompareTo(b.Index));
            
            for (int i = 0; i < lowPoints.Count; i++)
            {
                if (i > 0)
                {
                    lowPoints[i].PreviousIndex = lowPoints[i - 1].Index;
                }
                
                if (i < lowPoints.Count - 1)
                {
                    lowPoints[i].NextIndex = lowPoints[i + 1].Index;
                }
            }
        }
        
        /// <summary>
        /// Checks if the new bar sweeps any important liquidity points (PDH, PDL, PSH, PSL)
        /// </summary>
        public List<SwingPoint> CheckForSweptLiquidity(Candle currentBar, int currentIndex)
        {
            var sweptPoints = new List<SwingPoint>();
            
            // Get all swing points that could be swept (daily and session highs/lows)
            var liquidityPoints = _swingPoints.Where(sp =>
                (sp.LiquidityType == LiquidityType.PDH ||
                sp.LiquidityType == LiquidityType.PDL ||
                sp.LiquidityType == LiquidityType.PSH ||
                sp.LiquidityType == LiquidityType.PSL) && !sp.Swept).ToList();
            
            foreach (var point in liquidityPoints)
            {
                // Skip already swept points
                if (point.Swept)
                    continue;
                
                bool wasSwept = false;
                
                // Check for swept highs (PDH, PSH)
                if ((point.LiquidityType == LiquidityType.PDH || point.LiquidityType == LiquidityType.PSH) &&
                    currentBar.High >= point.Price && point.Index < currentIndex)
                {
                    wasSwept = true;
                }
                // Check for swept lows (PDL, PSL)
                else if ((point.LiquidityType == LiquidityType.PDL || point.LiquidityType == LiquidityType.PSL) &&
                         currentBar.Low <= point.Price && point.Index < currentIndex)
                {
                    wasSwept = true;
                }
                
                // If the point was swept, handle it
                if (wasSwept)
                {
                    point.Swept = true;
                    point.IndexOfSweepingCandle = currentIndex;
                    sweptPoints.Add(point);
                }
            }
            
            return sweptPoints;
        }
    }
}