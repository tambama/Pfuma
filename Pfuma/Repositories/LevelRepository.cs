using System.Collections.Generic;
using System.Linq;
using Pfuma.Models;
using Pfuma.Repositories.Base;

namespace Pfuma.Repositories;

/// <summary>
/// Repository for storing Level objects (FVGs, Order Blocks, etc.)
/// </summary>
public class LevelRepository : BaseRepository<Level>
{
    public List<Level> GetByType(LevelType type)
    {
        return Find(level => level.LevelType == type);
    }
        
    public List<Level> GetByDirection(Direction direction)
    {
        return Find(level => level.Direction == direction);
    }
        
    public List<Level> GetActive()
    {
        return Find(level => level.IsActive);
    }
        
    public List<Level> GetByTypeAndDirection(LevelType type, Direction direction)
    {
        return Find(level => level.LevelType == type && level.Direction == direction);
    }
        
    public Level GetMostRecent(LevelType type)
    {
        return GetByType(type)
            .OrderByDescending(l => l.Index)
            .FirstOrDefault();
    }
        
    public Level GetMostRecent(LevelType type, Direction direction)
    {
        return GetByTypeAndDirection(type, direction)
            .OrderByDescending(l => l.Index)
            .FirstOrDefault();
    }
}