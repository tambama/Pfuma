using cAlgo.API;

namespace Zuva.Extensions;

public static class TimeExtensions
{
    /// <summary>
    /// Determines if the current bar is the start of a new higher timeframe bar
    /// </summary>
    /// <param name="barTime">The opening time for this bar</param>
    /// <param name="higherTimeframe">The higher timeframe to compare against</param>
    /// <returns>True if the current bar is the start of a new higher timeframe bar</returns>
    public static bool IsStartOfHigherTimeframeBar(this DateTime barTime, TimeFrame higherTimeframe)
    {
        if (higherTimeframe == TimeFrame.Minute5)
        {
            return barTime.Minute % 5 == 0;
        }
        else if (higherTimeframe == TimeFrame.Minute15)
        {
            return barTime.Minute % 15 == 0;
        }
        else if (higherTimeframe == TimeFrame.Minute30)
        {
            return barTime.Minute % 30 == 0;
        }
        else if (higherTimeframe == TimeFrame.Hour)
        {
            return barTime.Minute == 0;
        }
        else if (higherTimeframe == TimeFrame.Hour4)
        {
            return barTime.Hour % 4 == 0 && barTime.Minute == 0;
        }
        else if (higherTimeframe == TimeFrame.Daily)
        {
            return barTime.Hour == 0 && barTime.Minute == 0;
        }
        else if (higherTimeframe == TimeFrame.Weekly)
        {
            // Assuming Monday is the start of the week (DayOfWeek.Monday == 1)
            return barTime.DayOfWeek == DayOfWeek.Monday && barTime.Hour == 0 && barTime.Minute == 0;
        }
        else if (higherTimeframe == TimeFrame.Monthly)
        {
            return barTime.Day == 1 && barTime.Hour == 0 && barTime.Minute == 0;
        }
        else
        {
            // For the base timeframe (Minute) every bar is the start of a new bar
            return true;
        }
    }
}