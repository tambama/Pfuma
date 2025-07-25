using System;

namespace Pfuma.Core.Events
{
    /// <summary>
    /// Base class for all pattern-related events
    /// </summary>
    public abstract class PatternEventBase
    {
        public DateTime Timestamp { get; }
        public int Index { get; }
        
        protected PatternEventBase(int index)
        {
            Timestamp = DateTime.UtcNow;
            Index = index;
        }
    }
}