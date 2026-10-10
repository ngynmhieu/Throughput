using System.Linq;
using NUnit.Framework;

namespace Throughput.Sim.Tests
{
    // Confirms the project wiring: the test assembly runs, and the Sim assembly stays free of Unity.
    public class SetupCheckTests
    {
        [Test]
        public void SimAssembly_DoesNotReferenceUnity()
        {
            var sim = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Throughput.Sim");

            if (sim == null)
                Assert.Inconclusive("Throughput.Sim has no code yet, so Unity hasn't built it.");

            var unityRefs = sim.GetReferencedAssemblies().Where(r => r.Name.StartsWith("UnityEngine"));
            Assert.IsEmpty(unityRefs, "Throughput.Sim must not reference Unity (A4).");
        }
    }
}
