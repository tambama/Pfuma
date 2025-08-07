using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Extensions;
using Pfuma.Models;
using Pfuma.Visualization.Base;

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
            var rectangleName = patternId;
            var labelName = $"{patternId}_Label";
            
            // Add object IDs to the list for tracking
            objectIds.Add(rectangleName);
            objectIds.Add(labelName);
            
            // Use different colors for HTF FVGs (more transparent/subtle)
            Color rectangleColor;
            Color textColor;
            
            if (htfFvg.Direction == Direction.Up)
            {
                // Bullish HTF FVG - Lighter green with more transparency
                rectangleColor = Color.FromArgb(30, 0, 255, 0); // Very transparent green
                textColor = Color.FromArgb(180, 0, 200, 0); // Darker green for text
            }
            else
            {
                // Bearish HTF FVG - Lighter red with more transparency
                rectangleColor = Color.FromArgb(30, 255, 0, 0); // Very transparent red
                textColor = Color.FromArgb(180, 200, 0, 0); // Darker red for text
            }
            
            // Calculate extended end time for better visibility
            // Extend the rectangle forward by the HTF period duration
            var htfPeriod = htfFvg.HighTime - htfFvg.LowTime;
            var extendedEndTime = htfFvg.HighTime.Add(htfPeriod.Add(htfPeriod)); // Extend by 2x HTF period
            
            // Draw rectangle with thicker border for HTF
            Chart.DrawRectangle(
                rectangleName,
                htfFvg.LowTime,
                htfFvg.Low,
                extendedEndTime,
                htfFvg.High,
                rectangleColor
            );
            
            // Add a border to make HTF FVGs more visible
            var borderName = $"{patternId}_Border";
            objectIds.Add(borderName + "_top");
            objectIds.Add(borderName + "_bottom");
            
            Chart.DrawTrendLine(
                borderName + "_top",
                htfFvg.LowTime,
                htfFvg.High,
                extendedEndTime,
                htfFvg.High,
                textColor,
                2, // Thicker line
                LineStyle.Dots
            );
            
            Chart.DrawTrendLine(
                borderName + "_bottom",
                htfFvg.LowTime,
                htfFvg.Low,
                extendedEndTime,
                htfFvg.Low,
                textColor,
                2, // Thicker line
                LineStyle.Dots
            );
            
            // Draw middle line (50% level)
            var midPoint = (htfFvg.High + htfFvg.Low) / 2;
            var middleLineName = $"{patternId}_Middle";
            objectIds.Add(middleLineName);
            
            Chart.DrawTrendLine(
                middleLineName,
                htfFvg.LowTime,
                midPoint,
                extendedEndTime,
                midPoint,
                textColor,
                1, // Standard thickness
                LineStyle.Solid
            );
            
            // Draw label with timeframe identifier
            var labelText = $"{tfLabel} FVG";
            
            Chart.DrawText(
                labelName,
                labelText,
                htfFvg.MidTime,
                midPoint,
                textColor
            );
            
            // Draw quadrants if enabled
            if (_indicatorSettings.Patterns.ShowQuadrants && htfFvg.Quadrants != null && htfFvg.Quadrants.Count > 0)
            {
                DrawQuadrants(htfFvg, rectangleName, objectIds, extendedEndTime);
            }
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