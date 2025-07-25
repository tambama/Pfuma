namespace Pfuma.Core.Interfaces;

/// <summary>
/// Interface for components that need initialization
/// </summary>
public interface IInitializable
{
    /// <summary>
    /// Initializes the component
    /// </summary>
    void Initialize();
        
    /// <summary>
    /// Disposes of resources
    /// </summary>
    void Dispose();
}