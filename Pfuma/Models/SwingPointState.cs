namespace Pfuma.Models;

/// <summary>
/// Tracks swing point state for HTF timeframes
/// </summary>
public class SwingPointState
{
    public int LastSwingHighIndex { get; set; } = -1;
    public int LastSwingLowIndex { get; set; } = -1;
    public double LastSwingHighValue { get; set; } = double.MinValue;
    public double LastSwingLowValue { get; set; } = double.MaxValue;
    public bool LastSwingWasHigh { get; set; } = false;
    public bool LastSwingWasLow { get; set; } = false;
    public int CurrentSwingPointNumber { get; set; } = 0;
    public SwingPoint LastHighSwingPoint { get; set; }
    public SwingPoint LastLowSwingPoint { get; set; }
}