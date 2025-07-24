using System.ComponentModel;

namespace Pfuma.Models;

public enum LiquidityName
{
    [Description("Normal")] N,
    [Description("A High")] AH,
    [Description("A Low")] AL,
    [Description("Pre-L High")]LPH,
    [Description("Pre-L Low")]LPL,
    [Description("LN High")] LH,
    [Description("LN Low")] LL,
    [Description("LN Lunch High")] LLH,
    [Description("LN Lunch Low")] LLL,
    [Description("Pre AM High")] NYPH,
    [Description("Pre AM Low")] NYPL,
    [Description("AM High")] NYAMH,
    [Description("AM Low")] NYAML,
    [Description("NY L High")] NYLH,
    [Description("NY L Low")] NYLL,
    [Description("Pre-NY High")] NYPPH,
    [Description("Pre-NY Low")] NYPPL,
    [Description("PM High")] NYPMH,
    [Description("PM Low")] NYPML,
    [Description("Daily High")] PDH,
    [Description("Daily Low")] PDL
}