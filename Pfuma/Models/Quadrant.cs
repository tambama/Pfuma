namespace Pfuma.Models;

/// <summary>
/// Represents a quadrant level in a PD Array (0%, 25%, 50%, 75%, 100%)
/// </summary>
public class Quadrant
{
    /// <summary>
    /// Creates a new quadrant level
    /// </summary>
    /// <param name="percent">Percentage (0, 25, 50, 75, 100)</param>
    /// <param name="price">Price level</param>
    public Quadrant(int percent, double price)
    {
        Percent = percent;
        Price = price;
        IsSwept = false;
    }

    /// <summary>
    /// Percentage of the quadrant (0, 25, 50, 75, 100)
    /// </summary>
    public int Percent { get; set; }
    
    /// <summary>
    /// Price level of this quadrant
    /// </summary>
    public double Price { get; set; }
    
    /// <summary>
    /// Whether this quadrant has been swept
    /// </summary>
    public bool IsSwept { get; set; }
    
    /// <summary>
    /// Index of the swing point that swept this quadrant (if applicable)
    /// </summary>
    public int SweptByIndex { get; set; }
}