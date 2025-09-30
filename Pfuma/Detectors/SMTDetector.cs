using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    public interface ISMTDetector
    {
        void Initialize(string smtSymbols);
    }

    /// <summary>
    /// Detects SMT (Smart Money Theory) Divergence between the current symbol and SMT symbols
    /// When a cycle is swept but corresponding SMT symbols don't follow, it indicates divergence
    /// </summary>
    public class SMTDetector : ISMTDetector
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly Action<string> _log;
        private readonly Indicator _indicator;
        private readonly CandleManager _candleManager;
        private List<string> _smtSymbols;
        private Dictionary<string, Bars> _smtBars;

        public SMTDetector(IEventAggregator eventAggregator, Indicator indicator, CandleManager candleManager, Action<string> log = null)
        {
            _eventAggregator = eventAggregator;
            _indicator = indicator;
            _candleManager = candleManager;
            _log = log;
            _smtSymbols = new List<string>();
            _smtBars = new Dictionary<string, Bars>();

            // Subscribe to cycle swept events and swing point removal events
            _eventAggregator.Subscribe<CycleSweptEvent>(OnCycleSwept);
            _eventAggregator.Subscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
        }

        /// <summary>
        /// Initialize SMT symbols from comma-separated string
        /// </summary>
        public void Initialize(string smtSymbols)
        {
            if (string.IsNullOrWhiteSpace(smtSymbols))
                return;

            try
            {
                _smtSymbols = smtSymbols.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                // Get bars for each SMT symbol
                foreach (var symbol in _smtSymbols)
                {
                    try
                    {
                        var symbolInfo = _indicator.Symbols.GetSymbol(symbol);
                        if (symbolInfo != null)
                        {
                            var bars = _indicator.MarketData.GetBars(_indicator.TimeFrame, symbol);
                            _smtBars[symbol] = bars;
                            _log?.Invoke($"SMT: Loaded data for symbol {symbol}");
                        }
                        else
                        {
                            _log?.Invoke($"SMT: Warning - Symbol {symbol} not found");
                        }
                    }
                    catch (Exception ex)
                    {
                        _log?.Invoke($"SMT: Error loading symbol {symbol}: {ex.Message}");
                    }
                }

                _log?.Invoke($"SMT: Initialized with {_smtBars.Count} symbols");
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error initializing symbols: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle cycle swept events to detect SMT divergence
        /// </summary>
        private void OnCycleSwept(CycleSweptEvent evt)
        {
            try
            {
                if (evt?.SweptCyclePoint == null || evt?.SweepingSwingPoint == null)
                    return;

                var sweptPoint = evt.SweptCyclePoint;
                var sweepingPoint = evt.SweepingSwingPoint;

                // Check for SMT divergence across all SMT symbols
                bool hasSMTDivergence = DetectSMTDivergence(sweptPoint, sweepingPoint);

                if (hasSMTDivergence)
                {
                    // Mark the sweeping swing point as having SMT and store SMT data
                    sweepingPoint.HasSMT = true;
                    sweepingPoint.SweptCyclePoint = sweptPoint;

                    // Store SMT symbol prices at the time of detection
                    StoreSMTSymbolPrices(sweepingPoint, sweptPoint);

                    // Mark the candle at the swing point index as having SMT
                    MarkCandleWithSMT(sweepingPoint.Index);

                    _log?.Invoke($"SMT Divergence detected: {sweepingPoint.Direction} swing point at {sweepingPoint.Price:F5}");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error in cycle swept handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle swing point removal events to clean up SMT lines
        /// </summary>
        private void OnSwingPointRemoved(SwingPointRemovedEvent evt)
        {
            try
            {
                var removedPoint = evt.SwingPoint;
                if (removedPoint?.HasSMT == true)
                {
                    // Remove SMT marking from the candle
                    //UnmarkCandleWithSMT(removedPoint.Index);

                    // Publish event to remove SMT visualization
                    _eventAggregator.Publish(new SMTLineRemovedEvent(removedPoint));
                    _log?.Invoke($"SMT line removal triggered for swing point at {removedPoint.Price:F5}");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error in swing point removed handler: {ex.Message}");
            }
        }

        /// <summary>
        /// Store SMT symbol prices for the sweeping point
        /// </summary>
        private void StoreSMTSymbolPrices(SwingPoint sweepingPoint, SwingPoint sweptPoint)
        {
            try
            {
                sweepingPoint.SMTSymbolPrices.Clear();

                foreach (var kvp in _smtBars)
                {
                    string symbol = kvp.Key;
                    Bars bars = kvp.Value;

                    if (bars != null && sweepingPoint.Index < bars.Count)
                    {
                        var bar = bars[sweepingPoint.Index];
                        double price = sweepingPoint.Direction == Direction.Up ? bar.High : bar.Low;
                        sweepingPoint.SMTSymbolPrices[symbol] = price;
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error storing symbol prices: {ex.Message}");
            }
        }

        /// <summary>
        /// Re-evaluate SMT divergence for a moved swing point
        /// </summary>
        public void ReEvaluateSMT(SwingPoint newSwingPoint, SwingPoint oldSwingPoint)
        {
            try
            {
                if (oldSwingPoint?.HasSMT != true || oldSwingPoint.SweptCyclePoint == null)
                    return;

                // Remove SMT marking from the old candle
                //UnmarkCandleWithSMT(oldSwingPoint.Index);

                // Copy SMT data from old swing point
                newSwingPoint.SweptCyclePoint = oldSwingPoint.SweptCyclePoint;

                // Re-evaluate SMT divergence with new price
                bool hasSMTDivergence = DetectSMTDivergence(oldSwingPoint.SweptCyclePoint, newSwingPoint);

                if (hasSMTDivergence)
                {
                    newSwingPoint.HasSMT = true;
                    StoreSMTSymbolPrices(newSwingPoint, oldSwingPoint.SweptCyclePoint);

                    // Mark the new candle as having SMT
                    MarkCandleWithSMT(newSwingPoint.Index);

                    _log?.Invoke($"SMT re-evaluated: Still valid for {newSwingPoint.Direction} swing point at {newSwingPoint.Price:F5}");
                }
                else
                {
                    newSwingPoint.HasSMT = false;

                    // Remove SMT marking from the new candle since SMT is no longer valid
                    //UnmarkCandleWithSMT(newSwingPoint.Index);

                    _log?.Invoke($"SMT re-evaluated: No longer valid for {newSwingPoint.Direction} swing point at {newSwingPoint.Price:F5}");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error re-evaluating SMT: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark the candle at the specified index as having SMT divergence
        /// </summary>
        private void MarkCandleWithSMT(int candleIndex)
        {
            try
            {
                var candle = _candleManager.GetCandle(candleIndex);
                if (candle != null)
                {
                    candle.HasSMT = true;
                    _log?.Invoke($"SMT: Marked candle at index {candleIndex} as having SMT");
                }
                else
                {
                    _log?.Invoke($"SMT: Warning - Candle at index {candleIndex} not found");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error marking candle with SMT: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove SMT marking from the candle at the specified index
        /// </summary>
        private void UnmarkCandleWithSMT(int candleIndex)
        {
            try
            {
                var candle = _candleManager.GetCandle(candleIndex);
                if (candle != null)
                {
                    candle.HasSMT = false;
                    _log?.Invoke($"SMT: Removed SMT marking from candle at index {candleIndex}");
                }
                else
                {
                    _log?.Invoke($"SMT: Warning - Candle at index {candleIndex} not found for unmarking");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error unmarking candle with SMT: {ex.Message}");
            }
        }

        /// <summary>
        /// Detect SMT divergence by comparing price action with SMT symbols
        /// </summary>
        private bool DetectSMTDivergence(SwingPoint sweptPoint, SwingPoint sweepingPoint)
        {
            if (_smtBars.Count == 0)
                return false;

            try
            {
                foreach (var kvp in _smtBars)
                {
                    string symbol = kvp.Key;
                    Bars bars = kvp.Value;

                    if (bars == null || sweptPoint.Index >= bars.Count || sweepingPoint.Index >= bars.Count)
                        continue;

                    // Get SMT symbol prices at both points
                    var sweptBar = bars[sweptPoint.Index];
                    var sweepingBar = bars[sweepingPoint.Index];

                    if (sweptBar == null || sweepingBar == null)
                        continue;

                    bool hasDivergence = false;

                    if (sweepingPoint.Direction == Direction.Up)
                    {
                        // Bullish: Cycle high was swept, check if SMT symbol also made higher high
                        double sweptHigh = sweptBar.High;
                        double sweepingHigh = sweepingBar.High;

                        // SMT divergence if current swing point is higher but SMT symbol is lower
                        if (sweepingPoint.Price > sweptPoint.Price && sweepingHigh <= sweptHigh)
                        {
                            hasDivergence = true;
                            _log?.Invoke($"SMT: Bullish divergence with {symbol} - Price: {sweepingPoint.Price:F5} > {sweptPoint.Price:F5}, SMT: {sweepingHigh:F5} <= {sweptHigh:F5}");
                        }
                    }
                    else if (sweepingPoint.Direction == Direction.Down)
                    {
                        // Bearish: Cycle low was swept, check if SMT symbol also made lower low
                        double sweptLow = sweptBar.Low;
                        double sweepingLow = sweepingBar.Low;

                        // SMT divergence if current swing point is lower but SMT symbol is higher
                        if (sweepingPoint.Price < sweptPoint.Price && sweepingLow >= sweptLow)
                        {
                            hasDivergence = true;
                            _log?.Invoke($"SMT: Bearish divergence with {symbol} - Price: {sweepingPoint.Price:F5} < {sweptPoint.Price:F5}, SMT: {sweepingLow:F5} >= {sweptLow:F5}");
                        }
                    }

                    // If any SMT symbol shows divergence, return true
                    if (hasDivergence)
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error detecting divergence: {ex.Message}");
                return false;
            }
        }
    }
}