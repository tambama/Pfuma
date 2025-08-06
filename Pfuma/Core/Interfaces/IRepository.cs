using System.Collections.Generic;

namespace Pfuma.Core.Interfaces;

/// <summary>
/// Interface for data repositories
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Adds an item to the repository
    /// </summary>
    void Add(T item);
        
    /// <summary>
    /// Removes an item from the repository
    /// </summary>
    void Remove(T item);
        
    /// <summary>
    /// Gets all items
    /// </summary>
    List<T> GetAll();
        
    /// <summary>
    /// Finds items matching a predicate
    /// </summary>
    List<T> Find(System.Func<T, bool> predicate);
        
    /// <summary>
    /// Checks if any items match a predicate
    /// </summary>
    bool Any(System.Func<T, bool> predicate);
        
    /// <summary>
    /// Clears the repository
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Gets the count of items in the repository
    /// </summary>
    int Count { get; }
}