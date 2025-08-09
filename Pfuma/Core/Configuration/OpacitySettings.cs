namespace Pfuma.Core.Configuration;

public class OpacitySettings
{
    public int OrderFlow { get; set; } = 8;
    public int FVG { get; set; } = 8;
    public int RejectionBlock { get; set; } = 20;
    public int OrderBlock { get; set; } = 20;
    public int ActivationRectangle { get; set; } = 15;
}