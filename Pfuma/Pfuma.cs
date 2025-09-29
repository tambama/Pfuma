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
        
        [Parameter("Gauntlet", DefaultValue = false, Group = "Patterns")]
        public bool ShowGauntlet { get; set; }
        
        [Parameter("Order Flow", DefaultValue = false, Group = "Patterns")]
        public bool ShowOrderFlow { get; set; }
        
        [Parameter("Liquidity Sweep", DefaultValue = false, Group = "Patterns")]
        public bool ShowLiquiditySweep { get; set; }
        
        [Parameter("Rejection Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowRejectionBlock { get; set; }
        
        [Parameter("Order Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowOrderBlock { get; set; }
        
        [Parameter("Breaker Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowBreakerBlock { get; set; }
        
        [Parameter("CISD", DefaultValue = false, Group = "Patterns")]
        public bool ShowCISD { get; set; }
        
        //[Parameter("Max CISD", DefaultValue = 2, Group = "Patterns")]
        public int MaxCisdsPerDirection { get; set; } = 1;
        
        [Parameter("Propulsion Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowPropulsionBlock { get; set; }
        
        [Parameter("Unicorn", DefaultValue = false, Group = "Patterns")]
        public bool ShowUnicorn { get; set; }

        // Venom parameters
        [Parameter("Show Venom", DefaultValue = false, Group = "Venom")]
        public bool ShowVenom { get; set; }
        
        [Parameter("Show Confirmed Venom Only", DefaultValue = false, Group = "Venom")]
        public bool ShowConfirmedVenom { get; set; }

        [Parameter("Show Entries", DefaultValue = false, Group = "Venom")]
        public bool ShowEntries { get; set; }

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
        
        // Time Management
        [Parameter("Macros", DefaultValue = false, Group = "Time")]
        public bool ShowMacros { get; set; }
        
        [Parameter("Macro Filter", DefaultValue = false, Group = "Time")]
        public bool MacroFilter { get; set; }
        
        [Parameter("Daily Levels", DefaultValue = true, Group = "Time")]
        public bool ShowDailyLevels { get; set; }
        
        [Parameter("Session Levels", DefaultValue = true, Group = "Time")]
        public bool ShowSessionLevels { get; set; }
        
        [Parameter("Cycle Fib Levels", DefaultValue = false, Group = "Fibonacci")]
        public bool ShowCycleFibLevels { get; set; }
        
        [Parameter("CISD Fib Levels", DefaultValue = false, Group = "Fibonacci")]
        public bool ShowCISDFibLevels { get; set; }
        
        [Parameter("Extended Fib", DefaultValue = true, Group = "Fibonacci")]
        public bool ShowExtendedFib { get; set; }
        
        [Parameter("Remove Fib Extensions", DefaultValue = false, Group = "Fibonacci")]
        public bool RemoveFibExtensions { get; set; }
        
        [Parameter("UTC Offset", DefaultValue = -4, Group = "Time")]
        public int UtcOffset { get; set; }
        
        // Notifications
        [Parameter("Enable Log", DefaultValue = false, Group = "Notifications")]
        public bool EnableLog { get; set; }
        
        [Parameter("Enable Telegram", DefaultValue = false, Group = "Notifications")]
        public bool EnableTelegram { get; set; }
        
        [Parameter("Notify Liquidity Sweep", DefaultValue = false, Group = "Notifications")]
        public bool NotifyLiquiditySweep { get; set; }
        
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
        private RejectionBlockDetector _rejectionBlockDetector;
        private OrderBlockDetector _orderBlockDetector;
        private BreakerBlockDetector _breakerBlockDetector;
        private CisdDetector _cisdDetector;
        private UnicornDetector _unicornDetector;
        private PropulsionBlockDetector _propulsionBlockDetector;
        private VenomDetector _venomDetector;
        
        // Visualizers
        private IVisualization<Level> _fvgVisualizer;
        private IVisualization<Level> _htfFvgVisualizer;
        private IVisualization<Level> _orderFlowVisualizer;
        private IVisualization<Level> _rejectionBlockVisualizer;
        private IVisualization<Level> _orderBlockVisualizer;
        private IVisualization<Level> _breakerBlockVisualizer;
        private IVisualization<Level> _cisdVisualizer;
        private IVisualization<Level> _unicornVisualizer;
        private IVisualization<Level> _propulsionBlockVisualizer;
        private IVisualization<Level> _venomVisualizer;
        private StatsVisualizer _statsVisualizer;

        // Bar tracking
        private Bar _previousBar;
        private int _previousBarIndex;
        
        #endregion
        
        #region Initialization
        
        protected override void Initialize()
        {
            try
            {
                Chart.RemoveAllObjects();
                
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
                    ShowGauntlet = ShowGauntlet,  // Show the 2 most recent FVGs
                    ShowHtfFvg = ShowHtfFvg,  // Only for HTF FVG visualization
                    ShowOrderFlow = ShowOrderFlow,  // Only for regular orderflow
                    ShowHtfOrderFlow = ShowHtfOrderFlow,  // Only for HTF orderflow
                    ShowLiquiditySweep = ShowLiquiditySweep,
                    ShowRejectionBlock = ShowRejectionBlock,
                    ShowOrderBlock = ShowOrderBlock,
                    ShowBreakerBlock = ShowBreakerBlock,
                    ShowCISD = ShowCISD,
                    MaxCisdsPerDirection = MaxCisdsPerDirection,
                    ShowPropulsionBlock = ShowPropulsionBlock,
                    ShowVenom = ShowVenom,
                    ShowConfirmedVenom = ShowConfirmedVenom,
                    ShowUnicorn = ShowUnicorn,
                    ShowQuadrants = ShowQuadrants,
                    ShowInsideKeyLevel = ShowInsideKeyLevel,
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
                    EnableTelegram = EnableTelegram,
                    NotifyLiquiditySweep = NotifyLiquiditySweep
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
            _notificationService = new NotificationService(
                EnableLog,
                EnableTelegram,
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
            _orderFlowVisualizer = new OrderFlowVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _rejectionBlockVisualizer = new RejectionBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _orderBlockVisualizer = new OrderBlockVisualizer(Chart, _settings.Visualization, _eventAggregator, EnableLog ? Print : null);
            _breakerBlockVisualizer = new BreakerBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _cisdVisualizer = new CisdVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _unicornVisualizer = new UnicornVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _propulsionBlockVisualizer = new PropulsionBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _venomVisualizer = new VenomVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
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
                ShowMacros, ShowDailyLevels, ShowSessionLevels, UtcOffset);
            
            // Now pass TimeManager to SwingPointDetector
            _swingPointDetector = new SwingPointDetector(_swingPointManager, _candleManager, _eventAggregator, _timeManager);
            
            // Initialize liquidity manager
            _liquidityManager = new LiquidityManager(Chart, _candleManager, _eventAggregator, _levelRepository, _swingPointRepository, _settings, _notificationService, EnableLog ? Print : null);
            
            // Initialize Fibonacci service and visualizer
            _fibonacciService = new FibonacciService(_eventAggregator);
            _fibonacciVisualizer = new FibonacciVisualizer(Chart, _fibonacciService, _eventAggregator, _candleManager, ShowCycleFibLevels, ShowCISDFibLevels, ShowExtendedFib, RemoveFibExtensions, EnableLog ? Print : null);
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
            
            _rejectionBlockDetector = new RejectionBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _rejectionBlockVisualizer, _swingPointDetector, _settings, EnableLog ? Print : null);
            
            _orderBlockDetector = new OrderBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _orderBlockVisualizer, _swingPointManager, _swingPointRepository, _settings, EnableLog ? Print : null);
            
            
            _breakerBlockDetector = new BreakerBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _levelRepository, _breakerBlockVisualizer, _settings, EnableLog ? Print : null);
            
            _cisdDetector = new CisdDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _cisdVisualizer, _fibonacciService, _fibonacciVisualizer, _swingPointRepository, _timeManager, _settings, EnableLog ? Print : null);
            
            _unicornDetector = new UnicornDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _levelRepository, _unicornVisualizer, _settings, EnableLog ? Print : null);
            
            _propulsionBlockDetector = new PropulsionBlockDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _propulsionBlockVisualizer, _swingPointRepository, _settings, EnableLog ? Print : null);
            
            _venomDetector = new VenomDetector(
                Chart, _candleManager, _eventAggregator, _levelRepository, _venomVisualizer, _settings, EnableLog ? Print : null);
            
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
            _unicornDetector.Initialize();
            _propulsionBlockDetector.Initialize();
            _venomDetector.Initialize();
            
            // Initialize liquidity manager and visualizers
            _liquidityManager.Initialize();
            (_orderBlockVisualizer as OrderBlockVisualizer)?.Initialize();
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
            _eventAggregator.Subscribe<VenomConfirmedEvent>(OnVenomConfirmed);

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
                
                // 1. Process time-based features
                if (index < Bars.Count && Bars[index] != null)
                {
                    _timeManager?.ProcessBar(index, Bars[index].OpenTime);
                }
                
                // Update and draw Fibonacci levels if enabled
                if (_fibonacciVisualizer != null)
                {
                    _fibonacciVisualizer.ShowCycleFibLevels = ShowCycleFibLevels;
                    _fibonacciVisualizer.ShowCISDFibLevels = ShowCISDFibLevels;
                    _fibonacciVisualizer.ShowExtendedFib = ShowExtendedFib;
                    if (ShowCycleFibLevels || ShowCISDFibLevels)
                    {
                        _fibonacciVisualizer.DrawFibonacciLevels();
                    }
                }
                
                // 2. Check CISD activation on previous bar
                if (ShowCISD && _cisdDetector != null && index > 1)
                {
                    CheckCisdActivation(_previousBar, _previousBarIndex);
                }
                
                // 3. Detect swing points
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
                
                // 6. Detect patterns - Always detect FVGs (ShowFVG only controls visualization)
                _fvgDetector?.Detect(_previousBarIndex);
                
                // Detect HTF FVGs (only if enabled and timeframes configured)
                if (ShowHtfFvg && !string.IsNullOrEmpty(Timeframes))
                {
                    _htfFvgDetector?.Detect(_previousBarIndex);
                }
                
                
                if (ShowRejectionBlock)
                {
                    _rejectionBlockDetector?.Detect(_previousBarIndex);
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
            if (evt.OrderBlock != null && evt.OrderBlock.Score >= 5)
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
            if (evt.CisdLevel != null && evt.CisdLevel.Score >= 5)
            {
                var orderBlock = evt.CisdLevel;
                var color = orderBlock.Direction == Direction.Up ? Color.Green : Color.Red;
                var price = orderBlock.Direction == Direction.Up ? orderBlock.Low : orderBlock.High;
                var time = orderBlock.Direction == Direction.Up ? orderBlock.LowTime : orderBlock.HighTime;
                Chart.DrawIcon($"macro-{time}", ChartIconType.Square,
                    time, price, color);
            }
        }

        private void OnVenomConfirmed(VenomConfirmedEvent evt)
        {
            if ((!ShowEntries && !ShowSignals) || evt?.Venom == null)
                return;

            var venom = evt.Venom;

            // Only process confirmed Venom patterns
            if (!venom.IsConfirmed)
                return;

            // Use the confirming swing point's index for the signal
            // This ensures signals appear at the bar where they're actually confirmed
            var currentIndex = evt.ConfirmingSwingPointIndex;
            var currentTime = currentIndex < Bars.Count ? Bars[currentIndex].OpenTime : DateTime.Now;

            double entry, stop;

            // Determine entry and stop based on Venom direction
            if (venom.Direction == Direction.Up) // Bullish Venom
            {
                entry = venom.High;
                stop = venom.IndexLowPrice;
            }
            else // Bearish Venom
            {
                entry = venom.Low;
                stop = venom.IndexHighPrice;
            }

            // Populate output data series if ShowEntries is enabled
            if (ShowEntries)
            {
                Entries[currentIndex] = entry;
                Stops[currentIndex] = stop;
            }

            // Create signal if ShowSignals is enabled
            if (ShowSignals && _signalManager != null)
            {
                _signalManager.AddSignal(entry, stop, venom.Direction, RiskReward, currentTime, currentIndex);
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
                _rejectionBlockDetector?.Dispose();
                _orderBlockDetector?.Dispose();
                _breakerBlockDetector?.Dispose();
                _cisdDetector?.Dispose();
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
                _unicornVisualizer?.Clear();
                _fibonacciVisualizer?.Dispose();
                
                // Clear repositories
                _swingPointRepository?.Clear();
                _levelRepository?.Clear();
                
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