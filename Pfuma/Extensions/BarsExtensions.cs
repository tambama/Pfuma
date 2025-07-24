using System;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Extensions
{
    public static class BarsExtension
    {
        public static Direction GetCandleDirection(this Bar candle)
        {
            return candle.Close > candle.Open ? Direction.Up : Direction.Down;
        }

        public static Direction GetCandleDirection(this Candle candle)
        {
            return candle.Close > candle.Open ? Direction.Up : Direction.Down;
        }

        /// <summary>
        /// Returns the minimum/maximum prices levels during an specific time period
        /// </summary>
        /// <param name="Bars">Bars to compare</param>
        /// <param name="startTime">Start Time (Inclusive)</param>
        /// <param name="endTime">End Time (Inclusive)</param>
        /// <returns>Tuple<double, double> (Item1 will be minimum price and Item2 will be maximum price)</returns>
        public static (DateTime minTime, int minIndex, double min, DateTime maxTime, int maxIndex, double max) GetMinMax(this Bars Bars, DateTime startTime, DateTime endTime)
        {
            var min = double.MaxValue;
            var minIndex = 0;
            var max = double.MinValue;
            var maxIndex = 0;

            for (var barIndex = 0; barIndex < Bars.Count; barIndex++)
            {
                var bar = Bars[barIndex];

                if (bar.OpenTime < startTime || bar.OpenTime > endTime)
                {
                    if (bar.OpenTime > endTime) break;

                    continue;
                }

                var newMin = Math.Min(min, bar.Low);
                var newMax = Math.Max(max, bar.High);

                if (newMin != min)
                {
                    min = newMin;
                    minIndex = barIndex;
                }

                if (newMax == max) continue;
                max = newMax;
                maxIndex = barIndex;
            }
            
            var minTime = Bars[minIndex].OpenTime;
            var maxTime = Bars[maxIndex].OpenTime;

            return (minTime, minIndex, min, maxTime, maxIndex, max);
        }
        
        /// <summary>
        /// Gets the indices for the previous higher timeframe bar relative to the current bar
        /// </summary>
        /// <param name="bars">The collection of bars</param>
        /// <param name="currentBarIndex">The index of the current bar</param>
        /// <param name="higherTimeframe">The higher timeframe to compare against</param>
        /// <returns>A tuple containing the start and end indices for the previous higher timeframe bar</returns>
        public static (int startIndex, int endIndex) GetPreviousHigherTimeframeBarRange(this Bars bars, int currentBarIndex, TimeFrame higherTimeframe)
        {
            // Ensure the bar index is valid
            if (currentBarIndex < 0 || currentBarIndex >= bars.Count)
                return (-1, -1);

            // First, find the start of the current higher timeframe bar
            var currentHigherTfStartIndex = -1;
            
            // Look backward from the current bar to find where the current higher timeframe bar starts
            for (var i = currentBarIndex; i >= 0; i--)
            {
                if (i != 0 && !bars[i].OpenTime.IsStartOfHigherTimeframeBar(higherTimeframe)) continue;
                
                currentHigherTfStartIndex = i;
                break;
            }
            
            if (currentHigherTfStartIndex <= 0)
                return (-1, -1); // Can't find previous higher timeframe bar
                
            // Find the start of the previous higher timeframe bar
            var previousHigherTfStartIndex = -1;
            
            // Continue looking backward to find the start of the previous higher timeframe bar
            for (var i = currentHigherTfStartIndex - 1; i >= 0; i--)
            {
                if (i != 0 && !bars[i].OpenTime.IsStartOfHigherTimeframeBar(higherTimeframe)) continue;
                previousHigherTfStartIndex = i;
                break;
            }
            
            if (previousHigherTfStartIndex < 0)
                return (-1, -1); // Can't find start of previous higher timeframe bar
            
            // Get the minutes of the higher timeframe
            var higherTimeframeMinutes = higherTimeframe.TimeFrameToMinutes();
            
            // Calculate the end index by adding (higher timeframe minutes - 1) to the start index
            // But make sure we don't exceed the start of the current higher timeframe
            var maxEndIndex = currentHigherTfStartIndex - 1;
            var calculatedEndIndex = previousHigherTfStartIndex + (higherTimeframeMinutes - 1);
            var previousHigherTfEndIndex = Math.Min(calculatedEndIndex, maxEndIndex);
            
            // Make sure we don't go beyond the available bars
            previousHigherTfEndIndex = Math.Min(previousHigherTfEndIndex, bars.Count - 1);
            
            return (previousHigherTfStartIndex, previousHigherTfEndIndex);
        }
        
        /// <summary>
        /// Creates a higher timeframe Candle object from a range of bars
        /// </summary>
        /// <param name="bars">The collection of bars</param>
        /// <param name="startIndex">The starting index (inclusive)</param>
        /// <param name="endIndex">The ending index (inclusive)</param>
        /// <returns>A Candle object representing the higher timeframe candle</returns>
        public static Candle GetHigherTimeframeCandle(this Bars bars, int startIndex, int endIndex)
        {
            // Validate indices
            if (startIndex < 0 || endIndex < 0 || startIndex >= bars.Count || endIndex >= bars.Count || startIndex > endIndex)
                return null;
    
            // Use the first bar's open and time
            double open = bars[startIndex].Open;
            DateTime time = bars[startIndex].OpenTime;
    
            // Initialize high and low values
            double high = double.MinValue;
            double low = double.MaxValue;
            int indexOfHigh = -1;
            int indexOfLow = -1;
    
            // Find the highest high and lowest low in the range
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (bars[i].High > high)
                {
                    high = bars[i].High;
                    indexOfHigh = i;
                }
        
                if (bars[i].Low < low)
                {
                    low = bars[i].Low;
                    indexOfLow = i;
                }
            }
    
            // Use the last bar's close
            double close = bars[endIndex].Close;
    
            // Create and return the HTF candle
            var candle = new Candle(bars[startIndex], startIndex)
            {
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Time = time,
                // Mark this as a high timeframe candle
                IsHighTimeframe = true,
                // Store indices of price extremes for reference
                IndexOfHigh = indexOfHigh,
                IndexOfLow = indexOfLow,
                TimeOfLow = bars[indexOfLow].OpenTime,
                TimeOfHigh = bars[indexOfHigh].OpenTime
            };
    
            return candle;
        }
    }
}