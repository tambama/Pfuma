using System;

namespace Pfuma.Core.Events;

/// <summary>
/// Event fired when entering a macro time
/// </summary>
public class MacroTimeEnteredEvent : PatternEventBase
{
    public DateTime MacroTime { get; }
        
    public MacroTimeEnteredEvent(DateTime macroTime, int index) : base(index)
    {
        MacroTime = macroTime;
    }
}