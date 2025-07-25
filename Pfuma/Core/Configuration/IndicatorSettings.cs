namespace Pfuma.Core.Configuration;

/// <summary>
/// Central configuration for all indicator settings
/// </summary>
public class IndicatorSettings
{
    // Swing Point Detection
    public SwingPointSettings SwingPoints { get; set; } = new();
        
    // Pattern Detection
    public PatternDetectionSettings Patterns { get; set; } = new();
        
    // Visualization
    public VisualizationSettings Visualization { get; set; } = new();
        
    // Notifications
    public NotificationSettings Notifications { get; set; } = new();
        
    // Time Management
    public TimeSettings Time { get; set; } = new();
}