using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Extensions
{
    /// <summary>
    /// Extension methods for Candle operations
    /// </summary>
    public static class CandleExtensions
    {
        /// <summary>
        /// Find the index and time of the highest price in a collection of candles
        /// </summary>
        public static (int index, DateTime time, double high) FindHighestPoint(this IEnumerable<Candle> candles)
        {
            if (candles == null || !candles.Any())
                return (-1, DateTime.MinValue, double.MinValue);

            var candlesList = candles.ToList();
            int highestIndex = -1;
            DateTime highestTime = DateTime.MinValue;
            double highestPrice = double.MinValue;
            
            for (int i = 0; i < candlesList.Count; i++)
            {
                var candle = candlesList[i];
                if (candle.High > highestPrice)
                {
                    highestPrice = candle.High;
                    highestIndex = candle.Index ?? -1;
                    highestTime = candle.Time;
                }
            }

            return (highestIndex, highestTime, highestPrice);
        }

        /// <summary>
        /// Find the index and time of the lowest price in a collection of candles
        /// </summary>
        public static (int index, DateTime time, double low) FindLowestPoint(this IEnumerable<Candle> candles)
        {
            if (candles == null || !candles.Any())
                return (-1, DateTime.MinValue, double.MaxValue);

            var candlesList = candles.ToList();
            int lowestIndex = -1;
            DateTime lowestTime = DateTime.MinValue;
            double lowestPrice = double.MaxValue;
            
            for (int i = 0; i < candlesList.Count; i++)
            {
                var candle = candlesList[i];
                if (candle.Low < lowestPrice)
                {
                    lowestPrice = candle.Low;
                    lowestIndex = candle.Index ?? -1;
                    lowestTime = candle.Time;
                }
            }

            return (lowestIndex, lowestTime, lowestPrice);
        }

        /// <summary>
        /// Check if a candle is bullish
        /// </summary>
        public static bool IsBullish(this Candle candle)
        {
            return candle?.Close > candle?.Open;
        }

        /// <summary>
        /// Check if a candle is bearish
        /// </summary>
        public static bool IsBearish(this Candle candle)
        {
            return candle?.Close < candle?.Open;
        }

        /// <summary>
        /// Get the body size of a candle
        /// </summary>
        public static double GetBodySize(this Candle candle)
        {
            if (candle == null) return 0;
            return Math.Abs(candle.Close - candle.Open);
        }

        /// <summary>
        /// Get the range of a candle (high - low)
        /// </summary>
        public static double GetRange(this Candle candle)
        {
            if (candle == null) return 0;
            return candle.High - candle.Low;
        }

        /// <summary>
        /// Gets the minimum and maximum candles from a collection of candles
        /// Returns the min and max candles with their indices and times
        /// </summary>
        public static (Candle minCandle, int minIndex, DateTime minTime, Candle maxCandle, int maxIndex, DateTime maxTime) GetMinMax(this IEnumerable<Candle> candles)
        {
            if (candles == null || !candles.Any())
                return (null, -1, DateTime.MinValue, null, -1, DateTime.MinValue);

            var candlesList = candles.ToList();
            
            Candle minCandle = null;
            Candle maxCandle = null;
            int minIndex = -1;
            int maxIndex = -1;
            DateTime minTime = DateTime.MinValue;
            DateTime maxTime = DateTime.MinValue;
            double minPrice = double.MaxValue;
            double maxPrice = double.MinValue;

            for (int i = 0; i < candlesList.Count; i++)
            {
                var candle = candlesList[i];
                
                if (candle.Low < minPrice)
                {
                    minPrice = candle.Low;
                    minCandle = candle;
                    minIndex = candle.Index ?? -1;
                    minTime = candle.Time;
                }
                
                if (candle.High > maxPrice)
                {
                    maxPrice = candle.High;
                    maxCandle = candle;
                    maxIndex = candle.Index ?? -1;
                    maxTime = candle.Time;
                }
            }

            return (minCandle, minIndex, minTime, maxCandle, maxIndex, maxTime);
        }
    }
}