using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Services.Time
{
    public interface IMacroTimeManager
    {
        void ProcessMacroTimes(DateTime marketTime, DateTime chartTime);
        List<TimeRange> GetMacroRanges();
    }

    /// <summary>
    /// Manages ICT macro time periods
    /// </summary>
    public class MacroTimeManager : IMacroTimeManager  
    {
        private readonly Chart _chart;
        private readonly NotificationService _notificationService;
        private readonly bool _showMacros;
        private readonly List<TimeRange> _macros;
        private readonly HashSet<string> _drawnMacroTimes = new HashSet<string>();

        public MacroTimeManager(
            Chart chart,
            NotificationService notificationService,
            bool showMacros)
        {
            _chart = chart;
            _notificationService = notificationService;
            _showMacros = showMacros;
            _macros = InitializeMacros();
        }

        public void ProcessMacroTimes(DateTime marketTime, DateTime chartTime)
        {
            if (!_showMacros || _chart == null) return;

            DateTime dateOnly = marketTime.Date;
            TimeSpan timeOfDay = marketTime.TimeOfDay;

            foreach (var macro in _macros)
            {
                string macroKey = $"{dateOnly:yyyyMMdd}-{macro.StartTime.Hours:D2}{macro.StartTime.Minutes:D2}";
                bool closeToMacroStart = Math.Abs((timeOfDay - macro.StartTime).TotalMinutes) < 1;

                if (closeToMacroStart && !_drawnMacroTimes.Contains(macroKey))
                {
                    DrawMacroLines(chartTime, macro);
                    _drawnMacroTimes.Add(macroKey);
                    _notificationService?.NotifyMacroTimeEntered(chartTime);
                }
            }
        }

        public List<TimeRange> GetMacroRanges() => _macros.ToList();

        private void DrawMacroLines(DateTime startTime, TimeRange macro)
        {
            string startId = $"macro-start-{startTime.Ticks}";
            _chart.DrawVerticalLine(startId, startTime, Color.Gray, 1, LineStyle.DotsRare);

            TimeSpan duration = macro.EndTime - macro.StartTime;
            DateTime endTime = startTime.Add(duration);
            
            string endId = $"macro-end-{endTime.Ticks}";
            _chart.DrawVerticalLine(endId, endTime, Color.Gray, 1, LineStyle.DotsRare);
        }

        public static List<TimeRange> InitializeMacros()
        {
            return new List<TimeRange>
            {
                new TimeRange(new TimeSpan(0, 50, 0), new TimeSpan(1, 10, 0)),
                new TimeRange(new TimeSpan(1, 50, 0), new TimeSpan(2, 10, 0)),
                new TimeRange(new TimeSpan(2, 50, 0), new TimeSpan(3, 10, 0)),
                new TimeRange(new TimeSpan(3, 50, 0), new TimeSpan(4, 10, 0)),
                new TimeRange(new TimeSpan(4, 50, 0), new TimeSpan(5, 10, 0)),
                new TimeRange(new TimeSpan(5, 50, 0), new TimeSpan(6, 10, 0)),
                new TimeRange(new TimeSpan(6, 50, 0), new TimeSpan(7, 10, 0)),
                new TimeRange(new TimeSpan(7, 50, 0), new TimeSpan(8, 10, 0)),
                new TimeRange(new TimeSpan(8, 50, 0), new TimeSpan(9, 10, 0)),
                new TimeRange(new TimeSpan(9, 50, 0), new TimeSpan(10, 10, 0)),
                new TimeRange(new TimeSpan(10, 50, 0), new TimeSpan(11, 10, 0)),
                new TimeRange(new TimeSpan(11, 50, 0), new TimeSpan(12, 10, 0)),
                new TimeRange(new TimeSpan(12, 50, 0), new TimeSpan(13, 10, 0)),
                new TimeRange(new TimeSpan(13, 50, 0), new TimeSpan(14, 10, 0)),
                new TimeRange(new TimeSpan(14, 50, 0), new TimeSpan(15, 10, 0)),
                new TimeRange(new TimeSpan(15, 45, 0), new TimeSpan(16, 00, 0)),
                new TimeRange(new TimeSpan(18, 50, 0), new TimeSpan(19, 10, 0)),
                new TimeRange(new TimeSpan(19, 50, 0), new TimeSpan(20, 10, 0)),
                new TimeRange(new TimeSpan(20, 50, 0), new TimeSpan(21, 10, 0)),
                new TimeRange(new TimeSpan(21, 50, 0), new TimeSpan(22, 10, 0)),
                new TimeRange(new TimeSpan(22, 50, 0), new TimeSpan(23, 10, 0)),
                new TimeRange(new TimeSpan(23, 50, 0), new TimeSpan(0, 10, 0))
            };
        }
    }
}