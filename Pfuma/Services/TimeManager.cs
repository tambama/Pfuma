using System;
using cAlgo.API;
using Pfuma.Detectors;
using Pfuma.Services.Time;

namespace Pfuma.Services
{
    /// <summary>
    /// Coordinates time-related functionality by delegating to specialized managers
    /// </summary>
    public class TimeManager
    {
        private readonly IMacroTimeManager _macroTimeManager;
        private readonly IDailyLevelManager _dailyLevelManager;
        private readonly ISessionLevelManager _sessionLevelManager;
        private readonly int _utcOffset;
        private readonly bool _showDailyLevels;
        private readonly bool _showSessionLevels;

        public TimeManager(
            Chart chart,
            CandleManager candleManager,
            SwingPointDetector swingPointDetector,
            NotificationService notificationService,
            bool showMacros = true,
            bool showDailyLevels = true,
            bool showSessionLevels = true,
            int utcOffset = -4)
        {
            _utcOffset = utcOffset;
            _showDailyLevels = showDailyLevels;
            _showSessionLevels = showSessionLevels;
            
            _macroTimeManager = new MacroTimeManager(
                chart, 
                notificationService, 
                showMacros);
            
            _dailyLevelManager = new DailyLevelManager(
                candleManager,
                swingPointDetector,
                chart,
                showDailyLevels);
                
            _sessionLevelManager = new SessionLevelManager(
                candleManager,
                swingPointDetector,
                chart,
                showSessionLevels);
        }

        /// <summary>
        /// Process a bar for all time-related features
        /// </summary>
        public void ProcessBar(int index, DateTime time)
        {
            try
            {
                DateTime marketTime = time.AddHours(_utcOffset);
                
                // Process macro times
                _macroTimeManager?.ProcessMacroTimes(marketTime, time);
                
                // Handle daily boundaries (18:00)
                if (_showDailyLevels && marketTime.Hour == 18 && marketTime.Minute == 0)
                {
                    _dailyLevelManager?.ProcessDailyBoundary(index);
                }
                
                // Process session levels
                if (_showSessionLevels)
                {
                    _sessionLevelManager?.ProcessBar(index, marketTime);
                }
            }
            catch (Exception)
            {
                // Silently handle errors to prevent indicator crash
                // In production, you might want to log this
            }
        }

        
        
        /// <summary>
        /// Check if the given time is within a macro time window
        /// </summary>
        public bool IsInMacroTime(DateTime time)
        {
            DateTime marketTime = time.AddHours(_utcOffset);
            TimeSpan timeOfDay = marketTime.TimeOfDay;
            
            var macroRanges = _macroTimeManager.GetMacroRanges();
            
            foreach (var macro in macroRanges)
            {
                if (timeOfDay >= macro.StartTime && timeOfDay <= macro.EndTime)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}