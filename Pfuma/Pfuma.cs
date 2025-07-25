using System;
using System.Collections.Generic;
using cAlgo.API;
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
        
        // Market Structure
        [Parameter("Show Structure", DefaultValue = false, Group = "Market Structure")]
        public bool ShowStructure { get; set; }
        
        [Parameter("Show CHoCH", DefaultValue = false, Group = "Market Structure")]
        public bool ShowChoch { get; set; }
        
        // Time Management
        [Parameter("Show Macros", DefaultValue = false, Group = "Time")]
        public bool ShowMacros { get; set; }
        
        [Parameter("UTC Offset", DefaultValue = -4, Group = "Time")]
        public int UtcOffset { get; set; }
        
        // Notifications
        [Parameter("Enable Log", DefaultValue = false, Group = "Notifications")]
        public bool EnableLog { get; set; }
        
        [Parameter("Enable Telegram", DefaultValue = false, Group = "Notifications")]
        public bool EnableTelegram { get; set; }
        
        [Parameter("Telegram Chat ID", DefaultValue = "", Group = "Notifications")]
        public string TelegramChatId { get; set; }
        
        [Parameter("Telegram Token", DefaultValue = "", Group = "Notifications")]
        public string TelegramToken { get; set; }
        
        #endregion
        
        #region Outputs
        
        [Output("Swing High", Color = Colors.White, PlotType = PlotType.Points, Thickness = 1)]
        public IndicatorDataSeries SwingHighs { get; set; }
        
        [Output("Swing Low", Color = Colors.White, PlotType = PlotType.Points, Thickness = 1)]
        public IndicatorDataSeries SwingLows { get; set; }
        
        #endregion
        
        #region Private Fields
        
        // Core Services
        private IEventAggregator _eventAggregator;
        private IndicatorSettings _settings;
        
        // Repositories
        private SwingPointRepository _swingPointRepository;
        private LevelRepository _levelRepository;
        
        // Detectors
        private SwingPointDetector _swingPointDetector;
        private FvgDetector _fvgDetector;
        private OrderBlockDetector _orderBlockDetector;
        
        // Visualizers
        private FvgVisualizer _fvgVisualizer;
        private OrderBlockVisualizer _orderBlockVisualizer;
        
        // Services
        private NotificationService _notificationService;
        private TimeManager _timeManager;
        private MarketStructureAnalyzer _marketStructureAnalyzer;
        
        // State
        private Bar _previousBar;
        private int _previousBarIndex;
        private bool _isInitialized = false;
        
        #endregion
        
        protected override void Initialize()
        {
            try
            {
                // Clear chart
                Chart.RemoveAllObjects();
                
                // Initialize settings from parameters
                InitializeSettings();
                
                // Initialize core services
                InitializeCoreServices();
                
                // Initialize repositories
                InitializeRepositories();
                
                // Initialize visualizers
                InitializeVisualizers();
                
                // Initialize detectors
                InitializeDetectors();
                
                // Initialize analyzers
                InitializeAnalyzers();
                
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
                
                // Process time-based features
                _timeManager?.ProcessBar(index, Bars[index].OpenTime);
                
                // Detect swing points
                if (ShowSwingPoints)
                {
                    _swingPointDetector?.ProcessBar(_previousBarIndex, candle);
                }
                
                // Detect patterns
                if (ShowFVG)
                {
                    _fvgDetector?.Detect(Bars, index);
                }
                
                if (ShowOrderBlock)
                {
                    _orderBlockDetector?.Detect(Bars, index);
                }
                
                // Update market structure
                // Note: This would be called via events in the full implementation
                
                // On the last bar, update relationships
                if (index == Bars.Count - 1)
                {
                    _swingPointDetector?.UpdateSwingPointRelationships();
                }
            }
            catch (Exception ex)
            {
                if (EnableLog)
                    Print($"Error in Calculate at index {index}: {ex.Message}");
            }
        }
        
        #region Initialization Methods
        
        private void InitializeSettings()
        {
            _settings = new IndicatorSettings
            {
                SwingPoints = new SwingPointSettings
                {
                    ShowSwingPoints = ShowSwingPoints,
                },
                Patterns = new PatternDetectionSettings
                {
                    ShowFVG = ShowFVG,
                    ShowOrderBlock = ShowOrderBlock,
                    ShowOrderFlow = ShowOrderFlow,
                    ShowLiquiditySweep = ShowLiquiditySweep
                },
                Visualization = new VisualizationSettings
                {
                    ShowStructure = ShowStructure,
                    ShowChoch = ShowChoch,
                    ShowMacros = ShowMacros
                },
                Notifications = new NotificationSettings
                {
                    EnableLog = EnableLog,
                    EnableTelegram = EnableTelegram,
                    TelegramChatId = TelegramChatId,
                    TelegramToken = TelegramToken
                },
                Time = new TimeSettings
                {
                    UtcOffset = UtcOffset
                }
            };

            _settings.Visualization.Patterns = _settings.Patterns;
            _settings.Visualization.Notifications = _settings.Notifications;
        }
        
        private void InitializeCoreServices()
        {
            _eventAggregator = new EventAggregator(EnableLog ? Print : null);
            
            _notificationService = new NotificationService(
                EnableLog,
                EnableTelegram,
                TelegramChatId,
                TelegramToken,
                Symbol.Name,
                UtcOffset,
                Print);
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
        }
        
        private void InitializeDetectors()
        {
            // Initialize swing point detector
            _swingPointDetector = new SwingPointDetector(SwingHighs, SwingLows);
            
            // Initialize FVG detector
            _fvgDetector = new FvgDetector(
                Chart,
                Bars,
                _eventAggregator,
                _levelRepository,
                _fvgVisualizer,
                _settings,
                EnableLog ? Print : null);
            _fvgDetector.Initialize();
            
            // Initialize Order Block detector
            _orderBlockDetector = new OrderBlockDetector(
                Chart,
                Bars,
                _eventAggregator,
                _levelRepository,
                _orderBlockVisualizer,
                _swingPointDetector,
                _settings,
                EnableLog ? Print : null);
            _orderBlockDetector.Initialize();
        }
        
        private void InitializeAnalyzers()
        {
            // Initialize time manager
            _timeManager = new TimeManager(
                Chart,
                Bars,
                _swingPointDetector,
                _notificationService,
                ShowMacros,
                false, // ShowFibLevels
                UtcOffset);
            
            // Initialize market structure analyzer
            // This would be initialized here in the full implementation
        }
        
        private void SubscribeToEvents()
        {
            // Subscribe to swing point events
            if (_swingPointDetector != null)
            {
                _swingPointDetector.SwingPointRemoved += OnSwingPointRemoved;
                _swingPointDetector.LiquiditySwept += OnLiquiditySwept;
            }
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
        
        #endregion
        
        #region Cleanup
        
        protected override void OnDestroy()
        {
            try
            {
                // Dispose detectors
                _fvgDetector?.Dispose();
                _orderBlockDetector?.Dispose();
                
                // Clear visualizers
                _fvgVisualizer?.Clear();
                _orderBlockVisualizer?.Clear();
                
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