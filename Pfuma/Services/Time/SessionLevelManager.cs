using System;
using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Detectors;
using Pfuma.Models;
using Pfuma.Services;
using Pfuma.Extensions;

namespace Pfuma.Services.Time;

/// <summary>
/// Manages session high/low level processing
/// </summary>
public class SessionLevelManager : ISessionLevelManager
{
    private readonly CandleManager _candleManager;
    private readonly SwingPointDetector _swingPointDetector;
    private readonly Chart _chart;
    private readonly bool _showSessionLevels;
    
    private readonly Dictionary<SessionType, SessionTracker> _sessionTrackers;
    private SessionType _lastProcessedSession = SessionType.None;
    private readonly Dictionary<int, SessionType> _hourToSessionMap;

    public SessionLevelManager(
        CandleManager candleManager,
        SwingPointDetector swingPointDetector,
        Chart chart,
        bool showSessionLevels = true)
    {
        _candleManager = candleManager;
        _swingPointDetector = swingPointDetector;
        _chart = chart;
        _showSessionLevels = showSessionLevels;
        
        _sessionTrackers = new Dictionary<SessionType, SessionTracker>();
        foreach (SessionType sessionType in Enum.GetValues(typeof(SessionType)))
        {
            if (sessionType != SessionType.None)
            {
                _sessionTrackers[sessionType] = new SessionTracker();
            }
        }
        
        _hourToSessionMap = new Dictionary<int, SessionType>
        {
            { 18, SessionType.Asia },
            { 19, SessionType.Asia },
            { 20, SessionType.Asia },
            { 21, SessionType.Asia },
            { 22, SessionType.Asia },
            { 23, SessionType.Asia },
            { 0, SessionType.LondonPre },
            { 1, SessionType.London },
            { 2, SessionType.London },
            { 3, SessionType.London },
            { 4, SessionType.London },
            { 5, SessionType.LondonLunch },
            { 6, SessionType.LondonLunch },
            { 7, SessionType.NewYorkPre },
            { 8, SessionType.NewYorkPre },
            { 9, SessionType.NewYorkPre },
            { 10, SessionType.NewYorkAM },
            { 11, SessionType.NewYorkAM },
            { 12, SessionType.NewYorkLunch },
            { 13, SessionType.NewYorkLunch },
            { 14, SessionType.NewYorkPMPre },
            { 15, SessionType.NewYorkPM },
            { 16, SessionType.NewYorkPM },
            { 17, SessionType.NewYorkPM }
        };
    }

    public void ProcessBar(int index, DateTime marketTime)
    {
        if (index >= _candleManager.Count) return;
        
        SessionType currentBarSession = GetCurrentSession(marketTime);
        
        if (_lastProcessedSession != currentBarSession && _lastProcessedSession != SessionType.None)
        {
            ProcessSessionLevels(index, _lastProcessedSession);
        }
        
        if (currentBarSession != SessionType.None)
        {
            var candle = _candleManager.GetCandle(index);
            if (candle != null)
            {
                UpdateSessionTracker(currentBarSession, index, candle.High, candle.Low);
            }
        }
        
        _lastProcessedSession = currentBarSession;
    }
    
    public SessionType GetCurrentSession(DateTime marketTime)
    {
        int hour = marketTime.Hour;
        int minute = marketTime.Minute;
        
        if (hour == 9)
        {
            return minute < 30 ? SessionType.NewYorkPre : SessionType.NewYorkAM;
        }
        else if (hour == 11)
        {
            return minute < 30 ? SessionType.NewYorkAM : SessionType.NewYorkLunch;
        }
        else if (hour == 13)
        {
            return minute < 30 ? SessionType.NewYorkLunch : SessionType.NewYorkPMPre;
        }
        else if (hour == 14)
        {
            return minute < 30 ? SessionType.NewYorkPMPre : SessionType.NewYorkPM;
        }
        
        if (_hourToSessionMap.TryGetValue(hour, out SessionType session))
        {
            return session;
        }
        
        return SessionType.None;
    }
    
    private void UpdateSessionTracker(SessionType session, int index, double high, double low)
    {
        if (session == SessionType.None) return;
        
        var tracker = _sessionTrackers[session];
        
        var candle = _candleManager.GetCandle(index);
        if (candle == null) return;
        
        if (tracker.StartTime == DateTime.MinValue)
            tracker.StartTime = candle.Time;
        
        tracker.EndTime = candle.Time;
        
        if (high > tracker.High)
        {
            tracker.High = high;
            tracker.HighIndex = index;
            tracker.HighTime = candle.Time;
        }
        
        if (low < tracker.Low)
        {
            tracker.Low = low;
            tracker.LowIndex = index;
            tracker.LowTime = candle.Time;
        }
    }
    
    private void ProcessSessionLevels(int currentIndex, SessionType session)
    {
        var tracker = _sessionTrackers[session];
        
        if (tracker.HighIndex < 0 || tracker.LowIndex < 0) return;
        
        var highCandle = _candleManager.GetCandle(tracker.HighIndex);
        var lowCandle = _candleManager.GetCandle(tracker.LowIndex);
        
        if (highCandle == null || lowCandle == null) return;
        
        LiquidityName highLiquidityName;
        LiquidityName lowLiquidityName;
        
        switch (session)
        {
            case SessionType.Asia:
                highLiquidityName = LiquidityName.AH;
                lowLiquidityName = LiquidityName.AL;
                break;
            case SessionType.LondonPre:
                highLiquidityName = LiquidityName.LPH;
                lowLiquidityName = LiquidityName.LPL;
                break;
            case SessionType.London:
                highLiquidityName = LiquidityName.LH;
                lowLiquidityName = LiquidityName.LL;
                break;
            case SessionType.LondonLunch:
                highLiquidityName = LiquidityName.LLH;
                lowLiquidityName = LiquidityName.LLL;
                break;
            case SessionType.NewYorkPre:
                highLiquidityName = LiquidityName.NYPH;
                lowLiquidityName = LiquidityName.NYPL;
                break;
            case SessionType.NewYorkAM:
                highLiquidityName = LiquidityName.NYAMH;
                lowLiquidityName = LiquidityName.NYAML;
                break;
            case SessionType.NewYorkLunch:
                highLiquidityName = LiquidityName.NYLH;
                lowLiquidityName = LiquidityName.NYLL;
                break;
            case SessionType.NewYorkPMPre:
                highLiquidityName = LiquidityName.NYPPH;
                lowLiquidityName = LiquidityName.NYPPL;
                break;
            case SessionType.NewYorkPM:
                highLiquidityName = LiquidityName.NYPMH;
                lowLiquidityName = LiquidityName.NYPML;
                break;
            default:
                highLiquidityName = LiquidityName.N;
                lowLiquidityName = LiquidityName.N;
                break;
        }
        
        CreateOrUpdateSpecialSwingPoint(
            tracker.HighIndex,
            tracker.High,
            tracker.HighTime,
            highCandle,
            SwingType.HH,
            LiquidityType.PSH,
            Direction.Up,
            highLiquidityName,
            tracker.EndTime);
        
        CreateOrUpdateSpecialSwingPoint(
            tracker.LowIndex,
            tracker.Low,
            tracker.LowTime,
            lowCandle,
            SwingType.LL,
            LiquidityType.PSL,
            Direction.Down,
            lowLiquidityName,
            tracker.EndTime);
        
        _sessionTrackers[session] = new SessionTracker();
    }
    
    private void CreateOrUpdateSpecialSwingPoint(
        int index,
        double price,
        DateTime time,
        Candle candle,
        SwingType swingType,
        LiquidityType liquidityType,
        Direction direction,
        LiquidityName liquidityName,
        DateTime endTime)
    {
        if (_swingPointDetector == null) return;
        
        var existingPoint = _swingPointDetector.GetSwingPointAtIndex(index);
        
        if (existingPoint != null)
        {
            if ((liquidityType == LiquidityType.PDH || liquidityType == LiquidityType.PDL) &&
                existingPoint.LiquidityType != LiquidityType.PDH &&
                existingPoint.LiquidityType != LiquidityType.PDL)
            {
                RemoveExistingSessionLabels(time, price);
            }
            
            existingPoint.LiquidityType = liquidityType;
            existingPoint.LiquidityName = liquidityName;
        }
        else
        {
            var swingPoint = new SwingPoint(
                index,
                price,
                time,
                candle,
                swingType,
                liquidityType,
                direction,
                liquidityName
            );
            
            _swingPointDetector.AddSpecialSwingPoint(swingPoint);
        }
        
        if (_chart != null && _showSessionLevels)
        {
            string id = $"{liquidityName.ToString().ToLower()}-{time.Ticks}";
            
            _chart.DrawStraightLine(
                id,
                time,
                price,
                endTime,
                price,
                liquidityName.ToString(),
                LineStyle.Solid,
                Color.Wheat,
                true,
                true
            );
        }
    }
    
    private void RemoveExistingSessionLabels(DateTime time, double price)
    {
        if (_chart == null) return;
        
        string[] sessionPrefixes =
        {
            "ah", "al", "lh", "ll", "lph", "lpl", "llh", "lll",
            "nyph", "nypl", "nyamh", "nyaml", "nylh", "nyll",
            "nypph", "nyppl", "nypmh", "nypml", "sh", "sl"
        };
        
        foreach (var prefix in sessionPrefixes)
        {
            string lineId = $"{prefix}-{time.Ticks}";
            string labelId = $"{lineId}-label";
            
            _chart.RemoveObject(lineId);
            _chart.RemoveObject(labelId);
        }
    }
}