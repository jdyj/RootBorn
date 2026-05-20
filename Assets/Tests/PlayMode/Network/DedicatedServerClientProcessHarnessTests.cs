using System.IO;
using NUnit.Framework;
using Rootborn.Tests.PlayMode.Network.Support;

namespace Rootborn.Tests.PlayMode.Network
{
    public sealed class DedicatedServerClientProcessHarnessTests
    {
        [Test]
        public void DEDICATED_HARNESS_PM_001_ReportsMissingExecutablePrerequisites()
        {
            var config = new DedicatedServerClientProcessHarnessConfig(
                "Builds/Server/Windows/missing-rootborn-server.exe",
                "Builds/Client/Windows/missing-rootborn.exe",
                "Builds/Logs/dedicated-harness-test",
                7788,
                "dedicated-harness-slot",
                "127.0.0.1",
                4);

            var diagnostics = DedicatedServerClientProcessHarness.ValidatePrerequisites(config);

            CollectionAssert.Contains(diagnostics, "Missing server executable: Builds/Server/Windows/missing-rootborn-server.exe");
            CollectionAssert.Contains(diagnostics, "Missing client executable: Builds/Client/Windows/missing-rootborn.exe");
        }

        [Test]
        public void DEDICATED_HARNESS_PM_002_BuildsServerAndClientStartInfoWithExplicitArgs()
        {
            var config = new DedicatedServerClientProcessHarnessConfig(
                "Builds/Server/Windows/rootborn-server.exe",
                "Builds/Client/Windows/rootborn.exe",
                "Builds/Logs/dedicated-harness-test",
                7789,
                "dedicated-harness-slot",
                "127.0.0.1",
                4);

            var server = DedicatedServerClientProcessHarness.BuildServerStartInfo(config);
            var client = DedicatedServerClientProcessHarness.BuildClientStartInfo(config);

            Assert.AreEqual("Builds/Server/Windows/rootborn-server.exe", server.FileName);
            StringAssert.Contains("-mode server", server.Arguments);
            StringAssert.Contains("-batchmode", server.Arguments);
            StringAssert.Contains("-nographics", server.Arguments);
            StringAssert.Contains("-port 7789", server.Arguments);
            StringAssert.Contains("-maxPlayers 4", server.Arguments);
            StringAssert.Contains("-saveSlot dedicated-harness-slot", server.Arguments);
            Assert.IsTrue(server.RedirectStandardOutput);
            Assert.IsTrue(server.RedirectStandardError);
            StringAssert.EndsWith(Path.Combine("Builds", "Server", "Windows"), NormalizeForDirectory(server.WorkingDirectory));

            Assert.AreEqual("Builds/Client/Windows/rootborn.exe", client.FileName);
            StringAssert.Contains("-mode client", client.Arguments);
            StringAssert.Contains("-joinIp 127.0.0.1", client.Arguments);
            StringAssert.Contains("-port 7789", client.Arguments);
            StringAssert.Contains("-saveSlot dedicated-harness-slot", client.Arguments);
            Assert.IsTrue(client.RedirectStandardOutput);
            Assert.IsTrue(client.RedirectStandardError);
            StringAssert.EndsWith(Path.Combine("Builds", "Client", "Windows"), NormalizeForDirectory(client.WorkingDirectory));
        }

        [Test]
        public void DEDICATED_HARNESS_PM_003_ReadinessProbeRecognizesSeparateProcessSignals()
        {
            string serverLog = "[ROOTBORN] Dedicated server started - port=7789 maxPlayers=4 saveSlot=dedicated-harness-slot\n[ROOTBORN] Dedicated server scene event type=LoadEventCompleted scene=Town client=0\n[ROOTBORN] Dedicated server client connected id=1";
            string clientLog = "[ROOTBORN] Client started - joinIp=127.0.0.1 port=7789 saveSlot=dedicated-harness-slot\n[ROOTBORN] Client connected callback id=1 localClientId=1\n[ROOTBORN] Client scene event type=LoadEventCompleted scene=Town client=1";

            var readiness = DedicatedServerClientReadinessProbe.FromLogs(serverLog, clientLog);

            Assert.IsTrue(readiness.ServerStarted);
            Assert.IsTrue(readiness.ClientStarted);
            Assert.IsTrue(readiness.ClientConnected);
            Assert.IsTrue(readiness.TownSynchronized);
            Assert.IsTrue(readiness.IsReady);
        }

        [Test]
        public void DEDICATED_HARNESS_PM_004_CleanupStopsBothProcessHandles()
        {
            var server = new FakeProcessHandle("server");
            var client = new FakeProcessHandle("client");

            using (var session = new DedicatedServerClientProcessSession(server, client))
            {
                Assert.IsFalse(session.IsDisposed);
            }

            Assert.IsTrue(server.KillCalled);
            Assert.IsTrue(server.DisposeCalled);
            Assert.IsTrue(client.KillCalled);
            Assert.IsTrue(client.DisposeCalled);
        }

        [Test]
        public void DEDICATED_HARNESS_PM_005_ProvidesDeterministicServerAndClientLogPaths()
        {
            var config = new DedicatedServerClientProcessHarnessConfig(
                "Builds/Server/Windows/rootborn-server.exe",
                "Builds/Client/Windows/rootborn.exe",
                "Builds/Logs/dedicated-harness-test",
                7790,
                "dedicated-harness-slot",
                "127.0.0.1",
                4);

            Assert.AreEqual("Builds/Logs/dedicated-harness-test/dedicated-server.log", NormalizeForSlash(DedicatedServerClientProcessHarness.ServerLogPath(config)));
            Assert.AreEqual("Builds/Logs/dedicated-harness-test/dedicated-client.log", NormalizeForSlash(DedicatedServerClientProcessHarness.ClientLogPath(config)));
        }

        private static string NormalizeForDirectory(string path)
        {
            return (path ?? string.Empty).Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
        }

        private static string NormalizeForSlash(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private sealed class FakeProcessHandle : IDedicatedProcessHandle
        {
            public FakeProcessHandle(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public bool HasExited { get; private set; }
            public bool KillCalled { get; private set; }
            public bool DisposeCalled { get; private set; }

            public void Kill()
            {
                KillCalled = true;
                HasExited = true;
            }

            public void Dispose()
            {
                DisposeCalled = true;
            }
        }
    }
}
