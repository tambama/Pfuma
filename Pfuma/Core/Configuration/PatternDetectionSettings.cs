namespace Pfuma.Core.Configuration;

public class PatternDetectionSettings
{
    // Order Flow
    public bool ShowOrderFlow { get; set; } = false;
    public bool ShowHtfOrderFlow { get; set; } = false;
    public bool ShowLiquiditySweep { get; set; } = false;
        
    // FVG
    public bool ShowFVG { get; set; } = false;
    public bool ShowHtfFvg { get; set; } = false;
        
    // CISD
    public bool ShowCISD { get; set; } = false;
    public int MaxCisdsPerDirection { get; set; } = 2;
    public bool ShowOTE { get; set; } = false;
    public bool ShowPropulsionBlock { get; set; } = false;

    // Special Patterns
    public bool ShowUnicorn { get; set; } = false;
    public bool Show369 { get; set; } = false;
    public bool ShowBreakerBlock { get; set; } = false;
    public bool ShowRejectionBlock { get; set; } = false;
    public bool ShowOrderBlock { get; set; } = false;
        
    // Quadrants
    public bool ShowQuadrants { get; set; } = false;
    public bool ShowInsideKeyLevel { get; set; } = false;

    // Swept Level Management
    public bool ClearSwept { get; set; } = true;
}