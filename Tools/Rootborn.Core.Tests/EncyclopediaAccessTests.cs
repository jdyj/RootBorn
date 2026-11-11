using System;
using NUnit.Framework;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    /// <summary>탐색이 둔 IExplorationEncyclopedia를 도감 진행이 잇는지 본다. 원래 테스트 없음.</summary>
    public sealed class EncyclopediaAccessTests
    {
        [Test]
        public void ENCYCLOPEDIA_ACCESS_UnlocksOnceWithClockTime()
        {
            var progress = new EncyclopediaProgress("slot-a", "player-a");
            long now = 42L;
            IExplorationEncyclopedia access = new EncyclopediaAccess(progress, () => now);

            Assert.IsTrue(access.TryUnlock("entry.a", 2));
            now = 99L;
            Assert.IsFalse(access.TryUnlock("entry.a", 2), "Same entry twice is not new.");
            Assert.IsFalse(access.TryUnlock(null, 1));
            Assert.AreEqual(2, progress.GetRevealStage("entry.a"));
            Assert.AreEqual(42L, progress.ToSaveData().Records[0].FirstDiscoveredUtcTicks);
            Assert.AreEqual(1, progress.UnlockedCount);
            Assert.Throws<ArgumentNullException>(() => new EncyclopediaAccess(progress, null));
        }
    }
}
