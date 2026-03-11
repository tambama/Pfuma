using System;
using Pfuma.Models;

namespace Pfuma.Services.Time;

/// <summary>
/// Shared base class for time gap levels (RTOG, FPFVG, etc.)
/// </summary>
public class TimeGapLevel
{
    public Level Level { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsDrawn { get; set; }
    public string ChartId { get; set; }
}
