using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Models;

namespace Pfuma.Visualization
{
    public class SMTVisualizer
    {
        private readonly Chart _chart;
        private readonly IEventAggregator _eventAggregator;
        private readonly Action<string> _logger;
        private readonly Dictionary<string, List<string>> _drawnObjects;
        private readonly bool _showSMT;

        public SMTVisualizer(
            Chart chart,
            IEventAggregator eventAggregator,
            bool showSMT,
            Action<string> logger = null)
        {
            _chart = chart;
            _eventAggregator = eventAggregator;
            _showSMT = showSMT;
            _logger = logger ?? (_ => { });
            _drawnObjects = new Dictionary<string, List<string>>();

            _eventAggregator.Subscribe<CycleSweptEvent>(OnCycleSwept);
            _eventAggregator.Subscribe<SMTLineRemovedEvent>(OnSMTLineRemoved);
        }

        private void OnCycleSwept(CycleSweptEvent evt)
        {
            if (!_showSMT || evt?.SweptCyclePoint == null || evt?.SweepingSwingPoint == null)
                return;

            var sweptPoint = evt.SweptCyclePoint;
            var sweepingPoint = evt.SweepingSwingPoint;

            if (!sweepingPoint.HasSMT)
                return;

            DrawSMTDivergenceLine(sweptPoint, sweepingPoint);
        }

        private void OnSMTLineRemoved(SMTLineRemovedEvent evt)
        {
            if (!_showSMT || evt?.SwingPoint == null)
                return;

            RemoveSMTDivergenceLine(evt.SwingPoint);
        }

        public void DrawSMTDivergenceLine(SwingPoint sweptPoint, SwingPoint sweepingPoint)
        {
            if (!_showSMT || sweptPoint == null || sweepingPoint == null)
                return;

            try
            {
                Color lineColor;
                if (sweepingPoint.Direction == Direction.Up)
                {
                    lineColor = Color.Green;
                }
                else
                {
                    lineColor = Color.Pink;
                }

                string lineId = $"smt-divergence-{sweptPoint.Index}-{sweepingPoint.Index}";

                _chart.DrawTrendLine(
                    lineId,
                    sweptPoint.Index,
                    sweptPoint.Price,
                    sweepingPoint.Index,
                    sweepingPoint.Price,
                    Color.FromArgb(150, lineColor),
                    2,
                    LineStyle.Dots
                );

                string groupId = $"smt-{sweepingPoint.Index}";
                if (!_drawnObjects.ContainsKey(groupId))
                    _drawnObjects[groupId] = new List<string>();
                _drawnObjects[groupId].Add(lineId);

                _logger($"SMT divergence line drawn: {sweepingPoint.Direction} from {sweptPoint.Price:F5} to {sweepingPoint.Price:F5}");
            }
            catch (Exception ex)
            {
                _logger($"Error drawing SMT divergence line: {ex.Message}");
            }
        }

        public void RemoveSMTDivergenceLine(SwingPoint swingPoint)
        {
            if (swingPoint == null)
                return;

            try
            {
                string groupId = $"smt-{swingPoint.Index}";
                if (_drawnObjects.ContainsKey(groupId))
                {
                    foreach (var objectId in _drawnObjects[groupId])
                    {
                        _chart.RemoveObject(objectId);
                    }
                    _drawnObjects[groupId].Clear();
                    _drawnObjects.Remove(groupId);

                    _logger($"SMT divergence line removed for swing point at index {swingPoint.Index}");
                }
            }
            catch (Exception ex)
            {
                _logger($"Error removing SMT divergence line: {ex.Message}");
            }
        }

        public void HandleSwingPointUpdate(SwingPoint oldSwingPoint, SwingPoint newSwingPoint)
        {
            if (!_showSMT)
                return;

            try
            {
                if (oldSwingPoint?.HasSMT == true)
                {
                    RemoveSMTDivergenceLine(oldSwingPoint);
                }

                if (newSwingPoint?.HasSMT == true && newSwingPoint.SweptCyclePoint != null)
                {
                    DrawSMTDivergenceLine(newSwingPoint.SweptCyclePoint, newSwingPoint);
                }
            }
            catch (Exception ex)
            {
                _logger($"Error handling swing point update in SMT visualizer: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            try
            {
                foreach (var objectGroup in _drawnObjects.Values)
                {
                    foreach (var objectId in objectGroup)
                    {
                        _chart.RemoveObject(objectId);
                    }
                }
                _drawnObjects.Clear();
            }
            catch (Exception ex)
            {
                _logger($"Error during SMT visualizer cleanup: {ex.Message}");
            }
        }
    }
}