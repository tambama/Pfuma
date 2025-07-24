using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Extensions;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Analyzes price action to identify and track order flow between swing points,
    /// and detects FVGs and Order Blocks
    /// </summary>
    public class PdArrayAnalyzer
    {
        // Add a delegate for logging
        private readonly Action<string> _logger;

        // Collection to store all order flow levels
        private readonly List<Level> _pdArrays = new List<Level>();

        // Collections for FVGs and Order Blocks (moved from FvgDetector)
        private readonly List<Level> _fvgs = new List<Level>();
        private readonly List<Level> _orderBlocks = new List<Level>();
        private readonly List<Level> _rejectionBlocks = new List<Level>();

        // Store history of swing points to identify patterns
        private readonly List<SwingPoint> _swingPointHistory = new List<SwingPoint>();

        // Chart reference for visualization
        private readonly Chart _chart;

        // Flag to control orderflow visualization
        private readonly bool _showOrderFlow;

        // Flag to control liquidity sweep visualization
        private readonly bool _showLiquiditySweep;

        // Flag to control gauntlet visualization
        private readonly bool _showGauntlet;

        // FVG and Order Block visualization flags (moved from FvgDetector)
        private readonly bool _showFVG;
        private readonly bool _showOrderBlock;
        private readonly bool _showQuadrants;
        private readonly bool _showInsideKeyLevel;
        private readonly bool _showRejectionBlock;

        // Flag to control CISD visualization
        private readonly bool _showCISD;
        private readonly int _maxCisdsPerDirection;
        private readonly List<Level> _cisdLevels = new List<Level>();

        // Breaker Blocks
        private readonly List<Level> _breakerBlocks = new List<Level>();
        private readonly bool _showBreakerBlock;

        // Unicorns
        private readonly List<Level> _unicorns = new List<Level>();
        private readonly bool _showUnicorn;

        // SMT
        private readonly bool _showSMT;
        private readonly string _smtPair;

        // Time
        private readonly bool _macroFilter;
        private readonly TimeManager _timeManager;

        // Notifications
        private readonly NotificationService _notificationService;

        public delegate double PairDataProviderDelegate(string pairSymbol, DateTime time, int index,
            Direction direction);

        public PairDataProviderDelegate PairDataProvider { get; set; }

        // Collection to store all gauntlet levels
        private readonly List<Level> _gauntlets = new List<Level>();

        // Reference to swing point detector (moved from FvgDetector)
        private readonly SwingPointDetector _swingPointDetector;

        // Reference to bars for finding specific candles
        private Bars Bars;

        /// <summary>
        /// Creates a new instance of the PD Array Analyzer with integrated FVG detection
        /// </summary>
        public PdArrayAnalyzer(
            Chart chart,
            Bars bars,
            bool showOrderFlow = false,
            bool showLiquiditySweep = false,
            bool showGauntlet = false,
            bool showFVG = false,
            bool showOrderBlock = false,
            bool showCISD = false,
            bool showBreakerBlock = false,
            bool showUnicorn = false,
            bool showQuadrants = false,
            bool showInsideKeyLevel = false,
            bool showRejectionBlock = false,
            int maxCisdsPerDirection = 2,
            SwingPointDetector swingPointDetector = null,
            bool showSMT = false,
            string smtPair = "",
            bool macroFilter = false,
            NotificationService notificationService = null,
            TimeManager timeManager = null,
            Action<string> logger = null)
        {
            _chart = chart;
            Bars = bars;
            _showOrderFlow = showOrderFlow;
            _showLiquiditySweep = showLiquiditySweep;
            _showGauntlet = showGauntlet;
            _showFVG = showFVG;
            _showOrderBlock = showOrderBlock;
            _showCISD = showCISD;
            _showBreakerBlock = showBreakerBlock;
            _showUnicorn = showUnicorn;
            _showQuadrants = showQuadrants;
            _showInsideKeyLevel = showInsideKeyLevel;
            _showRejectionBlock = showRejectionBlock;
            _maxCisdsPerDirection = maxCisdsPerDirection;
            _swingPointDetector = swingPointDetector;
            _showSMT = showSMT;
            _smtPair = smtPair;
            _macroFilter = _macroFilter;
            _notificationService = notificationService;
            _timeManager = timeManager;
            _logger = logger ?? (_ => { });
        }

        #region FVG Detection (Moved from FvgDetector)

        /// <summary>
        /// Detects Fair Value Gaps (FVGs) and Order Blocks in a series of bars
        /// </summary>
        public void DetectFVG(Bars bars, int currentIndex)
        {
            // Need at least 3 bars to detect a FVG
            if (currentIndex < 2)
                return;

            // Get the three consecutive bars
            var bar1 = bars[currentIndex - 2]; // First candle (order block candidate)
            var bar2 = bars[currentIndex - 1]; // Middle candle
            var bar3 = bars[currentIndex]; // Last candle

            // Check for bullish FVG (bar1's high is lower than bar3's low)
            if (bar1.High < bar3.Low)
            {
                // Check for volume imbalance between candle1 and candle2
                bool hasVolumeImbalance1 = bar1.Close < bar2.Open;

                // Determine low boundary based on volume imbalance
                double low = hasVolumeImbalance1 ? bar1.Close : bar1.High;

                // Check for volume imbalance between candle2 and candle3
                bool hasVolumeImbalance2 = bar2.Close < bar3.Open;

                // Determine high boundary based on volume imbalance
                double high = hasVolumeImbalance2 ? bar3.Open : bar3.Low;

                // Create a bullish FVG level
                var bullishFVG = new Level(
                    LevelType.FairValueGap,
                    low,
                    high,
                    bar1.OpenTime,
                    bar3.OpenTime,
                    bar2.OpenTime,
                    Direction.Up,
                    currentIndex - 2,
                    currentIndex,
                    currentIndex - 2,
                    currentIndex - 1, // Store the middle candle index for Gauntlet detection
                    Zone.Premium // FVGs in an uptrend are typically in the Premium zone
                );

                // Initialize quadrants for the bullish FVG
                bullishFVG.InitializeQuadrants();

                // Add to collection - always store FVGs regardless of visibility setting
                _fvgs.Add(bullishFVG);

                // Check if this FVG is a Unicorn
                CheckForUnicorns(bullishFVG);

                // Draw the FVG if visualization is enabled
                if (_showFVG)
                {
                    DrawFVG(bullishFVG);
                }

                // Check for bullish order block
                if (currentIndex >= 3 && _swingPointDetector != null)
                {
                    // First, check if candle1 is a swing point (specifically a swing low)
                    // We're looking for an opposing swing point in the direction of the recent swing
                    var bar1SwingPoint = _swingPointDetector.GetSwingPointAtIndex(currentIndex - 2);

                    // We need bar1 to be a swing low for a bullish order block
                    if (bar1SwingPoint != null && bar1SwingPoint.Direction == Direction.Down)
                    {
                        // Get the most recent swing point before this one
                        var previousSwingPoints = _swingPointDetector.GetAllSwingPoints()
                            .Where(sp => sp.Index < currentIndex - 2)
                            .OrderByDescending(sp => sp.Index)
                            .ToList();

                        // Find the most recent swing high (for confirming directional alignment)
                        var lastSwingHigh = previousSwingPoints
                            .FirstOrDefault(sp => sp.Direction == Direction.Up);

                        // Check if we've swept a previous swing low
                        bool sweptPreviousLow = false;
                        var previousSwingLow = previousSwingPoints
                            .FirstOrDefault(sp => sp.Direction == Direction.Down);

                        if (previousSwingLow != null)
                        {
                            sweptPreviousLow = bar1.Low <= previousSwingLow.Price &&
                                               bar1.Close > previousSwingLow.Price;
                        }

                        // Verify that the candle meets our order block criteria:
                        // 1. It's a swing low
                        // 2. Either it swept a previous swing low OR it came after a swing high
                        if (sweptPreviousLow || (lastSwingHigh != null && lastSwingHigh.Index < bar1SwingPoint.Index))
                        {
                            CreateBullishOrderBlock(bars, bar1, currentIndex - 2);
                        }
                    }
                }
            }

            // Check for bearish FVG (bar1's low is higher than bar3's high)
            else if (bar1.Low > bar3.High)
            {
                // Check for volume imbalance between candle1 and candle2
                bool hasVolumeImbalance1 = bar1.Close > bar2.Open;

                // Determine high boundary based on volume imbalance
                double high = hasVolumeImbalance1 ? bar1.Close : bar1.Low;

                // Check for volume imbalance between candle2 and candle3
                bool hasVolumeImbalance2 = bar2.Close > bar3.Open;

                // Determine low boundary based on volume imbalance
                double low = hasVolumeImbalance2 ? bar3.Open : bar3.High;

                // Create a bearish FVG level
                var bearishFVG = new Level(
                    LevelType.FairValueGap,
                    low,
                    high,
                    bar3.OpenTime,
                    bar1.OpenTime,
                    bar2.OpenTime,
                    Direction.Down,
                    currentIndex - 2,
                    currentIndex - 2,
                    currentIndex,
                    currentIndex - 1, // Store the middle candle index for Gauntlet detection
                    Zone.Discount // FVGs in a downtrend are typically in the Discount zone
                );

                // Initialize quadrants for the bearish FVG
                bearishFVG.InitializeQuadrants();

                // Add to collection - always store FVGs regardless of visibility setting
                _fvgs.Add(bearishFVG);

                // Check if this FVG is a Unicorn
                CheckForUnicorns(bearishFVG);

                // Draw the FVG if visualization is enabled
                if (_showFVG)
                {
                    DrawFVG(bearishFVG);
                }

                // Check for bearish order block
                if (currentIndex >= 3 && _swingPointDetector != null)
                {
                    // First, check if candle1 is a swing point (specifically a swing high)
                    // We're looking for an opposing swing point in the direction of the recent swing
                    var bar1SwingPoint = _swingPointDetector.GetSwingPointAtIndex(currentIndex - 2);

                    // We need bar1 to be a swing high for a bearish order block
                    if (bar1SwingPoint != null && bar1SwingPoint.Direction == Direction.Up)
                    {
                        // Get the most recent swing point before this one
                        var previousSwingPoints = _swingPointDetector.GetAllSwingPoints()
                            .Where(sp => sp.Index < currentIndex - 2)
                            .OrderByDescending(sp => sp.Index)
                            .ToList();

                        // Find the most recent swing low (for confirming directional alignment)
                        var lastSwingLow = previousSwingPoints
                            .FirstOrDefault(sp => sp.Direction == Direction.Down);

                        // Check if we've swept a previous swing high
                        bool sweptPreviousHigh = false;
                        var previousSwingHigh = previousSwingPoints
                            .FirstOrDefault(sp => sp.Direction == Direction.Up);

                        if (previousSwingHigh != null)
                        {
                            sweptPreviousHigh = bar1.High >= previousSwingHigh.Price &&
                                                bar1.Close < previousSwingHigh.Price;
                        }

                        // Verify that the candle meets our order block criteria:
                        // 1. It's a swing high
                        // 2. Either it swept a previous swing high OR it came after a swing low
                        if (sweptPreviousHigh || (lastSwingLow != null && lastSwingLow.Index < bar1SwingPoint.Index))
                        {
                            CreateBearishOrderBlock(bars, bar1, currentIndex - 2);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a bullish order block from a candle
        /// </summary>
        private void CreateBullishOrderBlock(Bars bars, Bar bar, int index)
        {
            // First, remove any rejection blocks at this index
            RemoveRejectionBlockAtIndex(index);

            // Create an order block from the candle's FULL range (high to low)
            var orderBlock = new Level(
                LevelType.OrderBlock,
                bar.Low, // Use the full candle low
                bar.High, // Use the full candle high
                bar.OpenTime,
                bar.OpenTime.AddMinutes(5), // 5 minute span for visualization
                bar.OpenTime,
                Direction.Up,
                index,
                index,
                index
            );

            // Check if we already have this order block to avoid duplicates
            if (!_orderBlocks.Any(ob =>
                    ob.Index == orderBlock.Index &&
                    ob.Direction == orderBlock.Direction))
            {
                // Initialize quadrants for the bearish order block
                orderBlock.InitializeQuadrants();

                _orderBlocks.Add(orderBlock);

                // Draw the order block if visualization is enabled
                if (_showOrderBlock)
                {
                    DrawOrderBlock(orderBlock);
                }
            }
        }

        /// <summary>
        /// Creates a bearish order block from a candle
        /// </summary>
        private void CreateBearishOrderBlock(Bars bars, Bar bar, int index)
        {
            // First, remove any rejection blocks at this index
            RemoveRejectionBlockAtIndex(index);

            // Create an order block from the candle's FULL range (high to low)
            var orderBlock = new Level(
                LevelType.OrderBlock,
                bar.Low, // Use the full candle low
                bar.High, // Use the full candle high
                bar.OpenTime,
                bar.OpenTime.AddMinutes(5), // 5 minute span for visualization
                bar.OpenTime,
                Direction.Down,
                index,
                index,
                index
            );

            // Check if we already have this order block to avoid duplicates
            if (!_orderBlocks.Any(ob =>
                    ob.Index == orderBlock.Index &&
                    ob.Direction == orderBlock.Direction))
            {
                // Initialize quadrants for the bullish order block
                orderBlock.InitializeQuadrants();

                _orderBlocks.Add(orderBlock);

                // Draw the order block if visualization is enabled
                if (_showOrderBlock)
                {
                    DrawOrderBlock(orderBlock);
                }
            }
        }

        /// <summary>
        /// Draws a Fair Value Gap on the chart
        /// </summary>
        private void DrawFVG(Level fvg)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this FVG
            string id = $"fvg-{fvg.Direction}-{fvg.Index}-{fvg.IndexHigh}-{fvg.IndexLow}";

            // Use the extended chart extension method for better FVG visualization
            _chart.DrawFairValueGap(fvg, id);

            // Draw quadrant levels for the FVG
            if (_showFVG && _showQuadrants)
            {
                DrawQuadrantLevels(fvg);
            }
        }

        /// <summary>
        /// Draws an Order Block on the chart
        /// </summary>
        private void DrawOrderBlock(Level orderBlock)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this order block
            string id = $"ob-{orderBlock.Direction}-{orderBlock.Index}";

            // Draw rectangle with 5-minute duration
            _chart.DrawRectangle(
                orderBlock,
                id,
                true, // Draw midpoint
                20 // Higher opacity for order blocks to make them more visible
            );

            // Draw quadrant levels for the order block
            if (_showOrderBlock && _showQuadrants)
            {
                DrawQuadrantLevels(orderBlock);
            }
        }

        /// <summary>
        /// Get all FVGs
        /// </summary>
        public List<Level> GetAllFVGs()
        {
            return _fvgs;
        }

        /// <summary>
        /// Get bullish FVGs
        /// </summary>
        public List<Level> GetBullishFVGs()
        {
            return _fvgs.Where(f => f.Direction == Direction.Up).ToList();
        }

        /// <summary>
        /// Get bearish FVGs
        /// </summary>
        public List<Level> GetBearishFVGs()
        {
            return _fvgs.Where(f => f.Direction == Direction.Down).ToList();
        }

        /// <summary>
        /// Get all Order Blocks
        /// </summary>
        public List<Level> GetAllOrderBlocks()
        {
            return _orderBlocks;
        }

        /// <summary>
        /// Get bullish Order Blocks
        /// </summary>
        public List<Level> GetBullishOrderBlocks()
        {
            return _orderBlocks.Where(ob => ob.Direction == Direction.Up).ToList();
        }

        /// <summary>
        /// Get bearish Order Blocks
        /// </summary>
        public List<Level> GetBearishOrderBlocks()
        {
            return _orderBlocks.Where(ob => ob.Direction == Direction.Down).ToList();
        }

        #endregion

        #region Order Flow Analysis

        /// <summary>
        /// Process a new swing point to update order flow tracking
        /// </summary>
        public void ProcessSwingPoint(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;

            // Add the new swing point to our history
            _swingPointHistory.Add(swingPoint);

            // Sort the history by index to ensure chronological order
            _swingPointHistory.Sort((a, b) => a.Index.CompareTo(b.Index));

            // Check for rejection block - add this line
            CheckForRejectionBlock(swingPoint);

            if (swingPoint.Direction == Direction.Down)
            {
                // Process a new swing low - calculate bullish orderflow
                ProcessNewSwingLow(swingPoint);
            }
            else if (swingPoint.Direction == Direction.Up)
            {
                // Process a new swing high - calculate bearish orderflow
                ProcessNewSwingHigh(swingPoint);
            }

            CheckQuadrantsOnSwingPoint(swingPoint);
            CheckCisdConfirmation(swingPoint, swingPoint.Index);
        }

        /// <summary>
        /// Handle the removal of a swing point
        /// </summary>
        public void RemoveSwingPoint(SwingPoint removedPoint)
        {
            if (removedPoint == null)
                return;

            // Remove the swing point from our history
            _swingPointHistory.RemoveAll(p => p.Index == removedPoint.Index &&
                                              p.Direction == removedPoint.Direction &&
                                              Math.Abs(p.Price - removedPoint.Price) < 0.0001);

            // Find any orderflow levels that reference this swing point
            var affectedArrays = new List<Level>();

            foreach (var array in _pdArrays)
            {
                bool isAffected = false;

                // Check if this array uses the removed point as one of its key points
                if (array.Direction == Direction.Up)
                {
                    // For bullish orderflow, check if removed point is the low or high
                    if (array.IndexLow == removedPoint.Index && removedPoint.Direction == Direction.Down)
                        isAffected = true;
                    else if (array.IndexHigh == removedPoint.Index && removedPoint.Direction == Direction.Up)
                        isAffected = true;
                }
                else // Direction.Down
                {
                    // For bearish orderflow, check if removed point is the high or low
                    if (array.IndexHigh == removedPoint.Index && removedPoint.Direction == Direction.Up)
                        isAffected = true;
                    else if (array.IndexLow == removedPoint.Index && removedPoint.Direction == Direction.Down)
                        isAffected = true;
                }

                // Check if this array references the removed point in its swept points
                if (!isAffected && array.SweptSwingPoints != null)
                {
                    foreach (var sweptPoint in array.SweptSwingPoints)
                    {
                        if (sweptPoint.Index == removedPoint.Index &&
                            sweptPoint.Direction == removedPoint.Direction &&
                            Math.Abs(sweptPoint.Price - removedPoint.Price) < 0.0001)
                        {
                            isAffected = true;
                            break;
                        }
                    }
                }

                // If this array is affected, add it to our list
                if (isAffected)
                    affectedArrays.Add(array);
            }

            // Remove affected arrays from our collection
            foreach (var array in affectedArrays)
            {
                _pdArrays.Remove(array);

                // Remove any gauntlets associated with this array
                if (array.GauntletFVG != null)
                {
                    _gauntlets.Remove(array.GauntletFVG);

                    // Remove gauntlet visualization
                    if (_chart != null)
                    {
                        string gauntletId = $"gauntlet-{array.GauntletFVG.Direction}-{array.GauntletFVG.Index}";
                        _chart.RemoveObject(gauntletId);
                    }
                }

                // Clean up visualization
                if (_chart != null)
                {
                    // Remove orderflow visualization
                    string ofId = $"of-{array.Direction}-{array.Index}-{array.IndexHigh}-{array.IndexLow}";
                    _chart.RemoveObject(ofId);

                    // Remove liquidity sweep line if present
                    if (array.SweptSwingPoint != null)
                    {
                        string sweptId = $"swept-{array.Direction}-{array.Index}-{array.SweptSwingPoint.Index}";
                        _chart.RemoveObject(sweptId);
                    }
                }
            }

            // Add rejection block cleanup
            var rejectionBlocksToRemove = _rejectionBlocks
                .Where(rb => rb.Index == removedPoint.Index)
                .ToList();

            foreach (var rb in rejectionBlocksToRemove)
            {
                _rejectionBlocks.Remove(rb);

                // Clean up visualization
                if (_chart != null)
                {
                    string id = $"rb-{rb.Direction}-{rb.Index}";
                    _chart.RemoveObject(id);
                    _chart.RemoveObject($"{id}-midline");
                }
            }

            // If we removed any arrays, we need to recalculate
            if (affectedArrays.Count > 0)
            {
                // Regenerate orderflows based on the remaining points
                RegenerateOrderFlows();
            }
        }

        /// <summary>
        /// Regenerate all orderflow levels based on the current swing point history
        /// </summary>
        private void RegenerateOrderFlows()
        {
            // Clear existing order flows
            _pdArrays.Clear();

            // Clear existing gauntlets
            _gauntlets.Clear();

            // Remove all orderflow visualization
            if (_chart != null)
            {
                // This is a simplistic approach - in a real implementation we would 
                // need to be more selective about which objects to remove
                for (int i = 0; i < 1000; i++) // Arbitrary limit
                {
                    _chart.RemoveObject($"of-Up-{i}");
                    _chart.RemoveObject($"of-Down-{i}");
                    _chart.RemoveObject($"swept-Up-{i}");
                    _chart.RemoveObject($"swept-Down-{i}");
                    _chart.RemoveObject($"gauntlet-Up-{i}");
                    _chart.RemoveObject($"gauntlet-Down-{i}");
                }
            }

            // Sort the history by index to ensure chronological order
            _swingPointHistory.Sort((a, b) => a.Index.CompareTo(b.Index));

            // Skip if we don't have enough points
            if (_swingPointHistory.Count < 3)
                return;

            // Process each swing point to recreate order flows
            for (int i = 2; i < _swingPointHistory.Count; i++)
            {
                var swingPoint = _swingPointHistory[i];

                if (swingPoint.Direction == Direction.Down)
                {
                    ProcessNewSwingLow(swingPoint);
                }
                else if (swingPoint.Direction == Direction.Up)
                {
                    ProcessNewSwingHigh(swingPoint);
                }
            }
        }

        /// <summary>
        /// Process a new swing low to calculate bullish orderflow
        /// </summary>
        private void ProcessNewSwingLow(SwingPoint newSwingLow)
        {
            // To calculate bullish orderflow when a new low is created, we need:
            // 1. The previous swing low (before the most recent swing high)
            // 2. The most recent swing high

            // Get chronologically ordered swing highs and lows
            var swingHighs = _swingPointHistory.Where(p => p.Direction == Direction.Up)
                .OrderByDescending(p => p.Index)
                .ToList();

            var swingLows = _swingPointHistory.Where(p => p.Direction == Direction.Down)
                .OrderByDescending(p => p.Index)
                .ToList();

            // We need at least one swing high and two swing lows (including the new one)
            if (swingHighs.Count < 1 || swingLows.Count < 2)
                return;

            // The most recent swing high
            var recentSwingHigh = swingHighs.First();

            // The previous swing low (not the current one we're processing)
            var previousSwingLow = swingLows.Count > 1 ? swingLows[1] : null;

            // Make sure the previous swing low came before the recent swing high
            if (previousSwingLow != null && previousSwingLow.Index < recentSwingHigh.Index)
            {
                // Create a bullish orderflow level from the previous swing low to the recent swing high
                var bullishOrderFlow = new Level(
                    LevelType.Orderflow,
                    previousSwingLow.Price,
                    recentSwingHigh.Price,
                    previousSwingLow.Time,
                    recentSwingHigh.Time,
                    null,
                    Direction.Up,
                    previousSwingLow.Index, // Index is the swing low index for bullish orderflow
                    recentSwingHigh.Index, // IndexHigh is the recent swing high index
                    previousSwingLow.Index // IndexLow is the previous swing low index
                );

                {
                    // Look for bearish rejection blocks (from bullish swing points)
                    var bearishRejectionBlocks = _rejectionBlocks
                        .Where(rb => rb.Direction == Direction.Down)
                        .OrderByDescending(rb => rb.Index)
                        .ToList();

                    // Associate the most recent bearish rejection block if it exists
                    var recentBearishRejectionBlock = bearishRejectionBlocks.FirstOrDefault();
                    if (recentBearishRejectionBlock != null)
                    {
                        // Store relationship
                        bullishOrderFlow.RejectionBlock = recentBearishRejectionBlock;
                    }
                }

                // For bullish orderflow, set SMT value only for the swing high
                if (_showSMT && !string.IsNullOrEmpty(_smtPair))
                {
                    // Get the pair's HIGH price at the time of the swing high
                    double pairPrice = GetPairPriceAtIndex(recentSwingHigh.Index, Direction.Up);
                    if (pairPrice > 0)
                    {
                        recentSwingHigh.SMTValue = pairPrice;
                    }
                }

                InitializeQuadrants(bullishOrderFlow);

                // Check for swept swing highs
                CheckForSweptSwingHighs(bullishOrderFlow);

                // Add to collection
                _pdArrays.Add(bullishOrderFlow);

                // Draw the orderflow rectangle if visualization is enabled
                if (_showOrderFlow)
                {
                    DrawOrderFlow(bullishOrderFlow);
                }

                // Draw swept liquidity line if applicable - independent of orderflow visibility
                if (bullishOrderFlow.SweptSwingPoint != null)
                {
                    DrawSweptLiquidityLine(bullishOrderFlow);
                }

                // Draw gauntlet if it exists and visualization is enabled
                if (bullishOrderFlow.GauntletFVG != null && _showGauntlet)
                {
                    DrawGauntlet(bullishOrderFlow.GauntletFVG);
                }
            }
        }

        /// <summary>
        /// Process a new swing high to calculate bearish orderflow
        /// </summary>
        private void ProcessNewSwingHigh(SwingPoint newSwingHigh)
        {
            // To calculate bearish orderflow when a new high is created, we need:
            // 1. The previous swing high (before the most recent swing low)
            // 2. The most recent swing low

            // Get chronologically ordered swing highs and lows
            var swingHighs = _swingPointHistory.Where(p => p.Direction == Direction.Up)
                .OrderByDescending(p => p.Index)
                .ToList();

            var swingLows = _swingPointHistory.Where(p => p.Direction == Direction.Down)
                .OrderByDescending(p => p.Index)
                .ToList();

            // We need at least two swing highs (including the new one) and one swing low
            if (swingHighs.Count < 2 || swingLows.Count < 1)
                return;

            // The most recent swing low
            var recentSwingLow = swingLows.First();

            // The previous swing high (not the current one we're processing)
            var previousSwingHigh = swingHighs.Count > 1 ? swingHighs[1] : null;

            // Make sure the previous swing high came before the recent swing low
            if (previousSwingHigh != null && previousSwingHigh.Index < recentSwingLow.Index)
            {
                // Create a bearish orderflow level from the previous swing high to the recent swing low
                var bearishOrderFlow = new Level(
                    LevelType.Orderflow,
                    recentSwingLow.Price,
                    previousSwingHigh.Price,
                    recentSwingLow.Time,
                    previousSwingHigh.Time,
                    null,
                    Direction.Down,
                    previousSwingHigh.Index, // Index is the swing high index for bearish orderflow
                    previousSwingHigh.Index, // IndexHigh is the previous swing high index
                    recentSwingLow.Index // IndexLow is the recent swing low index
                );

                {
                    // Look for bullish rejection blocks (from bearish swing points)
                    var bullishRejectionBlocks = _rejectionBlocks
                        .Where(rb => rb.Direction == Direction.Up)
                        .OrderByDescending(rb => rb.Index)
                        .ToList();

                    // Associate the most recent bullish rejection block if it exists
                    var recentBullishRejectionBlock = bullishRejectionBlocks.FirstOrDefault();
                    if (recentBullishRejectionBlock != null)
                    {
                        // Store relationship
                        bearishOrderFlow.RejectionBlock = recentBullishRejectionBlock;
                    }
                }

                // For bearish orderflow, set SMT value only for the swing low
                if (_showSMT && !string.IsNullOrEmpty(_smtPair))
                {
                    // Get the pair's LOW price at the time of the swing low
                    double pairPrice = GetPairPriceAtIndex(recentSwingLow.Index, Direction.Down);
                    if (pairPrice > 0)
                    {
                        recentSwingLow.SMTValue = pairPrice;
                    }
                }

                InitializeQuadrants(bearishOrderFlow);

                // Check for swept swing lows
                CheckForSweptSwingLows(bearishOrderFlow);

                // Add to collection
                _pdArrays.Add(bearishOrderFlow);

                // Draw the orderflow rectangle if visualization is enabled
                if (_showOrderFlow)
                {
                    DrawOrderFlow(bearishOrderFlow);
                }

                // Draw swept liquidity line if applicable - independent of orderflow visibility
                if (bearishOrderFlow.SweptSwingPoint != null)
                {
                    DrawSweptLiquidityLine(bearishOrderFlow);
                }

                // Draw gauntlet if it exists and visualization is enabled
                if (bearishOrderFlow.GauntletFVG != null && _showGauntlet)
                {
                    DrawGauntlet(bearishOrderFlow.GauntletFVG);
                }
            }
        }

        /// <summary>
        /// Checks if the bullish orderflow level swept any swing highs
        /// </summary>
        private void CheckForSweptSwingHighs(Level orderflow)
        {
            // Get all unswept swing highs
            var unsweptSwingHighs = _swingPointHistory
                .Where(p => p.Direction == Direction.Up && !p.Swept)
                .OrderByDescending(p => p.Price)
                .ToList();

            // Check if any swing highs were swept (high of orderflow > swing high AND low of orderflow < swing high)
            var sweptHighs = unsweptSwingHighs
                .Where(h => orderflow.High > h.Price && orderflow.Low < h.Price && h.Index < orderflow.IndexHigh)
                .ToList();

            // Only proceed if we have swept points
            if (sweptHighs.Count > 0)
            {
                // Initialize the swept swing points collection
                orderflow.SweptSwingPoints = new List<SwingPoint>();

                // Find the highest swept swing high (the extreme point)
                var highestSweptPoint = sweptHighs.OrderByDescending(h => h.Price).First();

                // Now find the exact candle that swept this extreme point
                int sweepingCandleIndex = FindSweepingCandleForPoint(orderflow, highestSweptPoint);
                orderflow.IndexOfSweepingCandle = sweepingCandleIndex;

                // Mark all swept points
                foreach (var sweptPoint in sweptHighs)
                {
                    // Mark it as swept
                    sweptPoint.Swept = true;
                    sweptPoint.SweptLiquidity = true;
                    // Use the same sweeping candle index for all (from the extreme point)
                    sweptPoint.IndexOfSweepingCandle = sweepingCandleIndex;

                    // Add to the collection of swept points
                    orderflow.SweptSwingPoints.Add(sweptPoint);
                }

                // Set the extreme point as the primary swept point for visualization
                orderflow.SweptSwingPoint = highestSweptPoint;

                DetectCisdLevel(orderflow);

                // Check for SMT divergence if enabled
                if (_showSMT && !string.IsNullOrEmpty(_smtPair))
                {
                    // Get the current swing point at sweeping candle index
                    var sweepingSwingPoint = _swingPointDetector.GetSwingPointAtIndex(sweepingCandleIndex);

                    if (sweepingSwingPoint != null && highestSweptPoint != null)
                    {
                        // Check for SMT divergence when price makes new high but the pair makes a lower high
                        if (sweepingSwingPoint.Price > highestSweptPoint.Price &&
                            sweepingSwingPoint.SMTValue < highestSweptPoint.SMTValue)
                        {
                            sweepingSwingPoint.HasSMT = true;
                            sweepingSwingPoint.SMTSource = highestSweptPoint;

                            // Draw a dotted trendline connecting the points
                            DrawSmtDivergence(highestSweptPoint, sweepingSwingPoint);
                        }
                    }
                }

                // Add score based on how many sweep points were triggered
                // More points = higher score
                orderflow.Score += Math.Min(3, sweptHighs.Count); // Cap at 3 for scoring

                // Check for Gauntlet pattern after finding the sweeping candle
                CheckForGauntlet(orderflow, sweepingCandleIndex);
            }
        }

        /// <summary>
        /// Checks if the bearish orderflow level swept any swing lows
        /// </summary>
        private void CheckForSweptSwingLows(Level orderflow)
        {
            // Get all unswept swing lows
            var unsweptSwingLows = _swingPointHistory
                .Where(p => p.Direction == Direction.Down && !p.Swept)
                .OrderBy(p => p.Price)
                .ToList();

            // Check if any swing lows were swept (low of orderflow < swing low AND high of orderflow > swing low)
            var sweptLows = unsweptSwingLows
                .Where(l => orderflow.Low < l.Price && orderflow.High > l.Price && l.Index < orderflow.IndexLow)
                .ToList();

            // Only proceed if we have swept points
            if (sweptLows.Count > 0)
            {
                // Initialize the swept swing points collection
                orderflow.SweptSwingPoints = new List<SwingPoint>();

                // Find the lowest swept swing low (the extreme point)
                var lowestSweptPoint = sweptLows.OrderBy(l => l.Price).First();

                // Now find the exact candle that swept this extreme point
                int sweepingCandleIndex = FindSweepingCandleForPoint(orderflow, lowestSweptPoint);
                orderflow.IndexOfSweepingCandle = sweepingCandleIndex;

                // Mark all swept points
                foreach (var sweptPoint in sweptLows)
                {
                    // Mark it as swept
                    sweptPoint.Swept = true;
                    sweptPoint.SweptLiquidity = true;
                    // Use the same sweeping candle index for all (from the extreme point)
                    sweptPoint.IndexOfSweepingCandle = sweepingCandleIndex;

                    // Add to the collection of swept points
                    orderflow.SweptSwingPoints.Add(sweptPoint);
                }

                // Set the extreme point as the primary swept point for visualization
                orderflow.SweptSwingPoint = lowestSweptPoint;

                DetectCisdLevel(orderflow);

                // Check for SMT divergence if enabled
                if (_showSMT && !string.IsNullOrEmpty(_smtPair))
                {
                    // Get the current swing point at sweeping candle index
                    var sweepingSwingPoint = _swingPointDetector.GetSwingPointAtIndex(sweepingCandleIndex);

                    if (sweepingSwingPoint != null && lowestSweptPoint != null)
                    {
                        // Check for SMT divergence when price makes new low but the pair makes a higher low
                        if (sweepingSwingPoint.Price < lowestSweptPoint.Price &&
                            sweepingSwingPoint.SMTValue > lowestSweptPoint.SMTValue)
                        {
                            sweepingSwingPoint.HasSMT = true;
                            sweepingSwingPoint.SMTSource = lowestSweptPoint;

                            // Draw a dotted trendline connecting the points
                            DrawSmtDivergence(lowestSweptPoint, sweepingSwingPoint);
                        }
                    }
                }

                // Add score based on how many sweep points were triggered
                // More points = higher score
                orderflow.Score += Math.Min(3, sweptLows.Count); // Cap at 3 for scoring

                // Check for Gauntlet pattern after finding the sweeping candle
                CheckForGauntlet(orderflow, sweepingCandleIndex);
            }
        }

        /// <summary>
        /// Checks if the sweeping candle is part of an FVG pattern to detect Gauntlets
        /// </summary>
        private void CheckForGauntlet(Level orderflow, int sweepingCandleIndex)
        {
            // Skip if index is invalid
            if (sweepingCandleIndex < 1 || sweepingCandleIndex >= Bars.Count)
                return;

            // Skip if the orderflow doesn't have swept liquidity
            if (orderflow.SweptSwingPoint == null)
                return;

            // Get all FVGs
            if (_fvgs == null || _fvgs.Count == 0)
                return;

            // Get the time of the sweeping candle for macro time check
            DateTime sweepingCandleTime = Bars[sweepingCandleIndex].OpenTime;

            // Check if we're filtering by macro time and if this candle is in a macro period
            bool isInMacro = !_macroFilter || (_timeManager != null && _timeManager.IsInMacroTime(sweepingCandleTime));

            // Skip processing if we're filtering by macro and not in a macro time
            if (_macroFilter && !isInMacro)
                return;

            // First, find the last FVG within the orderflow
            var lastFvgInOrderflow = FindLastFVGInOrderflow(orderflow, _fvgs);
            SwingPoint lastSwingPoint = null;

            // If we found a matching FVG, check if its second or third candle is the sweeping candle
            if (lastFvgInOrderflow != null)
            {
                bool isSweepingCandlePartOfFVG = false;

                if (orderflow.Direction == Direction.Up)
                {
                    // For bullish FVGs, check if the sweeping candle matches either the middle or high candle
                    isSweepingCandlePartOfFVG =
                        (lastFvgInOrderflow.IndexMid == sweepingCandleIndex) ||
                        (lastFvgInOrderflow.IndexHigh == sweepingCandleIndex);

                    lastSwingPoint = _swingPointHistory.FirstOrDefault(o => o.Index == orderflow.IndexHigh);
                }
                else // Direction.Down
                {
                    // For bearish FVGs, check if the sweeping candle matches either the middle or low candle
                    isSweepingCandlePartOfFVG =
                        (lastFvgInOrderflow.IndexMid == sweepingCandleIndex) ||
                        (lastFvgInOrderflow.IndexLow == sweepingCandleIndex);

                    lastSwingPoint = _swingPointHistory.FirstOrDefault(o => o.Index == orderflow.IndexLow);
                }

                // If the sweeping candle is part of the FVG, mark it as a Gauntlet
                if (isSweepingCandlePartOfFVG)
                {
                    // Ensure the FVG and orderflow have the same direction
                    if (lastFvgInOrderflow.Direction != orderflow.Direction)
                    {
                        return; // Skip if directions don't match
                    }
                    
                    // Mark the FVG as a Gauntlet
                    lastFvgInOrderflow.IsGauntlet = true;

                    // Associate it with the orderflow
                    orderflow.GauntletFVG = lastFvgInOrderflow;

                    // Add to our collection of Gauntlets if not already present
                    if (!_gauntlets.Any(g => g.Index == lastFvgInOrderflow.Index &&
                                             g.Direction == lastFvgInOrderflow.Direction))
                    {
                        _gauntlets.Add(lastFvgInOrderflow);

                        // Send notification for Gauntlet detection
                        _notificationService?.NotifyGauntletDetected(lastFvgInOrderflow.Direction);
                    }

                    // Draw it if visualization is enabled
                    if (_showGauntlet)
                    {
                        DrawGauntlet(lastFvgInOrderflow);
                    }
                }
            }
        }

        /// <summary>
        /// Finds the last (most recent) FVG contained within an orderflow's boundaries
        /// </summary>
        private Level FindLastFVGInOrderflow(Level orderflow, List<Level> allFvgs)
        {
            // Define the price and time boundaries of the orderflow
            double lowPrice = orderflow.Low;
            double highPrice = orderflow.High;
            DateTime earliestTime = orderflow.Direction == Direction.Up ? orderflow.LowTime : orderflow.HighTime;
            DateTime latestTime = orderflow.Direction == Direction.Up ? orderflow.HighTime : orderflow.LowTime;

            // Filter FVGs that have the same direction as the orderflow and are contained within its boundaries
            var matchingFvgs = allFvgs
                .Where(fvg =>
                    // Same direction
                    fvg.Direction == orderflow.Direction &&
                    // Within price boundaries (at least partially)
                    !(fvg.High < lowPrice || fvg.Low > highPrice) &&
                    // Within time boundaries
                    fvg.MidTime >= earliestTime && fvg.MidTime <= latestTime)
                // Sort by index (descending) to get the most recent first
                .OrderByDescending(fvg => fvg.Index)
                .ToList();

            // Return the most recent FVG if any were found
            return matchingFvgs.FirstOrDefault();
        }

        /// <summary>
        /// Finds the exact candle that swept a specific swing point
        /// </summary>
        private int FindSweepingCandleForPoint(Level orderflow, SwingPoint sweptPoint)
        {
            // If no point was provided, use the default index
            if (sweptPoint == null)
                return orderflow.Direction == Direction.Up ? orderflow.IndexHigh : orderflow.IndexLow;

            // Get the price of the swept point
            double sweepPrice = sweptPoint.Price;

            // Define search range based on direction of the orderflow
            int startIndex = orderflow.Direction == Direction.Up ? orderflow.IndexLow : orderflow.IndexHigh;
            int endIndex = orderflow.Direction == Direction.Up ? orderflow.IndexHigh : orderflow.IndexLow;

            // Ensure we have valid indices
            if (startIndex < 0 || endIndex < 0 || startIndex >= Bars.Count || endIndex >= Bars.Count)
                return orderflow.Direction == Direction.Up
                    ? orderflow.IndexHigh
                    : orderflow.IndexLow; // Default fallback

            // Search for the candle that swept the price
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (orderflow.Direction == Direction.Up && Bars[i].High > sweepPrice)
                {
                    return i; // This is the candle that swept the high
                }
                else if (orderflow.Direction == Direction.Down && Bars[i].Low < sweepPrice)
                {
                    return i; // This is the candle that swept the low
                }
            }

            // If no specific candle is found, default to the extreme index
            return orderflow.Direction == Direction.Up ? orderflow.IndexHigh : orderflow.IndexLow;
        }

        private void DrawInsideKeyLevelIcon(SwingPoint swingPoint)
        {
            if (_chart == null)
                return;

            Color color = swingPoint.Direction == Direction.Up ? Color.Green : Color.Red;
            var icon = _chart.DrawIcon($"kl-{swingPoint.Time}", ChartIconType.Circle, swingPoint.Time, swingPoint.Price,
                color);
        }

        /// <summary>
        /// Draws a liquidity sweep line on the chart to show when a swing point was swept
        /// </summary>
        private void DrawSweptLiquidityLine(Level orderflow)
        {
            if (_chart == null || orderflow.SweptSwingPoint == null)
                return;

            // Skip drawing if liquidity sweep visualization is disabled
            if (!_showLiquiditySweep)
                return;

            // Get the swept swing point (the extreme one)
            var sweptPoint = orderflow.SweptSwingPoint;

            // Create a unique ID for this liquidity sweep line
            string id = $"swept-{orderflow.Direction}-{orderflow.Index}-{sweptPoint.Index}";

            // Determine the start time (the swept swing point)
            DateTime startTime = sweptPoint.Time;

            // Determine the price (the price of the swept swing point)
            double price = sweptPoint.Price;

            // Get the time of the actual sweeping candle using the stored index
            DateTime endTime;
            if (orderflow.IndexOfSweepingCandle >= 0 && orderflow.IndexOfSweepingCandle < Bars.Count)
            {
                endTime = Bars[orderflow.IndexOfSweepingCandle].OpenTime;
            }
            else
            {
                // Fallback to the order flow's extreme time if index is invalid
                endTime = orderflow.Direction == Direction.Up ? orderflow.HighTime : orderflow.LowTime;
            }

            // Draw the dotted yellow line without label as requested
            _chart.DrawStraightLine(
                id,
                startTime,
                price,
                endTime,
                price,
                null, // No label
                LineStyle.Dots,
                Color.Yellow,
                false, // Don't show label
                true, // Remove existing
                false // Not extended
            );
        }

        /// <summary>
        /// Draw an order flow rectangle on the chart
        /// </summary>
        private void DrawOrderFlow(Level level)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this order flow
            string id = $"of-{level.Direction}-{level.Index}-{level.IndexHigh}-{level.IndexLow}";

            // Draw rectangle with the appropriate color based on direction
            _chart.DrawOrderFlowRectangle(level, id);
        }

        /// <summary>
        /// Draws a Gauntlet on the chart using the order block drawing method
        /// </summary>
        private void DrawGauntlet(Level gauntlet)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this Gauntlet
            string id = $"gauntlet-{gauntlet.Direction}-{gauntlet.Index}-{gauntlet.IndexHigh}-{gauntlet.IndexLow}";

            // Use the existing DrawRectangle method from ChartExtensions
            // but with higher opacity to distinguish from other elements
            _chart.DrawRectangle(
                gauntlet,
                id,
                true, // Draw midpoint
                25, // Higher opacity for Gauntlets
                length: 1
            );
        }

        #endregion

        #region CISD and Breaker Blocks

        // Detect CISD from orderflow that swept liquidity
        private void DetectCisdLevel(Level orderflow)
        {
            // Only process orderflows that swept liquidity
            if (orderflow.SweptSwingPoint == null)
                return;

            // Define search range based on direction of the orderflow
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);

            // Ensure we have valid indices
            if (startIndex < 0 || endIndex < 0 || startIndex >= Bars.Count || endIndex >= Bars.Count)
                return;

            if (orderflow.Direction == Direction.Up) // Bullish orderflow creates bearish CISD
            {
                // Find all sets of consecutive bullish candles within the orderflow
                List<List<int>> bullishSets = new();
                List<int> currentSet = new();

                // Scan through the bullish orderflow
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var bar = Bars[i];
                    var direction = bar.GetCandleDirection();

                    if (direction == Direction.Up)
                    {
                        // Add this bullish candle to the current set
                        currentSet.Add(i);
                    }
                    else if (currentSet.Count > 0)
                    {
                        // We hit a bearish candle, store the current set and reset
                        bullishSets.Add(new List<int>(currentSet));
                        currentSet.Clear();
                    }
                }

                // Don't forget to add the last set if it exists
                if (currentSet.Count > 0)
                {
                    bullishSets.Add(new List<int>(currentSet));
                }

                // If we have no sets, we can't create a CISD
                if (bullishSets.Count == 0)
                    return;

                // Use the last set of consecutive bullish candles
                var lastBullishSet = bullishSets[bullishSets.Count - 1];

                if (lastBullishSet.Count == 0)
                    return;

                // Get the first and last index of the last set
                int firstBullishIndex = lastBullishSet.Min();
                int lastBullishIndex = lastBullishSet.Max();

                // Create a BEARISH CISD level
                var cisdLevel = new Level(
                    LevelType.CISD,
                    Bars[firstBullishIndex].Open, // Low is the opening price of first bullish candle
                    Bars[lastBullishIndex].Close, // High is the closing price of last bullish candle
                    Bars[firstBullishIndex].OpenTime,
                    Bars[lastBullishIndex].OpenTime,
                    null,
                    Direction.Down, // BEARISH direction for bullish sequence
                    firstBullishIndex,
                    lastBullishIndex,
                    firstBullishIndex
                );

                // Associate with orderflow
                orderflow.CISDLevel = cisdLevel;

                // Add to CISD collection
                _cisdLevels.Add(cisdLevel);

                // Manage max CISDs before adding the new one
                ManageMaxCisdCount(Direction.Down);

                var point = _swingPointHistory.FirstOrDefault(p =>
                    p.Index == orderflow.IndexHigh && p.Direction == Direction.Up);
                if (point != null && point.InsideKeyLevel && _showInsideKeyLevel)
                {
                    //DrawInsideKeyLevelIcon(point);
                }
            }
            else // Direction.Down (bearish orderflow creates bullish CISD)
            {
                // Find all sets of consecutive bearish candles within the orderflow
                List<List<int>> bearishSets = new();
                List<int> currentSet = new();

                // Scan through the bearish orderflow
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var bar = Bars[i];
                    var direction = bar.GetCandleDirection();

                    if (direction == Direction.Down)
                    {
                        // Add this bearish candle to the current set
                        currentSet.Add(i);
                    }
                    else if (currentSet.Count > 0)
                    {
                        // We hit a bullish candle, store the current set and reset
                        bearishSets.Add(new List<int>(currentSet));
                        currentSet.Clear();
                    }
                }

                // Don't forget to add the last set if it exists
                if (currentSet.Count > 0)
                {
                    bearishSets.Add(new List<int>(currentSet));
                }

                // If we have no sets, we can't create a CISD
                if (bearishSets.Count == 0)
                    return;

                // Use the last set of consecutive bearish candles
                var lastBearishSet = bearishSets[bearishSets.Count - 1];

                if (lastBearishSet.Count == 0)
                    return;

                // Get the first and last index of the last set
                int firstBearishIndex = lastBearishSet.Min();
                int lastBearishIndex = lastBearishSet.Max();

                // Create a BULLISH CISD level
                var cisdLevel = new Level(
                    LevelType.CISD,
                    Bars[lastBearishIndex].Close, // Low is the closing price of last bearish candle
                    Bars[firstBearishIndex].Open, // High is the opening price of first bearish candle
                    Bars[lastBearishIndex].OpenTime,
                    Bars[firstBearishIndex].OpenTime,
                    null,
                    Direction.Up, // BULLISH direction for bearish sequence
                    firstBearishIndex,
                    firstBearishIndex,
                    lastBearishIndex
                );

                // Associate with orderflow
                orderflow.CISDLevel = cisdLevel;

                // Add to CISD collection
                _cisdLevels.Add(cisdLevel);

                // Manage max CISDs before adding the new one
                ManageMaxCisdCount(Direction.Up);

                var point = _swingPointHistory.FirstOrDefault(p =>
                    p.Index == orderflow.IndexLow && p.Direction == Direction.Down);
                if (point != null && point.InsideKeyLevel && _showInsideKeyLevel)
                {
                    //DrawInsideKeyLevelIcon(point);
                }
            }
        }

        // Check for CISD confirmation and activation
        // Check for CISD confirmation and activation
        private void CheckCisdConfirmation(SwingPoint swingPoint, int currentIndex)
        {
            // Get all CISD levels that are not yet confirmed
            var pendingCisdLevels = _cisdLevels
                .Where(cisd => !cisd.IsConfirmed)
                .ToList();

            // Check for CISD confirmation
            foreach (var cisd in pendingCisdLevels)
            {
                // Check if we're filtering by macro time and if this swing point is in a macro
                bool isInMacro = !_macroFilter || (_timeManager != null && _timeManager.IsInMacroTime(swingPoint.Time));

                // Skip processing if we're filtering by macro and not in a macro time
                if (_macroFilter && !isInMacro)
                    continue;

                if (cisd.Direction == Direction.Up) // Bullish CISD
                {
                    // Bullish CISD is confirmed when a bullish candle closes above the CISD high
                    if (swingPoint.CandleDirection == Direction.Up && swingPoint.Bar.Open < cisd.High &&
                        swingPoint.Bar.Close > cisd.High)
                    {
                        cisd.IsConfirmed = true;
                        // Set the confirming candle index
                        cisd.IndexOfConfirmingCandle = swingPoint.Index;

                        // Find and assign the breaker block
                        var breakerBlock = FindBreakerBlockForCisd(cisd);
                        if (breakerBlock != null)
                        {
                            // Only add the breaker if the low of the breaker is not higher than the CISD price
                            if (breakerBlock.Low <= cisd.High)
                            {
                                cisd.BreakerBlock = breakerBlock;
                                _breakerBlocks.Add(breakerBlock);

                                // Draw the breaker block if visualization is enabled
                                if (_showBreakerBlock)
                                {
                                    DrawBreakerBlock(breakerBlock);
                                }
                            }
                        }

                        // Draw a confirmation line
                        if (_chart != null && _showCISD)
                        {
                            string confirmId = $"cisd-confirm-{cisd.Direction}-{cisd.Index}-{swingPoint.Index}";
                            _chart.DrawStraightLine(
                                confirmId,
                                cisd.HighTime,
                                cisd.High,
                                swingPoint.Time,
                                cisd.High,
                                null,
                                LineStyle.Solid,
                                Color.Green,
                                false,
                                true,
                                false
                            );
                        }

                        // Send notification for CISD confirmation
                        _notificationService?.NotifyCisdConfirmation(Direction.Up);
                    }
                }
                else // Direction.Down (Bearish CISD)
                {
                    // Bearish CISD is confirmed when a bearish candle closes below the CISD low
                    if (swingPoint.CandleDirection == Direction.Down && swingPoint.Bar.Open > cisd.Low &&
                        swingPoint.Bar.Close < cisd.Low)
                    {
                        cisd.IsConfirmed = true;
                        // Set the confirming candle index
                        cisd.IndexOfConfirmingCandle = swingPoint.Index;

                        // Find and assign the breaker block
                        var breakerBlock = FindBreakerBlockForCisd(cisd);
                        if (breakerBlock != null)
                        {
                            // Only add the breaker if the high of the breaker is not lower than the CISD price
                            if (breakerBlock.High >= cisd.Low)
                            {
                                cisd.BreakerBlock = breakerBlock;
                                _breakerBlocks.Add(breakerBlock);

                                // Draw the breaker block if visualization is enabled
                                if (_showBreakerBlock)
                                {
                                    DrawBreakerBlock(breakerBlock);
                                }
                            }
                        }

                        // Draw a confirmation line
                        if (_chart != null && _showCISD)
                        {
                            string confirmId = $"cisd-confirm-{cisd.Direction}-{cisd.Index}-{swingPoint.Index}";
                            _chart.DrawStraightLine(
                                confirmId,
                                cisd.LowTime,
                                cisd.Low,
                                swingPoint.Time,
                                cisd.Low,
                                null,
                                LineStyle.Solid,
                                Color.Pink,
                                false,
                                true,
                                false
                            );
                        }

                        // Send notification for CISD confirmation
                        _notificationService?.NotifyCisdConfirmation(Direction.Down);
                    }
                }
            }
        }

        // Method to check for CISD activation on previous bar
        public void CheckCisdActivationOnBar(Bar previousBar, int barIndex)
        {
            if (previousBar == null)
                return;

            // Get all CISD levels that are confirmed but not activated
            var confirmedCisdLevels = _cisdLevels
                .Where(cisd => cisd.IsConfirmed && !cisd.Activated)
                .ToList();

            // Check for CISD activation on this bar
            foreach (var cisd in confirmedCisdLevels)
            {
                if (cisd.Direction == Direction.Up) // Bullish CISD
                {
                    // Bullish CISD is activated when price moves below the CISD level (high)
                    if (previousBar.Open > cisd.High && previousBar.Low < cisd.High)
                    {
                        cisd.Activated = true;

                        // Draw a CISD activation line
                        if (_chart != null && _showCISD)
                        {
                            string id = $"cisd-{cisd.Direction}-{cisd.Index}-{barIndex}";
                            double priceLevel = cisd.High;
                            DateTime startTime = cisd.HighTime;
                            Color cisdColor = Color.FromArgb(70, Color.Green);

                            _chart.DrawStraightLine(
                                id,
                                startTime,
                                priceLevel,
                                previousBar.OpenTime,
                                priceLevel,
                                null, // No label
                                LineStyle.Dots,
                                cisdColor,
                                false, // No label displayed
                                true, // Remove existing
                                false, // Not extended
                                true
                            );
                        }
                    }
                }
                else // Direction.Down (Bearish CISD)
                {
                    // Bearish CISD is activated when price moves above the CISD level (low)
                    if (previousBar.Open < cisd.Low && previousBar.High > cisd.Low)
                    {
                        cisd.Activated = true;

                        // Draw a CISD activation line
                        if (_chart != null && _showCISD)
                        {
                            string id = $"cisd-{cisd.Direction}-{cisd.Index}-{barIndex}";
                            double priceLevel = cisd.Low;
                            DateTime startTime = cisd.LowTime;
                            Color cisdColor = Color.FromArgb(50, Color.Pink);

                            _chart.DrawStraightLine(
                                id,
                                startTime,
                                priceLevel,
                                previousBar.OpenTime,
                                priceLevel,
                                null, // No label
                                LineStyle.Dots,
                                cisdColor,
                                false, // No label displayed
                                true, // Remove existing
                                false, // Not extended
                                true
                            );
                        }
                    }
                }
            }
        }

        // Manage max CISD count
        private void ManageMaxCisdCount(Direction direction)
        {
            // Get unconfirmed CISDs of the specified direction
            var unconfirmedCisds = _cisdLevels
                .Where(cisd => cisd.Direction == direction && !cisd.IsConfirmed)
                .OrderBy(cisd => cisd.Index) // Order by index to get the oldest first
                .ToList();

            // If we already have the maximum number, remove the oldest ones
            while (unconfirmedCisds.Count >= _maxCisdsPerDirection && unconfirmedCisds.Count > 0)
            {
                var oldestCisd = unconfirmedCisds.First();

                // Remove from collection
                _cisdLevels.Remove(oldestCisd);
                unconfirmedCisds.Remove(oldestCisd);
            }
        }

        private Level FindBreakerBlockForCisd(Level cisd)
        {
            if (cisd.Direction == Direction.Up) // Bullish CISD
            {
                // Find the previous bullish orderflow
                var previousBullishOrderflow = _pdArrays
                    .Where(p => p.Direction == Direction.Up && p.Index < cisd.Index)
                    .OrderByDescending(p => p.Index)
                    .FirstOrDefault();

                if (previousBullishOrderflow == null)
                    return null;

                // Find the last set of consecutive bullish candles in this orderflow
                var lastConsecutiveBullishCandles =
                    FindLastConsecutiveCandlesInOrderflow(previousBullishOrderflow, Direction.Up);

                if (lastConsecutiveBullishCandles.Count == 0)
                    return null;

                // Get indices of the first and last bullish candles
                int firstBullishCandleIndex = lastConsecutiveBullishCandles.First();
                int lastBullishCandleIndex = lastConsecutiveBullishCandles.Last();

                // Get the actual candles
                Bar firstBullishCandle = Bars[firstBullishCandleIndex];
                Bar lastBullishCandle = Bars[lastBullishCandleIndex];

                // Create a bullish breaker block
                return new Level(
                    LevelType.BreakerBlock,
                    firstBullishCandle.Low, // Low of first bullish candle
                    lastBullishCandle.High, // High of last bullish candle
                    firstBullishCandle.OpenTime, // Time of first bullish candle
                    lastBullishCandle.OpenTime, // Time of last bullish candle
                    null, // No mid time
                    Direction.Up, // Bullish direction
                    firstBullishCandleIndex, // Index of first bullish candle
                    lastBullishCandleIndex, // Index of last bullish candle
                    firstBullishCandleIndex // Index of low (same as first candle)
                );
            }
            else // Direction.Down (Bearish CISD)
            {
                // Find the previous bearish orderflow
                var previousBearishOrderflow = _pdArrays
                    .Where(p => p.Direction == Direction.Down && p.Index < cisd.Index)
                    .OrderByDescending(p => p.Index)
                    .FirstOrDefault();

                if (previousBearishOrderflow == null)
                    return null;

                // Find the last set of consecutive bearish candles in this orderflow
                var lastConsecutiveBearishCandles =
                    FindLastConsecutiveCandlesInOrderflow(previousBearishOrderflow, Direction.Down);

                if (lastConsecutiveBearishCandles.Count == 0)
                    return null;

                // Get indices of the first and last bearish candles
                int firstBearishCandleIndex = lastConsecutiveBearishCandles.First();
                int lastBearishCandleIndex = lastConsecutiveBearishCandles.Last();

                // Get the actual candles
                Bar firstBearishCandle = Bars[firstBearishCandleIndex];
                Bar lastBearishCandle = Bars[lastBearishCandleIndex];

                // Create a bearish breaker block
                return new Level(
                    LevelType.BreakerBlock,
                    lastBearishCandle.Low, // Low of last bearish candle
                    firstBearishCandle.High, // High of first bearish candle
                    lastBearishCandle.OpenTime, // Time of last bearish candle
                    firstBearishCandle.OpenTime, // Time of first bearish candle
                    null, // No mid time
                    Direction.Down, // Bearish direction
                    firstBearishCandleIndex, // Index of first bearish candle
                    firstBearishCandleIndex, // Index of high (same as first candle)
                    lastBearishCandleIndex // Index of low (same as last candle)
                );
            }
        }

        private List<int> FindLastConsecutiveCandlesInOrderflow(Level orderflow, Direction direction)
        {
            // Define search range based on direction of the orderflow
            int startIndex = Math.Min(orderflow.IndexLow, orderflow.IndexHigh);
            int endIndex = Math.Max(orderflow.IndexLow, orderflow.IndexHigh);

            // Ensure we have valid indices
            if (startIndex < 0 || endIndex < 0 || startIndex >= Bars.Count || endIndex >= Bars.Count)
                return new List<int>();

            List<int> lastConsecutiveCandles = new List<int>();
            bool foundFirstMatchingCandle = false;

            // Start from the end and work backward
            for (int i = endIndex; i >= startIndex; i--)
            {
                var bar = Bars[i];
                var barDirection = bar.GetCandleDirection();

                if (barDirection == direction)
                {
                    // Add to our consecutive candles collection
                    lastConsecutiveCandles.Insert(0, i); // Insert at beginning to maintain correct order
                    foundFirstMatchingCandle = true; // We've found at least one matching candle
                }
                else
                {
                    // If we haven't found any matching candles yet, continue searching
                    if (!foundFirstMatchingCandle)
                        continue;

                    // Once we hit a candle of the opposite direction AFTER finding matching candles, we break
                    break;
                }
            }

            return lastConsecutiveCandles;
        }

        private void DrawBreakerBlock(Level breakerBlock)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this breaker block
            string id =
                $"breaker-{breakerBlock.Direction}-{breakerBlock.Index}-{breakerBlock.IndexHigh}-{breakerBlock.IndexLow}";

            // Draw rectangle with appropriate styling
            _chart.DrawRectangle(
                breakerBlock,
                id,
                true, // Draw midpoint
                20 // Higher opacity for better visibility
            );
        }

        #endregion

        #region Rejection Blocks

        private void CheckForRejectionBlock(SwingPoint swingPoint)
        {
            if (swingPoint == null || swingPoint.Bar == null)
                return;

            var candle = swingPoint.Bar;
            bool isBullishCandle = candle.Close > candle.Open;
            double bodySize = Math.Abs(candle.Close - candle.Open);

            // Skip if body size is very small (to avoid division by zero or tiny bodies)
            if (bodySize < 0.0001)
                return;

            // For Bullish Swing Points (creating Bearish Rejection Blocks)
            if (swingPoint.Direction == Direction.Up)
            {
                // Calculate upper wick size - always use only the upper wick for bullish swing points
                double upperWick;
                double lowerBoundary;

                if (isBullishCandle)
                {
                    // For bullish candles: upper wick = High - Close
                    upperWick = candle.High - candle.Close;
                    lowerBoundary = candle.Close;
                }
                else
                {
                    // For bearish candles: upper wick = High - Open
                    upperWick = candle.High - candle.Open;
                    lowerBoundary = candle.Open;
                }

                // Check if upper wick is significantly larger than body
                if (upperWick > bodySize * 1.5) // Using 1.5x as threshold for significant rejection
                {
                    // Create a bearish rejection block
                    var rejectionBlock = new Level(
                        LevelType.RejectionBlock,
                        lowerBoundary, // Lower boundary is where the wick starts
                        candle.High, // Upper boundary is high
                        candle.Time,
                        candle.Time.AddMinutes(5), // 5 minute duration for visualization
                        candle.Time,
                        Direction.Down, // Bearish direction
                        swingPoint.Index,
                        swingPoint.Index,
                        swingPoint.Index,
                        swingPoint.Index,
                        Zone.Premium // Typically in premium zone
                    );

                    // Initialize quadrants for the rejection block
                    rejectionBlock.InitializeQuadrants();

                    // Add to collection if not already present
                    if (!_rejectionBlocks.Any(rb =>
                            rb.Index == rejectionBlock.Index &&
                            rb.Direction == rejectionBlock.Direction))
                    {
                        _rejectionBlocks.Add(rejectionBlock);

                        // Draw if enabled
                        if (_showRejectionBlock)
                        {
                            DrawRejectionBlock(rejectionBlock);
                        }
                    }
                }
            }
            // For Bearish Swing Points (creating Bullish Rejection Blocks)
            else if (swingPoint.Direction == Direction.Down)
            {
                // Calculate lower wick size - always use only the lower wick for bearish swing points
                double lowerWick;
                double upperBoundary;

                if (isBullishCandle)
                {
                    // For bullish candles: lower wick = Open - Low
                    lowerWick = candle.Open - candle.Low;
                    upperBoundary = candle.Open;
                }
                else
                {
                    // For bearish candles: lower wick = Close - Low
                    lowerWick = candle.Close - candle.Low;
                    upperBoundary = candle.Close;
                }

                // Check if lower wick is significantly larger than body
                if (lowerWick > bodySize * 1.5) // Using 1.5x as threshold for significant rejection
                {
                    // Create a bullish rejection block
                    var rejectionBlock = new Level(
                        LevelType.RejectionBlock,
                        candle.Low, // Lower boundary is low
                        upperBoundary, // Upper boundary is where the wick starts
                        candle.Time,
                        candle.Time.AddMinutes(5), // 5 minute duration for visualization
                        candle.Time,
                        Direction.Up, // Bullish direction
                        swingPoint.Index,
                        swingPoint.Index,
                        swingPoint.Index,
                        swingPoint.Index,
                        Zone.Discount // Typically in discount zone
                    );

                    // Initialize quadrants for the rejection block
                    rejectionBlock.InitializeQuadrants();

                    // Add to collection if not already present
                    if (!_rejectionBlocks.Any(rb =>
                            rb.Index == rejectionBlock.Index &&
                            rb.Direction == rejectionBlock.Direction))
                    {
                        _rejectionBlocks.Add(rejectionBlock);

                        // Draw if enabled
                        if (_showRejectionBlock)
                        {
                            DrawRejectionBlock(rejectionBlock);
                        }
                    }
                }
            }
        }

// Step 4: Method to draw rejection blocks
        private void DrawRejectionBlock(Level rejectionBlock)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this rejection block
            string id = $"rb-{rejectionBlock.Direction}-{rejectionBlock.Index}";

            // Set color based on direction
            Color color = rejectionBlock.Direction == Direction.Up
                ? Color.FromArgb(10, Color.Green)
                : Color.FromArgb(10, Color.Red);

            // Draw rectangle with distinctive style
            var rectangle = _chart.DrawRectangle(
                id,
                rejectionBlock.LowTime,
                rejectionBlock.Low,
                rejectionBlock.HighTime,
                rejectionBlock.High,
                color);

            rectangle.IsFilled = true;

            // Draw dotted line for midpoint
            string midLineId = $"{id}-midline";
            _chart.DrawTrendLine(
                midLineId,
                rejectionBlock.LowTime,
                rejectionBlock.Mid,
                rejectionBlock.HighTime,
                rejectionBlock.Mid,
                Color.FromArgb(70, Color.White),
                1,
                LineStyle.Dots);
        }

        public List<Level> GetAllRejectionBlocks()
        {
            return _rejectionBlocks;
        }

        public List<Level> GetRejectionBlocks(Direction direction)
        {
            return _rejectionBlocks.Where(rb => rb.Direction == direction).ToList();
        }

        public Level GetLastRejectionBlock(Direction direction)
        {
            return _rejectionBlocks
                .Where(rb => rb.Direction == direction)
                .OrderByDescending(rb => rb.Index)
                .FirstOrDefault();
        }

        private void RemoveRejectionBlockAtIndex(int index)
        {
            // Find any rejection blocks at this index
            var rejectionBlock = _rejectionBlocks.FirstOrDefault(rb => rb.Index == index);

            // If we found a rejection block, remove it
            if (rejectionBlock != null)
            {
                _rejectionBlocks.Remove(rejectionBlock);

                // Clean up visualization
                if (_chart != null)
                {
                    // Remove the rejection block rectangle and midline
                    if (_showRejectionBlock)
                    {
                        string id = $"rb-{rejectionBlock.Direction}-{rejectionBlock.Index}";
                        _chart.RemoveObject(id);
                        _chart.RemoveObject($"{id}-midline");
                    }

                    // Remove all quadrant lines for this rejection block
                    if (_showQuadrants)
                    {
                        // Quadrant percentages: 0, 25, 50, 75, 100
                        int[] percentages = { 0, 25, 50, 75, 100 };

                        foreach (int percent in percentages)
                        {
                            string quadId = $"quad-{rejectionBlock.Direction}-{rejectionBlock.Index}-{percent}";
                            _chart.RemoveObject(quadId);
                        }
                    }
                }
            }
        }

        #endregion

        #region Unicorn Detection

        // Add a new method to check for Unicorns when a new FVG is detected
        public void CheckForUnicorns(Level fvg)
        {
            // Skip if we don't have confirmed CISDs yet
            if (fvg == null)
                return;

            // Get all confirmed CISDs that have breaker blocks
            var confirmedCisdsWithBreakers = _cisdLevels
                .Where(cisd => cisd.IsConfirmed && !cisd.Activated && cisd.BreakerBlock != null)
                .ToList();

            foreach (var cisd in confirmedCisdsWithBreakers)
            {
                // Check if one of the FVG candles is the CISD confirming candle
                bool fvgInvolvesCisdCandle = false;
                bool passesDirectionalRequirement = false;

                if (fvg.Direction == Direction.Up) // Bullish FVG
                {
                    // For bullish FVG, check if any of its candles match the CISD confirming candle
                    fvgInvolvesCisdCandle =
                        fvg.Index == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexMid == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexHigh == cisd.IndexOfConfirmingCandle;

                    passesDirectionalRequirement = fvg.Low < cisd.High;
                }
                else // Direction.Down (Bearish FVG)
                {
                    // For bearish FVG, check if any of its candles match the CISD confirming candle
                    fvgInvolvesCisdCandle =
                        fvg.Index == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexMid == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexLow == cisd.IndexOfConfirmingCandle;

                    passesDirectionalRequirement = fvg.High > cisd.Low;
                }

                // If the FVG involves the CISD confirming candle, check for intersection with breaker block
                if (!fvgInvolvesCisdCandle || !passesDirectionalRequirement) continue;

                bool intersectsWithBreaker = CheckIntersection(fvg, cisd.BreakerBlock);

                if (intersectsWithBreaker)
                {
                    // This FVG is a Unicorn - mark it
                    fvg.LevelType = LevelType.Unicorn;

                    // Add to unicorns collection
                    _unicorns.Add(fvg);

                    // Draw the unicorn if visualization is enabled
                    if (_showUnicorn)
                    {
                        DrawUnicorn(fvg);
                    }

                    // Only need to find one match
                    break;
                }
            }
        }

        // Helper method to check if two levels intersect
        private bool CheckIntersection(Level level1, Level level2)
        {
            // Check for price range intersection
            bool priceIntersects = !(level1.High < level2.Low || level1.Low > level2.High);

            // Both price and time must intersect
            return priceIntersects;
        }

        // Method to draw a Unicorn visualization
        private void DrawUnicorn(Level unicorn)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this Unicorn
            string id = $"unicorn-{unicorn.Direction}-{unicorn.Index}-{unicorn.IndexHigh}-{unicorn.IndexLow}";

            // Draw with a distinctive style - red solid line as specified in LineType.Unicorn style
            Color unicornColor = unicorn.Direction == Direction.Up ? Color.Green : Color.Pink;

            // Draw a distinctive pattern - rectangle with higher opacity
            var rectangle = _chart.DrawRectangle(
                id,
                unicorn.LowTime,
                unicorn.Low,
                unicorn.HighTime,
                unicorn.High,
                unicornColor);

            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(50, unicornColor); // Higher opacity for Unicorns

            // Draw solid midline to make it stand out
            string midLineId = $"{id}-midline";
            _chart.DrawTrendLine(
                midLineId,
                unicorn.LowTime,
                unicorn.Mid,
                unicorn.HighTime,
                unicorn.Mid,
                unicornColor,
                2, // Thicker line
                LineStyle.Dots); // Solid line for Unicorns
        }

        #endregion

        #region SMT

        // Add to PdArrayAnalyzer.cs
        private void DrawSmtDivergence(SwingPoint sweptPoint, SwingPoint sweepingPoint)
        {
            if (_chart == null || sweptPoint == null || sweepingPoint == null)
                return;

            // Create a unique ID for this SMT divergence line
            string id = $"smt-{sweptPoint.Time.Ticks}-{sweepingPoint.Time.Ticks}";

            // Draw a dotted trendline connecting the swept point to the sweeping point
            _chart.DrawStraightLine(
                id,
                sweptPoint.Time,
                sweptPoint.Price,
                sweepingPoint.Time,
                sweepingPoint.Price,
                null, // Label it as SMT
                LineStyle.Dots,
                Color.FromArgb(60, Color.Yellow), // Use yellow color for visibility
                false, // Show label
                true, // Remove existing
                false // Not extended
            );

            // Draw an icon on the swing point with SMT divergence
            _chart.DrawIcon($"smt-icon-{sweepingPoint.Time.Ticks}", ChartIconType.Star,
                sweepingPoint.Time, sweepingPoint.Price, Color.FromArgb(60, Color.Yellow));
        }

        /// <summary>
        /// Gets the price of the pair instrument at the given index
        /// </summary>
        /// <param name="index">The index to get the pair price for</param>
        /// <returns>The price of the pair instrument, or 0 if not available</returns>
        private double GetPairPriceAtIndex(int index, Direction direction)
        {
            try
            {
                // Skip if SMT is disabled or no pair is specified
                if (!_showSMT || string.IsNullOrEmpty(_smtPair))
                    return 0;

                // Check if we have a data provider
                if (PairDataProvider == null)
                {
                    _logger?.Invoke($"No pair data provider available for {_smtPair}");
                    return 0;
                }

                // Use the data provider to get the pair price
                var time = Bars[index].OpenTime;
                double price = PairDataProvider(_smtPair, time, index, direction);

                // Log the retrieved price for debugging
                if (price > 0)
                {
                    // do nothing
                }
                else
                {
                    _logger?.Invoke($"WARNING: Got zero pair price for {_smtPair} at index {index} for {direction}");
                }

                return price;
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error getting pair price for {_smtPair} at index {index}: {ex.Message}");
                return 0;
            }
        }

        // Add getter method for SMT divergence points
        public List<SwingPoint> GetSMTDivergencePoints()
        {
            return _swingPointDetector?.GetAllSwingPoints()
                .Where(sp => sp.HasSMT)
                .ToList() ?? new List<SwingPoint>();
        }

        #endregion

        #region Helpers for all patterns

        /// <summary>
        /// Initializes quadrants for a newly created PD Array
        /// </summary>
        private void InitializeQuadrants(Level pdArray)
        {
            // Initialize the five quadrant points (0%, 25%, 50%, 75%, 100%)
            pdArray.InitializeQuadrants();

            // Draw quadrant levels if visualization is enabled
            if (_showOrderFlow && _showQuadrants)
            {
                DrawQuadrantLevels(pdArray);
            }
        }

        /// <summary>
        /// Draws horizontal lines for each quadrant level of a PD Array
        /// </summary>
        private void DrawQuadrantLevels(Level pdArray)
        {
            if (_chart == null)
                return;

            DateTime startTime = pdArray.Direction == Direction.Up ? pdArray.LowTime : pdArray.HighTime;
            DateTime endTime = pdArray.Direction == Direction.Up ? pdArray.HighTime : pdArray.LowTime;

            // Use pink for all unswept quadrants as requested
            Color unsweptColor = Color.Pink;
            Color sweptColor = Color.Gray;

            // Use different line styles for each quadrant
            LineStyle[] styles = new LineStyle[]
            {
                LineStyle.Solid, // 0%
                LineStyle.Dots, // 25%
                LineStyle.Solid, // 50% (mid)
                LineStyle.Dots, // 75%
                LineStyle.Solid // 100%
            };

            // Draw each quadrant line
            for (int i = 0; i < pdArray.Quadrants.Count; i++)
            {
                var quadrant = pdArray.Quadrants[i];

                // Create a unique ID for this quadrant line
                string id = $"quad-{pdArray.Direction}-{pdArray.Index}-{quadrant.Percent}";

                // Draw the line with appropriate style and color (pink for unswept, gray for swept)
                _chart.DrawStraightLine(
                    id,
                    startTime,
                    quadrant.Price,
                    endTime,
                    quadrant.Price,
                    null,
                    styles[i],
                    quadrant.IsSwept ? sweptColor : unsweptColor,
                    removeExisting: true
                );
            }
        }

        /// <summary>
        /// Updates the visualization when a quadrant is swept
        /// </summary>
        private void UpdateQuadrantVisualization(Level pdArray, Quadrant quadrant)
        {
            if (_chart == null)
                return;

            // Create a unique ID for this quadrant line
            string id = $"quad-{pdArray.Direction}-{pdArray.Index}-{quadrant.Percent}";

            // Remove the original line
            _chart.RemoveObject(id);

            // Calculate start and end times
            DateTime startTime = pdArray.Direction == Direction.Up ? pdArray.LowTime : pdArray.HighTime;
            DateTime endTime = pdArray.Direction == Direction.Up ? pdArray.HighTime : pdArray.LowTime;

            // Determine line style based on percentage
            LineStyle style = (quadrant.Percent % 50 == 0) ? LineStyle.Solid : LineStyle.Dots;

            // Draw a gray line to indicate it's swept
            _chart.DrawStraightLine(
                id,
                startTime,
                quadrant.Price,
                endTime,
                quadrant.Price,
                null,
                style,
                Color.Gray,
                removeExisting: true
            );
        }

        /// <summary>
        /// Checks if a swing point swept any quadrants in opposite-direction PD Arrays
        /// </summary>
        /// <summary>
        /// Checks if a swing point swept any quadrants in opposite-direction PD Arrays
        /// </summary>
        private void CheckQuadrantsOnSwingPoint(SwingPoint swingPoint)
        {
            // Skip if swing point is null
            if (swingPoint == null)
                return;

            // Process only PD Arrays with the OPPOSITE direction of the swing point
            Direction pdArrayDirection = swingPoint.Direction == Direction.Up ? Direction.Down : Direction.Up;

            // Get all relevant PD Array types
            var eligiblePdArrays = new List<Level>();
            eligiblePdArrays.AddRange(_orderBlocks.Where(l =>
                l.Direction == pdArrayDirection && l.IsActive && l.Index != swingPoint.Index));
            eligiblePdArrays.AddRange(_rejectionBlocks.Where(l =>
                l.Direction == pdArrayDirection && l.IsActive && l.Index != swingPoint.Index));

            // Flag to track if this swing point actually swept any quadrants
            bool sweptAnyQuadrants = false;
            bool shouldMarkAsInside = false; // Flag to control marking as inside
            Level sweptLevel = null;

            foreach (var pdArray in eligiblePdArrays)
            {
                // Check if any quadrants were swept by this swing point
                var sweptQuadrants = pdArray.CheckForSweptQuadrants(swingPoint);

                // If quadrants were swept by this specific swing point
                if (sweptQuadrants.Count > 0)
                {
                    sweptAnyQuadrants = true;
                    sweptLevel = pdArray; // Store the PD array that was swept

                    // Find highest/lowest quadrant in the PD array
                    double highestQuadrantPrice = pdArray.Quadrants.Max(q => q.Price);
                    double lowestQuadrantPrice = pdArray.Quadrants.Min(q => q.Price);

                    // For bullish swing points interacting with bearish quadrants
                    if (swingPoint.Direction == Direction.Up && pdArray.Direction == Direction.Down)
                    {
                        // Mark as inside if open is BELOW the highest quadrant but high is ABOVE the lowest quadrant
                        shouldMarkAsInside = swingPoint.Bar.Open < highestQuadrantPrice &&
                                             swingPoint.Bar.High > lowestQuadrantPrice;
                    }
                    // For bearish swing points interacting with bullish quadrants
                    else if (swingPoint.Direction == Direction.Down && pdArray.Direction == Direction.Up)
                    {
                        // Mark as inside if open is ABOVE the lowest quadrant but low is BELOW the highest quadrant
                        shouldMarkAsInside = swingPoint.Bar.Open > lowestQuadrantPrice &&
                                             swingPoint.Bar.Low < highestQuadrantPrice;
                    }

                    // Update visualization for swept quadrants
                    if (_showQuadrants)
                    {
                        foreach (var quadrant in sweptQuadrants)
                        {
                            UpdateQuadrantVisualization(pdArray, quadrant);
                        }
                    }

                    // Draw a rectangle from the PD array to this swing point if we're marking as inside
                    if (shouldMarkAsInside && _showInsideKeyLevel)
                    {
                        _chart.DrawLineFromLevelToPoint(pdArray, swingPoint);
                    }
                }
            }

            // Only mark the swing point as inside a key level if it swept quadrants
            // AND passes our inside key level condition
            if (sweptAnyQuadrants && shouldMarkAsInside)
            {
                swingPoint.InsideKeyLevel = true;
                swingPoint.SweptKeyLevel = sweptLevel;
            }
        }

        #endregion

        #region Getters for all pattern types

        // Getter methods for Unicorns
        public List<Level> GetAllUnicorns()
        {
            return _unicorns;
        }

        public List<Level> GetUnicorns(Direction direction)
        {
            return _unicorns.Where(u => u.Direction == direction).ToList();
        }

        public Level GetLastUnicorn(Direction direction)
        {
            return _unicorns
                .Where(u => u.Direction == direction)
                .OrderByDescending(u => u.Index)
                .FirstOrDefault();
        }

        // CISD getters
        public List<Level> GetAllCISDLevels()
        {
            return _cisdLevels;
        }

        public List<Level> GetActiveCISDLevels()
        {
            return _cisdLevels.Where(cisd => cisd.Activated).ToList();
        }

        public List<Level> GetConfirmedCISDLevels()
        {
            return _cisdLevels.Where(cisd => cisd.IsConfirmed).ToList();
        }

        // Order flow getters
        public List<Level> GetPdArrays()
        {
            return _pdArrays;
        }

        public List<Level> GetBullishPdArrays()
        {
            return _pdArrays.Where(l => l.Direction == Direction.Up).ToList();
        }

        public List<Level> GetBearishPdArrays()
        {
            return _pdArrays.Where(l => l.Direction == Direction.Down).ToList();
        }

        public Level GetLastBullishPdArray()
        {
            return _pdArrays.Where(l => l.Direction == Direction.Up)
                .OrderByDescending(l => l.Index)
                .FirstOrDefault();
        }

        public Level GetLastBearishPdArray()
        {
            return _pdArrays.Where(l => l.Direction == Direction.Down)
                .OrderByDescending(l => l.Index)
                .FirstOrDefault();
        }

        // Liquidity sweep getters
        public List<Level> GetLiquiditySweepLevels()
        {
            return _pdArrays.Where(l => l.SweptSwingPoint != null).ToList();
        }

        // Gauntlet getters
        public List<Level> GetGauntlets()
        {
            return _gauntlets;
        }

        public List<Level> GetGauntlets(Direction direction)
        {
            return _gauntlets.Where(g => g.Direction == direction).ToList();
        }

        // Breaker Block getters
        public List<Level> GetAllBreakerBlocks()
        {
            return _breakerBlocks;
        }

        public List<Level> GetBullishBreakerBlocks()
        {
            return _breakerBlocks.Where(b => b.Direction == Direction.Up).ToList();
        }

        public List<Level> GetBearishBreakerBlocks()
        {
            return _breakerBlocks.Where(b => b.Direction == Direction.Down).ToList();
        }

        // Add a method to get active PD Arrays
        public List<Level> GetActivePdArrays()
        {
            return _pdArrays.Where(l => l.IsActive).ToList();
        }

// Add a method to get active PD Arrays by direction
        public List<Level> GetActivePdArrays(Direction direction)
        {
            return _pdArrays.Where(l => l.IsActive && l.Direction == direction).ToList();
        }

        // Initialize with existing swing points
        public void Initialize(List<SwingPoint> swingPoints)
        {
            if (swingPoints == null || swingPoints.Count < 3) // Need at least 3 points to form an orderflow
                return;

            // Clear existing history
            _swingPointHistory.Clear();

            // Add all swing points to our history
            _swingPointHistory.AddRange(swingPoints);

            // Sort by index to ensure chronological order
            _swingPointHistory.Sort((a, b) => a.Index.CompareTo(b.Index));

            // Process each swing point in sequence
            for (int i = 2; i < _swingPointHistory.Count; i++)
            {
                var currentPoint = _swingPointHistory[i];

                if (currentPoint.Direction == Direction.Down)
                {
                    ProcessNewSwingLow(currentPoint);
                }
                else if (currentPoint.Direction == Direction.Up)
                {
                    ProcessNewSwingHigh(currentPoint);
                }

                // NEW: Check if this swing point sweeps any quadrants
                CheckQuadrantsOnSwingPoint(currentPoint);
            }
        }

        #endregion
    }
}