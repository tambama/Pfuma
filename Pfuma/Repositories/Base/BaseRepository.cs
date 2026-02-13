using System;
using System.Collections.Generic;
using System.Linq;
using Pfuma.Core.Interfaces;

namespace Pfuma.Repositories.Base
{
    /// <summary>
    /// Base implementation of the repository pattern.
    /// Note: No locking needed — cTrader indicators run on a single thread.
    /// </summary>
    public class BaseRepository<T> : IRepository<T> where T : class
    {
        protected readonly List<T> Items;

        public BaseRepository()
        {
            Items = new List<T>();
        }

        public virtual void Add(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            Items.Add(item);
        }

        public virtual void Remove(T item)
        {
            if (item == null)
                return;

            Items.Remove(item);
        }

        public virtual List<T> GetAll()
        {
            return new List<T>(Items);
        }

        public virtual List<T> Find(Func<T, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return Items.Where(predicate).ToList();
        }

        public virtual bool Any(Func<T, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return Items.Any(predicate);
        }

        public virtual void Clear()
        {
            Items.Clear();
        }

        public int Count => Items.Count;
    }
}
