using System;
using System.Collections.Generic;
using Pfuma.Extensions;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Service for sending notifications via logging and Telegram
    /// </summary>
    public class NotificationService
    {
        // Add a delegate for logging
        private readonly Action<string> _logger;

        private readonly TelegramService _telegramService;
        private readonly bool _enableLog;
        private readonly bool _enableTelegram;
        private readonly string _chatId;
        private readonly string _token;
        private readonly string _symbol;
        private readonly int _utcOffset;

        // Track notification times
        private Dictionary<string, DateTime> _lastNotifications = new Dictionary<string, DateTime>();

        public NotificationService(
            bool enableLog,
            bool enableTelegram,
            string chatId,
            string token,
            string symbol,
            int Utc,
            Action<string> logger = null)
        {
            _logger = logger ?? (_ => { });
            _enableLog = enableLog;
            _enableTelegram = enableTelegram;
            _chatId = chatId;
            _token = token;
            _symbol = symbol;
            _utcOffset = Utc;
            _telegramService = new TelegramService();
        }

        /// <summary>
        /// Send a notification about a CISD confirmation
        /// </summary>
        public void NotifyCisdConfirmation(Direction direction)
        {
            string key = $"CISD_{direction}_{_symbol}";

            // Only send notification if we haven't sent one for this direction and symbol in the last 10 seconds
            if (_lastNotifications.TryGetValue(key, out var lastTime) &&
                (DateTime.UtcNow.AddHours(_utcOffset) - lastTime).TotalSeconds < 10)
            {
                return; // Skip duplicate notification
            }

            _lastNotifications[key] = DateTime.UtcNow.AddHours(_utcOffset);

            string directionText = direction == Direction.Up ? "Bullish" : "Bearish";
            string message = $"{directionText} CISD confirmed for {_symbol} at {DateTime.UtcNow.AddHours(_utcOffset)}";

            SendNotification(message);
        }

        /// <summary>
        /// Send a notification about a Gauntlet detection
        /// </summary>
        public void NotifyGauntletDetected(Direction direction)
        {
            string key = $"Gauntlet_{direction}_{_symbol}";

            // Only send notification if we haven't sent one for this direction and symbol in the last 10 seconds
            if (_lastNotifications.TryGetValue(key, out var lastTime) &&
                (DateTime.UtcNow.AddHours(_utcOffset) - lastTime).TotalSeconds < 10)
            {
                return; // Skip duplicate notification
            }

            _lastNotifications[key] = DateTime.UtcNow.AddHours(_utcOffset);

            string directionText = direction == Direction.Up ? "Bullish" : "Bearish";
            string message =
                $"{directionText} Gauntlet detected for {_symbol} at {DateTime.UtcNow.AddHours(_utcOffset)}";

            SendNotification(message);
        }

        /// <summary>
        /// Send a notification about liquidity being swept
        /// </summary>
        public void NotifyLiquiditySwept(SwingPoint sweptPoint, LiquidityType liquidityType)
        {
            string liquidityName = liquidityType.GetDescription();
            string message = $"{liquidityName} swept for {_symbol} at {DateTime.UtcNow.AddHours(_utcOffset)}";

            SendNotification(message);
        }

        /// <summary>
        /// Send a notification when price enters a macro time period (always sends regardless of notification settings)
        /// </summary>
        public void NotifyMacroTimeEntered(DateTime time)
        {
            return;
            // Adjust time for UTC offset to get the market time
            DateTime marketTime = time.AddHours(_utcOffset);

            if (marketTime >= DateTime.UtcNow.AddHours(_utcOffset + 1))
                return;

            string message =
                $"MACRO TIME ALERT: Price entered macro time for {_symbol} at {marketTime:HH:mm:ss} (market time).";

            // ALWAYS log to cTrader regardless of _enableLog setting
            _logger(message);

            // ALWAYS send Telegram message if credentials are available, regardless of _enableTelegram setting
            if (!string.IsNullOrEmpty(_chatId) && !string.IsNullOrEmpty(_token))
            {
                try
                {
                    string result = _telegramService.SendTelegram(_chatId, _token, message);

                    // Log error if there was an issue sending the Telegram message
                    if (result.StartsWith("ERROR"))
                    {
                        _logger($"Failed to send macro time Telegram message: {result}");
                    }
                }
                catch (Exception ex)
                {
                    _logger($"Exception sending macro time Telegram message: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Send a notification via configured channels
        /// </summary>
        private void SendNotification(string message)
        {
            // Log to cTrader if enabled
            if (_enableLog)
            {
                _logger(message);
            }

            // Send Telegram message if enabled
            if (_enableTelegram && !string.IsNullOrEmpty(_chatId) && !string.IsNullOrEmpty(_token))
            {
                try
                {
                    string result = _telegramService.SendTelegram(_chatId, _token, message);

                    // Log error if there was an issue sending the Telegram message
                    if (result.StartsWith("ERROR"))
                    {
                        _logger($"Failed to send Telegram message: {result}");
                    }
                }
                catch (Exception ex)
                {
                    _logger($"Exception sending Telegram message: {ex.Message}");
                }
            }
        }
    }
}