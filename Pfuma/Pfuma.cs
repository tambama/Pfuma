using cAlgo.API;
using System.Collections.Generic;
using System;
using System.Linq;
using cAlgo.API.Internals;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class Pfuma : Indicator
    {
        [Parameter("Swing Points", DefaultValue = true)]
        public bool ShowSwingPoints { get; set; }

        [Parameter("HTF Swing Points", DefaultValue = false)]
        public bool ShowHtfSwingPoints { get; set; }

        [Parameter("HTF", DefaultValue = "H1")]
        public string HTF { get; set; }

        [Parameter("UTC Offset", Group = "Time Management", DefaultValue = -4)]
        public int UtcOffset { get; set; }

        [Parameter("Macro Times", Group = "Time Management", DefaultValue = true)]
        public bool ShowMacros { get; set; }
        [Parameter("Macro Filter", Group = "Time Management", DefaultValue = false)]
        public bool MacroFilter { get; set; }

        [Parameter("Market Structure", Group = "Market Structure", DefaultValue = true)]
        public bool ShowMarketStructure { get; set; }

        [Parameter("Structure", Group = "Market Structure", DefaultValue = true)]
        public bool ShowStructure { get; set; }

        [Parameter("CHOCH", Group = "Market Structure", DefaultValue = true)]
        public bool ShowChoch { get; set; }

        [Parameter("CISD", Group = "Market Structure", DefaultValue = false)]
        public bool ShowCISD { get; set; }

        [Parameter("Max CISD", Group = "Market Structure", DefaultValue = 2)]
        public int MaxCisdsPerDirection { get; set; }

        [Parameter("Order Flow", Group = "PD Arrays", DefaultValue = false)]
        public bool ShowOrderFlow { get; set; }

        [Parameter("Fair Value Gaps", Group = "PD Arrays", DefaultValue = true)]
        public bool ShowFVG { get; set; }

        [Parameter("Order Blocks", Group = "PD Arrays", DefaultValue = true)]
        public bool ShowOrderBlock { get; set; }

        [Parameter("Rejection Blocks", Group = "PD Arrays", DefaultValue = false)]
        public bool ShowRejectionBlock { get; set; }
        [Parameter("Breaker Blocks", Group = "PD Arrays", DefaultValue = false)]
        public bool ShowBreakerBlock { get; set; }

        [Parameter("Unicorn", Group = "PD Arrays", DefaultValue = true)]
        public bool ShowUnicorn { get; set; }

        [Parameter("Gauntlets", Group = "PD Arrays", DefaultValue = false)]
        public bool ShowGauntlet { get; set; }

        [Parameter("Quadrants", Group = "PD Arrays", DefaultValue = false)]
        public bool ShowQuadrants { get; set; }

        [Parameter("Inside Key Level", Group = "PD Arrays", DefaultValue = false)]
        public bool ShowInsideKeyLevel { get; set; }

        [Parameter("Liquidity Sweeps", Group = "Liquidity", DefaultValue = true)]
        public bool ShowLiquiditySweep { get; set; }

        [Parameter("STDV", Group = "Liquidity", DefaultValue = true)]
        public bool ShowStdv { get; set; }

        [Parameter("Session Fib", Group = "Liquidity", DefaultValue = false)]
        public bool ShowFibonacciLevels { get; set; }

        [Parameter("SMT", Group = "SMT", DefaultValue = false)]
        public bool ShowSMT { get; set; }

        [Parameter("Pair", Group = "SMT", DefaultValue = "")]
        public string SMTPair { get; set; }
        [Parameter("Enable Log", Group = "Notifications", DefaultValue = false)]
        public bool EnableLog { get; set; }
        
        [Parameter("Enable Telegram", Group = "Notifications", DefaultValue = false)]
        public bool EnableTelegram { get; set; }

        [Output("Swing High", Color = Colors.White, PlotType = PlotType.Points, Thickness = 1)]
        public IndicatorDataSeries SwingHighs { get; set; }

        [Output("Swing Low", Color = Colors.White, PlotType = PlotType.Points, Thickness = 1)]
        public IndicatorDataSeries SwingLows { get; set; }

        [Output("HTF Swing High", Color = Colors.Green, PlotType = PlotType.Points, Thickness = 8)]
        public IndicatorDataSeries HtfSwingHighs { get; set; }

        [Output("HTF Swing Low", Color = Colors.Red, PlotType = PlotType.Points, Thickness = 8)]
        public IndicatorDataSeries HtfSwingLows { get; set; }

        // Market Structure Series
        [Output("Higher High", Color = Colors.Pink, PlotType = PlotType.Points, Thickness = 12)]
        public IndicatorDataSeries HigherHighs { get; set; }

        [Output("Lower High", Color = Colors.Pink, PlotType = PlotType.Points, Thickness = 12)]
        public IndicatorDataSeries LowerHighs { get; set; }

        [Output("Higher Low", Color = Colors.Pink, PlotType = PlotType.Points, Thickness = 12)]
        public IndicatorDataSeries HigherLows { get; set; }

        [Output("Lower Low", Color = Colors.Pink, PlotType = PlotType.Points, Thickness = 12)]
        public IndicatorDataSeries LowerLows { get; set; }

        private SwingPointDetector _swingDetector;
        private List<SwingPoint> _swingPoints;
        private SwingPointDetector _htfSwingDetector;
        private List<SwingPoint> _htfSwingPoints;
        private readonly List<StandardDeviation> _standardDeviations = new List<StandardDeviation>();

        private TimeFrame _highTimeFrame;

        // Keep track of processed HTF bars to avoid duplicate processing
        private readonly Dictionary<DateTime, bool> _processedHtfBars = new Dictionary<DateTime, bool>();

        private Bar _currentBar;
        private int _currentBarIndex;

        private Bar _previousBar;
        private int _previousBarIndex;

        // SMT
        private Symbol _pairSymbol;
        private Bars _pairBars;

        // Market structure analyzer
        private MarketStructureAnalyzer _marketStructureAnalyzer;

        // PD Array analyzer
        private PdArrayAnalyzer _pdArrayAnalyzer;

        // Time Manager
        private TimeManager _timeManager;
        
        // Notification Service
        private NotificationService _notificationService;
        private const string TelegramChatId = "5631623580";
        private const string TelegramToken = "7507336625:AAHM4oYlg_5XIjzzCNFCR_oyLu1Y69qkvns";

        // Flag to track if we have enough data for market structure analysis
        private bool _marketStructureInitialized = false;

        // Flag to track if PD Array analyzer is initialized
        private bool _pdArrayAnalyzerInitialized = false;

        protected override void Initialize()
        {
            // Delete all obk=jects from Chart
            Chart.RemoveAllObjects();
            
            // Initialize the notification service
            _notificationService = new NotificationService(
                EnableLog, 
                EnableTelegram, 
                TelegramChatId, 
                TelegramToken,
                Symbol.Name,
                UtcOffset,
                message => Print(message));

            // Initialize the swing detector
            _swingPoints = new List<SwingPoint>();
            _htfSwingPoints = new List<SwingPoint>();

            _swingDetector = new SwingPointDetector(SwingHighs, SwingLows);
            _htfSwingDetector = new SwingPointDetector(HtfSwingHighs, HtfSwingLows);

            // Wire up the SwingPointRemoved event
            _swingDetector.SwingPointRemoved += OnSwingPointRemoved;
            _htfSwingDetector.SwingPointRemoved += OnSwingPointRemoved;

            _highTimeFrame = HTF.GetTimeFrameFromString();

            try
            {
                _timeManager = new TimeManager(
                    Chart,
                    Bars,
                    _swingDetector,
                    _notificationService,
                    ShowMacros,
                    ShowFibonacciLevels,
                    UtcOffset);
            }
            catch (Exception ex)
            {
                Print("Error initializing Time Manager: " + ex.Message);
                // Disable to prevent further errors
                ShowMacros = false;
            }

            // Initialize PD Array analyzer with integrated FVG detection
            try
            {
                _pdArrayAnalyzer = new PdArrayAnalyzer(
                    Chart,
                    Bars,
                    ShowOrderFlow,
                    ShowLiquiditySweep,
                    ShowGauntlet,
                    ShowFVG, // Pass ShowFVG directly
                    ShowOrderBlock, // Pass ShowOrderBlock directly
                    ShowCISD,
                    ShowBreakerBlock,
                    ShowUnicorn,
                    ShowQuadrants,
                    ShowInsideKeyLevel,
                    ShowRejectionBlock,
                    MaxCisdsPerDirection,
                    _swingDetector, // Pass swing detector for order block detection
                    ShowSMT,
                    SMTPair,
                    MacroFilter,
                    _notificationService,
                    _timeManager,
                    message => Print(message));
            }
            catch (Exception ex)
            {
                Print("Error initializing PD Array Analyzer: " + ex.Message);
                // Disable to prevent further errors
                ShowOrderFlow = false;
                ShowLiquiditySweep = false;
                ShowGauntlet = false;
                ShowFVG = false;
                ShowOrderBlock = false;
            }

            if (ShowSMT && !string.IsNullOrEmpty(SMTPair))
            {
                InitializePairSymbol();

                // Then set the data provider - make sure we use the correct method
                _pdArrayAnalyzer.PairDataProvider = GetPairPrice;
            }

            // Initialize market structure analyzer if enabled
            try
            {
                _marketStructureAnalyzer = new MarketStructureAnalyzer(
                    Chart,
                    SwingHighs,
                    SwingLows,
                    HigherHighs,
                    LowerHighs,
                    LowerLows,
                    HigherLows,
                    ShowStructure,
                    ShowChoch,
                    ShowStdv,
                    _standardDeviations,
                    message => Print(message) // Pass Print method as logger
                );
            }
            catch (Exception ex)
            {
                Print("Error initializing Market Structure Analyzer: " + ex.Message);
                // Disable to prevent further errors
                ShowMarketStructure = false;
            }
        }

        public override void Calculate(int index)
        {
            // Need at least 2 bars to calculate
            if (index <= 1)
                return;

            _currentBar = Bars[index];
            _currentBarIndex = index;
            _previousBar = Bars[index - 1];
            _previousBarIndex = index - 1;

            // Process for macro time periods
            if (_timeManager != null)
            {
                try
                {
                    _timeManager.ProcessBar(index, _currentBar.OpenTime);
                }
                catch (Exception ex)
                {
                    Print("Error in macro time processing: " + ex.Message);
                }
            }

            // Check for CISD activation on previous bar
            if (_pdArrayAnalyzer != null && index > 1)
            {
                try
                {
                    _pdArrayAnalyzer.CheckCisdActivationOnBar(_previousBar, _previousBarIndex);
                }
                catch (Exception ex)
                {
                    Print("Error in CISD activation check: " + ex.Message);
                }
            }

            // Process for FVG and Order Block detection (now using PdArrayAnalyzer)
            if (_pdArrayAnalyzer != null)
            {
                try
                {
                    _pdArrayAnalyzer.DetectFVG(Bars, _previousBarIndex);
                }
                catch (Exception ex)
                {
                    Print("Error in FVG/OrderBlock detection: " + ex.Message);
                }
            }

            if (ShowSwingPoints)
            {
                try
                {
                    // Create a new candle object from the previous bar
                    var candle = new Candle(_previousBar, _previousBarIndex);

                    // Pass the current bar properties to the regular swing detector
                    _swingDetector.ProcessBar(_previousBarIndex, candle);

                    // Get any newly identified swing point
                    var swingPointsAtIndex = _swingDetector.GetSwingPointsAtIndex(_previousBarIndex);
                    if (swingPointsAtIndex.Count > 0)
                    {
                        swingPointsAtIndex.OrderBy(s => s.Number);

                        // Process each swing point in the sorted order
                        foreach (var swingPoint in swingPointsAtIndex)
                        {
                            // Process for market structure if enabled
                            if (ShowMarketStructure && _marketStructureAnalyzer != null)
                            {
                                // If we have enough swing points, initialize the market structure analyzer
                                var allSwingPoints = _swingDetector.GetAllSwingPoints();

                                if (!_marketStructureInitialized && allSwingPoints.Count >= 2)
                                {
                                    _marketStructureAnalyzer.Initialize(allSwingPoints);
                                    _marketStructureInitialized = true;
                                }
                                else if (_marketStructureInitialized)
                                {
                                    // Process the new swing point for market structure analysis
                                    _marketStructureAnalyzer.ProcessSwingPoint(swingPoint);
                                }
                            }

                            if (swingPoint.Bar != null && _swingDetector != null)
                            {
                                _swingDetector.CheckForSweptLiquidity(swingPoint.Bar, swingPoint.Index);
                            }

                            if (_timeManager != null)
                            {
                                _timeManager.CheckFibonacciSweep(swingPoint);
                            }

                            // Process for PD Array analysis
                            if (_pdArrayAnalyzer != null)
                            {
                                // If we have enough swing points, initialize the PD Array analyzer
                                var allSwingPoints = _swingDetector.GetAllSwingPoints();

                                if (!_pdArrayAnalyzerInitialized && allSwingPoints.Count >= 2)
                                {
                                    _pdArrayAnalyzer.Initialize(allSwingPoints);
                                    _pdArrayAnalyzerInitialized = true;
                                }
                                else if (_pdArrayAnalyzerInitialized)
                                {
                                    // Process the new swing point for PD Array analysis
                                    _pdArrayAnalyzer.ProcessSwingPoint(swingPoint);
                                }
                            }
                        }
                    }

                    // Update the relationships between swing points
                    if (index == Bars.Count - 1) // Only on the last bar for efficiency
                    {
                        _swingDetector.UpdateSwingPointRelationships();
                        _swingPoints = _swingDetector.GetAllSwingPoints();
                    }
                }
                catch (Exception ex)
                {
                    Print("Error in swing point processing: " + ex.Message);
                }
            }

            // High Timeframe Processing
            if (ShowHtfSwingPoints && _currentBar.OpenTime.IsStartOfHigherTimeframeBar(_highTimeFrame))
            {
                try
                {
                    // Get the previous HTF bar indices
                    var (startIndex, endIndex) =
                        Bars.GetPreviousHigherTimeframeBarRange(_currentBarIndex, _highTimeFrame);

                    if (startIndex >= 0 && endIndex >= 0)
                    {
                        // Create the HTF candle from the range of bars
                        var htfCandle = Bars.GetHigherTimeframeCandle(startIndex, endIndex);

                        // Check if we've already processed this HTF bar
                        if (htfCandle != null && !_processedHtfBars.ContainsKey(htfCandle.Time))
                        {
                            // Process the HTF candle using our specialized HTF swing point detector
                            _htfSwingDetector.ProcessHighTimeframeBar(htfCandle);

                            // Mark this HTF bar as processed
                            _processedHtfBars[htfCandle.Time] = true;

                            // Update HTF swing point relationships at the end of all calculations
                            if (index == Bars.Count - 1)
                            {
                                _htfSwingDetector.UpdateSwingPointRelationships();
                                _htfSwingPoints = _htfSwingDetector.GetAllSwingPoints();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print("Error in HTF processing: " + ex.Message);
                }
            }
        }

        private void InitializePairSymbol()
        {
            if (!ShowSMT || string.IsNullOrEmpty(SMTPair))
                return;

            try
            {
                _pairSymbol = Symbols.GetSymbol(SMTPair);
                if (_pairSymbol != null)
                {
                    // Get the bars data for this symbol with the same timeframe
                    _pairBars = MarketData.GetBars(TimeFrame, SMTPair);
                }
                else
                {
                    Print($"Symbol '{SMTPair}' not found. SMT functionality will be disabled.");
                    ShowSMT = false;
                }
            }
            catch (Exception ex)
            {
                Print($"Error initializing pair symbol: {ex.Message}");
                // Disable SMT to prevent further errors
                ShowSMT = false;
            }
        }

        private double GetPairPrice(string pairSymbol, DateTime time, int index, Direction direction)
        {
            // If we don't have pair data, return 0
            if (_pairSymbol == null || _pairBars == null)
            {
                Print($"No pair symbol or bars initialized for {pairSymbol}");
                return 0;
            }

            if (_pairBars.Count == 0)
            {
                Print($"Pair bars collection is empty for {pairSymbol}");
                return 0;
            }

            try
            {
                // Method 1: Try to find the bar at the exact same time
                for (int i = 0; i < _pairBars.Count; i++)
                {
                    if (_pairBars[i].OpenTime == time)
                    {
                        // Return high for bullish, low for bearish
                        if (direction == Direction.Up)
                        {
                            return _pairBars[i].High;
                        }
                        else
                        {
                            return _pairBars[i].Low;
                        }
                    }
                }

                // Method 2: If we can't find an exact time match, try to find the closest bar
                int closestIndex = -1;
                TimeSpan minTimeDiff = TimeSpan.MaxValue;

                for (int i = 0; i < _pairBars.Count; i++)
                {
                    TimeSpan timeDiff = _pairBars[i].OpenTime > time
                        ? _pairBars[i].OpenTime - time
                        : time - _pairBars[i].OpenTime;

                    if (timeDiff < minTimeDiff)
                    {
                        minTimeDiff = timeDiff;
                        closestIndex = i;
                    }
                }

                if (closestIndex >= 0)
                {
                    // Return high for bullish, low for bearish
                    if (direction == Direction.Up)
                    {
                        return _pairBars[closestIndex].High;
                    }
                    else
                    {
                        return _pairBars[closestIndex].Low;
                    }
                }

                // Method 3: If all else fails, try to use the same index if it's in range
                if (index < _pairBars.Count)
                {
                    // Return high for bullish, low for bearish
                    if (direction == Direction.Up)
                    {
                        return _pairBars[index].High;
                    }
                    else
                    {
                        return _pairBars[index].Low;
                    }
                }

                Print($"Could not find matching bar for {pairSymbol} at time {time}");
                return 0;
            }
            catch (Exception ex)
            {
                Print($"Error getting pair price: {ex.Message}");
                Print($"Stack trace: {ex.StackTrace}");
                return 0;
            }
        }

        // Methods to expose swing points to other components
        public List<SwingPoint> GetAllSwingPoints()
        {
            return _swingPoints ?? new List<SwingPoint>();
        }

        public List<SwingPoint> GetAllHtfSwingPoints()
        {
            return _htfSwingPoints ?? new List<SwingPoint>();
        }

        public SwingPoint GetLastSwingHigh()
        {
            return _swingDetector?.GetLastSwingHigh();
        }

        public SwingPoint GetLastSwingLow()
        {
            return _swingDetector?.GetLastSwingLow();
        }

        public SwingPoint GetLastHtfSwingHigh()
        {
            return _htfSwingDetector?.GetLastSwingHigh();
        }

        public SwingPoint GetLastHtfSwingLow()
        {
            return _htfSwingDetector?.GetLastSwingLow();
        }

        // Get market structure information
        public Direction GetMarketBias()
        {
            return (ShowMarketStructure && _marketStructureAnalyzer != null)
                ? _marketStructureAnalyzer.GetBias()
                : Direction.Up;
        }

        public List<SwingPoint> GetExternalLiquidityPoints()
        {
            return (ShowMarketStructure && _marketStructureAnalyzer != null)
                ? _marketStructureAnalyzer.GetExternalLiquidityPoints()
                : new List<SwingPoint>();
        }

        // Get order flow information
        public List<Level> GetPdArrays()
        {
            return _pdArrayAnalyzer?.GetPdArrays() ?? new List<Level>();
        }

        public List<Level> GetBullishPdArrays()
        {
            return _pdArrayAnalyzer?.GetBullishPdArrays() ?? new List<Level>();
        }

        public List<Level> GetBearishPdArrays()
        {
            return _pdArrayAnalyzer?.GetBearishPdArrays() ?? new List<Level>();
        }

        public Level GetLastBullishPdArray()
        {
            return _pdArrayAnalyzer?.GetLastBullishPdArray();
        }

        public Level GetLastBearishPdArray()
        {
            return _pdArrayAnalyzer?.GetLastBearishPdArray();
        }

        // Get FVG information - now using PdArrayAnalyzer
        public List<Level> GetAllFVGs()
        {
            return _pdArrayAnalyzer?.GetAllFVGs() ?? new List<Level>();
        }

        public List<Level> GetBullishFVGs()
        {
            return _pdArrayAnalyzer?.GetBullishFVGs() ?? new List<Level>();
        }

        public List<Level> GetBearishFVGs()
        {
            return _pdArrayAnalyzer?.GetBearishFVGs() ?? new List<Level>();
        }

        // Get Order Block information - now using PdArrayAnalyzer
        public List<Level> GetAllOrderBlocks()
        {
            return _pdArrayAnalyzer?.GetAllOrderBlocks() ?? new List<Level>();
        }

        public List<Level> GetBullishOrderBlocks()
        {
            return _pdArrayAnalyzer?.GetBullishOrderBlocks() ?? new List<Level>();
        }

        public List<Level> GetBearishOrderBlocks()
        {
            return _pdArrayAnalyzer?.GetBearishOrderBlocks() ?? new List<Level>();
        }

        // Get orderflow levels that swept liquidity
        public List<Level> GetLiquiditySweepLevels()
        {
            return _pdArrayAnalyzer?.GetLiquiditySweepLevels() ?? new List<Level>();
        }

        // Get all orderflow levels that swept multiple liquidity points
        public List<Level> GetMultipleSweptLevels()
        {
            return _pdArrayAnalyzer?.GetPdArrays().Where(l => l.SweptSwingPoints?.Count > 1).ToList() ??
                   new List<Level>();
        }

        // Get all swing points that had their liquidity swept
        public List<SwingPoint> GetSweptSwingPoints()
        {
            List<SwingPoint> sweptPoints = new List<SwingPoint>();

            var levels = _pdArrayAnalyzer?.GetPdArrays();
            if (levels != null)
            {
                foreach (var level in levels)
                {
                    if (level.SweptSwingPoints != null)
                    {
                        sweptPoints.AddRange(level.SweptSwingPoints);
                    }
                }
            }

            return sweptPoints;
        }

        // Get all Gauntlets
        public List<Level> GetGauntlets()
        {
            return _pdArrayAnalyzer?.GetGauntlets() ?? new List<Level>();
        }

        // Get Gauntlets by direction
        public List<Level> GetGauntlets(Direction direction)
        {
            return _pdArrayAnalyzer?.GetGauntlets(direction) ?? new List<Level>();
        }

        // Get the most recent Gauntlet by direction
        public Level GetLastGauntlet(Direction direction)
        {
            return _pdArrayAnalyzer?.GetGauntlets(direction)
                .OrderByDescending(g => g.Index)
                .FirstOrDefault();
        }

        // Add this method to handle swing point removal events
        private void OnSwingPointRemoved(SwingPoint removedPoint)
        {
            // Skip if the PD Array analyzer isn't initialized yet
            if (!_pdArrayAnalyzerInitialized || _pdArrayAnalyzer == null)
                return;

            try
            {
                // Notify the PD Array analyzer about the removed swing point
                _pdArrayAnalyzer.RemoveSwingPoint(removedPoint);
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the indicator
                Print($"Error handling swing point removal: {ex.Message}");
            }
        }

        public List<Level> GetAllCISDLevels()
        {
            return _pdArrayAnalyzer?.GetAllCISDLevels() ?? new List<Level>();
        }

        public List<Level> GetActiveCISDLevels()
        {
            return _pdArrayAnalyzer?.GetActiveCISDLevels() ?? new List<Level>();
        }

        public List<Level> GetConfirmedCISDLevels()
        {
            return _pdArrayAnalyzer?.GetConfirmedCISDLevels() ?? new List<Level>();
        }

        public bool IsInMacroTime(DateTime time)
        {
            return _timeManager?.IsInMacroTime(time) ?? false;
        }

        public List<TimeRange> GetMacros()
        {
            return _timeManager?.GetMacros() ?? new List<TimeRange>();
        }

        public List<Level> GetAllBreakerBlocks()
        {
            return _pdArrayAnalyzer?.GetAllBreakerBlocks() ?? new List<Level>();
        }

        public List<Level> GetBullishBreakerBlocks()
        {
            return _pdArrayAnalyzer?.GetBullishBreakerBlocks() ?? new List<Level>();
        }

        public List<Level> GetBearishBreakerBlocks()
        {
            return _pdArrayAnalyzer?.GetBearishBreakerBlocks() ?? new List<Level>();
        }

        public List<Level> GetAllUnicorns()
        {
            return _pdArrayAnalyzer?.GetAllUnicorns() ?? new List<Level>();
        }

        public List<Level> GetUnicorns(Direction direction)
        {
            return _pdArrayAnalyzer?.GetUnicorns(direction) ?? new List<Level>();
        }

        public Level GetLastUnicorn(Direction direction)
        {
            return _pdArrayAnalyzer?.GetLastUnicorn(direction);
        }

        // Add these public methods to expose active PD Array information
        public List<Level> GetActivePdArrays()
        {
            return _pdArrayAnalyzer?.GetActivePdArrays() ?? new List<Level>();
        }

        public List<Level> GetActiveBullishPdArrays()
        {
            return _pdArrayAnalyzer?.GetActivePdArrays(Direction.Up) ?? new List<Level>();
        }

        public List<Level> GetActiveBearishPdArrays()
        {
            return _pdArrayAnalyzer?.GetActivePdArrays(Direction.Down) ?? new List<Level>();
        }

        // Get active FVGs (where at least one quadrant is not swept)
        public List<Level> GetActiveFVGs()
        {
            return _pdArrayAnalyzer?.GetAllFVGs().Where(fvg => fvg.IsActive).ToList() ?? new List<Level>();
        }

        // Get active OrderBlocks (where at least one quadrant is not swept)
        public List<Level> GetActiveOrderBlocks()
        {
            return _pdArrayAnalyzer?.GetAllOrderBlocks().Where(ob => ob.IsActive).ToList() ?? new List<Level>();
        }

        // Get swing points that swept quadrants
        public List<SwingPoint> GetSwingPointsThatSweptQuadrants()
        {
            return _swingPoints?.Where(sp => sp.InsideKeyLevel).ToList() ?? new List<SwingPoint>();
        }

        // Get SMT divergence points
        public List<SwingPoint> GetSMTDivergencePoints()
        {
            if (!ShowSMT || _swingDetector == null)
                return new List<SwingPoint>();

            return _swingDetector.GetAllSwingPoints()
                .Where(sp => sp.HasSMT)
                .ToList();
        }
    }
}