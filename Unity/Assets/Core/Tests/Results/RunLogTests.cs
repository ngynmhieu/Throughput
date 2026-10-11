using System;
using NUnit.Framework;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Results
{
    public class RunLogTests
    {
        private static Station Desk(string name) => new Station(name, 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));

        [Test]
        public void RecordsOneVisitPerStation()
        {
            Station id = Desk("id"), scan = Desk("scan");
            var p = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p);
            log.OnJoinQueue(0.0, id, p);
            log.OnStartService(2.0, id, p);
            log.OnEndService(5.0, id, p);
            log.OnJoinQueue(7.0, scan, p);
            log.OnStartService(10.0, scan, p);
            log.OnEndService(11.0, scan, p);
            log.OnExit(11.0, p);

            PassengerRecord r = log.Passengers[0];
            Assert.That(r.Visits.Count, Is.EqualTo(2));
            Assert.That(r.Visits[1].Station, Is.EqualTo("scan"));
            Assert.That(r.Visits[1].Wait, Is.EqualTo(3.0));
            Assert.That(r.Visits[0].LeftAt, Is.EqualTo(5.0));
            // First-visit shortcuts
            Assert.That(r.Station, Is.EqualTo("id"));
            Assert.That(r.Wait, Is.EqualTo(2.0));
            Assert.That(r.ServiceEnd, Is.EqualTo(5.0));
        }

        [Test]
        public void Rejects_StepAtStationThePassengerIsNotAt()
        {
            Station id = Desk("id"), scan = Desk("scan");
            var p = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p);
            log.OnJoinQueue(0.0, id, p);

            Assert.Throws<InvalidOperationException>(() => log.OnStartService(1.0, scan, p));
        }

        [Test]
        public void Rejects_GivingUpWithoutBeingInLine()
        {
            var p = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p);

            Assert.Throws<InvalidOperationException>(() => log.OnRenege(5.0, Desk("desk"), p));
        }
    }
}
