using System;
using Pfuma.Models;

namespace Pfuma.Services.Time;

/// <summary>
/// Interface for managing session high/low levels
/// </summary>
public interface ISessionLevelManager
{
    /// <summary>
    /// Process a bar to check for session boundaries and update session tracking
    /// </summary>
    void ProcessBar(int index, DateTime marketTime);
    
    /// <summary>
    /// Gets the current session type based on market time
    /// </summary>
    SessionType GetCurrentSession(DateTime marketTime);
}