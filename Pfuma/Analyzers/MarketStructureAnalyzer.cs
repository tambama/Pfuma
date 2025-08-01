using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;
using StandardDeviation = Pfuma.Models.StandardDeviation;

namespace Pfuma.Analyzers
{
    /// <summary>
    /// Analyzes market structure including BOS, CHOCH, and market bias
    /// </summary>
    public class MarketStructureAnalyzer : IInitializable
    {
        private readonly Chart _chart;
        private readonly IEventAggregator _eventAggregator;
        private readonly IndicatorSettings _settings;
        private readonly Action<string> _logger;
        
        // Indicator data series for marking structure
        private readonly IndicatorDataSeries _higherHighs;
        private readonly IndicatorDataSeries _lowerHighs;
        private readonly IndicatorDataSeries _lowerLows;
        private readonly IndicatorDataSeries _higherLows;
        
        // Structure tracking
        private Direction _bias = Direction.Up;
        private SwingPoint _highBOS;
        private SwingPoint _lowBOS;
        private SwingPoint _highCHOCH;
        private SwingPoint _lowCHOCH;
        private SwingPoint _highIND;
        private SwingPoint _lowIND;
        
        // Standard deviation management
        private readonly List<StandardDeviation> _standardDeviations;
        private DateTime? _previousHighIndTime;
        private DateTime? _previousLowIndTime;
        
        // External liquidity tracking
        private readonly List<SwingPoint> _externalLiquidity;
        
        // Swing points reference
        private readonly List<SwingPoint> _swingPoints;
        private readonly List<SwingPoint> _orderedHighs;
        private readonly List<SwingPoint> _orderedLows;
        
        public MarketStructureAnalyzer(
            Chart chart,
            IEventAggregator eventAggregator,
            IndicatorDataSeries higherHighs,
            IndicatorDataSeries lowerHighs,
            IndicatorDataSeries lowerLows,
            IndicatorDataSeries higherLows,
            IndicatorSettings settings,
            Action<string> logger = null)
        {
            _chart = chart;
            _eventAggregator = eventAggregator;
            _higherHighs = higherHighs;
            _lowerHighs = lowerHighs;
            _lowerLows = lowerLows;
            _higherLows = higherLows;
            _settings = settings;
            _logger = logger ?? (_ => { });
            
            _standardDeviations = new List<StandardDeviation>();
            _externalLiquidity = new List<SwingPoint>();
            _swingPoints = new List<SwingPoint>();
            _orderedHighs = new List<SwingPoint>();
            _orderedLows = new List<SwingPoint>();
        }
        
        public void Initialize()
        {
            // Subscribe to swing point events
            _eventAggregator.Subscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            _eventAggregator.Subscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
        }
        
        public void Dispose()
        {
            _eventAggregator.Unsubscribe<SwingPointDetectedEvent>(OnSwingPointDetected);
            _eventAggregator.Unsubscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
            _standardDeviations.Clear();
            _externalLiquidity.Clear();
        }
        
        /// <summary>
        /// Process a new swing point for market structure analysis
        /// </summary>
        public void ProcessSwingPoint(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;
            
            // Add to tracking lists
            _swingPoints.Add(swingPoint);
            
            if (swingPoint.Direction == Direction.Up)
            {
                _orderedHighs.Add(swingPoint);
                _orderedHighs.Sort((a, b) => b.Price.CompareTo(a.Price));
            }
            else
            {
                _orderedLows.Add(swingPoint);
                _orderedLows.Sort((a, b) => a.Price.CompareTo(b.Price));
            }
            
            // Analyze structure based on swing point type
            if (swingPoint.Direction == Direction.Up)
            {
                AnalyzeHighPoint(swingPoint);
            }
            else
            {
                AnalyzeLowPoint(swingPoint);
            }
        }
        
        /// <summary>
        /// Analyze a new high point for structure breaks
        /// </summary>
        private void AnalyzeHighPoint(SwingPoint highPoint)
        {
            // In an uptrend, check for break of structure (new higher high)
            if (_bias == Direction.Up && _highBOS != null && highPoint.Price > _highBOS.Price)
            {
                // Mark as higher high
                highPoint.SwingType = SwingType.HH;
                MarkHigherHigh(highPoint);
                
                // Update BOS
                _highBOS = highPoint;
                
                // Create standard deviation if enabled
                if (_settings.Visualization.ShowStandardDeviation && _lowBOS != null)
                {
                    CreateStandardDeviation(_lowBOS, highPoint);
                }
                
                // Publish BOS event
                _eventAggregator.Publish(new BreakOfStructureEvent(highPoint, Direction.Up));
            }
            // In a downtrend, check for change of character
            else if (_bias == Direction.Down && _highCHOCH != null && highPoint.Price > _highCHOCH.Price)
            {
                // Change of character - trend reversal
                _bias = Direction.Up;
                highPoint.SwingType = SwingType.HH;
                MarkHigherHigh(highPoint);
                
                // Draw CHOCH line if enabled
                if (_settings.MarketStructure.ShowChoch)
                {
                    DrawChochLine(_highCHOCH, highPoint);
                }
                
                // Reset structure
                _highBOS = highPoint;
                _lowBOS = null;
                _highCHOCH = null;
                _lowCHOCH = null;
                
                // Publish CHOCH event
                _eventAggregator.Publish(new ChangeOfCharacterEvent(highPoint, Direction.Up));
            }
            // Mark inducement points
            else if (_bias == Direction.Down && _orderedHighs.Count > 0)
            {
                _highIND = _orderedHighs[0];
                CleanupPreviousInducement(_previousHighIndTime, true);
                _previousHighIndTime = _highIND.Time;
                
                if (_settings.MarketStructure.ShowStructure)
                {
                    _chart.DrawIcon($"{_highIND.Time}-ind", ChartIconType.Diamond, 
                                  _highIND.Time, _highIND.Price, Color.Pink);
                }
            }
        }
        
        /// <summary>
        /// Analyze a new low point for structure breaks
        /// </summary>
        private void AnalyzeLowPoint(SwingPoint lowPoint)
        {
            // In a downtrend, check for break of structure (new lower low)
            if (_bias == Direction.Down && _lowBOS != null && lowPoint.Price < _lowBOS.Price)
            {
                // Mark as lower low
                lowPoint.SwingType = SwingType.LL;
                MarkLowerLow(lowPoint);
                
                // Update BOS
                _lowBOS = lowPoint;
                
                // Create standard deviation if enabled
                if (_settings.Visualization.ShowStandardDeviation && _highBOS != null)
                {
                    CreateStandardDeviation(_highBOS, lowPoint);
                }
                
                // Publish BOS event
                _eventAggregator.Publish(new BreakOfStructureEvent(lowPoint, Direction.Down));
            }
            // In an uptrend, check for change of character
            else if (_bias == Direction.Up && _lowCHOCH != null && lowPoint.Price < _lowCHOCH.Price)
            {
                // Change of character - trend reversal
                _bias = Direction.Down;
                lowPoint.SwingType = SwingType.LL;
                MarkLowerLow(lowPoint);
                
                // Draw CHOCH line if enabled
                if (_settings.MarketStructure.ShowChoch)
                {
                    DrawChochLine(_lowCHOCH, lowPoint);
                }
                
                // Reset structure
                _lowBOS = lowPoint;
                _highBOS = null;
                _lowCHOCH = null;
                _highCHOCH = null;
                
                // Publish CHOCH event
                _eventAggregator.Publish(new ChangeOfCharacterEvent(lowPoint, Direction.Down));
            }
            // Mark inducement points
            else if (_bias == Direction.Up && _orderedLows.Count > 0)
            {
                _lowIND = _orderedLows[0];
                CleanupPreviousInducement(_previousLowIndTime, false);
                _previousLowIndTime = _lowIND.Time;
                
                if (_settings.MarketStructure.ShowStructure)
                {
                    _chart.DrawIcon($"{_lowIND.Time}-ind", ChartIconType.Diamond, 
                                  _lowIND.Time, _lowIND.Price, Color.Pink);
                }
            }
        }
        
        /// <summary>
        /// Creates standard deviation levels between two swing points
        /// </summary>
        private void CreateStandardDeviation(SwingPoint from, SwingPoint to)
        {
            var stdDev = new StandardDeviation
            {
                Index = to.Index,
                OneTime = to.Time,
                TwoTime = from.Time,
                One = to.Price,
                Two = from.Price
            };
            
            // Calculate standard deviation levels
            double range = Math.Abs(to.Price - from.Price);
            double midPoint = (to.Price + from.Price) / 2;
            
            stdDev.MinusTwo = midPoint - (2 * range / 4);
            stdDev.MinusFour = midPoint - (4 * range / 4);
            
            _standardDeviations.Add(stdDev);
            
            // Draw standard deviation lines if enabled
            if (_settings.Visualization.ShowStandardDeviation)
            {
                DrawStandardDeviationLines(stdDev);
            }
        }
        
        /// <summary>
        /// Draws standard deviation lines on the chart
        /// </summary>
        private void DrawStandardDeviationLines(StandardDeviation stdDev)
        {
            // Draw -2 STD line
            _chart.DrawTrendLine(
                $"{stdDev.OneTime.Ticks}-two",
                stdDev.TwoTime,
                stdDev.MinusTwo,
                stdDev.OneTime,
                stdDev.MinusTwo,
                Color.Green,
                1,
                LineStyle.Solid
            );
            
            // Draw -4 STD line
            _chart.DrawTrendLine(
                $"{stdDev.OneTime.Ticks}-four",
                stdDev.TwoTime,
                stdDev.MinusFour,
                stdDev.OneTime,
                stdDev.MinusFour,
                Color.Red,
                1,
                LineStyle.Solid
            );
        }
        
        /// <summary>
        /// Draws a CHOCH line between two points
        /// </summary>
        private void DrawChochLine(SwingPoint from, SwingPoint to)
        {
            _chart.DrawTrendLine(
                $"choch-{from.Time}-{to.Time}",
                from.Time,
                from.Price,
                to.Time,
                from.Price,
                _bias == Direction.Up ? Color.Green : Color.Red,
                2,
                LineStyle.Solid
            );
        }
        
        /// <summary>
        /// Marks a higher high in the indicator series
        /// </summary>
        private void MarkHigherHigh(SwingPoint point)
        {
            if (point.Index >= 0 && point.Index < _higherHighs.Count)
            {
                _higherHighs[point.Index] = point.Price;
            }
            
            if (_settings.MarketStructure.ShowStructure)
            {
                _chart.DrawIcon($"{point.Time}-hh", ChartIconType.Circle, 
                              point.Time, point.Price, Color.Green);
            }
            
            _externalLiquidity.Add(point);
        }
        
        /// <summary>
        /// Marks a lower low in the indicator series
        /// </summary>
        private void MarkLowerLow(SwingPoint point)
        {
            if (point.Index >= 0 && point.Index < _lowerLows.Count)
            {
                _lowerLows[point.Index] = point.Price;
            }
            
            if (_settings.MarketStructure.ShowStructure)
            {
                _chart.DrawIcon($"{point.Time}-ll", ChartIconType.Circle, 
                              point.Time, point.Price, Color.Red);
            }
            
            _externalLiquidity.Add(point);
        }
        
        /// <summary>
        /// Cleans up previous inducement markings
        /// </summary>
        private void CleanupPreviousInducement(DateTime? previousTime, bool isHigh)
        {
            if (previousTime.HasValue)
            {
                _chart.RemoveObject($"{previousTime.Value}-ind");
            }
        }
        
        /// <summary>
        /// Gets the current market bias
        /// </summary>
        public Direction GetBias()
        {
            return _bias;
        }
        
        /// <summary>
        /// Gets external liquidity points
        /// </summary>
        public List<SwingPoint> GetExternalLiquidityPoints()
        {
            return _externalLiquidity.ToList();
        }
        
        /// <summary>
        /// Gets all standard deviations
        /// </summary>
        public List<StandardDeviation> GetStandardDeviations()
        {
            return _standardDeviations.ToList();
        }
        
        private void OnSwingPointDetected(SwingPointDetectedEvent evt)
        {
            ProcessSwingPoint(evt.SwingPoint);
        }
        
        private void OnSwingPointRemoved(SwingPointRemovedEvent evt)
        {
            // Remove from tracking lists
            _swingPoints.RemoveAll(p => p.Index == evt.SwingPoint.Index);
            _orderedHighs.RemoveAll(p => p.Index == evt.SwingPoint.Index);
            _orderedLows.RemoveAll(p => p.Index == evt.SwingPoint.Index);
            _externalLiquidity.RemoveAll(p => p.Index == evt.SwingPoint.Index);
            
            // Remove associated standard deviations
            _standardDeviations.RemoveAll(sd => sd.Index == evt.SwingPoint.Index);
        }
    }
}