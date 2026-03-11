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
        /// Send a notification about liquidity being swept
        /// </summary>
        public void NotifyLiquiditySwept(SwingPoint sweptPoint, LiquidityType liquidityType)
        {
            string liquidityName = liquidityType.GetDescription();
            string message = $"{liquidityName} swept for {_symbol} at {DateTime.UtcNow.AddHours(_utcOffset)}";

            SendNotification(message);
        }
        
        /// <summary>
        /// Send a notification when session or daily levels are swept
        /// </summary>
        public void NotifyLiquiditySweep(SwingPoint liquidityLevel, SwingPoint sweepingSwingPoint)
        {
            // Determine the type of liquidity swept
            string liquidityType = "";
            string emoji = "";
            
            switch (liquidityLevel.LiquidityName)
            {
                case LiquidityName.PDH:
                    liquidityType = "Previous Daily High (PDH)";
                    emoji = "📈";
                    break;
                case LiquidityName.PDL:
                    liquidityType = "Previous Daily Low (PDL)";
                    emoji = "📉";
                    break;
                case LiquidityName.AH:
                    liquidityType = "Asia Session High";
                    emoji = "🌏";
                    break;
                case LiquidityName.AL:
                    liquidityType = "Asia Session Low";
                    emoji = "🌏";
                    break;
                case LiquidityName.LH:
                    liquidityType = "London Session High";
                    emoji = "🏴󐁧󐁢󐁥󐁮󐁧󐁿";
                    break;
                case LiquidityName.LL:
                    liquidityType = "London Session Low";
                    emoji = "🏴󐁧󐁢󐁥󐁮󐁧󐁿";
                    break;
                case LiquidityName.NYAMH:
                    liquidityType = "New York AM High";
                    emoji = "🗽";
                    break;
                case LiquidityName.NYAML:
                    liquidityType = "New York AM Low";
                    emoji = "🗽";
                    break;
                case LiquidityName.NYPMH:
                    liquidityType = "New York PM High";
                    emoji = "🌃";
                    break;
                case LiquidityName.NYPML:
                    liquidityType = "New York PM Low";
                    emoji = "🌃";
                    break;
                default:
                    liquidityType = liquidityLevel.LiquidityName.ToString();
                    emoji = "💧";
                    break;
            }
            
            string direction = sweepingSwingPoint.Direction == Direction.Up ? "Bullish" : "Bearish";
            string message = $"{emoji} LIQUIDITY SWEEP!\n" +
                           $"Symbol: {_symbol}\n" +
                           $"Level: {liquidityType} at {liquidityLevel.Price:F5}\n" +
                           $"Swept by: {direction} swing at {sweepingSwingPoint.Price:F5}\n" +
                           $"Time: {DateTime.UtcNow.AddHours(_utcOffset):yyyy-MM-dd HH:mm:ss}";
            
            SendNotification(message);
        }


        /// <summary>
        /// Send a notification when an Order Block is created inside a key level
        /// </summary>
        public void NotifyOrderBlockInsideKeyLevel(Level orderBlock, Level keyLevel)
        {
            string obType = orderBlock.Direction == Direction.Up ? "Bullish" : "Bearish";
            string keyLevelType = GetKeyLevelTypeDescription(keyLevel);
            string message = $"💎 {obType} Order Block FORMED INSIDE {keyLevelType} for {_symbol} - Range: {orderBlock.Low:F5}-{orderBlock.High:F5} - Time: {DateTime.UtcNow.AddHours(_utcOffset):HH:mm:ss}";

            SendNotification(message);
        }

        /// <summary>
        /// Send a notification when a CISD is confirmed inside a key level
        /// </summary>
        public void NotifyCisdInsideKeyLevel(Level cisd, Level keyLevel)
        {
            string cisdType = cisd.Direction == Direction.Up ? "Bullish" : "Bearish";
            string keyLevelType = GetKeyLevelTypeDescription(keyLevel);
            string message = $"🚀 {cisdType} CISD CONFIRMED INSIDE {keyLevelType} for {_symbol} - Range: {cisd.Low:F5}-{cisd.High:F5} - Time: {DateTime.UtcNow.AddHours(_utcOffset):HH:mm:ss}";

            SendNotification(message);
        }

        /// <summary>
        /// Send a notification when Cycle30 liquidity is swept
        /// </summary>
        public void SendCycle30LiquiditySweepNotification(string cycleType, double cyclePrice, double sweepPrice, Direction direction)
        {
            string directionIcon = direction == Direction.Up ? "📈" : "📉";
            string sweepAction = direction == Direction.Up ? "ABOVE" : "BELOW";

            string message = $"{directionIcon} CYCLE30 LIQUIDITY SWEEP! {cycleType} at {cyclePrice:F5} swept {sweepAction} by price {sweepPrice:F5} - {_symbol} - Time: {DateTime.UtcNow.AddHours(_utcOffset):HH:mm:ss}";

            SendNotification(message);
        }

        /// <summary>
        /// Get a friendly description of a key level type
        /// </summary>
        private string GetKeyLevelTypeDescription(Level keyLevel)
        {
            var direction = keyLevel.Direction == Direction.Up ? "Bullish" : "Bearish";
            return keyLevel.LevelType switch
            {
                LevelType.OrderBlock => $"{direction} Order Block",
                LevelType.CISD => $"{direction} CISD",
                LevelType.FairValueGap => $"{direction} FVG",
                LevelType.BreakerBlock => $"{direction} Breaker Block",
                LevelType.RejectionBlock => $"{direction} Rejection Block",
                _ => $"{direction} Key Level"
            };
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