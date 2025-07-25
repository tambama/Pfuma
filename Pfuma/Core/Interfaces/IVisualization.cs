namespace Pfuma.Core.Interfaces;

/// <summary>
/// Interface for visualization components
/// </summary>
public interface IVisualization<T> where T : class
{
    /// <summary>
    /// Draws the visual representation of the pattern
    /// </summary>
    void Draw(T pattern);
        
    /// <summary>
    /// Updates an existing visual representation
    /// </summary>
    void Update(T pattern);
        
    /// <summary>
    /// Removes the visual representation
    /// </summary>
    void Remove(T pattern);
        
    /// <summary>
    /// Clears all visualizations
    /// </summary>
    void Clear();
}