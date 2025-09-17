using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Services
{
    public class SignalManager
    {
        private readonly List<Signal> _signals;
        private readonly Chart _chart;
        private readonly NotificationService _notificationService;
        private Signal _currentSignal;

        public SignalManager(Chart chart, NotificationService notificationService)
        {
            _signals = new List<Signal>();
            _chart = chart;
            _notificationService = notificationService;
        }

        public void AddSignal(double entry, double stop, Direction direction, double riskReward, DateTime timestamp, int barIndex)
        {
            // Only allow one signal at a time - close current signal if exists
            if (_currentSignal != null && _currentSignal.Status != SignalStatus.Closed)
            {
                _currentSignal.Status = SignalStatus.Closed;
                _currentSignal.Result = SignalResult.BE;
                _currentSignal.CloseTime = timestamp;
                _currentSignal.CloseBarIndex = barIndex;
            }

            // Calculate take profit based on risk reward ratio
            double takeProfit;
            double risk = Math.Abs(entry - stop);

            if (direction == Direction.Up)
            {
                takeProfit = entry + (risk * riskReward);
            }
            else
            {
                takeProfit = entry - (risk * riskReward);
            }

            var signal = new Signal(
                Guid.NewGuid().ToString(),
                entry,
                stop,
                takeProfit,
                direction,
                timestamp,
                barIndex
            );

            _signals.Add(signal);
            _currentSignal = signal;

            // Send Telegram notification
            SendSignalNotification(signal);
        }

        public void ProcessBar(Bar bar, int barIndex)
        {
            if (_currentSignal == null || _currentSignal.Status == SignalStatus.Closed)
                return;

            var signal = _currentSignal;
            var high = bar.High;
            var low = bar.Low;
            var open = bar.Open;
            var close = bar.Close;

            // Check for signal activation (Ready -> Open)
            if (signal.Status == SignalStatus.Ready)
            {
                bool activated = false;

                if (signal.Direction == Direction.Up) // Buy Signal
                {
                    // When candle opens above entry and low touches entry
                    if (open > signal.Entry && low <= signal.Entry)
                    {
                        activated = true;
                    }
                }
                else // Sell Signal
                {
                    // When candle opens below entry and high touches entry
                    if (open < signal.Entry && high >= signal.Entry)
                    {
                        activated = true;
                    }
                }

                if (activated)
                {
                    signal.Status = SignalStatus.Open;
                    signal.OpenTime = bar.OpenTime;
                    signal.OpenBarIndex = barIndex;
                }
            }

            // Check for signal closure (Take Profit, Stop Loss)
            if (signal.Status == SignalStatus.Open || signal.Status == SignalStatus.Ready)
            {
                bool closed = false;
                SignalResult result = SignalResult.None;

                if (signal.Direction == Direction.Up) // Buy Signal
                {
                    // Check Stop Loss: high above stop and low below stop
                    if (high >= signal.Stop && low <= signal.Stop)
                    {
                        closed = true;
                        result = SignalResult.L;
                    }
                    // Check Take Profit: low below TP and high above TP
                    else if (low <= signal.TakeProfit && high >= signal.TakeProfit)
                    {
                        closed = true;
                        result = SignalResult.TP;
                    }
                }
                else // Sell Signal
                {
                    // Check Stop Loss: low below stop and high above stop
                    if (low <= signal.Stop && high >= signal.Stop)
                    {
                        closed = true;
                        result = SignalResult.L;
                    }
                    // Check Take Profit: high above TP and low below TP
                    else if (high >= signal.TakeProfit && low <= signal.TakeProfit)
                    {
                        closed = true;
                        result = SignalResult.TP;
                    }
                }

                if (closed)
                {
                    signal.Status = SignalStatus.Closed;
                    signal.Result = result;
                    signal.CloseTime = bar.OpenTime;
                    signal.CloseBarIndex = barIndex;

                    // Send close notification
                    SendCloseNotification(signal);
                }
            }
        }

        private void SendSignalNotification(Signal signal)
        {
            var direction = signal.Direction == Direction.Up ? "BUY" : "SELL";
            var message = $"🔔 New {direction} Signal\n" +
                         $"Entry: {signal.Entry:F5}\n" +
                         $"Stop: {signal.Stop:F5}\n" +
                         $"TP: {signal.TakeProfit:F5}\n" +
                         $"Time: {signal.Timestamp:yyyy-MM-dd HH:mm}";

            // Use reflection to call SendNotification since it's private
            var sendMethod = _notificationService?.GetType().GetMethod("SendNotification",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sendMethod?.Invoke(_notificationService, new object[] { message });
        }

        private void SendCloseNotification(Signal signal)
        {
            var direction = signal.Direction == Direction.Up ? "BUY" : "SELL";
            var result = signal.Result == SignalResult.TP ? "✅ PROFIT" :
                        signal.Result == SignalResult.L ? "❌ LOSS" : "⚪ BREAK EVEN";

            var message = $"🔚 {direction} Signal Closed\n" +
                         $"Result: {result}\n" +
                         $"Entry: {signal.Entry:F5}\n" +
                         $"Close Time: {signal.CloseTime:yyyy-MM-dd HH:mm}";

            // Use reflection to call SendNotification since it's private
            var sendMethod = _notificationService?.GetType().GetMethod("SendNotification",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sendMethod?.Invoke(_notificationService, new object[] { message });
        }

        public SignalStats GetStats()
        {
            var closedSignals = _signals.Where(s => s.Status == SignalStatus.Closed).ToList();
            var totalTrades = closedSignals.Count;
            var wins = closedSignals.Count(s => s.Result == SignalResult.TP);
            var losses = closedSignals.Count(s => s.Result == SignalResult.L);
            var winRate = totalTrades > 0 ? (double)wins / totalTrades * 100 : 0;

            // Calculate total days from first signal to now
            var totalDays = 0;
            if (_signals.Count > 0)
            {
                var firstSignalDate = _signals.Min(s => s.Timestamp).Date;
                var today = DateTime.Now.Date;
                totalDays = (int)(today - firstSignalDate).TotalDays + 1;
            }

            return new SignalStats
            {
                TotalTrades = totalTrades,
                Wins = wins,
                Losses = losses,
                WinRate = winRate,
                TotalDays = totalDays
            };
        }

        public List<Signal> GetAllSignals()
        {
            return _signals.ToList();
        }

        public Signal GetCurrentSignal()
        {
            return _currentSignal;
        }
    }

    public class SignalStats
    {
        public int TotalTrades { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double WinRate { get; set; }
        public int TotalDays { get; set; }
    }
}