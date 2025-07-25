using System.Collections.Generic;
using System.Linq;
using Pfuma.Models;
using Pfuma.Repositories.Base;

namespace Pfuma.Repositories;

/// <summary>
/// Repository for storing SwingPoint objects
/// </summary>
public class SwingPointRepository : BaseRepository<SwingPoint>
{
    public List<SwingPoint> GetHighs()
    {
        return Find(sp => sp.Direction == Direction.Up);
    }
        
    public List<SwingPoint> GetLows()
    {
        return Find(sp => sp.Direction == Direction.Down);
    }
        
    public SwingPoint GetLastHigh()
    {
        return GetHighs()
            .OrderByDescending(sp => sp.Index)
            .FirstOrDefault();
    }
        
    public SwingPoint GetLastLow()
    {
        return GetLows()
            .OrderByDescending(sp => sp.Index)
            .FirstOrDefault();
    }
        
    public List<SwingPoint> GetAtIndex(int index)
    {
        return Find(sp => sp.Index == index);
    }
        
    public SwingPoint GetByIndex(int index)
    {
        return Items.FirstOrDefault(sp => sp.Index == index);
    }
        
    public List<SwingPoint> GetUnswept()
    {
        return Find(sp => !sp.Swept);
    }
        
    public List<SwingPoint> GetSwept()
    {
        return Find(sp => sp.Swept);
    }
        
    public List<SwingPoint> GetByLiquidityType(LiquidityType type)
    {
        return Find(sp => sp.LiquidityType == type);
    }
}