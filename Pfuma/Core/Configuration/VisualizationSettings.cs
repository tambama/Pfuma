namespace Pfuma.Core.Configuration;

public class VisualizationSettings
{
        
    // Time-based
    public bool ShowMacros { get; set; } = true;
        
    // Colors
    public ColorSettings Colors { get; set; } = new();
        
    // Opacity levels
    public OpacitySettings Opacity { get; set; } = new();
    
    // Patterns
    public PatternDetectionSettings Patterns { get; set; } = new();
    
    // Notifications
    public NotificationSettings Notifications { get; set; } = new();
    
    public bool ShowLabels { get; set; } = true;
    public bool ShowExtendedLines { get; set; } = false;
}