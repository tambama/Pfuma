using cAlgo.API;

namespace Pfuma.Extensions;

public static class TimeFrameExtensions
{
    /// <summary>
    /// Converts a timeframe string to a TimeFrame enum value
    /// </summary>
    public static TimeFrame GetTimeFrameFromString(this string timeframeStr)
    {
        switch (timeframeStr.ToUpper())
        {
            case "M1":
                return TimeFrame.Minute;
            case "M5":
                return TimeFrame.Minute5;
            case "M15":
                return TimeFrame.Minute15;
            case "M30":
                return TimeFrame.Minute30;
            case "H1":
                return TimeFrame.Hour;
            case "H4":
                return TimeFrame.Hour4;
            case "D1":
                return TimeFrame.Daily;
            case "W1":
                return TimeFrame.Weekly;
            case "MN1":
                return TimeFrame.Monthly;
            default:
                return TimeFrame.Hour4; // Default to H4 if input is invalid
        }
    }

    /// <summary>
    /// Converts a TimeFrame enum value to its shortname string representation
    /// </summary>
    public static string GetShortName(this TimeFrame timeFrame)
    {
        if (timeFrame == TimeFrame.Minute)
            return "M1";
        else if (timeFrame == TimeFrame.Minute5)
            return "M5";
        else if (timeFrame == TimeFrame.Minute15)
            return "M15";
        else if (timeFrame == TimeFrame.Minute30)
            return "M30";
        else if (timeFrame == TimeFrame.Hour)
            return "H1";
        else if (timeFrame == TimeFrame.Hour4)
            return "H4";
        else if (timeFrame == TimeFrame.Daily)
            return "D1";
        else if (timeFrame == TimeFrame.Weekly)
            return "W1";
        else if (timeFrame == TimeFrame.Monthly)
            return "MN1";
        else
            return "Unknown";
    }

    /// <summary>
    /// Gets the number of current timeframe bars that make up a single higher timeframe bar
    /// </summary>
    /// <param name="currentTimeframe">The current timeframe</param>
    /// <param name="higherTimeframe">The higher timeframe</param>
    /// <returns>The number of current timeframe bars in one higher timeframe bar</returns>
    public static int GetPeriodicity(this TimeFrame currentTimeframe, TimeFrame higherTimeframe)
    {
        // Convert timeframes to minutes for calculation
        int currentMinutes = TimeFrameToMinutes(currentTimeframe);
        int higherMinutes = TimeFrameToMinutes(higherTimeframe);

        // Calculate how many current timeframe bars fit in one higher timeframe bar
        if (currentMinutes == 0 || higherMinutes == 0 || currentMinutes > higherMinutes)
            return 0; // Invalid case or current timeframe is larger than higher timeframe

        return higherMinutes / currentMinutes;
    }

    /// <summary>
    /// Converts a TimeFrame to its equivalent in minutes
    /// </summary>
    /// <param name="timeFrame">The TimeFrame to convert</param>
    /// <returns>The number of minutes in the TimeFrame</returns>
    public static int TimeFrameToMinutes(this TimeFrame timeFrame)
    {
        // Map timeframes to their equivalent in minutes
        if (timeFrame == TimeFrame.Minute)
            return 1;
        else if (timeFrame == TimeFrame.Minute5)
            return 5;
        else if (timeFrame == TimeFrame.Minute15)
            return 15;
        else if (timeFrame == TimeFrame.Minute30)
            return 30;
        else if (timeFrame == TimeFrame.Hour)
            return 60;
        else if (timeFrame == TimeFrame.Hour4)
            return 240;
        else if (timeFrame == TimeFrame.Daily)
            return 1440; // 24 hours * 60 minutes
        else if (timeFrame == TimeFrame.Weekly)
            return 10080; // 7 days * 24 hours * 60 minutes
        else if (timeFrame == TimeFrame.Monthly)
            return 43200; // Approximation: 30 days * 24 hours * 60 minutes
        else
            return 0;
    }
}