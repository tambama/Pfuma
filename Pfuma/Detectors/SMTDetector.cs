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

            _eventAggregator.Subscribe<CycleSweptEvent>(OnCycleSwept);
            _eventAggregator.Subscribe<SwingPointRemovedEvent>(OnSwingPointRemoved);
        }

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

        private void OnCycleSwept(CycleSweptEvent evt)
        {
            try
            {
                if (evt?.SweptCyclePoint == null || evt?.SweepingSwingPoint == null)
                    return;

                var sweptPoint = evt.SweptCyclePoint;
                var sweepingPoint = evt.SweepingSwingPoint;

                bool hasSMTDivergence = DetectSMTDivergence(sweptPoint, sweepingPoint);

                if (hasSMTDivergence)
                {
                    sweepingPoint.HasSMT = true;
                    sweepingPoint.SweptCyclePoint = sweptPoint;

                    StoreSMTSymbolPrices(sweepingPoint, sweptPoint);
                    MarkCandleWithSMT(sweepingPoint.Index);

                    _log?.Invoke($"SMT Divergence detected: {sweepingPoint.Direction} swing point at {sweepingPoint.Price:F5}");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error in cycle swept handler: {ex.Message}");
            }
        }

        private void OnSwingPointRemoved(SwingPointRemovedEvent evt)
        {
            try
            {
                var removedPoint = evt.SwingPoint;
                if (removedPoint?.HasSMT == true)
                {
                    _eventAggregator.Publish(new SMTLineRemovedEvent(removedPoint));
                    _log?.Invoke($"SMT line removal triggered for swing point at {removedPoint.Price:F5}");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error in swing point removed handler: {ex.Message}");
            }
        }

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

        public void ReEvaluateSMT(SwingPoint newSwingPoint, SwingPoint oldSwingPoint)
        {
            try
            {
                if (oldSwingPoint?.HasSMT != true || oldSwingPoint.SweptCyclePoint == null)
                    return;

                newSwingPoint.SweptCyclePoint = oldSwingPoint.SweptCyclePoint;

                bool hasSMTDivergence = DetectSMTDivergence(oldSwingPoint.SweptCyclePoint, newSwingPoint);

                if (hasSMTDivergence)
                {
                    newSwingPoint.HasSMT = true;
                    StoreSMTSymbolPrices(newSwingPoint, oldSwingPoint.SweptCyclePoint);
                    MarkCandleWithSMT(newSwingPoint.Index);

                    _log?.Invoke($"SMT re-evaluated: Still valid for {newSwingPoint.Direction} swing point at {newSwingPoint.Price:F5}");
                }
                else
                {
                    newSwingPoint.HasSMT = false;

                    _log?.Invoke($"SMT re-evaluated: No longer valid for {newSwingPoint.Direction} swing point at {newSwingPoint.Price:F5}");
                }
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error re-evaluating SMT: {ex.Message}");
            }
        }

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
            }
            catch (Exception ex)
            {
                _log?.Invoke($"SMT: Error marking candle with SMT: {ex.Message}");
            }
        }

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

                    var sweptBar = bars[sweptPoint.Index];
                    var sweepingBar = bars[sweepingPoint.Index];

                    if (sweptBar == null || sweepingBar == null)
                        continue;

                    bool hasDivergence = false;

                    if (sweepingPoint.Direction == Direction.Up)
                    {
                        double sweptHigh = sweptBar.High;
                        double sweepingHigh = sweepingBar.High;

                        if (sweepingPoint.Price > sweptPoint.Price && sweepingHigh <= sweptHigh)
                        {
                            hasDivergence = true;
                            _log?.Invoke($"SMT: Bullish divergence with {symbol} - Price: {sweepingPoint.Price:F5} > {sweptPoint.Price:F5}, SMT: {sweepingHigh:F5} <= {sweptHigh:F5}");
                        }
                    }
                    else if (sweepingPoint.Direction == Direction.Down)
                    {
                        double sweptLow = sweptBar.Low;
                        double sweepingLow = sweepingBar.Low;

                        if (sweepingPoint.Price < sweptPoint.Price && sweepingLow >= sweptLow)
                        {
                            hasDivergence = true;
                            _log?.Invoke($"SMT: Bearish divergence with {symbol} - Price: {sweepingPoint.Price:F5} < {sweptPoint.Price:F5}, SMT: {sweepingLow:F5} >= {sweptLow:F5}");
                        }
                    }

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