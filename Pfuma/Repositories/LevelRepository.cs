using System;
using System.Collections.Generic;
using System.Linq;
using Pfuma.Models;
using Pfuma.Repositories.Base;

namespace Pfuma.Repositories;

/// <summary>
/// Repository for storing Level objects (FVGs, Order Blocks, etc.)
/// Maintains type-indexed caches for O(1) lookups instead of full scans.
/// </summary>
public class LevelRepository : BaseRepository<Level>
{
    private readonly Dictionary<LevelType, List<Level>> _typeCache = new();
    private readonly Dictionary<LevelType, Level> _mostRecentByType = new();
    private readonly Dictionary<(LevelType, Direction), Level> _mostRecentByTypeAndDirection = new();

    public override void Add(Level item)
    {
        base.Add(item);
        AddToTypeCache(item);
        UpdateMostRecent(item);
    }

    public override void Remove(Level item)
    {
        base.Remove(item);
        RemoveFromTypeCache(item);
        RebuildMostRecentForType(item.LevelType);
    }

    public override void Clear()
    {
        base.Clear();
        _typeCache.Clear();
        _mostRecentByType.Clear();
        _mostRecentByTypeAndDirection.Clear();
    }

    public List<Level> GetByType(LevelType type)
    {
        if (_typeCache.TryGetValue(type, out var list))
            return list;
        return new List<Level>();
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
        if (_typeCache.TryGetValue(type, out var list))
            return list.Where(l => l.Direction == direction).ToList();
        return new List<Level>();
    }

    public Level GetMostRecent(LevelType type)
    {
        _mostRecentByType.TryGetValue(type, out var level);
        return level;
    }

    public Level GetMostRecent(LevelType type, Direction direction)
    {
        _mostRecentByTypeAndDirection.TryGetValue((type, direction), out var level);
        return level;
    }

    private void AddToTypeCache(Level item)
    {
        if (!_typeCache.TryGetValue(item.LevelType, out var list))
        {
            list = new List<Level>();
            _typeCache[item.LevelType] = list;
        }
        list.Add(item);
    }

    private void RemoveFromTypeCache(Level item)
    {
        if (_typeCache.TryGetValue(item.LevelType, out var list))
        {
            list.Remove(item);
        }
    }

    private void UpdateMostRecent(Level item)
    {
        // Update by type only
        if (!_mostRecentByType.TryGetValue(item.LevelType, out var current) || item.Index >= current.Index)
        {
            _mostRecentByType[item.LevelType] = item;
        }

        // Update by type + direction
        var key = (item.LevelType, item.Direction);
        if (!_mostRecentByTypeAndDirection.TryGetValue(key, out var currentTD) || item.Index >= currentTD.Index)
        {
            _mostRecentByTypeAndDirection[key] = item;
        }
    }

    private void RebuildMostRecentForType(LevelType type)
    {
        // Rebuild most-recent-by-type
        if (_typeCache.TryGetValue(type, out var list) && list.Count > 0)
        {
            _mostRecentByType[type] = list.OrderByDescending(l => l.Index).First();

            // Rebuild most-recent-by-type-and-direction for both directions
            foreach (Direction dir in new[] { Direction.Up, Direction.Down })
            {
                var key = (type, dir);
                var dirItems = list.Where(l => l.Direction == dir).ToList();
                if (dirItems.Count > 0)
                    _mostRecentByTypeAndDirection[key] = dirItems.OrderByDescending(l => l.Index).First();
                else
                    _mostRecentByTypeAndDirection.Remove(key);
            }
        }
        else
        {
            _mostRecentByType.Remove(type);
            _mostRecentByTypeAndDirection.Remove((type, Direction.Up));
            _mostRecentByTypeAndDirection.Remove((type, Direction.Down));
        }
    }
}
