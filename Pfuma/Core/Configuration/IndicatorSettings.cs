namespace Pfuma.Core.Configuration;

/// <summary>
/// Central configuration for all indicator settings
/// </summary>
public class IndicatorSettings
{
    public PatternDetectionSettings Patterns { get; set; } = new PatternDetectionSettings();
    public TimeSettings Time { get; set; } = new TimeSettings();
    public VisualizationSettings Visualization { get; set; } = new VisualizationSettings();
    public NotificationSettings Notifications { get; set; } = new NotificationSettings();
}