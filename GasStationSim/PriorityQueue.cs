using System;
using System.Collections.Generic;

namespace GasStationSim
{
    // Приоритетная очередь на основе списка и сортировки.
    public class PriorityQueue<T> where T : IComparable<T>
    {
        private List<T> data = new List<T>();

        public void Enqueue(T item)
        {
            data.Add(item);
            data.Sort();
        }

        public T Dequeue()
        {
            var item = data[0];
            data.RemoveAt(0);
            return item;
        }

        public int Count => data.Count;
    }
}
