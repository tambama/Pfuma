using System;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;

namespace Pfuma.Analyzers
{
    /// <summary>
    /// Coordinates and orchestrates PD Array analysis across multiple detectors
    /// This is a refactored version that delegates to individual detectors
    /// </summary>
    public class PdArrayAnalyzer : IInitializable
    {
        private readonly Chart _chart;
        private readonly IEventAggregator _eventAggregator;
        private readonly IndicatorSettings _settings;
        private readonly Action<string> _logger;
        
        // SMT Support
        public delegate double PairDataProviderDelegate(string pairSymbol, DateTime time, int index, Direction direction);
        public PairDataProviderDelegate PairDataProvider { get; set; }
        
        public PdArrayAnalyzer(
            Chart chart,
            IEventAggregator eventAggregator,
            IndicatorSettings settings,
            Action<string> logger = null)
        {
            _chart = chart;
            _eventAggregator = eventAggregator;
            _settings = settings;
            _logger = logger ?? (_ => { });
        }
        
        public void Initialize()
        {
            // Subscribe to events from detectors
            _eventAggregator.Subscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
            _eventAggregator.Subscribe<FvgDetectedEvent>(OnFvgDetected);
            _eventAggregator.Subscribe<CisdConfirmedEvent>(OnCisdConfirmed);
        }
        
        public void Dispose()
        {
            _eventAggregator.Unsubscribe<OrderFlowDetectedEvent>(OnOrderFlowDetected);
            _eventAggregator.Unsubscribe<FvgDetectedEvent>(OnFvgDetected);
            _eventAggregator.Unsubscribe<CisdConfirmedEvent>(OnCisdConfirmed);
        }
        
        /// <summary>
        /// Check for SMT divergence between two swing points
        /// </summary>
        public void CheckForSmtDivergence(SwingPoint point1, SwingPoint point2)
        {
            if (!_settings.Patterns.ShowSMT || string.IsNullOrEmpty(_settings.Patterns.SmtPair) || PairDataProvider == null)
                return;
            
            try
            {
                // Get pair values at both points
                double pairValue1 = PairDataProvider(_settings.Patterns.SmtPair, point1.Time, point1.Index, point1.Direction);
                double pairValue2 = PairDataProvider(_settings.Patterns.SmtPair, point2.Time, point2.Index, point2.Direction);
                
                // Store SMT values
                point1.SMTValue = pairValue1;
                point2.SMTValue = pairValue2;
                
                // Check for divergence
                bool hasDivergence = false;
                
                if (point1.Direction == Direction.Up)
                {
                    // For highs: main makes higher high but pair makes lower high
                    hasDivergence = point2.Price > point1.Price && pairValue2 < pairValue1;
                }
                else
                {
                    // For lows: main makes lower low but pair makes higher low
                    hasDivergence = point2.Price < point1.Price && pairValue2 > pairValue1;
                }
                
                if (hasDivergence)
                {
                    point2.HasSMT = true;
                    point2.SMTSource = point1;
                    DrawSmtDivergence(point1, point2);
                }
            }
            catch (Exception ex)
            {
                _logger($"Error checking SMT divergence: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Draws SMT divergence on the chart
        /// </summary>
        private void DrawSmtDivergence(SwingPoint source, SwingPoint target)
        {
            if (_chart == null)
                return;
            
            string id = $"smt-{source.Time.Ticks}-{target.Time.Ticks}";
            
            _chart.DrawTrendLine(
                id,
                source.Time,
                source.Price,
                target.Time,
                target.Price,
                Color.Purple,
                2,
                LineStyle.Dots
            );
        }
        
        private void OnOrderFlowDetected(OrderFlowDetectedEvent evt)
        {
            // Additional processing for order flows if needed
            if (_settings.Notifications.EnableLog)
            {
                _logger($"Order flow detected: {evt.OrderFlow.Direction} at index {evt.OrderFlow.Index}");
            }
        }
        
        private void OnFvgDetected(FvgDetectedEvent evt)
        {
            // Additional processing for FVGs if needed
            if (_settings.Notifications.EnableLog)
            {
                _logger($"FVG detected: {evt.FvgLevel.Direction} at index {evt.FvgLevel.Index}");
            }
        }
        
        private void OnCisdConfirmed(CisdConfirmedEvent evt)
        {
            // Additional processing for confirmed CISDs if needed
            if (_settings.Notifications.EnableLog)
            {
                _logger($"CISD confirmed: {evt.CisdLevel.Direction} at index {evt.CisdLevel.IndexOfConfirmingCandle}");
            }
        }
    }
}