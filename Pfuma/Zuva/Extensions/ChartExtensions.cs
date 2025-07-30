using cAlgo.API;
using Zuva.Models;

namespace Zuva.Extensions
{
    public static class ChartExtensions
    {
        public static void DrawStraightLine(this Chart chart, string id, DateTime startTime, double startPrice, DateTime endTime, double endPrice, string label = null, LineStyle lineStyle = LineStyle.Solid, Color color = null, bool hasLabel = false, bool removeExisting = false, bool extended = false, bool editable = false, bool labelOnRight = false)
        {
            color ??= Color.Wheat;

            if (removeExisting)
            {
                chart.RemoveObject(id);
            }
        
            var line = chart.DrawTrendLine(id, startTime, startPrice, endTime, endPrice, color, 1, lineStyle);
        
            if (extended)
            {
                line.ExtendToInfinity = true;
            }

            if (editable)
            {
                line.IsInteractive = true;
            }

            if (!hasLabel) return;
        
            chart.RemoveObject($"{id}-label");
    
            // Position the text either at the start or end of the line based on labelOnRight parameter
            DateTime textTime = labelOnRight ? endTime : startTime;
            double textPrice = labelOnRight ? endPrice : startPrice;
    
            var text = chart.DrawText($"{id}-label", label, textTime, textPrice, color);
    
            // Set horizontal alignment based on position
            text.HorizontalAlignment = labelOnRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }
    
        public static void DrawCycle(this Chart chart, List<TimeRange> cycles, DateTime time)
        {
            var cycle = cycles.FirstOrDefault(c => c.StartTime == time.TimeOfDay);
            if (cycle != null)
            {
                chart.DrawVerticalLine("cycle-" + time, time, Color.Gray, 1, LineStyle.DotsRare);
            }
        }
    
        public static void DrawTrendLine(this Chart chart, string id, SwingPoint startPoint, SwingPoint endPoint, LineType lineType)
        {
            var lineStyle = lineType.GetLineStyle();
            
            chart.DrawTrendLine($"{id}-{lineStyle}-{startPoint.Time}", startPoint.Time, startPoint.Price, endPoint.Time, startPoint.Price, lineStyle.color, 1, lineStyle.style);
        }
    
        public static void DrawTrendLine(this Chart chart, string id, Level level, LineType lineType)
        {
            var lineStyle = lineType.GetLineStyle();
        
            var startTime = level.Direction == Direction.Up ? level.LowTime : level.HighTime;
            var startPrice = level.Entry ?? (level.Direction == Direction.Up ? level.Low : level.High);
            var endTime = level.StretchTo ?? (level.Direction == Direction.Up ? level.HighTime : level.LowTime);
            var endPrice = level.Direction == Direction.Up ? level.Low : level.High;
            
            chart.DrawTrendLine($"{id}-{lineStyle}-{startTime}", startTime, startPrice, endTime, startPrice, lineStyle.color, 1, lineStyle.style);
        }
    
        public static void DrawRectangle(this Chart chart, SwingPoint startPoint, SwingPoint endPoint, int opacity = 10)
        {
            var rectangle = chart.DrawRectangle($"rect-{startPoint.Time}", startPoint.Time, startPoint.Bar.Low, endPoint.Time, startPoint.Bar.High, Color.Pink);
            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(opacity, rectangle.Color);
        }

        public static void DrawRectangle(this Chart chart, Level level, string id, bool drawMidPoint = false, int opacity = 10, int length = 5)
        {
            var startTime = level.LowTime < level.HighTime ? level.LowTime : level.HighTime;
            var endTime = level.StretchTo ?? (length > 0 ? startTime.AddMinutes(length) : level.LowTime.AddMinutes(5));
            var startPrice = level.LowTime < level.HighTime ? level.Low : level.High;
            var endPrice = level.LowTime < level.HighTime ? level.High : level.Low;
        
            var color = level.Direction == Direction.Up ? Color.Green : Color.Pink;
        
            var rectangle = chart.DrawRectangle($"rect-{id}-{startTime}", startTime, startPrice, endTime, endPrice, color);
            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(opacity, rectangle.Color);

            if (drawMidPoint)
            {
                var line = chart.DrawTrendLine($"rect-{id}-ce-{startTime}", level.LowTime, level.Mid, endTime, level.Mid, Color.Wheat, 1, LineStyle.Dots);
            }
        }
        
        /// <summary>
        /// Draw an order flow rectangle on the chart with appropriate styling
        /// </summary>
        public static void DrawOrderFlowRectangle(this Chart chart, Level level, string id, int opacity = 8)
        {
            // For order flow, we want to cover the entire range from low to high
            DateTime startTime = level.Direction == Direction.Up ? level.LowTime : level.HighTime;
            DateTime endTime = level.Direction == Direction.Up ? level.HighTime : level.LowTime;
            double lowPrice = level.Low;
            double highPrice = level.High;
            
            // Set color based on direction (green for bullish, pink for bearish)
            Color color = level.Direction == Direction.Up ? Color.Green : Color.Pink;
            
            // Remove existing rectangle if present
            chart.RemoveObject(id);
            
            // Draw the rectangle with semi-transparency
            var rectangle = chart.DrawRectangle(id, startTime, lowPrice, endTime, highPrice, color);
            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(opacity, rectangle.Color);
            
            // Draw a dotted line for the midpoint
            string midLineId = $"{id}-midline";
            chart.RemoveObject(midLineId);
            chart.DrawTrendLine(midLineId, startTime, level.Mid, endTime, level.Mid, Color.Wheat, 1, LineStyle.Dots);
        }
    
        public static void DrawLevel(this Chart chart, Level level, bool drawMidPoint = false)
        {
            var lowEndTime = level.StretchTo ?? level.LowTime.AddMinutes(1);
            var highEndTime = level.StretchTo ?? level.HighTime.AddMinutes(1);
            var midEndTime = level.StretchTo ?? level.MidTime.AddMinutes(1);
            var midTime = level.LowTime > level.HighTime ? level.HighTime.AddMinutes(1) : level.LowTime.AddMinutes(1);
        
            var highLine = chart.DrawTrendLine($"hi-{level.HighTime}", level.HighTime, level.High, highEndTime, level.High, Color.Green, 1, LineStyle.Solid);

            var lowLine = chart.DrawTrendLine($"lo-{level.LowTime}", level.LowTime, level.Low, lowEndTime, level.Low, Color.Green, 1, LineStyle.Solid);

            if (drawMidPoint)
            {
                var midLine = chart.DrawTrendLine($"ce-{midTime}", midTime, level.Mid, midEndTime, level.Mid, Color.Green, 1, LineStyle.Dots);
            }
        }

        public static void DrawStandardDeviation(this Chart chart, StandardDeviation standardDeviation)
        {
            var time = standardDeviation.OneTime;
            var timePlusOne = time.AddMinutes(5);

            // Draw MinusTwo if it's not already swept (check flag, not value)
            if (!standardDeviation.IsMinusTwoSwept)
            {
                string twoId = $"{standardDeviation.OneTime.Ticks}-two";
                chart.DrawTrendLine(twoId, time, standardDeviation.MinusTwo, timePlusOne, standardDeviation.MinusTwo, Color.Green);
            }
            // Otherwise, ensure extended line is still drawn if it exists
            else if (!string.IsNullOrEmpty(standardDeviation.ExtendedTwoLineId))
            {
                // Don't remove extended lines
            }

            // Draw MinusFour if it's not already swept (check flag, not value)
            if (!standardDeviation.IsMinusFourSwept)
            {
                string fourId = $"{standardDeviation.OneTime.Ticks}-four";
                chart.DrawTrendLine(fourId, time, standardDeviation.MinusFour, timePlusOne, standardDeviation.MinusFour, Color.Red);
            }
            // Otherwise, ensure extended line is still drawn if it exists
            else if (!string.IsNullOrEmpty(standardDeviation.ExtendedFourLineId))
            {
                // Don't remove extended lines
            }
        }
        
        /// <summary>
        /// Draws a rectangle showing activation of a level with a midpoint line
        /// </summary>
        public static void DrawActivationRectangle(this Chart chart, Level level, SwingPoint activatingPoint, string prefix, int opacity = 15)
        {
            // Create a unique ID for this activation
            string id = $"{prefix}-{level.LowTime.Ticks}-{activatingPoint.Time.Ticks}";
    
            // Define the start and end times correctly
            DateTime startTime = level.LowTime < level.HighTime ? level.LowTime : level.HighTime;
            DateTime endTime = activatingPoint.Time;
    
            // Set color based on direction
            Color color = level.Direction == Direction.Up ? Color.Green : Color.Pink;
    
            // Draw the activation rectangle
            var rectangle = chart.DrawRectangle(
                $"{id}-rect", 
                startTime, 
                level.Low, 
                endTime, 
                level.High, 
                color);
        
            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(opacity, rectangle.Color); // Semi-transparent
    
            // Draw a dotted line for the midpoint
            chart.DrawTrendLine(
                $"{id}-midline", 
                startTime, 
                level.Mid, 
                endTime, 
                level.Mid, 
                Color.Wheat, 
                1, 
                LineStyle.Dots);
        
            return;
        
            // Add a label to identify what kind of activation this is
            var levelType = level.LevelType.ToString();
            chart.DrawText(
                $"{id}-label",
                $"{levelType} Activation", 
                startTime, 
                level.Mid, 
                color);
        }
    
        /// <summary>
        /// Updates the market bias display on the chart
        /// </summary>
        public static void UpdateBias(this Chart chart, Direction bias)
        {
            chart.RemoveObject("bias");
            var text = bias == Direction.Down ? "Bearish" : "Bullish";
            chart.DrawStaticText("bias", text, VerticalAlignment.Bottom, HorizontalAlignment.Left, Color.Wheat);
        }
        
        /// <summary>
        /// Draws a Fair Value Gap (FVG) on the chart with appropriate styling
        /// </summary>
        public static void DrawFairValueGap(this Chart chart, Level fvg, string id, int opacity = 8)
        {
            // For FVGs, we want to cover the area between low and high
            double lowPrice = fvg.Low;
            double highPrice = fvg.High;
            double midPrice = fvg.Mid;
            
            // Start time is the middle point (where the gap occurred)
            DateTime startTime = fvg.MidTime;
            
            // FVGs typically extend until they are filled, so we extend to the right
            DateTime endTime = startTime.AddMinutes(5); // Extend 5 minutes initially
            
            // Set color based on direction (green for bullish, red for bearish)
            Color color = fvg.Direction == Direction.Up ? Color.Green : Color.Red;
            
            // Remove existing FVG if present
            chart.RemoveObject(id);
            
            // Draw a semi-transparent rectangle for the FVG
            var rectangle = chart.DrawRectangle(id, startTime, lowPrice, endTime, highPrice, color);
            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(opacity, rectangle.Color);
            
            // Draw lines for the low, mid, and high points
            string lowLineId = $"{id}-low";
            string midLineId = $"{id}-mid";
            string highLineId = $"{id}-high";
            
            chart.RemoveObject(lowLineId);
            chart.RemoveObject(midLineId);
            chart.RemoveObject(highLineId);
            
            // Draw 1-minute long lines
            chart.DrawTrendLine(lowLineId, startTime, lowPrice, startTime.AddMinutes(1), lowPrice, color, 1, LineStyle.Solid);
            chart.DrawTrendLine(midLineId, startTime, midPrice, startTime.AddMinutes(1), midPrice, color, 1, LineStyle.Dots);
            chart.DrawTrendLine(highLineId, startTime, highPrice, startTime.AddMinutes(1), highPrice, color, 1, LineStyle.Solid);
        }
        
        /// <summary>
        /// Draws a Fibonacci retracement/extension on the chart
        /// </summary>
        public static void DrawFibonacci(this Chart chart, string id, DateTime startTime, double startPrice, 
            DateTime endTime, double endPrice, Color color, int opacity = 5)
        {
            // First, remove any existing Fibonacci with the same ID
            chart.RemoveObject(id);
            
            // Draw the main trend line from start to end
            var mainLine = chart.DrawTrendLine($"{id}-main", startTime, startPrice, endTime, endPrice, color);
            
            // Calculate distances
            double range = Math.Abs(endPrice - startPrice);
            bool isUp = endPrice > startPrice;
            
            // Draw level lines for common Fibonacci ratios
            double[] levels = { -2.0, -1.5, -1.0, -0.5, -0.25, 0.0, 0.114, 0.236, 0.382, 0.5, 0.618, 0.786, 0.886, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 };
            
            foreach (double level in levels)
            {
                // Calculate price at this ratio
                double levelPrice;
                if (isUp)
                {
                    levelPrice = startPrice + (range * level);
                }
                else
                {
                    levelPrice = startPrice - (range * level);
                }
                
                // Draw the line
                var line = chart.DrawTrendLine(
                    $"{id}-level-{level}",
                    startTime,
                    levelPrice,
                    endTime.AddHours(8), // Extend to the right
                    levelPrice,
                    color
                );
                
                // Make it semi-transparent
                line.Color = Color.FromArgb(opacity, line.Color);
                
                // Make all lines editable
                line.IsInteractive = true;
                
                // Add level labels
                chart.DrawText(
                    $"{id}-label-{level}", 
                    $"{level:0.###}", 
                    startTime, 
                    levelPrice, 
                    color
                );
            }
        }
        
        public static void DrawLineFromLevelToPoint(this Chart chart, Level pdArray, SwingPoint swingPoint)
        {
            if (chart == null || pdArray == null || swingPoint == null)
                return;

            // Create a consistent ID using the level and swing point properties
            string id = $"keyLevel-rect-{pdArray.Direction}-{pdArray.Index}";

            // Remove any existing rectangle with this ID before drawing a new one
            chart.RemoveObject(id);

            // Determine start point (from PD array)
            DateTime startTime;
            double lowPrice, highPrice;
            Color color;

            // For bearish PD arrays with bullish swing points
            if (pdArray.Direction == Direction.Down && swingPoint.Direction == Direction.Up)
            {
                startTime = pdArray.HighTime;
                // Use the low and high prices of the PD array
                lowPrice = pdArray.Low;
                highPrice = pdArray.High;
                color = Color.Pink;
            }
            // For bullish PD arrays with bearish swing points
            else
            {
                startTime = pdArray.LowTime;
                // Use the low and high prices of the PD array
                lowPrice = pdArray.Low;
                highPrice = pdArray.High;
                color = Color.Green;
            }

            // Draw a rectangle from the PD array to the swing point
            var rectangle = chart.DrawRectangle(
                id,
                startTime,
                lowPrice,
                swingPoint.Time,
                highPrice,
                color // Use yellow color to match liquidity sweep style
            );

            // Make it semi-transparent
            rectangle.IsFilled = true;
            rectangle.Color = Color.FromArgb(5, rectangle.Color);  // Low opacity (10 out of 255)
        }
    }
}