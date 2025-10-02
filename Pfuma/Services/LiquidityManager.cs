using System;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Services;
using Pfuma.Services.Time;
using Pfuma.Visualization;

namespace Pfuma.Services
{
    /// <summary>
    /// Manages liquidity sweeps of order blocks when swing points are created.
    /// Handles the deactivation of order blocks when price moves beyond their levels.
    /// </summary>
    public class LiquidityManager
    {
        private readonly Chart _chart;
        private readonly CandleManager _candleManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRepository<Level> _levelRepository;
        private readonly IRepository<SwingPoint> _swingPointRepository;
        private readonly IndicatorSettings _settings;
        private readonly NotificationService _notificationService;
        private readonly Action<string> _logger;

        // Track the last drawn inside key level dot
        private string _lastInsideKeyLevelDotId;

        // Optional cycle manager and visualizer for cycle liquidity sweeps
        private Cycle30Manager _cycle30Manager;
        private Cycle30Visualizer _cycle30Visualizer;

        public LiquidityManager(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> levelRepository,
            IRepository<SwingPoint> swingPointRepository,
            IndicatorSettings settings,
            NotificationService notificationService,
            Action<string> logger = null)
        {
            _chart = chart ?? throw new ArgumentNullException(nameof(chart));
            _candleManager = candleManager ?? throw new ArgumentNullException(nameof(candleManager));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _levelRepository = levelRepository ?? throw new ArgumentNullException(nameof(levelRepository));
            _swingPointRepository = swingPointRepository ?? throw new ArgumentNullException(nameof(swingPointRepository));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger;
        }

        /// <summary>
        /// Initialize the liquidity manager and subscribe to swing point events
        /// </summary>
        public void Initialize()
        {
            _eventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            _eventAggregator.Subscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
            _eventAggregator.Subscribe<OrderBlockDetectedEvent>(OnOrderBlockDetected);
            _eventAggregator.Subscribe<CisdConfirmedEvent>(OnCisdConfirmed);
            _eventAggregator.Subscribe<RejectionBlockDetectedEvent>(OnRejectionBlockDetected);
        }

        /// <summary>
        /// Set cycle manager and visualizer for cycle liquidity sweep detection
        /// </summary>
        public void SetCycleComponents(Cycle30Manager cycle30Manager, Cycle30Visualizer cycle30Visualizer)
        {
            _cycle30Manager = cycle30Manager;
            _cycle30Visualizer = cycle30Visualizer;
        }

        /// <summary>
        /// Handle swing point detection events to check for liquidity sweeps
        /// </summary>
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            if (evt?.SwingPoint == null)
                return;

            var swingPoint = evt.SwingPoint;

            if (swingPoint.Direction == Direction.Up)
            {
                // Bullish swing point created - check for bearish order block liquidity sweeps
                HandleBullishSwingPointLiquiditySweep(swingPoint);

                // Check for bearish rejection block liquidity sweeps
                HandleBullishSwingPointRejectionBlockSweep(swingPoint);

                // Check for bearish CISD liquidity sweeps
                HandleBullishSwingPointCisdLiquiditySweep(swingPoint);
                
                // Check for HTF FVG quadrant sweeps by bullish swing point
                HandleBullishSwingPointHtfFvgQuadrantSweep(swingPoint);
                
                // Check for session/daily high liquidity sweeps by bullish swing point
                HandleBullishSwingPointSessionDailyHighLiquiditySweep(swingPoint);
                
                // Check if bullish swing point is inside a bearish order block
                CheckBullishSwingPointInsideOrderBlock(swingPoint);
                
                // Check if bullish swing point is inside a bearish CISD
                CheckBullishSwingPointInsideCisd(swingPoint);

                // Check if bullish swing point is inside a bearish rejection block
                CheckBullishSwingPointInsideRejectionBlock(swingPoint);

                // Check for cycle low liquidity sweeps by bullish swing point
                HandleBullishSwingPointCycleLiquiditySweep(swingPoint);
            }
            else if (swingPoint.Direction == Direction.Down)
            {
                // Bearish swing point created - check for bullish order block liquidity sweeps
                HandleBearishSwingPointLiquiditySweep(swingPoint);

                // Check for bullish rejection block liquidity sweeps
                HandleBearishSwingPointRejectionBlockSweep(swingPoint);

                // Check for bullish CISD liquidity sweeps
                HandleBearishSwingPointCisdLiquiditySweep(swingPoint);

                // Check for HTF FVG quadrant sweeps by bearish swing point
                HandleBearishSwingPointHtfFvgQuadrantSweep(swingPoint);

                // Check for session/daily low liquidity sweeps by bearish swing point
                HandleBearishSwingPointSessionDailyLowLiquiditySweep(swingPoint);

                // Check if bearish swing point is inside a bullish order block
                CheckBearishSwingPointInsideOrderBlock(swingPoint);

                // Check if bearish swing point is inside a bullish CISD
                CheckBearishSwingPointInsideCisd(swingPoint);

                // Check if bearish swing point is inside a bullish rejection block
                CheckBearishSwingPointInsideRejectionBlock(swingPoint);

                // Check for cycle high liquidity sweeps by bearish swing point
                HandleBearishSwingPointCycleLiquiditySweep(swingPoint);
            }
        }

        /// <summary>
        /// Handle swing point removal events
        /// </summary>
        private void OnSwingPointRemoved(SwingPointRemovedEvent evt)
        {
            // Inside key level status is automatically cleared when swing point is removed
        }

        /// <summary>
        /// Handle order block detection events to check for key level extension opportunities
        /// When an order block is detected, check if its boundary swing point is marked as inside key level
        /// If so, extend the swept key level to include the swing point's candle
        /// </summary>
        private void OnOrderBlockDetected(OrderBlockDetectedEvent evt)
        {
            if (evt?.OrderBlock == null)
                return;

            var orderBlock = evt.OrderBlock;

            if (orderBlock.Direction == Direction.Up)
            {
                // Bullish order block - check if swing low is inside key level
                HandleBullishOrderBlockDetection(orderBlock);
            }
            else if (orderBlock.Direction == Direction.Down)
            {
                // Bearish order block - check if swing high is inside key level  
                HandleBearishOrderBlockDetection(orderBlock);
            }
        }

        /// <summary>
        /// Handle rejection block detection events to check for key level extension opportunities
        /// When a rejection block is detected, check if its boundary swing point is marked as inside key level
        /// If so, extend the swept key level to include the swing point's candle
        /// </summary>
        private void OnRejectionBlockDetected(RejectionBlockDetectedEvent evt)
        {
            if (evt?.RejectionBlock == null)
                return;

            var rejectionBlock = evt.RejectionBlock;

            if (rejectionBlock.Direction == Direction.Up)
            {
                // Bullish rejection block - check if swing low is inside key level
                HandleBullishRejectionBlockDetection(rejectionBlock);
            }
            else if (rejectionBlock.Direction == Direction.Down)
            {
                // Bearish rejection block - check if swing high is inside key level
                HandleBearishRejectionBlockDetection(rejectionBlock);
            }
        }

        /// <summary>
        /// Handle CISD confirmation events to check for key level extension opportunities
        /// When a CISD is confirmed, check if its boundary swing point is marked as inside key level
        /// If so, extend the swept key level to include the swing point's candle
        /// </summary>
        private void OnCisdConfirmed(CisdConfirmedEvent evt)
        {
            if (evt?.CisdLevel == null)
                return;

            var cisdLevel = evt.CisdLevel;

            if (cisdLevel.Direction == Direction.Down)
            {
                // Bearish CISD confirmed - check if swing high at IndexHigh is inside key level
                HandleBearishCisdConfirmed(cisdLevel);
            }
            else if (cisdLevel.Direction == Direction.Up)
            {
                // Bullish CISD confirmed - check if swing low at IndexLow is inside key level
                HandleBullishCisdConfirmed(cisdLevel);
            }
        }

        /// <summary>
        /// Handle HTF FVG quadrant sweeps by bullish swing points
        /// Bullish swing points sweep quadrants of bearish HTF FVGs
        /// </summary>
        private void HandleBullishSwingPointHtfFvgQuadrantSweep(SwingPoint bullishSwingPoint)
        {
            try
            {
                // Get all active bearish HTF FVGs
                var activeBearishHtfFvgs = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.FairValueGap &&
                        level.Direction == Direction.Down &&
                        level.IsActive &&
                        level.TimeFrame != null && level.TimeFrame != _chart.TimeFrame)  // HTF FVGs have different timeframe
                    .ToList();

                foreach (var htfFvg in activeBearishHtfFvgs)
                {
                    // Check if swing point sweeps any quadrants
                    var sweptQuadrants = htfFvg.CheckForSweptQuadrants(bullishSwingPoint);
                    
                    if (sweptQuadrants.Any())
                    {
                        // Mark swing point as inside HTF FVG key level
                        bullishSwingPoint.InsidePda = true;
                        bullishSwingPoint.Pda = htfFvg;
                        
                        // Draw a dot on the swing point to indicate it's inside a key level
                        DrawInsideKeyLevelDot(bullishSwingPoint);
                        
                        // Mark swing point as inside HTF FVG key level
                        
                        // Check if all quadrants are swept (HTF FVG becomes inactive)
                        if (!htfFvg.IsActive)
                        {
                            // Remove the entire HTF FVG from chart (rectangle, quadrants, label)
                            RemoveHtfFvgFromChart(htfFvg);
                            // HTF FVG now inactive and removed from chart
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish swing point HTF FVG quadrant sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle HTF FVG quadrant sweeps by bearish swing points
        /// Bearish swing points sweep quadrants of bullish HTF FVGs
        /// </summary>
        private void HandleBearishSwingPointHtfFvgQuadrantSweep(SwingPoint bearishSwingPoint)
        {
            try
            {
                // Get all active bullish HTF FVGs
                var activeBullishHtfFvgs = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.FairValueGap &&
                        level.Direction == Direction.Up &&
                        level.IsActive &&
                        level.TimeFrame != null && level.TimeFrame != _chart.TimeFrame)  // HTF FVGs have different timeframe
                    .ToList();

                foreach (var htfFvg in activeBullishHtfFvgs)
                {
                    // Check if swing point sweeps any quadrants
                    var sweptQuadrants = htfFvg.CheckForSweptQuadrants(bearishSwingPoint);
                    
                    if (sweptQuadrants.Any())
                    {
                        // Mark swing point as inside HTF FVG key level
                        bearishSwingPoint.InsidePda = true;
                        bearishSwingPoint.Pda = htfFvg;
                        
                        // Draw a dot on the swing point to indicate it's inside a key level
                        DrawInsideKeyLevelDot(bearishSwingPoint);
                        
                        // Mark swing point as inside HTF FVG key level
                        
                        // Check if all quadrants are swept (HTF FVG becomes inactive)
                        if (!htfFvg.IsActive)
                        {
                            // Remove the entire HTF FVG from chart (rectangle, quadrants, label)
                            RemoveHtfFvgFromChart(htfFvg);
                            // HTF FVG now inactive and removed from chart
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish swing point HTF FVG quadrant sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove HTF FVG from chart (rectangle, quadrants, and label)
        /// </summary>
        private void RemoveHtfFvgFromChart(Level htfFvg)
        {
            try
            {
                if (htfFvg == null || htfFvg.LevelType != LevelType.FairValueGap)
                    return;

                // Generate the HTF FVG pattern ID to match the visualizer
                var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
                var patternId = $"HTF_FVG_{tfLabel}_{htfFvg.Direction}_{htfFvg.Index}_{htfFvg.LowTime:yyyyMMddHHmmss}";
                
                // Remove the main rectangle
                _chart.RemoveObject(patternId);
                
                // Remove the label
                _chart.RemoveObject($"{patternId}_Label");
                
                // Remove quadrant lines if they exist
                _chart.RemoveObject($"{patternId}_Q25");
                _chart.RemoveObject($"{patternId}_Q50");
                _chart.RemoveObject($"{patternId}_Q75");
                
                // HTF FVG removed from chart
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error removing HTF FVG from chart: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish swing point creation - sweep bearish order blocks
        /// Get all active bearish order blocks where:
        /// - High of order block is lower than bullish swing point price
        /// - High of order block is higher than bullish swing point candle's low price
        /// Mark these order blocks as liquidity swept
        /// </summary>
        private void HandleBullishSwingPointLiquiditySweep(SwingPoint bullishSwingPoint)
        {
            try
            {
                // Get all active bearish order blocks
                var activeBearishOrderBlocks = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.OrderBlock &&
                        level.Direction == Direction.Down &&
                        level.IsActive)
                    .ToList();

                double swingPointPrice = bullishSwingPoint.Price;
                double swingPointCandleLow = bullishSwingPoint.Bar?.Low ?? swingPointPrice;

                foreach (var orderBlock in activeBearishOrderBlocks)
                {
                    // Check conditions for liquidity sweep:
                    // 1. High of order block is lower than swing point price
                    // 2. High of order block is higher than swing point candle's low
                    bool highBelowSwingPoint = orderBlock.High < swingPointPrice;
                    bool highAboveCandleLow = orderBlock.High > swingPointCandleLow;

                    if (highBelowSwingPoint && highAboveCandleLow)
                    {
                        // Mark order block as liquidity swept
                        orderBlock.IsLiquiditySwept = true;
                        orderBlock.SweptSwingPoint = bullishSwingPoint;
                        orderBlock.SweptIndex = bullishSwingPoint.Index;

                        // Publish liquidity sweep event first (so visualizer can check IsExtended)
                        _eventAggregator.Publish(new OrderBlockLiquiditySweptEvent(
                            orderBlock, 
                            bullishSwingPoint, 
                            bullishSwingPoint.Index));

                        // If the order block was not extended, remove it from the chart
                        if (!orderBlock.IsExtended)
                        {
                            RemoveKeyLevelFromChart(orderBlock);
                            // Non-extended order block removed
                        }

                        // Order block liquidity swept
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish swing point liquidity sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bearish swing point creation - sweep bullish order blocks
        /// Get all active bullish order blocks where:
        /// - Low of order block is higher than bearish swing point price
        /// - Low of order block is lower than bearish swing point candle's high price
        /// Mark these order blocks as liquidity swept
        /// </summary>
        private void HandleBearishSwingPointLiquiditySweep(SwingPoint bearishSwingPoint)
        {
            try
            {
                // Get all active bullish order blocks
                var activeBullishOrderBlocks = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.OrderBlock &&
                        level.Direction == Direction.Up &&
                        level.IsActive)
                    .ToList();

                double swingPointPrice = bearishSwingPoint.Price;
                double swingPointCandleHigh = bearishSwingPoint.Bar?.High ?? swingPointPrice;

                foreach (var orderBlock in activeBullishOrderBlocks)
                {
                    // Check conditions for liquidity sweep:
                    // 1. Low of order block is higher than swing point price
                    // 2. Low of order block is lower than swing point candle's high
                    bool lowAboveSwingPoint = orderBlock.Low > swingPointPrice;
                    bool lowBelowCandleHigh = orderBlock.Low < swingPointCandleHigh;

                    if (lowAboveSwingPoint && lowBelowCandleHigh)
                    {
                        // Mark order block as liquidity swept
                        orderBlock.IsLiquiditySwept = true;
                        orderBlock.SweptSwingPoint = bearishSwingPoint;
                        orderBlock.SweptIndex = bearishSwingPoint.Index;

                        // Publish liquidity sweep event first (so visualizer can check IsExtended)
                        _eventAggregator.Publish(new OrderBlockLiquiditySweptEvent(
                            orderBlock, 
                            bearishSwingPoint, 
                            bearishSwingPoint.Index));

                        // If the order block was not extended, remove it from the chart
                        if (!orderBlock.IsExtended)
                        {
                            RemoveKeyLevelFromChart(orderBlock);
                            // Non-extended order block removed
                        }

                        // Order block liquidity swept
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish swing point liquidity sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish swing point creation - sweep bearish rejection blocks
        /// Get all active bearish rejection blocks where:
        /// - High of rejection block is lower than bullish swing point price
        /// - High of rejection block is higher than bullish swing point candle's low price
        /// Mark these rejection blocks as liquidity swept
        /// </summary>
        private void HandleBullishSwingPointRejectionBlockSweep(SwingPoint bullishSwingPoint)
        {
            try
            {
                // Get all active bearish rejection blocks
                var activeBearishRejectionBlocks = _levelRepository
                    .Find(level =>
                        level.LevelType == LevelType.RejectionBlock &&
                        level.Direction == Direction.Down &&
                        level.IsActive)
                    .ToList();

                double swingPointPrice = bullishSwingPoint.Price;
                double swingPointCandleLow = bullishSwingPoint.Bar?.Low ?? swingPointPrice;

                foreach (var rejectionBlock in activeBearishRejectionBlocks)
                {
                    // Check conditions for liquidity sweep:
                    // 1. High of rejection block is lower than swing point price
                    // 2. High of rejection block is higher than swing point candle's low
                    bool highBelowSwingPoint = rejectionBlock.High < swingPointPrice;
                    bool highAboveCandleLow = rejectionBlock.High > swingPointCandleLow;

                    if (highBelowSwingPoint && highAboveCandleLow)
                    {
                        // Mark rejection block as liquidity swept
                        rejectionBlock.IsLiquiditySwept = true;
                        rejectionBlock.SweptSwingPoint = bullishSwingPoint;
                        rejectionBlock.SweptIndex = bullishSwingPoint.Index;

                        // If the rejection block was not extended, remove it from the chart
                        if (!rejectionBlock.IsExtended)
                        {
                            RemoveKeyLevelFromChart(rejectionBlock);
                            _logger?.Invoke($"Bearish rejection block swept and removed at {rejectionBlock.High:F5} by bullish swing point at {bullishSwingPoint.Price:F5}");
                        }
                        else
                        {
                            _logger?.Invoke($"Bearish rejection block swept (extended) at {rejectionBlock.High:F5} by bullish swing point at {bullishSwingPoint.Price:F5}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish swing point rejection block sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bearish swing point creation - sweep bullish rejection blocks
        /// Get all active bullish rejection blocks where:
        /// - Low of rejection block is higher than bearish swing point price
        /// - Low of rejection block is lower than bearish swing point candle's high price
        /// Mark these rejection blocks as liquidity swept
        /// </summary>
        private void HandleBearishSwingPointRejectionBlockSweep(SwingPoint bearishSwingPoint)
        {
            try
            {
                // Get all active bullish rejection blocks
                var activeBullishRejectionBlocks = _levelRepository
                    .Find(level =>
                        level.LevelType == LevelType.RejectionBlock &&
                        level.Direction == Direction.Up &&
                        level.IsActive)
                    .ToList();

                double swingPointPrice = bearishSwingPoint.Price;
                double swingPointCandleHigh = bearishSwingPoint.Bar?.High ?? swingPointPrice;

                foreach (var rejectionBlock in activeBullishRejectionBlocks)
                {
                    // Check conditions for liquidity sweep:
                    // 1. Low of rejection block is higher than swing point price
                    // 2. Low of rejection block is lower than swing point candle's high
                    bool lowAboveSwingPoint = rejectionBlock.Low > swingPointPrice;
                    bool lowBelowCandleHigh = rejectionBlock.Low < swingPointCandleHigh;

                    if (lowAboveSwingPoint && lowBelowCandleHigh)
                    {
                        // Mark rejection block as liquidity swept
                        rejectionBlock.IsLiquiditySwept = true;
                        rejectionBlock.SweptSwingPoint = bearishSwingPoint;
                        rejectionBlock.SweptIndex = bearishSwingPoint.Index;

                        // If the rejection block was not extended, remove it from the chart
                        if (!rejectionBlock.IsExtended)
                        {
                            RemoveKeyLevelFromChart(rejectionBlock);
                            _logger?.Invoke($"Bullish rejection block swept and removed at {rejectionBlock.Low:F5} by bearish swing point at {bearishSwingPoint.Price:F5}");
                        }
                        else
                        {
                            _logger?.Invoke($"Bullish rejection block swept (extended) at {rejectionBlock.Low:F5} by bearish swing point at {bearishSwingPoint.Price:F5}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish swing point rejection block sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish swing point creation - sweep bearish CISDs
        /// Get all active bearish CISDs where:
        /// - High of CISD is lower than bullish swing point price
        /// - High of CISD is higher than bullish swing point candle's low price
        /// Mark these CISDs as liquidity swept
        /// </summary>
        private void HandleBullishSwingPointCisdLiquiditySweep(SwingPoint bullishSwingPoint)
        {
            try
            {
                // Get all active bearish CISDs
                var activeBearishCisds = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.CISD &&
                        level.Direction == Direction.Down &&
                        level.IsActive)
                    .ToList();

                double swingPointPrice = bullishSwingPoint.Price;
                double swingPointCandleLow = bullishSwingPoint.Bar?.Low ?? swingPointPrice;

                foreach (var cisd in activeBearishCisds)
                {
                    // Check conditions for liquidity sweep:
                    // 1. High of CISD is lower than swing point price
                    // 2. High of CISD is higher than swing point candle's low
                    bool highBelowSwingPoint = cisd.High < swingPointPrice;
                    bool highAboveCandleLow = cisd.High > swingPointCandleLow;

                    if (highBelowSwingPoint && highAboveCandleLow)
                    {
                        // Mark CISD as liquidity swept
                        cisd.IsLiquiditySwept = true;
                        cisd.SweptSwingPoint = bullishSwingPoint;
                        cisd.SweptIndex = bullishSwingPoint.Index;

                        // Publish liquidity sweep event first (so visualizer can check IsExtended if needed)
                        _eventAggregator.Publish(new CisdLiquiditySweptEvent(
                            cisd, 
                            bullishSwingPoint, 
                            bullishSwingPoint.Index));

                        // If the CISD was not extended, remove it from the chart
                        if (!cisd.IsExtended)
                        {
                            RemoveCisdFromChart(cisd);
                            // Non-extended CISD removed
                        }

                        // CISD liquidity swept
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish swing point CISD liquidity sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bearish swing point creation - sweep bullish CISDs
        /// Get all active bullish CISDs where:
        /// - Low of CISD is higher than bearish swing point price
        /// - Low of CISD is lower than bearish swing point candle's high price
        /// Mark these CISDs as liquidity swept
        /// </summary>
        private void HandleBearishSwingPointCisdLiquiditySweep(SwingPoint bearishSwingPoint)
        {
            try
            {
                // Get all active bullish CISDs
                var activeBullishCisds = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.CISD &&
                        level.Direction == Direction.Up &&
                        level.IsActive)
                    .ToList();

                double swingPointPrice = bearishSwingPoint.Price;
                double swingPointCandleHigh = bearishSwingPoint.Bar?.High ?? swingPointPrice;

                foreach (var cisd in activeBullishCisds)
                {
                    // Check conditions for liquidity sweep:
                    // 1. Low of CISD is higher than swing point price
                    // 2. Low of CISD is lower than swing point candle's high
                    bool lowAboveSwingPoint = cisd.Low > swingPointPrice;
                    bool lowBelowCandleHigh = cisd.Low < swingPointCandleHigh;

                    if (lowAboveSwingPoint && lowBelowCandleHigh)
                    {
                        // Mark CISD as liquidity swept
                        cisd.IsLiquiditySwept = true;
                        cisd.SweptSwingPoint = bearishSwingPoint;
                        cisd.SweptIndex = bearishSwingPoint.Index;

                        // Publish liquidity sweep event first (so visualizer can check IsExtended if needed)
                        _eventAggregator.Publish(new CisdLiquiditySweptEvent(
                            cisd, 
                            bearishSwingPoint, 
                            bearishSwingPoint.Index));

                        // If the CISD was not extended, remove it from the chart
                        if (!cisd.IsExtended)
                        {
                            RemoveCisdFromChart(cisd);
                            // Non-extended CISD removed
                        }

                        // CISD liquidity swept
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish swing point CISD liquidity sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish swing point session/daily high liquidity sweeps
        /// Check if the bullish swing point sweeps any session highs (PSH) or daily highs (PDH)
        /// Update visual representation when sweeps occur
        /// </summary>
        private void HandleBullishSwingPointSessionDailyHighLiquiditySweep(SwingPoint bullishSwingPoint)
        {
            try
            {
                if (!_settings.Patterns.ShowLiquiditySweep)
                    return;

                // Get all active session and daily highs (PSH, PDH) that could be swept
                var activeHighs = _swingPointRepository
                    .Find(sp => 
                        (sp.LiquidityType == LiquidityType.PSH || sp.LiquidityType == LiquidityType.PDH) &&
                        !sp.Swept &&
                        sp.Index < bullishSwingPoint.Index) // Must be before the current swing point
                    .ToList();

                foreach (var sessionDailyHigh in activeHighs)
                {
                    // Check if bullish swing point price is equal to or above the session/daily high
                    if (bullishSwingPoint.Price >= sessionDailyHigh.Price)
                    {
                        // Mark the session/daily high as swept
                        sessionDailyHigh.Swept = true;
                        sessionDailyHigh.IndexOfSweepingCandle = bullishSwingPoint.Index;
                        
                        // Mark the bullish swing point as having swept liquidity
                        bullishSwingPoint.SweptLiquidity = true;
                        bullishSwingPoint.SweptLiquidityPrice = sessionDailyHigh.Price;
                        
                        // Update the corresponding candle's SweptLiquidity property
                        var candle = _candleManager.GetCandle(bullishSwingPoint.Index);
                        if (candle != null)
                        {
                            candle.SweptLiquidity = 1; // Set to 1 to indicate liquidity was swept
                        }
                        
                        // Update the visual representation
                        HandleLiquiditySweepVisualUpdate(sessionDailyHigh, bullishSwingPoint);
                        
                        // Session/Daily high liquidity swept
                        
                        // Send telegram notification if enabled
                        if (_settings.Notifications.NotifyLiquiditySweep && _settings.Notifications.EnableTelegram)
                        {
                            _notificationService.NotifyLiquiditySweep(sessionDailyHigh, bullishSwingPoint);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish swing point session/daily high liquidity sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bearish swing point session/daily low liquidity sweeps
        /// Check if the bearish swing point sweeps any session lows (PSL) or daily lows (PDL)
        /// Update visual representation when sweeps occur
        /// </summary>
        private void HandleBearishSwingPointSessionDailyLowLiquiditySweep(SwingPoint bearishSwingPoint)
        {
            try
            {
                if (!_settings.Patterns.ShowLiquiditySweep)
                    return;

                // Get all active session and daily lows (PSL, PDL) that could be swept
                var activeLows = _swingPointRepository
                    .Find(sp => 
                        (sp.LiquidityType == LiquidityType.PSL || sp.LiquidityType == LiquidityType.PDL) &&
                        !sp.Swept &&
                        sp.Index < bearishSwingPoint.Index) // Must be before the current swing point
                    .ToList();

                foreach (var sessionDailyLow in activeLows)
                {
                    // Check if bearish swing point price is equal to or below the session/daily low
                    if (bearishSwingPoint.Price <= sessionDailyLow.Price)
                    {
                        // Mark the session/daily low as swept
                        sessionDailyLow.Swept = true;
                        sessionDailyLow.IndexOfSweepingCandle = bearishSwingPoint.Index;
                        
                        // Mark the bearish swing point as having swept liquidity
                        bearishSwingPoint.SweptLiquidity = true;
                        bearishSwingPoint.SweptLiquidityPrice = sessionDailyLow.Price;
                        
                        // Update the corresponding candle's SweptLiquidity property
                        var candle = _candleManager.GetCandle(bearishSwingPoint.Index);
                        if (candle != null)
                        {
                            candle.SweptLiquidity = 1; // Set to 1 to indicate liquidity was swept
                        }
                        
                        // Update the visual representation
                        HandleLiquiditySweepVisualUpdate(sessionDailyLow, bearishSwingPoint);
                        
                        // Session/Daily low liquidity swept
                        
                        // Send telegram notification if enabled
                        if (_settings.Notifications.NotifyLiquiditySweep && _settings.Notifications.EnableTelegram)
                        {
                            _notificationService.NotifyLiquiditySweep(sessionDailyLow, bearishSwingPoint);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish swing point session/daily low liquidity sweep: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a bullish swing point is inside a bearish order block
        /// If inside multiple order blocks, select the most recent one (highest index)
        /// </summary>
        private void CheckBullishSwingPointInsideOrderBlock(SwingPoint bullishSwingPoint)
        {
            try
            {
                // Always perform detection logic, only skip visualization if disabled

                // Get all active bearish order blocks that contain this swing point
                var containingOrderBlocks = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.OrderBlock &&
                        level.Direction == Direction.Down &&
                        level.IsActive &&
                        level.High > bullishSwingPoint.Price &&  // Order block high above swing point
                        level.Low < bullishSwingPoint.Price)     // Order block low below swing point
                    .ToList();

                if (containingOrderBlocks.Any())
                {
                    // Select the most recent order block (highest index)
                    var mostRecentOrderBlock = containingOrderBlocks
                        .OrderByDescending(ob => ob.Index)
                        .First();

                    // Mark swing point as inside key level
                    bullishSwingPoint.InsidePda = true;
                    bullishSwingPoint.Pda = mostRecentOrderBlock;
                    
                    // Update the corresponding candle's SweptLiquidity property
                    var candle = _candleManager.GetCandle(bullishSwingPoint.Index);
                    if (candle != null)
                    {
                        candle.InsidePda = true; // Set to 1 to indicate liquidity was swept
                    }
                    
                    // Draw a dot on the swing point only if visualization is enabled
                    if (_settings.Patterns.ShowInsideKeyLevel)
                    {
                        DrawInsideKeyLevelDot(bullishSwingPoint);
                    }

                    // Swing point is inside order block
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error checking bullish swing point inside order block: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a bearish swing point is inside a bullish order block
        /// If inside multiple order blocks, select the most recent one (highest index)
        /// </summary>
        private void CheckBearishSwingPointInsideOrderBlock(SwingPoint bearishSwingPoint)
        {
            try
            {
                // Always perform detection logic, only skip visualization if disabled

                // Get all active bullish order blocks that contain this swing point
                var containingOrderBlocks = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.OrderBlock &&
                        level.Direction == Direction.Up &&
                        level.IsActive &&
                        level.Low < bearishSwingPoint.Price &&   // Order block low below swing point
                        level.High > bearishSwingPoint.Price)    // Order block high above swing point
                    .ToList();

                if (containingOrderBlocks.Any())
                {
                    // Select the most recent order block (highest index)
                    var mostRecentOrderBlock = containingOrderBlocks
                        .OrderByDescending(ob => ob.Index)
                        .First();

                    // Mark swing point as inside key level
                    bearishSwingPoint.InsidePda = true;
                    bearishSwingPoint.Pda = mostRecentOrderBlock;
                    
                    var candle = _candleManager.GetCandle(bearishSwingPoint.Index);
                    if (candle != null)
                    {
                        candle.InsidePda = true; // Set to 1 to indicate liquidity was swept
                    }
                    
                    // Draw a dot on the swing point only if visualization is enabled
                    if (_settings.Patterns.ShowInsideKeyLevel)
                    {
                        DrawInsideKeyLevelDot(bearishSwingPoint);
                    }

                    // Swing point is inside order block
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error checking bearish swing point inside order block: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a bullish swing point is inside a bearish CISD
        /// If inside multiple CISDs, select the most recent one (highest index)
        /// </summary>
        private void CheckBullishSwingPointInsideCisd(SwingPoint bullishSwingPoint)
        {
            try
            {
                // Always perform detection logic, only skip visualization if disabled

                // Get all confirmed and active bearish CISDs that contain this swing point
                var containingCisds = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.CISD &&
                        level.Direction == Direction.Down &&
                        level.IsConfirmed &&  // Only consider confirmed CISDs
                        level.IsActive &&
                        level.High > bullishSwingPoint.Price &&  // CISD high above swing point
                        level.Low < bullishSwingPoint.Price)     // CISD low below swing point
                    .ToList();

                if (containingCisds.Any())
                {
                    // Select the most recent CISD (highest index)
                    var mostRecentCisd = containingCisds
                        .OrderByDescending(cisd => cisd.Index)
                        .First();

                    // Mark swing point as inside key level
                    bullishSwingPoint.InsidePda = true;
                    bullishSwingPoint.Pda = mostRecentCisd;
                    
                    var candle = _candleManager.GetCandle(bullishSwingPoint.Index);
                    if (candle != null)
                    {
                        candle.InsidePda = true; // Set to 1 to indicate liquidity was swept
                    }
                    
                    // Draw a dot on the swing point only if visualization is enabled
                    if (_settings.Patterns.ShowInsideKeyLevel)
                    {
                        DrawInsideKeyLevelDot(bullishSwingPoint);
                    }

                    // Swing point is inside CISD
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error checking bullish swing point inside CISD: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a bearish swing point is inside a bullish CISD
        /// If inside multiple CISDs, select the most recent one (highest index)
        /// </summary>
        private void CheckBearishSwingPointInsideCisd(SwingPoint bearishSwingPoint)
        {
            try
            {
                // Always perform detection logic, only skip visualization if disabled

                // Get all confirmed and active bullish CISDs that contain this swing point
                var containingCisds = _levelRepository
                    .Find(level => 
                        level.LevelType == LevelType.CISD &&
                        level.Direction == Direction.Up &&
                        level.IsConfirmed &&  // Only consider confirmed CISDs
                        level.IsActive &&
                        level.Low < bearishSwingPoint.Price &&   // CISD low below swing point
                        level.High > bearishSwingPoint.Price)    // CISD high above swing point
                    .ToList();

                if (containingCisds.Any())
                {
                    // Select the most recent CISD (highest index)
                    var mostRecentCisd = containingCisds
                        .OrderByDescending(cisd => cisd.Index)
                        .First();

                    // Mark swing point as inside key level
                    bearishSwingPoint.InsidePda = true;
                    bearishSwingPoint.Pda = mostRecentCisd;
                    
                    var candle = _candleManager.GetCandle(bearishSwingPoint.Index);
                    if (candle != null)
                    {
                        candle.InsidePda = true; // Set to 1 to indicate liquidity was swept
                    }
                    
                    // Draw a dot on the swing point only if visualization is enabled
                    if (_settings.Patterns.ShowInsideKeyLevel)
                    {
                        DrawInsideKeyLevelDot(bearishSwingPoint);
                    }

                    // Swing point is inside CISD
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error checking bearish swing point inside CISD: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a bullish swing point is inside a bearish rejection block
        /// If inside multiple rejection blocks, select the most recent one (highest index)
        /// </summary>
        private void CheckBullishSwingPointInsideRejectionBlock(SwingPoint bullishSwingPoint)
        {
            try
            {
                // Always perform detection logic, only skip visualization if disabled

                // Get all active bearish rejection blocks that contain this swing point
                var containingRejectionBlocks = _levelRepository
                    .Find(level =>
                        level.LevelType == LevelType.RejectionBlock &&
                        level.Direction == Direction.Down &&
                        level.IsActive &&
                        level.High > bullishSwingPoint.Price &&  // Rejection block high above swing point
                        level.Low < bullishSwingPoint.Price)     // Rejection block low below swing point
                    .ToList();

                if (containingRejectionBlocks.Any())
                {
                    // Select the most recent rejection block (highest index)
                    var mostRecentRejectionBlock = containingRejectionBlocks
                        .OrderByDescending(rb => rb.Index)
                        .First();

                    // Mark swing point as inside key level
                    bullishSwingPoint.InsidePda = true;
                    bullishSwingPoint.Pda = mostRecentRejectionBlock;

                    // Update the corresponding candle's InsidePda property
                    var candle = _candleManager.GetCandle(bullishSwingPoint.Index);
                    if (candle != null)
                    {
                        candle.InsidePda = true;
                    }

                    // Draw a dot on the swing point only if visualization is enabled
                    if (_settings.Patterns.ShowInsideKeyLevel)
                    {
                        DrawInsideKeyLevelDot(bullishSwingPoint);
                    }

                    // Swing point is inside rejection block
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error checking bullish swing point inside rejection block: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a bearish swing point is inside a bullish rejection block
        /// If inside multiple rejection blocks, select the most recent one (highest index)
        /// </summary>
        private void CheckBearishSwingPointInsideRejectionBlock(SwingPoint bearishSwingPoint)
        {
            try
            {
                // Always perform detection logic, only skip visualization if disabled

                // Get all active bullish rejection blocks that contain this swing point
                var containingRejectionBlocks = _levelRepository
                    .Find(level =>
                        level.LevelType == LevelType.RejectionBlock &&
                        level.Direction == Direction.Up &&
                        level.IsActive &&
                        level.High > bearishSwingPoint.Price &&  // Rejection block high above swing point
                        level.Low < bearishSwingPoint.Price)     // Rejection block low below swing point
                    .ToList();

                if (containingRejectionBlocks.Any())
                {
                    // Select the most recent rejection block (highest index)
                    var mostRecentRejectionBlock = containingRejectionBlocks
                        .OrderByDescending(rb => rb.Index)
                        .First();

                    // Mark swing point as inside key level
                    bearishSwingPoint.InsidePda = true;
                    bearishSwingPoint.Pda = mostRecentRejectionBlock;

                    // Update the corresponding candle's InsidePda property
                    var candle = _candleManager.GetCandle(bearishSwingPoint.Index);
                    if (candle != null)
                    {
                        candle.InsidePda = true;
                    }

                    // Draw a dot on the swing point only if visualization is enabled
                    if (_settings.Patterns.ShowInsideKeyLevel)
                    {
                        DrawInsideKeyLevelDot(bearishSwingPoint);
                    }

                    // Swing point is inside rejection block
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error checking bearish swing point inside rejection block: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish order block detection - check if swing low is inside key level and extend that key level
        /// </summary>
        private void HandleBullishOrderBlockDetection(Level orderBlock)
        {
            try
            {
                // Only extend key levels from order blocks if both ShowOrderBlock and ShowInsideKeyLevel are true
                if (!_settings.Patterns.ShowOrderBlock || !_settings.Patterns.ShowInsideKeyLevel)
                {
                    // Order block extension disabled in settings
                    return;
                }

                // Find the swing low (bearish swing point) at the low boundary of this bullish order block
                var swingLow = _swingPointRepository
                    .Find(sp => sp.Direction == Direction.Down && 
                               Math.Abs(sp.Price - orderBlock.Low) < 0.00001 &&
                               sp.Index >= orderBlock.IndexLow - 5 && sp.Index <= orderBlock.IndexLow + 5)
                    .FirstOrDefault();

                if (swingLow == null || !swingLow.InsidePda || swingLow.Pda == null)
                    return;

                // Check if the swept key level is an HTF FVG
                if (swingLow.Pda.LevelType == LevelType.FairValueGap && swingLow.Pda.TimeFrame != null)
                {
                    // Extend the HTF FVG to include the swing point's candle
                    ExtendHtfFvg(swingLow.Pda, swingLow);
                    // HTF FVG extended from order block
                }
                else
                {
                    // Extend the regular key level to include the swing point's candle
                    ExtendKeyLevel(swingLow.Pda, swingLow);
                    // Key level extended from order block
                }
                
                // Send Telegram notification about order block formed inside key level
                _notificationService.NotifyOrderBlockInsideKeyLevel(orderBlock, swingLow.Pda);
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish order block detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bearish order block detection - check if swing high is inside key level and extend that key level
        /// </summary>
        private void HandleBearishOrderBlockDetection(Level orderBlock)
        {
            try
            {
                // Only extend key levels from order blocks if both ShowOrderBlock and ShowInsideKeyLevel are true
                if (!_settings.Patterns.ShowOrderBlock || !_settings.Patterns.ShowInsideKeyLevel)
                {
                    // Order block extension disabled in settings
                    return;
                }

                // Find the swing high (bullish swing point) at the high boundary of this bearish order block
                var swingHigh = _swingPointRepository
                    .Find(sp => sp.Direction == Direction.Up && 
                               Math.Abs(sp.Price - orderBlock.High) < 0.00001 &&
                               sp.Index >= orderBlock.IndexHigh - 5 && sp.Index <= orderBlock.IndexHigh + 5)
                    .FirstOrDefault();

                if (swingHigh == null || !swingHigh.InsidePda || swingHigh.Pda == null)
                    return;

                // Check if the swept key level is an HTF FVG
                if (swingHigh.Pda.LevelType == LevelType.FairValueGap && swingHigh.Pda.TimeFrame != null)
                {
                    // Extend the HTF FVG to include the swing point's candle
                    ExtendHtfFvg(swingHigh.Pda, swingHigh);
                    // HTF FVG extended from order block
                }
                else
                {
                    // Extend the regular key level to include the swing point's candle
                    ExtendKeyLevel(swingHigh.Pda, swingHigh);
                    // Key level extended from order block
                }
                
                // Send Telegram notification about order block formed inside key level
                _notificationService.NotifyOrderBlockInsideKeyLevel(orderBlock, swingHigh.Pda);
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish order block detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bearish CISD confirmation - check if swing high at exact IndexHigh is inside key level
        /// For bearish CISD, IndexHigh points to the exact swing high index
        /// </summary>
        private void HandleBearishCisdConfirmed(Level cisdLevel)
        {
            try
            {
                // Only extend key levels from CISD if ShowInsideKeyLevel is true
                if (!_settings.Patterns.ShowInsideKeyLevel)
                {
                    // CISD extension disabled in settings
                    return;
                }

                // Find the swing point at the CISD's exact IndexHigh
                var swingHigh = _swingPointRepository
                    .Find(sp => sp.Index == cisdLevel.IndexHigh)
                    .FirstOrDefault();

                if (swingHigh == null || !swingHigh.InsidePda || swingHigh.Pda == null)
                    return;

                // Check if the swept key level is an HTF FVG
                if (swingHigh.Pda.LevelType == LevelType.FairValueGap && swingHigh.Pda.TimeFrame != null)
                {
                    // Extend the HTF FVG to include the CISD swing high candle
                    ExtendHtfFvg(swingHigh.Pda, swingHigh);
                    // HTF FVG extended from CISD
                }
                else
                {
                    // Extend the regular key level to include the CISD swing high candle
                    ExtendKeyLevel(swingHigh.Pda, swingHigh);
                    // Key level extended from CISD
                }
                
                // Send Telegram notification about CISD confirmed inside key level
                _notificationService.NotifyCisdInsideKeyLevel(cisdLevel, swingHigh.Pda);
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish CISD confirmation: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish CISD confirmation - check if swing low at exact IndexLow is inside key level
        /// For bullish CISD, IndexLow points to the exact swing low index
        /// </summary>
        private void HandleBullishCisdConfirmed(Level cisdLevel)
        {
            try
            {
                // Only extend key levels from CISD if ShowInsideKeyLevel is true
                if (!_settings.Patterns.ShowInsideKeyLevel)
                {
                    // CISD extension disabled in settings
                    return;
                }

                // Find the swing point at the CISD's exact IndexLow
                var swingLow = _swingPointRepository
                    .Find(sp => sp.Index == cisdLevel.IndexLow)
                    .FirstOrDefault();

                if (swingLow == null || !swingLow.InsidePda || swingLow.Pda == null)
                    return;

                // Check if the swept key level is an HTF FVG
                if (swingLow.Pda.LevelType == LevelType.FairValueGap && swingLow.Pda.TimeFrame != null)
                {
                    // Extend the HTF FVG to include the CISD swing low candle
                    ExtendHtfFvg(swingLow.Pda, swingLow);
                    // HTF FVG extended from CISD
                }
                else
                {
                    // Extend the regular key level to include the CISD swing low candle
                    ExtendKeyLevel(swingLow.Pda, swingLow);
                    // Key level extended from CISD
                }
                
                // Send Telegram notification about CISD confirmed inside key level
                _notificationService.NotifyCisdInsideKeyLevel(cisdLevel, swingLow.Pda);
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish CISD confirmation: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish rejection block detection - check if swing low is inside key level and extend that key level
        /// </summary>
        private void HandleBullishRejectionBlockDetection(Level rejectionBlock)
        {
            try
            {
                // Only extend key levels from rejection blocks if both ShowRejectionBlock and ShowInsideKeyLevel are true
                if (!_settings.Patterns.ShowRejectionBlock || !_settings.Patterns.ShowInsideKeyLevel)
                {
                    // Rejection block extension disabled in settings
                    return;
                }

                // Find the swing low (bearish swing point) at the low boundary of this bullish rejection block
                var swingLow = _swingPointRepository
                    .Find(sp => sp.Direction == Direction.Down &&
                               Math.Abs(sp.Price - rejectionBlock.Low) < 0.00001 &&
                               sp.Index >= rejectionBlock.IndexLow - 5 && sp.Index <= rejectionBlock.IndexLow + 5)
                    .FirstOrDefault();

                if (swingLow == null || !swingLow.InsidePda || swingLow.Pda == null)
                    return;

                // Mark the rejection block as extended
                rejectionBlock.IsExtended = true;

                // Check if the swept key level is an HTF FVG
                if (swingLow.Pda.LevelType == LevelType.FairValueGap && swingLow.Pda.TimeFrame != null)
                {
                    // Extend the HTF FVG to include the swing point's candle
                    ExtendHtfFvg(swingLow.Pda, swingLow);
                    // HTF FVG extended from rejection block
                }
                else
                {
                    // Extend the regular key level to include the swing point's candle
                    ExtendKeyLevel(swingLow.Pda, swingLow);
                    // Key level extended from rejection block
                }

                _logger?.Invoke($"Bullish rejection block at {rejectionBlock.Low:F5} extended key level {swingLow.Pda.LevelType}");
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bullish rejection block detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bearish rejection block detection - check if swing high is inside key level and extend that key level
        /// </summary>
        private void HandleBearishRejectionBlockDetection(Level rejectionBlock)
        {
            try
            {
                // Only extend key levels from rejection blocks if both ShowRejectionBlock and ShowInsideKeyLevel are true
                if (!_settings.Patterns.ShowRejectionBlock || !_settings.Patterns.ShowInsideKeyLevel)
                {
                    // Rejection block extension disabled in settings
                    return;
                }

                // Find the swing high (bullish swing point) at the high boundary of this bearish rejection block
                var swingHigh = _swingPointRepository
                    .Find(sp => sp.Direction == Direction.Up &&
                               Math.Abs(sp.Price - rejectionBlock.High) < 0.00001 &&
                               sp.Index >= rejectionBlock.IndexHigh - 5 && sp.Index <= rejectionBlock.IndexHigh + 5)
                    .FirstOrDefault();

                if (swingHigh == null || !swingHigh.InsidePda || swingHigh.Pda == null)
                    return;

                // Mark the rejection block as extended
                rejectionBlock.IsExtended = true;

                // Check if the swept key level is an HTF FVG
                if (swingHigh.Pda.LevelType == LevelType.FairValueGap && swingHigh.Pda.TimeFrame != null)
                {
                    // Extend the HTF FVG to include the swing point's candle
                    ExtendHtfFvg(swingHigh.Pda, swingHigh);
                    // HTF FVG extended from rejection block
                }
                else
                {
                    // Extend the regular key level to include the swing point's candle
                    ExtendKeyLevel(swingHigh.Pda, swingHigh);
                    // Key level extended from rejection block
                }

                _logger?.Invoke($"Bearish rejection block at {rejectionBlock.High:F5} extended key level {swingHigh.Pda.LevelType}");
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error handling bearish rejection block detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Extend an HTF FVG to include the swing point's candle
        /// This extends both the rectangle and redraw the quadrants
        /// </summary>
        private void ExtendHtfFvg(Level htfFvg, SwingPoint swingPoint)
        {
            try
            {
                if (htfFvg == null || swingPoint == null || htfFvg.LevelType != LevelType.FairValueGap)
                    return;

                // Extend the time range of the HTF FVG to include the swing point's time
                bool wasExtended = false;
                if (swingPoint.Time > htfFvg.HighTime)
                {
                    htfFvg.HighTime = swingPoint.Time;
                    htfFvg.IndexHigh = swingPoint.Index;
                    wasExtended = true;
                }
                else if (swingPoint.Time < htfFvg.LowTime)
                {
                    htfFvg.LowTime = swingPoint.Time;
                    htfFvg.IndexLow = swingPoint.Index;
                    wasExtended = true;
                }

                if (wasExtended)
                {
                    // Mark the HTF FVG as extended
                    htfFvg.IsExtended = true;
                    
                    // Redraw the HTF FVG with extended time range (rectangle and quadrants)
                    RedrawHtfFvg(htfFvg);
                    
                    // HTF FVG time range extended
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error extending HTF FVG: {ex.Message}");
            }
        }

        /// <summary>
        /// Extend a key level rectangle to include the swing point's candle
        /// </summary>
        private void ExtendKeyLevel(Level keyLevel, SwingPoint swingPoint)
        {
            try
            {
                if (keyLevel == null || swingPoint == null)
                    return;

                // Only extend rectangles for CISD and OrderBlocks when ShowInsideKeyLevel is true
                if ((keyLevel.LevelType == LevelType.CISD || keyLevel.LevelType == LevelType.OrderBlock) && 
                    !_settings.Patterns.ShowInsideKeyLevel)
                {
                    // Rectangle extension disabled for CISD and OrderBlock when ShowInsideKeyLevel is false
                    return;
                }

                // Only extend regular CISDs if ShowCISD is true
                if (keyLevel.LevelType == LevelType.CISD && keyLevel.TimeFrame == null && !_settings.Patterns.ShowCISD)
                {
                    // CISD extension disabled in settings
                    return;
                }

                // Extend the time range of the key level to include the swing point's time
                bool wasExtended = false;
                if (swingPoint.Time > keyLevel.HighTime)
                {
                    keyLevel.HighTime = swingPoint.Time;
                    keyLevel.IndexHigh = swingPoint.Index;
                    wasExtended = true;
                }
                else if (swingPoint.Time < keyLevel.LowTime)
                {
                    keyLevel.LowTime = swingPoint.Time;
                    keyLevel.IndexLow = swingPoint.Index;
                    wasExtended = true;
                }

                if (wasExtended)
                {
                    // Mark the level as extended
                    keyLevel.IsExtended = true;
                    
                    // Redraw the rectangle with extended time range
                    RedrawKeyLevelRectangle(keyLevel);
                    
                    // Key level time range extended
                    
                    // TODO: Publish event that key level has been extended
                    // _eventAggregator.Publish(new KeyLevelExtendedEvent(keyLevel, swingPoint));
                }
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error extending key level: {ex.Message}");
            }
        }

        /// <summary>
        /// Redraw an HTF FVG with updated time properties (rectangle and quadrants)
        /// </summary>
        private void RedrawHtfFvg(Level htfFvg)
        {
            try
            {
                if (htfFvg == null || htfFvg.LevelType != LevelType.FairValueGap)
                    return;

                // Don't redraw HTF FVGs if ShowHtfFvg is false
                if (!_settings.Patterns.ShowHtfFvg)
                {
                    // Remove any existing HTF FVG objects since it shouldn't be displayed
                    var tfLabelForRemoval = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
                    var patternIdForRemoval = $"HTF_FVG_{tfLabelForRemoval}_{htfFvg.Direction}_{htfFvg.Index}_{htfFvg.LowTime:yyyyMMddHHmmss}";
                    
                    _chart.RemoveObject(patternIdForRemoval); // Rectangle
                    _chart.RemoveObject($"{patternIdForRemoval}_Label"); // Label
                    _chart.RemoveObject($"{patternIdForRemoval}_Q25"); // Quadrant lines
                    _chart.RemoveObject($"{patternIdForRemoval}_Q50");
                    _chart.RemoveObject($"{patternIdForRemoval}_Q75");
                    
                    // HTF FVG visualization disabled
                    return;
                }

                // Generate the HTF FVG pattern ID to match the visualizer
                var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
                var patternId = $"HTF_FVG_{tfLabel}_{htfFvg.Direction}_{htfFvg.Index}_{htfFvg.LowTime:yyyyMMddHHmmss}";
                
                // Remove existing objects
                _chart.RemoveObject(patternId); // Rectangle
                _chart.RemoveObject($"{patternId}_Label"); // Label
                _chart.RemoveObject($"{patternId}_Q25"); // Quadrant lines
                _chart.RemoveObject($"{patternId}_Q50");
                _chart.RemoveObject($"{patternId}_Q75");

                // Get appropriate color for the HTF FVG
                Color baseColor = htfFvg.Direction == Direction.Up ? Color.Green : Color.Red;
                
                // Determine start and end times for extended rectangle
                var startTime = htfFvg.LowTime < htfFvg.HighTime ? htfFvg.LowTime : htfFvg.HighTime;
                var endTime = htfFvg.LowTime < htfFvg.HighTime ? htfFvg.HighTime : htfFvg.LowTime;

                // Draw new extended rectangle
                var rectangle = _chart.DrawRectangle(
                    patternId,
                    startTime,
                    htfFvg.Low,
                    endTime,
                    htfFvg.High,
                    baseColor
                );

                rectangle.IsFilled = false;
                rectangle.Color = Color.FromArgb(30, baseColor); // 30% opacity for HTF FVGs
                
                // Redraw label
                var labelText = $"{tfLabel} FVG";
                var midPoint = (htfFvg.High + htfFvg.Low) / 2;
                
                _chart.DrawText(
                    $"{patternId}_Label",
                    labelText,
                    htfFvg.MidTime,
                    midPoint,
                    baseColor
                );
                
                // Redraw quadrant lines if quadrants are enabled
                if (htfFvg.Quadrants != null && htfFvg.Quadrants.Count > 0)
                {
                    var quadrantColor = Color.FromArgb(50, baseColor);
                    
                    // Draw 25% line
                    var q25 = htfFvg.Quadrants.FirstOrDefault(q => q.Percent == 25);
                    if (q25 != null)
                    {
                        _chart.DrawTrendLine(
                            $"{patternId}_Q25",
                            startTime,
                            q25.Price,
                            endTime,
                            q25.Price,
                            quadrantColor,
                            1,
                            LineStyle.DotsRare
                        );
                    }
                    
                    // Draw 50% line (CE - Consequent Encroachment)
                    var q50 = htfFvg.Quadrants.FirstOrDefault(q => q.Percent == 50);
                    if (q50 != null)
                    {
                        _chart.DrawTrendLine(
                            $"{patternId}_Q50",
                            startTime,
                            q50.Price,
                            endTime,
                            q50.Price,
                            quadrantColor,
                            2, // Thicker for 50% line
                            LineStyle.Dots
                        );
                    }
                    
                    // Draw 75% line
                    var q75 = htfFvg.Quadrants.FirstOrDefault(q => q.Percent == 75);
                    if (q75 != null)
                    {
                        _chart.DrawTrendLine(
                            $"{patternId}_Q75",
                            startTime,
                            q75.Price,
                            endTime,
                            q75.Price,
                            quadrantColor,
                            1,
                            LineStyle.DotsRare
                        );
                    }
                }

                // HTF FVG redrawn with updated time range
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error redrawing HTF FVG: {ex.Message}");
            }
        }

        /// <summary>
        /// Redraw the rectangle for a key level with updated time properties
        /// </summary>
        private void RedrawKeyLevelRectangle(Level keyLevel)
        {
            try
            {
                if (keyLevel == null)
                    return;

                // Don't redraw regular CISDs if ShowCISD is false
                if (keyLevel.LevelType == LevelType.CISD && keyLevel.TimeFrame == null && !_settings.Patterns.ShowCISD)
                {
                    // Remove any existing rectangles for this CISD since it shouldn't be displayed
                    string rectangleId = GenerateRectangleId(keyLevel);
                    _chart.RemoveObject(rectangleId);
                    _chart.RemoveObject($"{rectangleId}-midline");
                    // CISD visualization disabled
                    return;
                }

                // Don't redraw order blocks if ShowOrderBlock is false
                if (keyLevel.LevelType == LevelType.OrderBlock && !_settings.Patterns.ShowOrderBlock)
                {
                    // Remove any existing rectangles for this order block since it shouldn't be displayed
                    string rectangleId = GenerateRectangleId(keyLevel);
                    _chart.RemoveObject(rectangleId);
                    _chart.RemoveObject($"{rectangleId}-midline");
                    // Order block visualization disabled
                    return;
                }

                // Generate rectangle ID based on level type and properties
                string rectId = GenerateRectangleId(keyLevel);
                
                // Remove existing rectangle and middle line
                _chart.RemoveObject(rectId);
                _chart.RemoveObject($"{rectId}-midline");

                // Get appropriate color for the level type
                Color rectangleColor = GetLevelColor(keyLevel);

                // Determine start and end times
                var startTime = keyLevel.LowTime < keyLevel.HighTime ? keyLevel.LowTime : keyLevel.HighTime;
                var endTime = keyLevel.LowTime < keyLevel.HighTime ? keyLevel.HighTime : keyLevel.LowTime;

                // Draw new extended rectangle
                var rectangle = _chart.DrawRectangle(
                    rectId,
                    startTime,
                    keyLevel.Low,
                    endTime,
                    keyLevel.High,
                    rectangleColor
                );

                rectangle.IsFilled = true;
                int extensionOpacity = Math.Max(1, Math.Min(255, (int)(255 * _settings.Visualization.Opacity.Extension / 100.0)));
                rectangle.Color = Color.FromArgb(extensionOpacity, rectangleColor);

                // Draw middle line
                string midLineId = $"{rectId}-midline";
                double midPrice = (keyLevel.High + keyLevel.Low) / 2;
                
                _chart.DrawTrendLine(
                    midLineId,
                    startTime,
                    midPrice,
                    endTime,
                    midPrice,
                    rectangleColor,
                    1,
                    LineStyle.Dots
                );

                // Rectangle redrawn with updated time range
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error redrawing key level rectangle: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate a consistent rectangle ID for a key level
        /// </summary>
        private string GenerateRectangleId(Level keyLevel)
        {
            return $"rect-{keyLevel.LevelType}-{keyLevel.Low:F5}-{keyLevel.High:F5}-{keyLevel.Index}";
        }

        /// <summary>
        /// Get appropriate color for different level types
        /// </summary>
        private Color GetLevelColor(Level keyLevel)
        {
            return keyLevel.Direction == Direction.Up ? Color.Green : Color.Red;
        }
        
        /// <summary>
        /// Draw a dot on a swing point that is inside a key level
        /// Removes any previously drawn dot before drawing the new one
        /// </summary>
        private void DrawInsideKeyLevelDot(SwingPoint swingPoint)
        {
            try
            {
                // Only draw inside key level dots if ShowInsideKeyLevel is enabled
                if (!_settings.Patterns.ShowInsideKeyLevel)
                {
                    // Inside key level dots disabled
                    return;
                }

                // Remove the previous dot if it exists
                if (!string.IsNullOrEmpty(_lastInsideKeyLevelDotId))
                {
                    _chart.RemoveObject(_lastInsideKeyLevelDotId);
                    // Previous dot removed
                }
                
                // Generate a unique ID for the new dot
                string dotId = $"inside_key_level_dot_{swingPoint.Index}_{swingPoint.Time:yyyyMMddHHmmss}";
                
                // Determine the color based on swing point direction
                Color dotColor = swingPoint.Direction == Direction.Up ? Color.Green : Color.Red;
                
                // Draw the dot at the swing point location
                _chart.DrawIcon(
                    dotId,
                    ChartIconType.Circle,
                    swingPoint.Time,
                    swingPoint.Price,
                    dotColor
                );
                
                // Store the ID for future removal
                _lastInsideKeyLevelDotId = dotId;
                
                // Inside key level dot drawn
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error drawing inside key level dot: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove a key level from the chart by deleting its rectangle and middle line
        /// </summary>
        private void RemoveKeyLevelFromChart(Level keyLevel)
        {
            try
            {
                if (keyLevel == null)
                    return;
                
                // Generate the correct pattern ID based on level type
                // These must match the IDs used by the visualizers
                if (keyLevel.LevelType == LevelType.OrderBlock)
                {
                    // OrderBlockVisualizer uses: "ob-{direction}-{index}"
                    var patternId = $"ob-{keyLevel.Direction}-{keyLevel.Index}";
                    
                    // Remove order block specific objects
                    _chart.RemoveObject($"{patternId}-rect");
                    _chart.RemoveObject($"{patternId}-mid");
                    _chart.RemoveObject($"{patternId}-label");
                    
                    // Remove quadrant lines if they exist
                    _chart.RemoveObject($"{patternId}-q1");
                    _chart.RemoveObject($"{patternId}-q3");
                }
                else if (keyLevel.LevelType == LevelType.CISD)
                {
                    // CISD visualizer pattern - need to check what ID pattern is used
                    RemoveCisdFromChart(keyLevel);
                }
                
                // Also try to remove using the extended rectangle ID (if it was extended)
                if (keyLevel.IsExtended)
                {
                    string extendedRectId = GenerateRectangleId(keyLevel);
                    _chart.RemoveObject(extendedRectId);
                    _chart.RemoveObject($"{extendedRectId}-midline");
                }
                
                // Key level removed from chart
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error removing key level from chart: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove a CISD from the chart by deleting its visualization objects
        /// </summary>
        private void RemoveCisdFromChart(Level cisd)
        {
            try
            {
                if (cisd == null || cisd.LevelType != LevelType.CISD)
                    return;
                
                // CisdVisualizer uses: "cisd-{direction}-{index}"
                var patternId = $"cisd-{cisd.Direction}-{cisd.Index}";
                
                // Remove CISD specific objects
                _chart.RemoveObject($"{patternId}-activated");
                _chart.RemoveObject($"{patternId}-confirm");
                _chart.RemoveObject($"{patternId}-tf-label");
                
                // CISD removed from chart
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error removing CISD from chart: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle liquidity sweep visual updates for session and daily highs/lows
        /// Redraw the liquidity line from the original high/low to the sweeping candle
        /// Change label alignment from left to right and color to red if multiple liquidity is swept
        /// </summary>
        private void HandleLiquiditySweepVisualUpdate(SwingPoint sweptPoint, SwingPoint sweepingPoint)
        {
            try
            {
                if (sweptPoint == null || sweepingPoint == null)
                    return;

                // Handle highs (PDH, PSH) being swept by bullish swing points
                bool isHighSwept = (sweptPoint.LiquidityType == LiquidityType.PDH || sweptPoint.LiquidityType == LiquidityType.PSH) &&
                                  sweepingPoint.Direction == Direction.Up;

                // Handle lows (PDL, PSL) being swept by bearish swing points  
                bool isLowSwept = (sweptPoint.LiquidityType == LiquidityType.PDL || sweptPoint.LiquidityType == LiquidityType.PSL) &&
                                 sweepingPoint.Direction == Direction.Down;

                // Only process valid sweep combinations
                if (!isHighSwept && !isLowSwept)
                    return;

                // Create new line ID for the swept liquidity
                string originalId = $"{sweptPoint.LiquidityName.ToString().ToLower()}-{sweptPoint.Time.Ticks}";
                string sweptId = $"{originalId}-swept";

                // Remove the original line
                _chart.RemoveObject(originalId);
                _chart.RemoveObject($"{originalId}-label");

                // Keep original wheat color - don't change color based on multiple sweeps
                Color lineColor = Color.Wheat;

                // Draw new line from original level to sweeping candle
                _chart.DrawStraightLine(
                    sweptId,
                    sweptPoint.Time,           // Start at original level time
                    sweptPoint.Price,          // Start at level price
                    sweepingPoint.Time,        // End at sweeping candle time
                    sweptPoint.Price,          // End at same level price (horizontal line)
                    sweptPoint.LiquidityName.ToString(), // Label text
                    LineStyle.Solid,
                    lineColor,
                    hasLabel: true,
                    removeExisting: true,
                    labelOnRight: true         // Change label alignment to right
                );

                string levelType = isHighSwept ? "high" : "low";
                string swingType = sweepingPoint.Direction == Direction.Up ? "bullish" : "bearish";
                // Liquidity sweep visual updated
            }
            catch (Exception ex)
            {
                _logger?.Invoke($"Error updating liquidity sweep visual: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle bullish swing point cycle liquidity sweeps (sweeps cycle highs)
        /// </summary>
        private void HandleBullishSwingPointCycleLiquiditySweep(SwingPoint bullishSwingPoint)
        {
            if (_cycle30Manager == null || _cycle30Visualizer == null) return;

            // Check if this bullish swing point sweeps any cycle highs
            var cycleHighs = _cycle30Manager.GetCycleHighs().ToList(); // Convert to list to avoid collection modification issues

            foreach (var cycleHigh in cycleHighs)
            {
                // Check if bullish swing point swept this cycle high
                if (bullishSwingPoint.Price > cycleHigh.Price)
                {
                    // Mark swing point as having swept cycle liquidity
                    bullishSwingPoint.SweptCycle = true;

                    // Mark the candle that swept the cycle
                    var sweepingCandle = _candleManager.GetCandle(bullishSwingPoint.Index);
                    if (sweepingCandle != null)
                    {
                        sweepingCandle.SweptCycle = true;
                    }

                    // Draw sweep line if liquidity sweep visualization is enabled
                    if (_settings.Patterns.ShowLiquiditySweep)
                    {
                        _cycle30Visualizer.DrawLiquiditySweepLine(cycleHigh, bullishSwingPoint.Index, true);
                    }

                    // Remove the swept cycle point from collection to prevent duplicate sweeps
                    _cycle30Manager.RemoveSweptCyclePoint(cycleHigh);

                    // Publish cycle swept event for SMT detection
                    _eventAggregator.Publish(new CycleSweptEvent(cycleHigh, bullishSwingPoint));

                    _logger?.Invoke($"Cycle30 high swept at {cycleHigh.Price:F5} by bullish swing point at {bullishSwingPoint.Price:F5}");

                    // Send Telegram notification for Cycle30 liquidity sweep
                    if (_notificationService != null)
                    {
                        _notificationService.SendCycle30LiquiditySweepNotification(
                            "Cycle30 High",
                            cycleHigh.Price,
                            bullishSwingPoint.Price,
                            Direction.Up);
                    }
                }
            }
        }

        /// <summary>
        /// Handle bearish swing point cycle liquidity sweeps (sweeps cycle lows)
        /// </summary>
        private void HandleBearishSwingPointCycleLiquiditySweep(SwingPoint bearishSwingPoint)
        {
            if (_cycle30Manager == null || _cycle30Visualizer == null) return;

            // Check if this bearish swing point sweeps any cycle lows
            var cycleLows = _cycle30Manager.GetCycleLows().ToList(); // Convert to list to avoid collection modification issues

            foreach (var cycleLow in cycleLows)
            {
                // Check if bearish swing point swept this cycle low
                if (bearishSwingPoint.Price < cycleLow.Price)
                {
                    // Mark swing point as having swept cycle liquidity
                    bearishSwingPoint.SweptCycle = true;

                    // Mark the candle that swept the cycle
                    var sweepingCandle = _candleManager.GetCandle(bearishSwingPoint.Index);
                    if (sweepingCandle != null)
                    {
                        sweepingCandle.SweptCycle = true;
                    }

                    // Draw sweep line if liquidity sweep visualization is enabled
                    if (_settings.Patterns.ShowLiquiditySweep)
                    {
                        _cycle30Visualizer.DrawLiquiditySweepLine(cycleLow, bearishSwingPoint.Index, true);
                    }

                    // Remove the swept cycle point from collection to prevent duplicate sweeps
                    _cycle30Manager.RemoveSweptCyclePoint(cycleLow);

                    // Publish cycle swept event for SMT detection
                    _eventAggregator.Publish(new CycleSweptEvent(cycleLow, bearishSwingPoint));

                    _logger?.Invoke($"Cycle30 low swept at {cycleLow.Price:F5} by bearish swing point at {bearishSwingPoint.Price:F5}");

                    // Send Telegram notification for Cycle30 liquidity sweep
                    if (_notificationService != null)
                    {
                        _notificationService.SendCycle30LiquiditySweepNotification(
                            "Cycle30 Low",
                            cycleLow.Price,
                            bearishSwingPoint.Price,
                            Direction.Down);
                    }
                }
            }
        }

        /// <summary>
        /// Dispose of resources and unsubscribe from events
        /// </summary>
        public void Dispose()
        {
            _eventAggregator?.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            _eventAggregator?.Unsubscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
            _eventAggregator?.Unsubscribe<OrderBlockDetectedEvent>(OnOrderBlockDetected);
            _eventAggregator?.Unsubscribe<CisdConfirmedEvent>(OnCisdConfirmed);
        }
    }
}