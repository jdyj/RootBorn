using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Data.Save;
using Rootborn.Host.Net;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Screens.Start;

namespace Rootborn.Host.Tests
{
    /// <summary>방을 열려는 슬롯을 다른 곳이 잡고 있으면 "듣지 못했다"가 아니라 슬롯이 열려 있다고 알린다(창 없이 RoomHosting만).</summary>
    public sealed class RoomHostingSlotLockTests
    {
        private string _root;

        [SetUp]
        public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "rootborn-hosting-lock-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private sealed class FixedClock : IRoomClock { public double Now => 0; }

        [Test]
        public void OpeningARoomOnASlotSomeoneElseHolds_FailsWithSlotInUse_AndHasTextInEveryLanguage()
        {
            using var other = RoomSlotLock.Acquire(_root, "slot-0", "데디케이티드 서버");
            var versions = new RoomVersions(RoomProtocol.Version, "g", "d");
            var connection = new RoomConnection(() => versions, new SessionClientSettings(), slot => slot, entry => true);
            using var hosting = new RoomHosting(connection, () => new FileSaveStore(_root), new FixedClock(), null, null);

            hosting.Begin("127.0.0.1:0", 2, "slot-0", Array.Empty<string>());

            Assert.That(hosting.Phase, Is.EqualTo(RoomJoinPhase.Failed));
            Assert.That(hosting.FailureCode, Is.EqualTo(RoomHostText.SlotInUse));
            Assert.That(hosting.Room, Is.Null);
            string key = RoomJoinText.ReasonKey(hosting.FailureCode);
            Assert.That(key, Is.EqualTo("ui.join.reason.slot-in-use"));
            foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "localization"), "*.json"))
                StringAssert.Contains("\"" + key + "\"", File.ReadAllText(file), Path.GetFileName(file));
        }
    }
}
