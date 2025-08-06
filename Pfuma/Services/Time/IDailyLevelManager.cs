using Pfuma.Models;

namespace Pfuma.Services.Time
{
    public interface IDailyLevelManager
    {
        void ProcessDailyBoundary(int currentIndex);
    }
}