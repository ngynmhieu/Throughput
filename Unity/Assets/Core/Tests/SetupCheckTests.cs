using System.Linq;
using NUnit.Framework;

namespace Throughput.Core.Tests
{
    // Confirms the project wiring: the test assembly runs, and the Core assembly stays free of Unity.
    public class SetupCheckTests
    {
        [Test]
        public void CoreAssembly_DoesNotReferenceUnity()
        {
            var core = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Throughput.Core");

            if (core == null)
                Assert.Inconclusive("Throughput.Core has no code yet, so Unity hasn't built it.");

            var unityRefs = core.GetReferencedAssemblies().Where(r => r.Name.StartsWith("UnityEngine"));
            Assert.IsEmpty(unityRefs, "Throughput.Core must not reference Unity (A4).");
        }
    }
}
