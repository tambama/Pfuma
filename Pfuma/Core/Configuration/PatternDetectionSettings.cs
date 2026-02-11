namespace Pfuma.Core.Configuration;

public class PatternDetectionSettings
{
    // Order Flow
    public bool ShowOrderFlow { get; set; } = false;
    public bool RemoveSweptOrderflow { get; set; } = false;
    public bool ShowSweptOrderflow { get; set; } = false;
    public bool ShowHtfOrderFlow { get; set; } = false;
    public bool ShowLiquiditySweep { get; set; } = false;
        
    // FVG
    public bool ShowFVG { get; set; } = false;
    public bool ShowHtfFvg { get; set; } = false;
    public bool ShowHighTimeframeCandle { get; set; } = false;
    public bool ShowIFvg { get; set; } = false;
        
    // CISD
    public bool ShowCISD { get; set; } = false;
    public bool ShowHtfCisd { get; set; } = false;
    public int MaxCisdsPerDirection { get; set; } = 2;
    public bool ShowOTE { get; set; } = false;
    public bool ShowPropulsionBlock { get; set; } = false;

    // Special Patterns
    public bool ShowUnicorn { get; set; } = false;
    public bool ShowBreakerBlock { get; set; } = false;
    public bool ShowRejectionBlock { get; set; } = false;
    public bool ShowOrderBlock { get; set; } = false;
    public bool ShowHtfOrderBlock { get; set; } = false;
        
    // Quadrants
    public bool ShowQuadrants { get; set; } = false;
    public bool ShowInsideKeyLevel { get; set; } = false;
    public bool ShowInducement { get; set; } = false;

    // 3 Drives Pattern
    public bool Show3DrivesPattern { get; set; } = false;

    // Swept Level Management
    public bool ClearSwept { get; set; } = true;
}