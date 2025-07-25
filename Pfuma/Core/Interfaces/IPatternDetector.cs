using System.Collections.Generic;
using cAlgo.API;
using Pfuma.Models;

namespace Pfuma.Core.Interfaces;

/// <summary>
/// Base interface for all pattern detection components
/// </summary>
public interface IPatternDetector<T> where T : class
{
    /// <summary>
    /// Detects patterns in the given bar data
    /// </summary>
    void Detect(Bars bars, int currentIndex);
        
    /// <summary>
    /// Gets all detected patterns
    /// </summary>
    List<T> GetAll();
        
    /// <summary>
    /// Gets patterns filtered by direction
    /// </summary>
    List<T> GetByDirection(Direction direction);
        
    /// <summary>
    /// Clears all detected patterns
    /// </summary>
    void Clear();
        
    /// <summary>
    /// Validates if a pattern is still valid
    /// </summary>
    bool IsValid(T pattern, int currentIndex);
}