using System;
using System.Collections.Generic;

namespace Throughput.Core.Engine.Events
{
    /// <summary>Binary min-heap of events ordered by (Time, Type, Seq). Same inputs → same order, always.</summary>
    public sealed class EventList
    {
        private readonly List<EngineEvent> _heap = new List<EngineEvent>();
        private long _nextSeq;

        public int Count => _heap.Count;

        public void Push(EngineEvent e)
        {
            e.Seq = _nextSeq++;
            _heap.Add(e);
            SiftUp(_heap.Count - 1);
        }

        public EngineEvent Peek()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("The event list is empty.");
            return _heap[0];
        }

        public EngineEvent Pop()
        {
            EngineEvent top = Peek();
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0)
                SiftDown(0);
            return top;
        }

        private static bool Before(EngineEvent a, EngineEvent b)
        {
            if (a.Time != b.Time) return a.Time < b.Time;
            if (a.Type != b.Type) return a.Type < b.Type;
            return a.Seq < b.Seq;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Before(_heap[i], _heap[parent])) break;
                Swap(i, parent);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;
                int smallest = i;
                if (left < _heap.Count && Before(_heap[left], _heap[smallest])) smallest = left;
                if (right < _heap.Count && Before(_heap[right], _heap[smallest])) smallest = right;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            EngineEvent tmp = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = tmp;
        }
    }
}
