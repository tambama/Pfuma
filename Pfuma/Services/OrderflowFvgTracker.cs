using System.Collections.Generic;
using System.Linq;
using Pfuma.Models;

namespace Pfuma.Services
{
    /// <summary>
    /// Tracks FVGs for association with orderflows.
    /// Maintains separate collections for bullish and bearish FVGs.
    /// When an orderflow is detected, the corresponding FVGs are consumed and the tracker is cleared.
    /// </summary>
    public class OrderflowFvgTracker
    {
        private readonly List<Level> _bullishFvgs = new List<Level>();
        private readonly List<Level> _bearishFvgs = new List<Level>();

        /// <summary>
        /// Adds an FVG to the appropriate tracker based on its direction
        /// </summary>
        public void AddFvg(Level fvg)
        {
            if (fvg == null || fvg.LevelType != LevelType.FairValueGap)
                return;

            if (fvg.Direction == Direction.Up)
                _bullishFvgs.Add(fvg);
            else
                _bearishFvgs.Add(fvg);
        }

        /// <summary>
        /// Gets and clears all FVGs of the specified direction.
        /// Called when an orderflow of that direction is detected.
        /// </summary>
        public List<Level> ConsumeFvgs(Direction direction)
        {
            if (direction == Direction.Up)
            {
                var fvgs = _bullishFvgs.ToList();
                _bullishFvgs.Clear();
                return fvgs;
            }
            else
            {
                var fvgs = _bearishFvgs.ToList();
                _bearishFvgs.Clear();
                return fvgs;
            }
        }

        /// <summary>
        /// Gets the count of FVGs for the specified direction without consuming them
        /// </summary>
        public int GetCount(Direction direction)
        {
            return direction == Direction.Up ? _bullishFvgs.Count : _bearishFvgs.Count;
        }

        /// <summary>
        /// Clears all tracked FVGs
        /// </summary>
        public void Clear()
        {
            _bullishFvgs.Clear();
            _bearishFvgs.Clear();
        }
    }
}
