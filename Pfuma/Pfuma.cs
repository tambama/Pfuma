using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using Pfuma.Analyzers;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors;
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
        [Parameter("Show Swing Points", DefaultValue = true, Group = "Swing Points")]
        public bool ShowSwingPoints { get; set; }
        
        // Pattern Detection
        [Parameter("Show FVG", DefaultValue = false, Group = "Patterns")]
        public bool ShowFVG { get; set; }
        
        [Parameter("Show Order Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowOrderBlock { get; set; }
        
        [Parameter("Show Order Flow", DefaultValue = false, Group = "Patterns")]
        public bool ShowOrderFlow { get; set; }
        
        [Parameter("Show Liquidity Sweep", DefaultValue = false, Group = "Patterns")]
        public bool ShowLiquiditySweep { get; set; }
        
        [Parameter("Show Rejection Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowRejectionBlock { get; set; }
        
        [Parameter("Show Breaker Blocks", DefaultValue = false, Group = "Patterns")]
        public bool ShowBreakerBlock { get; set; }
        
        [Parameter("Show CISD", DefaultValue = false, Group = "Patterns")]
        public bool ShowCISD { get; set; }
        
        [Parameter("Max CISD", DefaultValue = 2, Group = "Patterns")]
        public int MaxCisdsPerDirection { get; set; }
        
        [Parameter("Show Gauntlet", DefaultValue = false, Group = "Patterns")]
        public bool ShowGauntlet { get; set; }
        
        [Parameter("Show Unicorn", DefaultValue = false, Group = "Patterns")]
        public bool ShowUnicorn { get; set; }
        
        // Market Structure
        [Parameter("Show Market Structure", DefaultValue = false, Group = "Market Structure")]
        public bool ShowMarketStructure { get; set; }
        
        [Parameter("Show Structure", DefaultValue = false, Group = "Market Structure")]
        public bool ShowStructure { get; set; }
        
        [Parameter("Show CHoCH", DefaultValue = false, Group = "Market Structure")]
        public bool ShowChoch { get; set; }
        
        [Parameter("Show Standard Deviation", DefaultValue = false, Group = "Market Structure")]
        public bool ShowStandardDeviation { get; set; }
        
        // Visualization
        [Parameter("Show Quadrants", DefaultValue = false, Group = "Visualization")]
        public bool ShowQuadrants { get; set; }
        
        [Parameter("Show Inside Key Level", DefaultValue = false, Group = "Visualization")]
        public bool ShowInsideKeyLevel { get; set; }
        
        // Time Management
        [Parameter("Show Macros", DefaultValue = false, Group = "Time")]
        public bool ShowMacros { get; set; }
        
        [Parameter("Macro Filter", DefaultValue = false, Group = "Time")]
        public bool MacroFilter { get; set; }
        
        [Parameter("Show Fibonacci Levels", DefaultValue = false, Group = "Time")]
        public bool ShowFibonacciLevels { get; set; }
        
        [Parameter("UTC Offset", DefaultValue = -4, Group = "Time")]
        public int UtcOffset { get; set; }
        
        // SMT
        [Parameter("Show SMT", DefaultValue = false, Group = "SMT")]
        public bool ShowSMT { get; set; }
        
        [Parameter("SMT Pair", DefaultValue = "", Group = "SMT")]
        public string SMTPair { get; set; }
        
        // Notifications
        [Parameter("Enable Log", DefaultValue = false, Group = "Notifications")]
        public bool EnableLog { get; set; }
        
        [Parameter("Enable Telegram", DefaultValue = false, Group = "Notifications")]
        public bool EnableTelegram { get; set; }
        
        #endregion
        
        #region Output Series
        
        [Output("Swing Highs", LineColor = "Lime", PlotType = PlotType.Points)]
        public IndicatorDataSeries SwingHighs { get; set; }
        
        [Output("Swing Lows", LineColor = "Red", PlotType = PlotType.Points)]
        public IndicatorDataSeries SwingLows { get; set; }
        
        [Output("Higher Highs", LineColor = "Green", PlotType = PlotType.Points)]
        public IndicatorDataSeries HigherHighs { get; set; }
        
        [Output("Lower Highs", LineColor = "Orange", PlotType = PlotType.Points)]
        public IndicatorDataSeries LowerHighs { get; set; }
        
        [Output("Lower Lows", LineColor = "Red", PlotType = PlotType.Points)]
        public IndicatorDataSeries LowerLows { get; set; }
        
        [Output("Higher Lows", LineColor = "Blue", PlotType = PlotType.Points)]
        public IndicatorDataSeries HigherLows { get; set; }
        
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
        private MarketStructureAnalyzer _marketStructureAnalyzer;
        private PdArrayAnalyzer _pdArrayAnalyzer;
        
        // Repositories
        private IRepository<SwingPoint> _swingPointRepository;
        private IRepository<Level> _levelRepository;
        
        // Detectors
        private FvgDetector _fvgDetector;
        private OrderBlockDetector _orderBlockDetector;
        private OrderFlowDetector _orderFlowDetector;
        private RejectionBlockDetector _rejectionBlockDetector;
        private BreakerBlockDetector _breakerBlockDetector;
        private CisdDetector _cisdDetector;
        private UnicornDetector _unicornDetector;
        private GauntletDetector _gauntletDetector;
        
        // Visualizers
        private IVisualization<Level> _fvgVisualizer;
        private IVisualization<Level> _orderBlockVisualizer;
        private IVisualization<Level> _orderFlowVisualizer;
        private IVisualization<Level> _rejectionBlockVisualizer;
        private IVisualization<Level> _breakerBlockVisualizer;
        private IVisualization<Level> _cisdVisualizer;
        private IVisualization<Level> _unicornVisualizer;
        private IVisualization<Level> _gauntletVisualizer;
        
        // Bar tracking
        private Bar _previousBar;
        private int _previousBarIndex;
        
        #endregion
        
        #region Initialization
        
        protected override void Initialize()
        {
            try
            {
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
                
                if (EnableLog)
                    Print("Pfuma indicator initialized successfully");
            }
            catch (Exception ex)
            {
                Print($"Error initializing Pfuma: {ex.Message}");
                _isInitialized = false;
            }
        }
        
        private void InitializeConfiguration()
        {
            _settings = new IndicatorSettings
            {
                Patterns = new PatternDetectionSettings
                {
                    ShowFVG = ShowFVG,
                    ShowOrderBlock = ShowOrderBlock,
                    ShowOrderFlow = ShowOrderFlow,
                    ShowLiquiditySweep = ShowLiquiditySweep,
                    ShowRejectionBlock = ShowRejectionBlock,
                    ShowBreakerBlock = ShowBreakerBlock,
                    ShowCISD = ShowCISD,
                    MaxCisdsPerDirection = MaxCisdsPerDirection,
                    ShowGauntlet = ShowGauntlet,
                    ShowUnicorn = ShowUnicorn,
                    ShowQuadrants = ShowQuadrants,
                    ShowInsideKeyLevel = ShowInsideKeyLevel,
                    ShowSMT = ShowSMT,
                    SmtPair = SMTPair
                },
                MarketStructure = new MarketStructureSettings
                {
                    ShowMarketStructure = ShowMarketStructure,
                    ShowStructure = ShowStructure,
                    ShowChoch = ShowChoch,
                    ShowStandardDeviation = ShowStandardDeviation
                },
                Time = new TimeSettings
                {
                    ShowMacros = ShowMacros,
                    MacroFilter = MacroFilter,
                    ShowFibonacciLevels = ShowFibonacciLevels,
                    UtcOffset = UtcOffset
                },
                Notifications = new NotificationSettings
                {
                    EnableLog = EnableLog,
                    EnableTelegram = EnableTelegram
                },
                Visualization = new VisualizationSettings()
            };
            
            // Connect settings references
            _settings.Visualization.Patterns = _settings.Patterns;
            _settings.Visualization.Notifications = _settings.Notifications;
        }
        
        private void InitializeCoreServices()
        {
            _eventAggregator = new EventAggregator();
            _notificationService = new NotificationService(Symbol.Name, _settings.Time.UtcOffset, EnableLog ? Print : null);
        }
        
        private void InitializeRepositories()
        {
            _swingPointRepository = new SwingPointRepository();
            _levelRepository = new LevelRepository();
        }
        
        private void InitializeVisualizers()
        {
            _fvgVisualizer = new FvgVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _orderBlockVisualizer = new OrderBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _orderFlowVisualizer = new OrderFlowVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _rejectionBlockVisualizer = new RejectionBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _breakerBlockVisualizer = new BreakerBlockVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _cisdVisualizer = new CisdVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _unicornVisualizer = new UnicornVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
            _gauntletVisualizer = new GauntletVisualizer(Chart, _settings.Visualization, EnableLog ? Print : null);
        }
        
        private void InitializeServicesAndAnalyzers()
        {
            // Initialize swing point detector
            _swingPointDetector = new SwingPointDetector(SwingHighs, SwingLows);
            
            // Initialize time manager
            _timeManager = new TimeManager(
                Chart, Bars, _swingPointDetector, _notificationService,
                ShowMacros, ShowFibonacciLevels, UtcOffset);
            
            // Initialize market structure analyzer
            if (ShowMarketStructure)
            {
                _marketStructureAnalyzer = new MarketStructureAnalyzer(
                    Chart, _eventAggregator, HigherHighs, LowerHighs, 
                    LowerLows, HigherLows, _settings, EnableLog ? Print : null);
                _marketStructureAnalyzer.Initialize();
            }
            
            // Initialize PD Array Analyzer for SMT coordination
            if (ShowSMT)
            {
                _pdArrayAnalyzer = new PdArrayAnalyzer(
                    Chart, _eventAggregator, _settings, EnableLog ? Print : null);
                _pdArrayAnalyzer.Initialize();
            }
        }
        
        private void InitializeDetectors()
        {
            // Pattern detectors
            _fvgDetector = new FvgDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _fvgVisualizer, _settings, EnableLog ? Print : null);
            
            _orderBlockDetector = new OrderBlockDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _orderBlockVisualizer, _swingPointDetector, _settings, EnableLog ? Print : null);
            
            _orderFlowDetector = new OrderFlowDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _orderFlowVisualizer, _swingPointDetector, _settings, EnableLog ? Print : null);
            
            _rejectionBlockDetector = new RejectionBlockDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _rejectionBlockVisualizer, _settings, EnableLog ? Print : null);
            
            _breakerBlockDetector = new BreakerBlockDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _levelRepository, _breakerBlockVisualizer, _settings, EnableLog ? Print : null);
            
            _cisdDetector = new CisdDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _cisdVisualizer, _settings, EnableLog ? Print : null);
            
            _unicornDetector = new UnicornDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _levelRepository, _unicornVisualizer, _settings, EnableLog ? Print : null);
            
            // Gauntlet detector needs time manager
            _gauntletDetector = new GauntletDetector(
                Chart, Bars, _eventAggregator, _levelRepository, _levelRepository, 
                _gauntletVisualizer, _timeManager, _settings, EnableLog ? Print : null);
            
            // Initialize all detectors
            _fvgDetector.Initialize();
            _orderBlockDetector.Initialize();
            _orderFlowDetector.Initialize();
            _rejectionBlockDetector.Initialize();
            _breakerBlockDetector.Initialize();
            _cisdDetector.Initialize();
            _unicornDetector.Initialize();
            _gauntletDetector.Initialize();
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
        }
        
        #endregion
        
        #region Calculate Method
        
        public override void Calculate(int index)
        {
            if (!_isInitialized || index <= 1)
                return;
            
            try
            {
                _previousBar = Bars[index - 1];
                _previousBarIndex = index - 1;
                
                // Create candle object from previous bar
                var candle = new Candle(_previousBar, _previousBarIndex, TimeFrame);
                
                // 1. Process time-based features
                _timeManager?.ProcessBar(index, Bars[index].OpenTime);
                
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
                
                // 4. Check Fibonacci level sweeps
                if (ShowFibonacciLevels && _timeManager != null)
                {
                    _timeManager.CheckFibonacciSweeps(_previousBar, _previousBarIndex);
                }
                
                // 5. Check for special liquidity sweeps (PDH, PDL, PSH, PSL)
                // Note: Regular swing point liquidity sweeps are handled by OrderFlowDetector
                if (ShowLiquiditySweep && _swingPointDetector != null)
                {
                    // This functionality could be moved to TimeManager or SwingPointDetector
                    // For now, liquidity sweep detection for special points is handled via events
                }
                
                // 6. Detect patterns
                if (ShowFVG)
                {
                    _fvgDetector?.Detect(Bars, index);
                }
                
                if (ShowOrderBlock)
                {
                    _orderBlockDetector?.Detect(Bars, index);
                }
                
                if (ShowRejectionBlock)
                {
                    _rejectionBlockDetector?.Detect(Bars, index);
                }
                
                // 7. Update market structure (handled via events)
                
                // 8. Initialize detectors with swing points when ready
                if (!_detectorsInitialized && _swingPointRepository.Count() >= 3)
                {
                    InitializeDetectorsWithSwingPoints();
                    _detectorsInitialized = true;
                }
                
                // 9. Check for inside key levels on swing points
                if (ShowInsideKeyLevel && ShowQuadrants)
                {
                    CheckInsideKeyLevels();
                }
            }
            catch (Exception ex)
            {
                if (EnableLog)
                    Print($"Error in Calculate: {ex.Message}");
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
                    _eventAggregator.Publish(new CisdActivatedEvent(cisd));
                    
                    if (EnableLog)
                        Print($"CISD activated: {cisd.Direction} at index {previousBarIndex}");
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
            
            if (EnableLog)
                Print($"Initialized detectors with {swingPoints.Count} swing points");
        }
        
        /// <summary>
        /// Check if swing points are inside key levels (quadrants)
        /// </summary>
        private void CheckInsideKeyLevels()
        {
            var swingPoints = _swingPointRepository.GetAll();
            var pdArrays = _levelRepository.Find(l => 
                (l.LevelType == LevelType.OrderBlock || 
                 l.LevelType == LevelType.RejectionBlock) && 
                l.IsActive);
            
            foreach (var swingPoint in swingPoints)
            {
                foreach (var pdArray in pdArrays)
                {
                    // Check if swing point sweeps any quadrants
                    var sweptQuadrants = pdArray.CheckForSweptQuadrants(swingPoint);
                    
                    if (sweptQuadrants.Count > 0 && !swingPoint.InsideKeyLevel)
                    {
                        swingPoint.InsideKeyLevel = true;
                        swingPoint.SweptKeyLevel = pdArray;
                        
                        // Draw inside key level icon
                        if (_orderFlowVisualizer != null)
                        {
                            DrawInsideKeyLevelIcon(swingPoint);
                        }
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
                
                // Check for SMT divergence if enabled
                if (ShowSMT && _pdArrayAnalyzer != null && _swingPointRepository.Count() > 1)
                {
                    var previousPoint = _swingPointRepository
                        .Find(sp => sp.Direction == evt.SwingPoint.Direction && 
                                   sp.Index < evt.SwingPoint.Index)
                        .OrderByDescending(sp => sp.Index)
                        .FirstOrDefault();
                    
                    if (previousPoint != null)
                    {
                        _pdArrayAnalyzer.CheckForSmtDivergence(previousPoint, evt.SwingPoint);
                    }
                }
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
            return _levelRepository?.GetByType(LevelType.FairValueGap) ?? new List<Level>();
        }
        
        /// <summary>
        /// Gets all detected Order Blocks
        /// </summary>
        public List<Level> GetAllOrderBlocks()
        {
            return _levelRepository?.GetByType(LevelType.OrderBlock) ?? new List<Level>();
        }
        
        /// <summary>
        /// Gets all order flows (with liquidity sweeps)
        /// </summary>
        public List<Level> GetAllOrderFlows()
        {
            return _levelRepository?.GetByType(LevelType.Orderflow) ?? new List<Level>();
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
        /// Gets market bias
        /// </summary>
        public Direction GetMarketBias()
        {
            return _marketStructureAnalyzer?.GetBias() ?? Direction.Up;
        }
        
        #endregion
        
        #region Cleanup
        
        protected override void OnDestroy()
        {
            try
            {
                // Dispose detectors
                _fvgDetector?.Dispose();
                _orderBlockDetector?.Dispose();
                _orderFlowDetector?.Dispose();
                _rejectionBlockDetector?.Dispose();
                _breakerBlockDetector?.Dispose();
                _cisdDetector?.Dispose();
                _unicornDetector?.Dispose();
                _gauntletDetector?.Dispose();
                
                // Dispose analyzers
                _marketStructureAnalyzer?.Dispose();
                _pdArrayAnalyzer?.Dispose();
                
                // Clear visualizers
                _fvgVisualizer?.Clear();
                _orderBlockVisualizer?.Clear();
                _orderFlowVisualizer?.Clear();
                _rejectionBlockVisualizer?.Clear();
                _breakerBlockVisualizer?.Clear();
                _cisdVisualizer?.Clear();
                _unicornVisualizer?.Clear();
                _gauntletVisualizer?.Clear();
                
                // Clear repositories
                _swingPointRepository?.Clear();
                _levelRepository?.Clear();
                
                // Clear event aggregator
                _eventAggregator?.Clear();
                
                if (EnableLog)
                    Print("Pfuma indicator destroyed");
            }
            catch (Exception ex)
            {
                Print($"Error in OnDestroy: {ex.Message}");
            }
        }
        
        #endregion
    }
}