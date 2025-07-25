using System;
using System.Collections.Generic;
using System.Linq;
using Pfuma.Core.Interfaces;

namespace Pfuma.Repositories.Base
{
    /// <summary>
    /// Base implementation of the repository pattern
    /// </summary>
    public class BaseRepository<T> : IRepository<T> where T : class
    {
        protected readonly List<T> Items;
        protected readonly object LockObject = new object();
        
        public BaseRepository()
        {
            Items = new List<T>();
        }
        
        public virtual void Add(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            
            lock (LockObject)
            {
                Items.Add(item);
            }
        }
        
        public virtual void Remove(T item)
        {
            if (item == null)
                return;
            
            lock (LockObject)
            {
                Items.Remove(item);
            }
        }
        
        public virtual List<T> GetAll()
        {
            lock (LockObject)
            {
                return new List<T>(Items);
            }
        }
        
        public virtual List<T> Find(Func<T, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            
            lock (LockObject)
            {
                return Items.Where(predicate).ToList();
            }
        }
        
        public virtual bool Any(Func<T, bool> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));
            
            lock (LockObject)
            {
                return Items.Any(predicate);
            }
        }
        
        public virtual void Clear()
        {
            lock (LockObject)
            {
                Items.Clear();
            }
        }
        
        public int Count
        {
            get
            {
                lock (LockObject)
                {
                    return Items.Count;
                }
            }
        }
    }
}