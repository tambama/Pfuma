using System;

namespace Pfuma.Models;

/// <summary>
/// Represents a time range with start and end times
/// </summary>
public class TimeRange
{
    public TimeSpan StartTime { get; }
    public TimeSpan EndTime { get; }

    public TimeRange(TimeSpan startTime, TimeSpan endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }

    public bool Contains(TimeSpan time)
    {
        return time >= StartTime && time <= EndTime;
    }

    public TimeSpan Duration => EndTime - StartTime;
}
