using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Repositories;
using Pfuma.Services;
using Pfuma.Services.Time;
using Pfuma.Visualization;

namespace Pfuma
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class Pfuma : Indicator
    {
        #region Parameters
        
        // Swing Points
        [Parameter("Swing Points", DefaultValue = true, Group = "Swing Points")]
        public bool ShowSwingPoints { get; set; }
        
        // Pattern Detection
        [Parameter("FVG", DefaultValue = false, Group = "Patterns")]
        public bool ShowFVG { get; set; }

        [Parameter("iFVG", DefaultValue = false, Group = "Patterns")]
        public bool ShowIFvg { get; set; }

        [Parameter("Order Flow", DefaultValue = false, Group = "Patterns")]
        public bool ShowOrderFlow { get; set; }

        [Parameter("Remove Swept Orderflow", DefaultValue = false, Group = "Patterns")]
        public bool RemoveSweptOrderflow { get; set; }

        [Parameter("Show Swept Orderflow", DefaultValue = false, Group = "Patterns")]
        public bool ShowSweptOrderflow { get; set; }

        [Parameter("Liquidity Sweep", DefaultValue = false, Group = "Patterns")]
        public bool ShowLiquiditySweep { get; set; }
        
        [Parameter("Rejection Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowRejectionBlock { get; set; }

        [Parameter("Next Array", DefaultValue = false, Group = "Patterns")]
        public bool ShowNextArray { get; set; }
        
        [Parameter("Order Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowOrderBlock { get; set; }
        
        [Parameter("Breaker Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowBreakerBlock { get; set; }
        
        [Parameter("CISD", DefaultValue = false, Group = "Patterns")]
        public bool ShowCISD { get; set; }

        //[Parameter("Max CISD", DefaultValue = 2, Group = "Patterns")]
        public int MaxCisdsPerDirection { get; set; } = 1;

        [Parameter("OTE", DefaultValue = false, Group = "Patterns")]
        public bool ShowOTE { get; set; }

        [Parameter("Propulsion Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowPropulsionBlock { get; set; }
        
        [Parameter("Unicorn", DefaultValue = false, Group = "Patterns")]
        public bool ShowUnicorn { get; set; }

        [Parameter("Clear Swept", DefaultValue = true, Group = "Patterns")]
        public bool ClearSwept { get; set; }

        // Signals
        [Parameter("Show Signals", DefaultValue = false, Group = "Signals")]
        public bool ShowSignals { get; set; }

        [Parameter("Risk Reward", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0, Group = "Signals")]
        public double RiskReward { get; set; }

        // Visualization
        [Parameter("Quadrants", DefaultValue = false, Group = "Visualization")]
        public bool ShowQuadrants { get; set; }
        
        [Parameter("Inside Key Level", DefaultValue = false, Group = "Visualization")]
        public bool ShowInsideKeyLevel { get; set; }

        [Parameter("Inducement", DefaultValue = false, Group = "Visualization")]
        public bool ShowInducement { get; set; }
        
        // Time Management
        [Parameter("Macros", DefaultValue = false, Group = "Time")]
        public bool ShowMacros { get; set; }
        
        [Parameter("Macro Filter", DefaultValue = false, Group = "Time")]
        public bool MacroFilter { get; set; }
        
        [Parameter("Daily Levels", DefaultValue = true, Group = "Time")]
        public bool ShowDailyLevels { get; set; }
        
        [Parameter("Session Levels", DefaultValue = true, Group = "Time")]
        public bool ShowSessionLevels { get; set; }

        [Parameter("Opening Times", DefaultValue = false, Group = "Time")]
        public bool ShowOpeningTimes { get; set; }

        [Parameter("Cycle Fib Levels", DefaultValue = false, Group = "Fibonacci")]
        public bool ShowCycleFibLevels { get; set; }

        [Parameter("CISD Fib Levels", DefaultValue = false, Group = "Fibonacci")]
        public bool ShowCISDFibLevels { get; set; }

        [Parameter("OTE Fib Levels", DefaultValue = false, Group = "Fibonacci")]
        public bool ShowOTEFibLevels { get; set; }

        [Parameter("Extended Fib", DefaultValue = true, Group = "Fibonacci")]
        public bool ShowExtendedFib { get; set; }
        
        [Parameter("Remove Fib Extensions", DefaultValue = false, Group = "Fibonacci")]
        public bool RemoveFibExtensions { get; set; }
        
        [Parameter("UTC Offset", DefaultValue = -4, Group = "Time")]
        public int UtcOffset { get; set; }
        
        // Notifications
        [Parameter("Enable Log", DefaultValue = false, Group = "Notifications")]
        public bool EnableLog { get; set; }

        // Telegram
        [Parameter("Send Liquidity", DefaultValue = false, Group = "Telegram")]
        public bool SendLiquidity { get; set; }

        [Parameter("Send CISD", DefaultValue = false, Group = "Telegram")]
        public bool SendCISD { get; set; }

        [Parameter("Send Order Block", DefaultValue = false, Group = "Telegram")]
        public bool SendOrderBlock { get; set; }

        [Parameter("Send Inside Key Level", DefaultValue = false, Group = "Telegram")]
        public bool SendInsideKeyLevel { get; set; }
        
        // Timeframe Visualization
        [Parameter("See Timeframe", DefaultValue = "", Group = "Visualization")]
        public string SeeTimeframe { get; set; }
        
        [Parameter("Extension Opacity", DefaultValue = 10, MinValue = 0, MaxValue = 100, Group = "Visualization")]
        public int ExtensionOpacity { get; set; }
        
        [Parameter("Timeframes", DefaultValue = "H1", Group = "Multi-Timeframe")]
        public string Timeframes { get; set; }
        
        [Parameter("High Timeframe Candle", DefaultValue = false, Group = "Multi-Timeframe")]
        public bool ShowHighTimeframeCandle { get; set; }
        
        [Parameter("HTF Swing Points", DefaultValue = false, Group = "Multi-Timeframe")]
        public bool ShowHtfSwingPoints { get; set; }
        
        [Parameter("HTF FVG", DefaultValue = false, Group = "Multi-Timeframe")]
        public bool ShowHtfFvg { get; set; }
        
        [Parameter("HTF Order Flow", DefaultValue = false, Group = "Multi-Timeframe")]
        public bool ShowHtfOrderFlow { get; set; }

        [Parameter("HTF CISD", DefaultValue = false, Group = "Multi-Timeframe")]
        public bool ShowHtfCisd { get; set; }

        [Parameter("HTF Order Block", DefaultValue = false, Group = "Multi-Timeframe")]
        public bool ShowHtfOrderBlock { get; set; }

        #endregion
        
        #region Output Series
        
        [Output("Swing Highs", LineColor = "White", PlotType = PlotType.Points)]
        public IndicatorDataSeries SwingHighs { get; set; }
        
        [Output("Swing Lows", LineColor = "White", PlotType = PlotType.Points)]
        public IndicatorDataSeries SwingLows { get; set; }
        

        [Output("Entries", LineColor = "Green", PlotType = PlotType.Points)]
        public IndicatorDataSeries Entries { get; set; }

        [Output("Stops", LineColor = "Red", PlotType = PlotType.Points)]
        public IndicatorDataSeries Stops { get; set; }

        #endregion
        
        #region Private Fields
        
        // Core Components
        private IEventAggregator _eventAggregator;
        private IndicatorSettings _settings;
        private bool _isInitialized;
        private bool _detectorsInitialized;
        
        // Services
        private NotificationService _notificationService;
        private TimeManager _timeManager;
        private SwingPointDetector _swingPointDetector;
        private SwingPointManager _swingPointManager;
        private CandleManager _candleManager;
        private LiquidityManager _liquidityManager;
        private IFibonacciService _fibonacciService;
        private FibonacciVisualizer _fibonacciVisualizer;
        private IFibonacciSweepDetector _fibonacciSweepDetector;
        private SignalManager _signalManager;
        // Repositories
        private IRepository<SwingPoint> _swingPointRepository;
        private IRepository<Level> _levelRepository;
        
        // Detectors
        private FvgDetector _fvgDetector;
        private HtfFvgDetector _htfFvgDetector;
        private OrderFlowDetector _orderFlowDetector;
        private HtfOrderFlowDetector _htfOrderFlowDetector;
        private OrderFlowManager _orderFlowManager;
        private RejectionBlockDetector _rejectionBlockDetector;
        private OrderBlockDetector _orderBlockDetector;
        private BreakerBlockDetector _breakerBlockDetector;
        private CisdDetector _cisdDetector;
        private HtfCisdDetector _htfCisdDetector;
        private HtfOrderBlockDetector _htfOrderBlockDetector;
        private UnicornDetector _unicornDetector;
        private PropulsionBlockDetector _propulsionBlockDetector;
        
        // Visualizers
        private IVisualization<Level> _fvgVisualizer;
        private IVisualization<Level> _htfFvgVisualizer;
        private HtfCandleVisualizer _htfCandleVisualizer;
        private IVisualization<Level> _orderFlowVisualizer;
        private IVisualization<Level> _rejectionBlockVisualizer;
        private IVisualization<Level> _orderBlockVisualizer;
        private IVisualization<Level> _breakerBlockVisualizer;
        private IVisualization<Level> _cisdVisualizer;
        private IVisualization<Level> _htfCisdVisualizer;
        private IVisualization<Level> _htfOrderBlockVisualizer;
        private IVisualization<Level> _unicornVisualizer;
        private IVisualization<Level> _propulsionBlockVisualizer;
        private StatsVisualizer _statsVisualizer;

        // Bar tracking
        private Bar _previousBar;
        private int _previousBarIndex;

        // Managers
        private NextArrayManager _nextArrayManager;
        
        #endregion
        
        #region Initialization
        
        protected override void Initialize()
        {
            try
            {
                Chart.RemoveAllObjects();

                // Print current symbol name
                Print($"Current symbol: {Symbol.Name}");

                // Initialize configuration
                InitializeConfiguration();

                // Initialize core services
                InitializeCoreServices();
                
                // Initialize repositories
                InitializeRepositories();
                
                // Initialize visualizers
                InitializeVisualizers();
                
                // Initialize services and analyzers
                InitializeServicesAndAnalyzers();
                
                // Initialize detectors
                InitializeDetectors();
                
                // Subscribe to events
                SubscribeToEvents();
                
                _isInitialized = true;
                
            }
            catch
            {
                // Initialization error - indicator will not function
                _isInitialized = false;
            }
        }
        
        private void InitializeConfiguration()
        {
            _settings = new IndicatorSettings
            {
                Patterns = new PatternDetectionSettings
                {
                    ShowFVG = ShowFVG,  // Only for LTF FVG visualization
                    ShowHtfFvg = ShowHtfFvg,  // Only for HTF FVG visualization
                    ShowHighTimeframeCandle = ShowHighTimeframeCandle,  // For HTF candle high/low dots
                    ShowIFvg = ShowIFvg,  // Only for iFVG visualization
                    ShowOrderFlow = ShowOrderFlow,  // Only for regular orderflow
                    RemoveSweptOrderflow = RemoveSweptOrderflow,
                    ShowSweptOrderflow = ShowSweptOrderflow,
                    ShowHtfOrderFlow = ShowHtfOrderFlow,  // Only for HTF orderflow
                    ShowLiquiditySweep = ShowLiquiditySweep,
                    ShowRejectionBlock = ShowRejectionBlock,
                    ShowOrderBlock = ShowOrderBlock,
                    ShowHtfOrderBlock = ShowHtfOrderBlock,
                    ShowBreakerBlock = ShowBreakerBlock,
                    ShowCISD = ShowCISD,
                    ShowHtfCisd = ShowHtfCisd,
                    MaxCisdsPerDirection = MaxCisdsPerDirection,
                    ShowOTE = ShowOTE,
                    ShowPropulsionBlock = ShowPropulsionBlock,
                    ShowUnicorn = ShowUnicorn,
                    ShowQuadrants = ShowQuadrants,
                    ShowInsideKeyLevel = ShowInsideKeyLevel,
                    ShowInducement = ShowInducement,
                    ClearSwept = ClearSwept,
                },
                Time = new TimeSettings
                {
                    ShowMacros = ShowMacros,
                    MacroFilter = MacroFilter,
                    ShowDailyLevels = ShowDailyLevels,
                    ShowSessionLevels = ShowSessionLevels,
                    UtcOffset = UtcOffset
                },
                Notifications = new NotificationSettings
                {
                    EnableLog = EnableLog,
                    SendLiquidity = SendLiquidity,
                    SendCISD = SendCISD,
                    SendOrderBlock = SendOrderBlock,
                    SendInsideKeyLevel = SendInsideKeyLevel
                },
                Visualization = new VisualizationSettings
                {
                    Opacity = new OpacitySettings
                    {
                        Extension = ExtensionOpacity
                    }
                }
            };
            
            // Connect settings references
            _settings.Visualization.Patterns = _settings.Patterns;
            _settings.Visualization.Notifications = _settings.Notifications;
            _settings.Visualization.SeeTimeframe = SeeTimeframe;
        }
        
        private void InitializeCoreServices()
        {
            _eventAggregator = new EventAggregator();

            // Enable Telegram if any of the send flags are enabled
            bool enableTelegram = SendLiquidity || SendCISD || SendOrderBlock || SendInsideKeyLevel;

            _notificationService = new NotificationService(
                EnableLog,
                enableTelegram,
                _settings.Notifications.TelegramChatId,
                _settings.Notifications.TelegramToken,
                Symbol.Name,
                _settings.Time.UtcOffset,
                EnableLog ? Print : null);

            _signalManager = new SignalManager(Chart, _notificationService);
            _statsVisualizer = new StatsVisualizer(Chart, _signalManager);
        }
        
        private void InitializeRepositories()
        {
            _swingPointRepository = new SwingPointRepository();
            _levelRepository = new LevelRepository();
        }
        
        private void InitializeVisualizers()
        {
            _fvgVisualizer = new FvgVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _htfFvgVisualizer = new HtfFvgVisualizer(Chart, _settings);
            _htfCandleVisualizer = new HtfCandleVisualizer(Chart, _eventAggregator, _settings);
            _orderFlowVisualizer = new OrderFlowVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _rejectionBlockVisualizer = new RejectionBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _orderBlockVisualizer = new OrderBlockVisualizer(Chart, _settings.Visualization, _eventAggregator, EnableLog ? Print : null);
            _breakerBlockVisualizer = new BreakerBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _cisdVisualizer = new CisdVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _htfCisdVisualizer = new HtfCisdVisualizer(Chart, _settings);
            _htfOrderBlockVisualizer = new HtfOrderBlockVisualizer(Chart, _settings);
            _unicornVisualizer = new UnicornVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _propulsionBlockVisualizer = new PropulsionBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
        }
        
        private void InitializeServicesAndAnalyzers()
        {
            // Initialize candle manager
            _candleManager = new CandleManager(Bars, TimeFrame, Chart, UtcOffset, EnableLog ? Print : null, Timeframes, ShowHighTimeframeCandle, ShowHtfSwingPoints, _eventAggregator);

            // Initialize swing point manager
            _swingPointManager = new SwingPointManager(SwingHighs, SwingLows);

            // Initialize swing point detector (without TimeManager initially)
            _swingPointDetector = new SwingPointDetector(_swingPointManager, _candleManager, _eventAggregator);
            
            // Initialize time manager
            _timeManager = new TimeManager(
                Chart, _candleManager, _swingPointDetector, _notificationService, _eventAggregator,
                ShowMacros, ShowDailyLevels, ShowSessionLevels, ShowOpeningTimes, UtcOffset);
            
            // Now pass TimeManager to SwingPointDetector
            _swingPointDetector = new SwingPointDetector(_swingPointManager, _candleManager, _eventAggregator, _timeManager);
            
            // Initialize liquidity manager
            _liquidityManager = new LiquidityManager(Chart, _candleManager, _eventAggregator, _levelRepository, _swingPointRepository, _settings, _notificationService, EnableLog ? Print : null);

            // Set opening time manager for open level sweep visualization
            _liquidityManager.SetOpeningTimeManager(_timeManager.OpeningTimeManager);

            // Initialize Fibonacci service and visualizer
            _fibonacciService = new FibonacciService(_eventAggregator);
            _fibonacciVisualizer = new FibonacciVisualizer(Chart, _fibonacciService, _eventAggregator, _candleManager, ShowCycleFibLevels, ShowCISDFibLevels, ShowOTEFibLevels, ShowExtendedFib, RemoveFibExtensions, EnableLog ? Print : null);
            _fibonacciSweepDetector = new FibonacciSweepDetector(_fibonacciService, _eventAggregator, EnableLog ? Print : null);
            
        }
        
        private void InitializeDetectors()
        {
            // Pattern detectors
            _fvgDetector = new FvgDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _fvgVisualizer, _settings, EnableLog ? Print : null);
            
            
            _orderFlowDetector = new OrderFlowDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _orderFlowVisualizer, _swingPointDetector, _settings, EnableLog ? Print : null);
            
            _htfOrderFlowDetector = new HtfOrderFlowDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _orderFlowVisualizer, _settings, EnableLog ? Print : null);

            _orderFlowManager = new OrderFlowManager(
                _eventAggregator, _levelRepository, _orderFlowVisualizer, Chart, _settings, EnableLog ? Print : null);

            // Initialize NextArrayManager if enabled
            if (ShowNextArray)
            {
                _nextArrayManager = new NextArrayManager(
                    _eventAggregator, _levelRepository, Chart, _settings, ExtensionOpacity, EnableLog ? Print : null);
            }

            _rejectionBlockDetector = new RejectionBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _rejectionBlockVisualizer, _swingPointDetector, _settings, EnableLog ? Print : null);
            
            _orderBlockDetector = new OrderBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _orderBlockVisualizer, _swingPointManager, _swingPointRepository, _settings, EnableLog ? Print : null);
            
            
            _breakerBlockDetector = new BreakerBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _levelRepository, _breakerBlockVisualizer, _settings, EnableLog ? Print : null);
            
            _cisdDetector = new CisdDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _cisdVisualizer, _fibonacciService, _fibonacciVisualizer, _swingPointRepository, _timeManager, _settings, EnableLog ? Print : null);

            _htfCisdDetector = new HtfCisdDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _htfCisdVisualizer, _settings, EnableLog ? Print : null);

            _htfOrderBlockDetector = new HtfOrderBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _htfOrderBlockVisualizer, _settings, EnableLog ? Print : null);

            _unicornDetector = new UnicornDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _levelRepository, _unicornVisualizer, _settings, EnableLog ? Print : null);
            
            _propulsionBlockDetector = new PropulsionBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _propulsionBlockVisualizer, _swingPointRepository, _settings, EnableLog ? Print : null);

            // HTF FVG detector (uses specialized HTF FVG visualizer)
            _htfFvgDetector = new HtfFvgDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _htfFvgVisualizer, _settings, EnableLog ? Print : null);
            
            // Initialize all detectors
            _fvgDetector.Initialize();
            _htfFvgDetector.Initialize();
            _orderFlowDetector.Initialize();
            _htfOrderFlowDetector.Initialize();
            _rejectionBlockDetector.Initialize();
            _orderBlockDetector.Initialize();
            _breakerBlockDetector.Initialize();
            _cisdDetector.Initialize();
            _htfCisdDetector.Initialize();
            _htfOrderBlockDetector.Initialize();
            _unicornDetector.Initialize();
            _propulsionBlockDetector.Initialize();

            // Initialize liquidity manager and visualizers
            _liquidityManager.Initialize();
            (_orderBlockVisualizer as OrderBlockVisualizer)?.Initialize();
            _htfCandleVisualizer?.Initialize();
        }
        
        private void SubscribeToEvents()
        {
            // Subscribe to swing point events
            if (_swingPointDetector != null)
            {
                _swingPointDetector.SwingPointRemoved += OnSwingPointRemoved;
                _swingPointDetector.LiquiditySwept += OnLiquiditySwept;
            }
            
            // Subscribe to swing point detection for repository management
            _eventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            _eventAggregator.Subscribe<OrderBlockDetectedEvent>(OnOrderBlockDetected);
            _eventAggregator.Subscribe<CisdConfirmedEvent>(OnCISDDetected);
        }
        
        #endregion
        
        #region Calculate Method
        
        public override void Calculate(int index)
        {
            if (!_isInitialized || index <= 1 || index >= Bars.Count)
                return;

            try
            {
                // Validate array bounds
                if (index - 1 < 0 || index - 1 >= Bars.Count)
                    return;

                _previousBarIndex = index - 1;
                
                // Process the previous bar and get the candle from CandleManager
                var candle = _candleManager.ProcessBar(_previousBarIndex);
                
                if (candle == null)
                    return;
                
                _previousBar = Bars[_previousBarIndex];
                
                // Also process the current bar for pattern detection
                _candleManager.ProcessBar(index);
                
                // 1. Process time-based features
                if (index < Bars.Count && Bars[index] != null)
                {
                    _timeManager?.ProcessBar(index, Bars[index].OpenTime);
                }
                
                // Update Fibonacci visualizer settings
                // Note: Drawing is now handled automatically via FibonacciLevelCreatedEvent
                if (_fibonacciVisualizer != null)
                {
                    _fibonacciVisualizer.ShowCycleFibLevels = ShowCycleFibLevels;
                    _fibonacciVisualizer.ShowCISDFibLevels = ShowCISDFibLevels;
                    _fibonacciVisualizer.ShowOTEFibLevels = ShowOTEFibLevels;
                    _fibonacciVisualizer.ShowExtendedFib = ShowExtendedFib;

                    // Clear levels if settings are disabled
                    _fibonacciVisualizer.UpdateSettingsVisibility();
                }

                // 2. Check CISD activation on previous bar
                if (ShowCISD && _cisdDetector != null && index > 1)
                {
                    CheckCisdActivation(_previousBar, _previousBarIndex);
                }

                // 3. Detect FVGs first (before swing points so candles are marked with FVG info)
                _fvgDetector?.Detect(_previousBarIndex);

                // Detect HTF FVGs (only if enabled and timeframes configured)
                if (ShowHtfFvg && !string.IsNullOrEmpty(Timeframes))
                {
                    _htfFvgDetector?.Detect(_previousBarIndex);
                }

                // 4. Detect swing points (after FVG detection)
                if (ShowSwingPoints)
                {
                    _swingPointDetector?.ProcessBar(_previousBarIndex, candle);
                }

                // 5. Check for special liquidity sweeps (PDH, PDL, PSH, PSL)
                // Note: Regular swing point liquidity sweeps are handled by OrderFlowDetector
                if (ShowLiquiditySweep && _swingPointDetector != null)
                {
                    // This functionality could be moved to TimeManager or SwingPointDetector
                    // For now, liquidity sweep detection for special points is handled via events
                }
                
                
                if (ShowRejectionBlock)
                {
                    _rejectionBlockDetector?.Detect(_previousBarIndex);
                }

                // Check if next arrays are broken
                if (ShowNextArray && _nextArrayManager != null)
                {
                    _nextArrayManager.CheckNextArraysBroken(candle);
                }
                
                if (ShowOrderBlock)
                {
                    _orderBlockDetector?.Detect(_previousBarIndex);
                }
                
                
                // 7. Update market structure (handled via events)
                
                // 8. Initialize detectors with swing points when ready
                if (!_detectorsInitialized && _swingPointRepository.Count >= 3)
                {
                    InitializeDetectorsWithSwingPoints();
                    _detectorsInitialized = true;
                    
                }
                
                // 9. Check for inside key levels on swing points
                if (ShowInsideKeyLevel && ShowQuadrants)
                {
                    CheckInsideKeyLevels();
                }

                // 10. Process signals if enabled
                if (ShowSignals && _signalManager != null && index < Bars.Count)
                {
                    _signalManager.ProcessBar(Bars[index], index);

                    // Update stats display every 10 bars to avoid excessive updates
                    if (index % 10 == 0 && _statsVisualizer != null)
                    {
                        _statsVisualizer.UpdateStats();
                    }
                }

            }
            catch
            {
                // Silently handle calculation errors
            }
        }
        
        /// <summary>
        /// Checks for CISD activation on the previous bar
        /// </summary>
        private void CheckCisdActivation(Bar previousBar, int previousBarIndex)
        {
            var activeCisdLevels = _levelRepository.Find(l => 
                l.LevelType == LevelType.CISD && 
                l.IsConfirmed && 
                !l.Activated);
            
            foreach (var cisd in activeCisdLevels)
            {
                bool activated = false;
                
                if (cisd.Direction == Direction.Up)
                {
                    // Bullish CISD activates when price closes below the low
                    activated = previousBar.Close < cisd.Low;
                }
                else
                {
                    // Bearish CISD activates when price closes above the high
                    activated = previousBar.Close > cisd.High;
                }
                
                if (activated)
                {
                    cisd.Activated = true;
                    cisd.ActivationIndex = previousBarIndex;
                    
                    // Publish activation event
                    _eventAggregator.Publish(new CisdActivatedEvent(cisd, previousBarIndex));
                    
                }
            }
        }
        
        /// <summary>
        /// Initialize detectors with existing swing points
        /// </summary>
        private void InitializeDetectorsWithSwingPoints()
        {
            var swingPoints = _swingPointRepository.GetAll();
            
            // Initialize order flow detector with swing points
            foreach (var sp in swingPoints)
            {
                _orderFlowDetector?.ProcessSwingPoint(sp);
            }
            
        }
        
        /// <summary>
        /// Check if swing points are inside key levels (quadrants)
        /// </summary>
        private void CheckInsideKeyLevels()
        {
            var swingPoints = _swingPointRepository.GetAll();
            var pdArrays = _levelRepository.Find(l => 
                l.LevelType == LevelType.RejectionBlock && 
                l.IsActive);
            
            foreach (var swingPoint in swingPoints)
            {
                foreach (var pdArray in pdArrays)
                {
                    // Check if swing point sweeps any quadrants
                    var sweptQuadrants = pdArray.CheckForSweptQuadrants(swingPoint);
                    
                    if (sweptQuadrants.Count > 0 && !swingPoint.InsidePda)
                    {
                        swingPoint.InsidePda = true;
                        swingPoint.Pda = pdArray;
                    }
                }
            }
        }
        
        private void DrawInsideKeyLevelIcon(SwingPoint swingPoint)
        {
            var color = swingPoint.Direction == Direction.Up ? Color.Green : Color.Red;
            Chart.DrawIcon($"kl-{swingPoint.Time}", ChartIconType.Circle, 
                          swingPoint.Time, swingPoint.Price, color);
        }
        
        private void DrawInsideMacroIcon(SwingPoint swingPoint)
        {
            var color = swingPoint.Direction == Direction.Up ? Color.Gold : Color.Orange;
            Chart.DrawIcon($"macro-{swingPoint.Time}", ChartIconType.Square, 
                          swingPoint.Time, swingPoint.Price, color);
        }
        
        
        #endregion
        
        #region Event Handlers
        
        private void OnSwingPointRemoved(SwingPoint removedPoint)
        {
            _eventAggregator.Publish(new SwingPointRemovedEvent(removedPoint));
        }
        
        private void OnLiquiditySwept(SwingPoint sweptPoint, int sweepingCandleIndex, Candle sweepingCandle)
        {
            _eventAggregator.Publish(new LiquiditySweptEvent(sweptPoint, sweepingCandleIndex, sweepingCandle));
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            if (evt.SwingPoint != null)
            {
                _swingPointRepository.Add(evt.SwingPoint);

                // Draw icon if swing point is inside macro time
                if (evt.SwingPoint.InsideMacro)
                {
                    //DrawInsideMacroIcon(evt.SwingPoint);
                }

            }
        }

        private void OnOrderBlockDetected(OrderBlockDetectedEvent evt)
        {
            if (evt.OrderBlock != null && evt.OrderBlock.Score >= 10)
            {
                var orderBlock = evt.OrderBlock;
                var color = orderBlock.Direction == Direction.Up ? Color.Green : Color.Red;
                var price = orderBlock.Direction == Direction.Up ? orderBlock.Low : orderBlock.High;
                var time = orderBlock.Direction == Direction.Up ? orderBlock.LowTime : orderBlock.HighTime;
                Chart.DrawIcon($"macro-{time}", ChartIconType.Square,
                    time, price, color);
            }
        }

        private void OnCISDDetected(CisdConfirmedEvent evt)
        {
            var cisds = _levelRepository.Find(l => l.LevelType == LevelType.CISD);
            if (evt.CisdLevel != null && evt.CisdLevel.Score >= 10)
            {
                var orderBlock = evt.CisdLevel;
                var color = orderBlock.Direction == Direction.Up ? Color.Green : Color.Red;
                var price = orderBlock.Direction == Direction.Up ? orderBlock.Low : orderBlock.High;
                var time = orderBlock.Direction == Direction.Up ? orderBlock.LowTime : orderBlock.HighTime;
                Chart.DrawIcon($"macro-{time}", ChartIconType.Square,
                    time, price, color);
            }
        }

        #endregion
        
        #region Public Methods for External Access
        
        /// <summary>
        /// Gets all detected swing points
        /// </summary>
        public List<SwingPoint> GetAllSwingPoints()
        {
            return _swingPointDetector?.GetAllSwingPoints() ?? new List<SwingPoint>();
        }
        
        /// <summary>
        /// Gets all detected FVGs
        /// </summary>
        public List<Level> GetAllFVGs()
        {
            return (_levelRepository as LevelRepository)?.GetByType(LevelType.FairValueGap) ?? new List<Level>();
        }
        
        /// <summary>
        /// Gets all detected Rejection Blocks
        /// </summary>
        public List<Level> GetAllRejectionBlocks()
        {
            return (_levelRepository as LevelRepository)?.GetByType(LevelType.RejectionBlock) ?? new List<Level>();
        }
        
        /// <summary>
        /// Gets all detected Order Blocks
        /// </summary>
        public List<Level> GetAllOrderBlocks()
        {
            return (_levelRepository as LevelRepository)?.GetByType(LevelType.OrderBlock) ?? new List<Level>();
        }
        
        /// <summary>
        /// Gets all order flows (with liquidity sweeps)
        /// </summary>
        public List<Level> GetAllOrderFlows()
        {
            return (_levelRepository as LevelRepository)?.GetByType(LevelType.Orderflow) ?? new List<Level>();
        }
        
        /// <summary>
        /// Gets order flows that swept liquidity
        /// </summary>
        public List<Level> GetLiquiditySweepOrderFlows()
        {
            return _levelRepository?.Find(l => 
                l.LevelType == LevelType.Orderflow && 
                l.SweptSwingPoint != null) ?? new List<Level>();
        }
        
        /// <summary>
        /// Gets all detected CISDs (Change in State of Delivery)
        /// </summary>
        public List<Level> GetAllCISDs()
        {
            var allLevels = (_levelRepository as LevelRepository)?.GetAll() ?? new List<Level>();
            var cisdLevels = allLevels.Where(l => l.LevelType == LevelType.CISD).ToList();
            
            
            return cisdLevels;
        }
        
        /// <summary>
        /// Gets market bias
        /// </summary>
        
        /// <summary>
        /// Gets all higher timeframe candles for a specific timeframe
        /// </summary>
        public List<Candle> GetHigherTimeframeCandles(TimeFrame timeframe)
        {
            return _candleManager?.GetHigherTimeframeCandles(timeframe) ?? new List<Candle>();
        }
        
        /// <summary>
        /// Gets all configured higher timeframes
        /// </summary>
        public List<TimeFrame> GetHigherTimeframes()
        {
            return _candleManager?.GetHigherTimeframes() ?? new List<TimeFrame>();
        }
        
        /// <summary>
        /// Gets the last HTF candle for a specific timeframe
        /// </summary>
        public Candle GetLastHigherTimeframeCandle(TimeFrame timeframe)
        {
            return _candleManager?.GetLastHigherTimeframeCandle(timeframe);
        }
        
        /// <summary>
        /// Gets HTF swing points for a specific timeframe
        /// </summary>
        public List<SwingPoint> GetHtfSwingPoints(TimeFrame timeframe)
        {
            return _candleManager?.GetHtfSwingPoints(timeframe) ?? new List<SwingPoint>();
        }
        
        /// <summary>
        /// Gets HTF swing highs for a specific timeframe
        /// </summary>
        public List<SwingPoint> GetHtfSwingHighs(TimeFrame timeframe)
        {
            return _candleManager?.GetHtfSwingHighs(timeframe) ?? new List<SwingPoint>();
        }
        
        /// <summary>
        /// Gets HTF swing lows for a specific timeframe
        /// </summary>
        public List<SwingPoint> GetHtfSwingLows(TimeFrame timeframe)
        {
            return _candleManager?.GetHtfSwingLows(timeframe) ?? new List<SwingPoint>();
        }
        
        /// <summary>
        /// Gets the last HTF swing high for a specific timeframe
        /// </summary>
        public SwingPoint GetLastHtfSwingHigh(TimeFrame timeframe)
        {
            return _candleManager?.GetLastHtfSwingHigh(timeframe);
        }
        
        /// <summary>
        /// Gets the last HTF swing low for a specific timeframe
        /// </summary>
        public SwingPoint GetLastHtfSwingLow(TimeFrame timeframe)
        {
            return _candleManager?.GetLastHtfSwingLow(timeframe);
        }
        
        
        #endregion
        
        #region Cleanup
        
        protected override void OnDestroy()
        {
            try
            {
                // Dispose detectors
                _fvgDetector?.Dispose();
                _htfFvgDetector?.Dispose();
                _orderFlowDetector?.Dispose();
                _orderFlowManager?.Dispose();
                _rejectionBlockDetector?.Dispose();
                _orderBlockDetector?.Dispose();
                _breakerBlockDetector?.Dispose();
                _cisdDetector?.Dispose();
                _htfCisdDetector?.Dispose();
                _htfOrderBlockDetector?.Dispose();
                _unicornDetector?.Dispose();
                
                // Dispose services
                _liquidityManager?.Dispose();
                
                // Clear visualizers
                _fvgVisualizer?.Clear();
                _orderFlowVisualizer?.Clear();
                _rejectionBlockVisualizer?.Clear();
                _orderBlockVisualizer?.Clear();
                _breakerBlockVisualizer?.Clear();
                _cisdVisualizer?.Clear();
                _htfCisdVisualizer?.Clear();
                _htfOrderBlockVisualizer?.Clear();
                _unicornVisualizer?.Clear();
                _fibonacciVisualizer?.Dispose();
                
                // Clear repositories
                _swingPointRepository?.Clear();
                _levelRepository?.Clear();

                // Dispose managers
                _nextArrayManager?.Dispose();

                // Clear event aggregator
                _eventAggregator?.Clear();
                
            }
            catch
            {
                // Error during cleanup
            }
        }
        
        #endregion
    }
}