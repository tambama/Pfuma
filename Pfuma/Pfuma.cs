using cAlgo.API;
using System.Collections.Generic;
using System;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class Pfuma : Indicator
    {
        [Parameter("Swing Points", DefaultValue = true)]
        public bool ShowSwingPoints { get; set; }

        [Output("Swing High", Color = Colors.White, PlotType = PlotType.Points, Thickness = 1)]
        public IndicatorDataSeries SwingHighs { get; set; }

        [Output("Swing Low", Color = Colors.White, PlotType = PlotType.Points, Thickness = 1)]
        public IndicatorDataSeries SwingLows { get; set; }

        private SwingPointDetector _swingDetector;
        private List<SwingPoint> _swingPoints;

        private Bar _previousBar;
        private int _previousBarIndex;

        protected override void Initialize()
        {
            // Delete all objects from Chart
            Chart.RemoveAllObjects();

            // Initialize the swing detector
            _swingPoints = new List<SwingPoint>();
            _swingDetector = new SwingPointDetector(SwingHighs, SwingLows);
        }

        public override void Calculate(int index)
        {
            // Need at least 2 bars to calculate
            if (index <= 1)
                return;

            _previousBar = Bars[index - 1];
            _previousBarIndex = index - 1;

            if (ShowSwingPoints)
            {
                try
                {
                    // Create a new candle object from the previous bar
                    var candle = new Candle(_previousBar, _previousBarIndex);

                    // Pass the current bar properties to the regular swing detector
                    _swingDetector.ProcessBar(_previousBarIndex, candle);

                    // Update the relationships between swing points
                    if (index == Bars.Count - 1) // Only on the last bar for efficiency
                    {
                        _swingDetector.UpdateSwingPointRelationships();
                        _swingPoints = _swingDetector.GetAllSwingPoints();
                    }
                }
                catch (Exception ex)
                {
                    Print("Error in swing point processing: " + ex.Message);
                }
            }
        }

        // Methods to expose swing points to other components
        public List<SwingPoint> GetAllSwingPoints()
        {
            return _swingPoints ?? new List<SwingPoint>();
        }

        public SwingPoint GetLastSwingHigh()
        {
            return _swingDetector?.GetLastSwingHigh();
        }

        public SwingPoint GetLastSwingLow()
        {
            return _swingDetector?.GetLastSwingLow();
        }
    }
}