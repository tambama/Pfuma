namespace Pfuma.Core.Configuration;

public class TimeSettings
{
    public bool ShowMacros { get; set; } = false;
    public bool MacroFilter { get; set; } = false;
    public bool ShowFibonacciLevels { get; set; } = false;
    public int UtcOffset { get; set; } = -4;
}