namespace Pfuma.Core.Configuration;

public class TimeSettings
{
    public bool ShowMacros { get; set; } = false;
    public bool MacroFilter { get; set; } = false;
    public bool ShowDailyLevels { get; set; } = true;
    public bool ShowSessionLevels { get; set; } = true;
    public bool ShowCycles30 { get; set; } = false;
    public int UtcOffset { get; set; } = -4;
}