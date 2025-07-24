using System;

namespace Pfuma.Models;

public class TimeRange
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public TimeRange(TimeSpan startTime, TimeSpan endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }
}