using System;
using cAlgo.API;
using Pfuma.Services;

namespace Pfuma.Visualization
{
    public class StatsVisualizer
    {
        private readonly Chart _chart;
        private readonly SignalManager _signalManager;
        private string _statsTextObjectName = "SignalStats";

        public StatsVisualizer(Chart chart, SignalManager signalManager)
        {
            _chart = chart;
            _signalManager = signalManager;
        }

        public void UpdateStats()
        {
            if (_signalManager == null || _chart == null)
                return;

            var stats = _signalManager.GetStats();

            // Create stats text
            var statsText = $"📊 Win Rate: {stats.WinRate:F1}%\n" +
                           $"Trades: {stats.TotalTrades}\n" +
                           $"Wins: {stats.Wins}\n" +
                           $"Losses: {stats.Losses}\n" +
                           $"Days: {stats.TotalDays}\n" +
                           $"Win Rate: {stats.WinRate:F1}%";

            // Remove existing stats text if it exists
            _chart.RemoveObject(_statsTextObjectName);

            // Draw new stats text in bottom left corner
            _chart.DrawStaticText(_statsTextObjectName, statsText,
                VerticalAlignment.Bottom,
                HorizontalAlignment.Left,
                Color.White)
                .FontSize = 10;
        }

        public void RemoveStats()
        {
            _chart.RemoveObject(_statsTextObjectName);
        }
    }
}