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
            public const int RejectionBlockLookback = 3;
            public const int OrderBlockLookback = 10;
            public const int ConsecutiveCandlesMin = 1;
        }
        
        
        // Quadrant Percentages
        public static readonly int[] QuadrantPercentages = { 0, 25, 50, 75, 100 };
        
    }
}