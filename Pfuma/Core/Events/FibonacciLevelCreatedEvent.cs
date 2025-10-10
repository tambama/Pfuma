using Pfuma.Models;

namespace Pfuma.Core.Events;

/// <summary>
/// Event raised when a new Fibonacci level is created
/// </summary>
public class FibonacciLevelCreatedEvent : PatternEventBase
{
    public FibonacciLevel FibonacciLevel { get; }
    public FibType FibType { get; }

    public FibonacciLevelCreatedEvent(FibonacciLevel fibonacciLevel, int index) : base(index)
    {
        FibonacciLevel = fibonacciLevel;
        FibType = fibonacciLevel.FibType;
    }
}
