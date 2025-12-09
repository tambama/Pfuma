using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using Pfuma.Core.Configuration;
using Pfuma.Core.Events;
using Pfuma.Core.Interfaces;
using Pfuma.Detectors.Base;
using Pfuma.Models;
using Pfuma.Services;

namespace Pfuma.Detectors
{
    /// <summary>
    /// Detects Unicorn patterns (FVGs that intersect with breaker blocks and involve CISD confirming candles)
    /// </summary>
    public class UnicornDetector : BasePatternDetector<Level>
    {
        private readonly IVisualization<Level> _visualizer;
        private readonly IRepository<Level> _cisdRepository;
        
        public UnicornDetector(
            Chart chart,
            CandleManager candleManager,
            IEventAggregator eventAggregator,
            IRepository<Level> repository,
            IRepository<Level> cisdRepository,
            IVisualization<Level> visualizer,
            IndicatorSettings settings,
            Action<string> logger = null)
            : base(chart, candleManager, eventAggregator, repository, settings, logger)
        {
            _visualizer = visualizer;
            _cisdRepository = cisdRepository;
        }
        
        protected override List<Level> PerformDetection(int currentIndex)
        {
            // Unicorn detection is triggered by FVG detection events
            return new List<Level>();
        }
        
        /// <summary>
        /// Checks if an FVG qualifies as a Unicorn pattern
        /// </summary>
        public void CheckForUnicorn(Level fvg)
        {
            if (fvg == null || fvg.LevelType != LevelType.FairValueGap)
                return;
            
            // Get all confirmed CISDs that have breaker blocks
            var confirmedCisdsWithBreakers = _cisdRepository
                .Find(cisd => cisd.LevelType == LevelType.CISD &&
                              cisd.IsConfirmed && 
                              !cisd.Activated && 
                              cisd.BreakerBlock != null);
            
            foreach (var cisd in confirmedCisdsWithBreakers)
            {
                // Check if one of the FVG candles is the CISD confirming candle
                bool fvgInvolvesCisdCandle = false;
                bool passesDirectionalRequirement = false;
                
                if (fvg.Direction == Direction.Up) // Bullish FVG
                {
                    // Check if any of the FVG's candles match the CISD confirming candle
                    fvgInvolvesCisdCandle =
                        fvg.Index == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexMid == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexHigh == cisd.IndexOfConfirmingCandle;
                    
                    // Bullish FVG should be below the CISD high
                    passesDirectionalRequirement = fvg.Low < cisd.High;
                }
                else // Bearish FVG
                {
                    // Check if any of the FVG's candles match the CISD confirming candle
                    fvgInvolvesCisdCandle =
                        fvg.Index == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexMid == cisd.IndexOfConfirmingCandle ||
                        fvg.IndexLow == cisd.IndexOfConfirmingCandle;
                    
                    // Bearish FVG should be above the CISD low
                    passesDirectionalRequirement = fvg.High > cisd.Low;
                }
                
                // If the FVG involves the CISD confirming candle and passes directional requirement
                if (!fvgInvolvesCisdCandle || !passesDirectionalRequirement) 
                    continue;
                
                // Check if the FVG intersects with the breaker block
                bool intersectsWithBreaker = CheckIntersection(fvg, cisd.BreakerBlock);
                
                if (intersectsWithBreaker)
                {
                    // Create a new Unicorn level based on the FVG (keep original FVG intact)
                    var unicorn = new Level(
                        LevelType.Unicorn,
                        fvg.Low,
                        fvg.High,
                        fvg.LowTime,
                        fvg.HighTime,
                        fvg.MidTime,
                        fvg.Direction,
                        fvg.Index,
                        fvg.IndexHigh,
                        fvg.IndexLow,
                        fvg.IndexMid,
                        fvg.Zone
                    );

                    // Copy additional properties
                    unicorn.TimeFrame = fvg.TimeFrame;
                    unicorn.IndexHighPrice = fvg.IndexHighPrice;
                    unicorn.IndexLowPrice = fvg.IndexLowPrice;
                    unicorn.SourceFvg = fvg;
                    unicorn.BreakerBlock = cisd.BreakerBlock;

                    // Store the unicorn in the repository
                    Repository.Add(unicorn);

                    // Publish detection event for the new unicorn
                    PublishDetectionEvent(unicorn, unicorn.Index);

                    // Only need to find one match
                    break;
                }
            }
        }
        
        /// <summary>
        /// Checks if two levels intersect in price range
        /// </summary>
        private bool CheckIntersection(Level level1, Level level2)
        {
            if (level1 == null || level2 == null)
                return false;
            
            // Check for price range intersection
            // Two ranges intersect if:
            // - level1's high is >= level2's low AND
            // - level1's low is <= level2's high
            bool priceIntersects = !(level1.High < level2.Low || level1.Low > level2.High);
            
            return priceIntersects;
        }
        
        protected override void PublishDetectionEvent(Level unicorn, int currentIndex)
        {
            EventAggregator.Publish(new UnicornDetectedEvent(unicorn));
            
            if (Settings.Patterns.ShowUnicorn && _visualizer != null)
            {
                _visualizer.Draw(unicorn);
            }
        }
        
        protected override void LogDetection(Level unicorn, int currentIndex)
        {
            if (Settings.Notifications.EnableLog)
            {
                Logger($"Unicorn detected: {unicorn.Direction} at index {currentIndex}");
            }
        }
        
        public override List<Level> GetByDirection(Direction direction)
        {
            return Repository.Find(u => 
                u.LevelType == LevelType.Unicorn && 
                u.Direction == direction);
        }
        
        public override bool IsValid(Level unicorn, int currentIndex)
        {
            return unicorn != null && 
                   unicorn.LevelType == LevelType.Unicorn;
        }
        
        public Level GetLastUnicorn(Direction direction)
        {
            return GetByDirection(direction)
                .OrderByDescending(u => u.Index)
                .FirstOrDefault();
        }
        
        protected override void SubscribeToEvents()
        {
            EventAggregator.Subscribe<FvgDetectedEvent>(OnFvgDetected);
            EventAggregator.Subscribe<BreakerBlockDetectedEvent>(OnBreakerBlockDetected);
        }
        
        protected override void UnsubscribeFromEvents()
        {
            EventAggregator.Unsubscribe<FvgDetectedEvent>(OnFvgDetected);
            EventAggregator.Unsubscribe<BreakerBlockDetectedEvent>(OnBreakerBlockDetected);
        }
        
        private void OnFvgDetected(FvgDetectedEvent evt)
        {
            CheckForUnicorn(evt.FvgLevel);
        }
        
        private void OnBreakerBlockDetected(BreakerBlockDetectedEvent evt)
        {
            // When a new breaker block is detected, check existing FVGs for unicorn patterns
            var existingFvgs = Repository.Find(l => l.LevelType == LevelType.FairValueGap);
            
            foreach (var fvg in existingFvgs)
            {
                CheckForUnicorn(fvg);
            }
        }
    }
}