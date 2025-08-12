using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;
using static Pfuma.Core.Configuration.Constants;

namespace Pfuma.Visualization
{
    /// <summary>
    /// Visualizes Higher Timeframe Fair Value Gaps with distinct styling
    /// </summary>
    public class HtfFvgVisualizer : BaseVisualizer<Level>
    {
        private readonly IndicatorSettings _indicatorSettings;
        
        public HtfFvgVisualizer(Chart chart, IndicatorSettings settings) 
            : base(chart, settings.Visualization, null)
        {
            _indicatorSettings = settings;
        }
        
        protected override string GetPatternId(Level htfFvg)
        {
            var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
            return $"HTF_FVG_{tfLabel}_{htfFvg.Direction}_{htfFvg.Index}_{htfFvg.LowTime:yyyyMMddHHmmss}";
        }
        
        protected override void PerformDraw(Level htfFvg, string patternId, List<string> objectIds)
        {
            if (htfFvg == null || htfFvg.LevelType != LevelType.FairValueGap)
                return;
            
            // Only draw if ShowHtfFvg is enabled
            if (!_indicatorSettings.Patterns.ShowHtfFvg)
                return;
            
            var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
            
            // Get directional color - green for bullish, red for bearish
            Color baseColor = GetDirectionalColor(htfFvg.Direction);
            
            // Draw main rectangle using same approach as regular FVGs
            DrawHtfFvgRectangle(htfFvg, patternId, objectIds, baseColor);
            
            // Draw label with timeframe identifier
            var labelName = $"{patternId}_Label";
            objectIds.Add(labelName);
            
            var labelText = $"{tfLabel} FVG";
            var midPoint = (htfFvg.High + htfFvg.Low) / 2;
            
            Chart.DrawText(
                labelName,
                labelText,
                htfFvg.MidTime,
                midPoint,
                baseColor
            );
            
            // Draw quadrants if enabled
            if (_indicatorSettings.Patterns.ShowQuadrants && htfFvg.Quadrants != null && htfFvg.Quadrants.Count > 0)
            {
                // Use the same end time as the rectangle (HighTime)
                DrawQuadrants(htfFvg, patternId, objectIds, htfFvg.HighTime);
            }
        }
        
        private void DrawHtfFvgRectangle(Level htfFvg, string patternId, List<string> objectIds, Color baseColor)
        {
            string rectangleId = patternId;
            
            // For bullish HTF FVGs: draw from index low (candle 1) to index high (candle 3)
            // For bearish HTF FVGs: draw from index low (candle 3) to index high (candle 1)  
            DateTime startTime = htfFvg.LowTime;   // Time of the start candle
            DateTime endTime = htfFvg.HighTime;    // Time of the end candle
            
            var rectangle = Chart.DrawRectangle(
                rectangleId,
                startTime,
                htfFvg.Low,
                endTime,
                htfFvg.High,
                baseColor);
            
            rectangle.IsFilled = false;
            rectangle.Color = ApplyOpacity(baseColor, Settings.Opacity.FVG);
            
            objectIds.Add(rectangleId);
        }
        
        private void DrawQuadrants(Level htfFvg, string baseObjectName, List<string> objectIds, DateTime extendedEndTime)
        {
            var tfLabel = htfFvg.TimeFrame?.GetShortName() ?? "HTF";
            
            // Use subtle colors for quadrant lines
            var quadrantColor = htfFvg.Direction == Direction.Up
                ? Color.FromArgb(50, 0, 255, 0)
                : Color.FromArgb(50, 255, 0, 0);
            
            // Draw 25% line
            var q25 = htfFvg.Quadrants.FirstOrDefault(q => q.Percent == 25);
            if (q25 != null)
            {
                var lineName = $"{baseObjectName}_Q25";
                objectIds.Add(lineName);
                Chart.DrawTrendLine(
                    lineName,
                    htfFvg.LowTime,
                    q25.Price,
                    extendedEndTime,
                    q25.Price,
                    quadrantColor,
                    1,
                    LineStyle.DotsRare
                );
            }
            
            // Draw 50% line (CE - Consequent Encroachment)
            var q50 = htfFvg.Quadrants.FirstOrDefault(q => q.Percent == 50);
            if (q50 != null)
            {
                var lineName = $"{baseObjectName}_Q50";
                objectIds.Add(lineName);
                Chart.DrawTrendLine(
                    lineName,
                    htfFvg.LowTime,
                    q50.Price,
                    extendedEndTime,
                    q50.Price,
                    quadrantColor,
                    2, // Thicker for 50% line
                    LineStyle.Dots
                );
                
                // Add CE label
                var ceLabelName = $"{baseObjectName}_CE_Label";
                objectIds.Add(ceLabelName);
                Chart.DrawText(
                    ceLabelName,
                    $"{tfLabel} CE",
                    htfFvg.MidTime,
                    q50.Price,
                    quadrantColor
                );
            }
            
            // Draw 75% line
            var q75 = htfFvg.Quadrants.FirstOrDefault(q => q.Percent == 75);
            if (q75 != null)
            {
                var lineName = $"{baseObjectName}_Q75";
                objectIds.Add(lineName);
                Chart.DrawTrendLine(
                    lineName,
                    htfFvg.LowTime,
                    q75.Price,
                    extendedEndTime,
                    q75.Price,
                    quadrantColor,
                    1,
                    LineStyle.DotsRare
                );
            }
        }
    }
}