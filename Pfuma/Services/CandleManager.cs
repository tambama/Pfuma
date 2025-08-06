using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Manages the collection of custom Candle objects for the indicator
    /// </summary>
    public class CandleManager
    {
        private readonly List<Candle> _candles;
        private readonly Bars _bars;
        private readonly TimeFrame _timeFrame;
        private int _lastProcessedIndex = -1;

        public CandleManager(Bars bars, TimeFrame timeFrame)
        {
            _bars = bars;
            _timeFrame = timeFrame;
            _candles = new List<Candle>();
        }

        /// <summary>
        /// Process a new bar and add it to the candle collection
        /// </summary>
        public Candle ProcessBar(int index)
        {
            // Check if we already processed this index
            if (index <= _lastProcessedIndex && index < _candles.Count)
            {
                // Update the existing candle if the bar has changed
                var existingCandle = _candles[index];
                var bar = _bars[index];
                
                existingCandle.Open = bar.Open;
                existingCandle.High = bar.High;
                existingCandle.Low = bar.Low;
                existingCandle.Close = bar.Close;
                
                return existingCandle;
            }

            // Create a new candle
            var newBar = _bars[index];
            var candle = new Candle(newBar, index, _timeFrame);

            // Add or update the candle in the collection
            if (index < _candles.Count)
            {
                _candles[index] = candle;
            }
            else
            {
                // Fill any gaps
                while (_candles.Count < index)
                {
                    var gapBar = _bars[_candles.Count];
                    _candles.Add(new Candle(gapBar, _candles.Count, _timeFrame));
                }
                _candles.Add(candle);
            }

            _lastProcessedIndex = Math.Max(_lastProcessedIndex, index);
            return candle;
        }

        /// <summary>
        /// Get a candle at a specific index
        /// </summary>
        public Candle GetCandle(int index)
        {
            if (index < 0 || index >= _candles.Count)
                return null;
            
            return _candles[index];
        }

        /// <summary>
        /// Get the last candle
        /// </summary>
        public Candle GetLastCandle()
        {
            if (_candles.Count == 0)
                return null;
                
            return _candles[_candles.Count - 1];
        }

        /// <summary>
        /// Get a range of candles
        /// </summary>
        public List<Candle> GetCandles(int startIndex, int count)
        {
            if (startIndex < 0 || startIndex >= _candles.Count)
                return new List<Candle>();

            var endIndex = Math.Min(startIndex + count, _candles.Count);
            return _candles.GetRange(startIndex, endIndex - startIndex);
        }

        /// <summary>
        /// Get all candles
        /// </summary>
        public List<Candle> GetAllCandles()
        {
            return new List<Candle>(_candles);
        }

        /// <summary>
        /// Get candles between two times
        /// </summary>
        public List<Candle> GetCandlesBetween(DateTime startTime, DateTime endTime)
        {
            return _candles.Where(c => c.Time >= startTime && c.Time <= endTime).ToList();
        }

        /// <summary>
        /// Find the highest candle in a range
        /// </summary>
        public (Candle candle, int index, double high) FindHighest(int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex >= _candles.Count || startIndex > endIndex)
                return (null, -1, double.MinValue);

            Candle highestCandle = null;
            int highestIndex = -1;
            double highestPrice = double.MinValue;

            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = _candles[i];
                if (candle.High > highestPrice)
                {
                    highestPrice = candle.High;
                    highestCandle = candle;
                    highestIndex = i;
                }
            }

            return (highestCandle, highestIndex, highestPrice);
        }

        /// <summary>
        /// Find the lowest candle in a range
        /// </summary>
        public (Candle candle, int index, double low) FindLowest(int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex >= _candles.Count || startIndex > endIndex)
                return (null, -1, double.MaxValue);

            Candle lowestCandle = null;
            int lowestIndex = -1;
            double lowestPrice = double.MaxValue;

            for (int i = startIndex; i <= endIndex; i++)
            {
                var candle = _candles[i];
                if (candle.Low < lowestPrice)
                {
                    lowestPrice = candle.Low;
                    lowestCandle = candle;
                    lowestIndex = i;
                }
            }

            return (lowestCandle, lowestIndex, lowestPrice);
        }

        /// <summary>
        /// Get the count of candles
        /// </summary>
        public int Count => _candles.Count;

        /// <summary>
        /// Get the current timeframe
        /// </summary>
        public TimeFrame TimeFrame => _timeFrame;
    }
}