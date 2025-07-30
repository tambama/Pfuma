namespace Zuva.Models;

public class SessionTracker
{
    public double High { get; set; } = double.MinValue;
    public double Low { get; set; } = double.MaxValue;
    public int HighIndex { get; set; } = -1;
    public int LowIndex { get; set; } = -1;
    public DateTime HighTime { get; set; }
    public DateTime LowTime { get; set; }
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    public DateTime EndTime { get; set; } = DateTime.MinValue;
}