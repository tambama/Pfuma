namespace Zuva.Models;

public class StandardDeviation
{
    private double _range;

    /// <summary>
    /// Creates a new StandardDeviation instance using two price points
    /// </summary>
    /// <param name="index">Index of the swing point zero</param>
    /// <param name="zero">The first price point (usually high)</param>
    /// <param name="one">The second price point (usually low)</param>
    /// <param name="date">The time of swing point zero</param>
    /// <param name="direction">Direction of the standard deviation</param>
    public StandardDeviation(int index, double zero, double one, DateTime date, Direction direction)
    {
        Index = index;
        Zero = zero;
        One = one;
        OneTime = date;
        Direction = direction;
        AllSwept = false;
        IsMinusTwoSwept = false;
        IsMinusFourSwept = false;
        _range = Math.Abs(zero - one);
        
        // Calculate the standard deviation values
        CalculateDeviations();
    }

    public double Zero { get; set; }
    public double One { get; set; }
    public double MinusTwo { get; set; }
    public double MinusFour { get; set; }
    public DateTime OneTime { get; set; }
    public int Index { get; set; }
    public Direction Direction { get; set; }
    public bool AllSwept { get; set; }
    
    // Add flags to track which levels are swept but keep values
    public bool IsMinusTwoSwept { get; set; }
    public bool IsMinusFourSwept { get; set; }
    
    // Add properties to store extended line IDs
    public string ExtendedTwoLineId { get; set; }
    public string ExtendedFourLineId { get; set; }

    private void CalculateDeviations()
    {
        // Determine direction (if Zero > One, we're going down, otherwise up)
        bool isDowntrend = Zero > One;
        
        if (isDowntrend)
        {
            // For downtrends, extensions continue downward
            MinusTwo = One - (2 * _range);
            MinusFour = One - (4 * _range);
        }
        else
        {
            // For uptrends, extensions continue upward
            MinusTwo = One + (2 * _range);
            MinusFour = One + (4 * _range);
        }
    }
    
    public void Update()
    {
        _range = Math.Abs(Zero - One);
        CalculateDeviations();
    }
    
    /// <summary>
    /// Mark a standard deviation level as swept
    /// </summary>
    /// <param name="isMinusTwo">True if MinusTwo level was swept, false if MinusFour</param>
    public void MarkLevelAsSwept(bool isMinusTwo)
    {
        if (isMinusTwo)
        {
            // Instead of setting MinusTwo to 0, just mark it as swept but keep the value
            // MinusTwo = 0;
            IsMinusTwoSwept = true;
        }
        else
        {
            // Instead of setting MinusFour to 0, just mark it as swept but keep the value
            // MinusFour = 0;
            IsMinusFourSwept = true;
        }

        // Check if all levels are swept
        if (IsMinusTwoSwept && IsMinusFourSwept)
        {
            AllSwept = true;
        }
    }
}