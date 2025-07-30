using System.ComponentModel;

namespace Zuva.Models;

public enum LiquidityType
{
    [Description("Normal Swing Point")] Normal,
    [Description("Previous Daily High")] PDH,
    [Description("Previous Daily Low")] PDL,
    [Description("Previous Session High")] PSH,
    [Description("Previous Session Low")] PSL,
    [Description("Previous Cycle High")] PCH,
    [Description("Previous Cycle Low")] PCL
}