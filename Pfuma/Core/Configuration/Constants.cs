namespace Pfuma.Core.Configuration
{
    /// <summary>
    /// Central location for all constants used throughout the indicator
    /// </summary>
    public static class Constants
    {
        // Calculation Constants
        public static class Calculations
        {
            public const double PriceTolerance = 0.0001;
            public const double RejectionWickMultiplier = 1.5;
            public const int MaxSwingPointScore = 3;
            public const int MinimumBarsRequired = 2;
            public const int MinimumSwingPointsForOrderFlow = 3;
        }
        
        // Time Constants
        public static class Time
        {
            public const int MacroStartMinutesBefore = 10;
            public const int MacroEndMinutesAfter = 10;
            public const int FibExtensionHours = 8;
            public const int LevelExtensionMinutes = 5;
            public const int SweptLineExtensionMinutes = 1;
            public const int MacroNotificationCooldownSeconds = 10;
        }
        
        // Drawing Constants
        public static class Drawing
        {
            public const int DefaultLineThickness = 1;
            public const int BoldLineThickness = 2;
            public const double DefaultOpacity = 0.5;
            public const string LineIdSeparator = "-";
            public const string SwingHighPrefix = "hi";
            public const string SwingLowPrefix = "lo";
            public const string MidPointPrefix = "ce";
        }
        
        // Pattern Detection
        public static class Patterns
        {
            public const int FvgRequiredBars = 3;
            public const int OrderBlockLookback = 3;
            public const int ConsecutiveCandlesMin = 1;
        }
        
        // Fibonacci Levels
        public static readonly double[] FibonacciRatios = 
        { 
            -2.0, -1.5, -1.0, -0.5, -0.25, 0.0, 0.114, 0.236, 
            0.382, 0.5, 0.618, 0.786, 0.886, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0 
        };
        
        public static readonly double[] TrackedFibonacciRatios = 
        { 
            -2.0, -1.5, -1.0, -0.5, -0.25, 0.0, 0.114, 0.886, 
            1.0, 1.25, 1.5, 2.0, 2.5, 3.0 
        };
        
        // Quadrant Percentages
        public static readonly int[] QuadrantPercentages = { 0, 25, 50, 75, 100 };
        
        // Session Times (in hours)
        public static class SessionHours
        {
            public const int AsiaStart = 18;
            public const int AsiaEnd = 23;
            public const int LondonPreStart = 0;
            public const int LondonStart = 1;
            public const int LondonEnd = 4;
            public const int NewYorkPreStart = 7;
            public const int NewYorkAMStart = 9;
            public const int NewYorkAMStartMinute = 30;
            public const int DailyBoundaryHour = 18;
        }
    }
}