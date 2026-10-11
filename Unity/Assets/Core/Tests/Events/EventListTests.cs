using System;
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Events;

namespace Throughput.Core.Tests.Events
{
    public class EventListTests
    {
        private static EngineEvent At(double time, EventType type = EventType.Arrive, string tag = null)
            => new EngineEvent(time, type, tag, null);

        [Test]
        public void PopsInTimeOrder()
        {
            var list = new EventList();
            foreach (double t in new[] { 5.0, 1.0, 4.0, 2.0, 3.0, 0.5 })
                list.Push(At(t));

            var popped = new List<double>();
            while (list.Count > 0)
                popped.Add(list.Pop().Time);

            Assert.That(popped, Is.EqualTo(new[] { 0.5, 1.0, 2.0, 3.0, 4.0, 5.0 }));
        }

        [Test]
        public void SameTime_EndServiceBeforeArrive()
        {
            // A desk that frees up at t can serve someone arriving at t (spec S-D4).
            var list = new EventList();
            list.Push(At(10.0, EventType.Arrive));
            list.Push(At(10.0, EventType.EndService));

            Assert.That(list.Pop().Type, Is.EqualTo(EventType.EndService));
            Assert.That(list.Pop().Type, Is.EqualTo(EventType.Arrive));
        }

        [Test]
        public void SameTimeAndType_KeepsInsertionOrder()
        {
            var list = new EventList();
            for (int i = 0; i < 50; i++)
                list.Push(At(7.0, EventType.Arrive, "e" + i));

            for (int i = 0; i < 50; i++)
                Assert.That(list.Pop().Node, Is.EqualTo("e" + i));
        }

        [Test]
        public void Empty_PeekAndPopThrow()
        {
            var list = new EventList();
            Assert.Throws<InvalidOperationException>(() => list.Peek());
            Assert.Throws<InvalidOperationException>(() => list.Pop());
        }
    }
}
