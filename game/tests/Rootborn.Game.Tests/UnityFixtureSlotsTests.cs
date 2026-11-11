using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 3: Unity 판 QA 세이브 슬롯을 하나도 빼지 않고 임시 폴더로 복사해 시작하고, 하루를 끝내고 다음 날로 간 뒤
    /// 원래 있던 파일이 남는지, Core가 모르는 파일(metadata.json 등)이 바이트 그대로인지 본다. 자동 테스트다.
    /// </summary>
    public sealed class UnityFixtureSlotsTests
    {
        // Unity 판이 쓰고 GameSession은 읽지도 쓰지도 않는 파일.
        private static readonly string[] ForeignFiles = { SaveFileNames.Metadata, SaveFileNames.WorldProgress, "student-day-result-quest-inventory-summary.json" };

        public static IEnumerable<TestCaseData> Slots()
        {
            foreach (var group in Directory.GetDirectories(TestWorld.FixtureSaves))
                foreach (var slot in Directory.GetDirectories(group))
                    yield return new TestCaseData(Path.GetFileName(group) + "/" + Path.GetFileName(slot), Path.GetFileName(slot))
                        .SetName("UnityFixtureSlot_" + Path.GetFileName(group) + "_" + Path.GetFileName(slot));
        }

        [Test]
        public void ThereAreFixtureSlotsToTry()
        {
            Assert.That(Slots().Count(), Is.GreaterThanOrEqualTo(2));
        }

        [TestCaseSource(nameof(Slots))]
        public void StartsEndsTheDayAndGoesOn_WithoutLosingOrChangingForeignFiles(string fixture, string slot)
        {
            using var saves = new TempSaves();
            saves.CopyFixture(fixture, slot);
            var original = SlotCheckpoints.Read(saves, slot);
            Assert.That(original.Keys, Does.Contain(SaveFileNames.Metadata), "고정 세이브에 metadata.json이 있다");

            var game = TestWorld.Start(saves.Store, slot);
            Assert.That(SlotCheckpoints.Same(original, SlotCheckpoints.Read(saves, slot)), Is.True, "읽기만 해서는 아무것도 쓰지 않는다");

            int day = game.Student.CurrentDay;
            if (game.Student.DayState == StudentDayState.InProgress) game.VisitLocation("location.town-square");
            if (game.Student.DayState == StudentDayState.InProgress) Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day + 1));

            var after = SlotCheckpoints.Read(saves, slot);
            Assert.That(after.Keys, Is.SupersetOf(original.Keys), "원래 있던 파일이 지워지지 않았다");
            foreach (var name in ForeignFiles.Where(original.ContainsKey))
                Assert.That(after[name].AsSpan().SequenceEqual(original[name]), Is.True, name + "은 그대로다");
            Assert.That(after.Keys.Where(k => k.EndsWith(".tmp", StringComparison.Ordinal)), Is.Empty, "임시 파일이 남지 않았다");

            var again = TestWorld.Start(saves.Store, slot);
            Assert.That(again.Student.CurrentDay, Is.EqualTo(day + 1));
        }

        [TestCaseSource(nameof(Slots))]
        public void EveryFileIsStillJsonAfterTheCurrentVersionRewrites(string fixture, string slot)
        {
            using var saves = new TempSaves();
            saves.CopyFixture(fixture, slot);
            var game = TestWorld.Start(saves.Store, slot);
            game.VisitLocation("location.town-square");

            foreach (var pair in SlotCheckpoints.Read(saves, slot))
                Assert.DoesNotThrow(() => System.Text.Json.JsonDocument.Parse(pair.Value).Dispose(), pair.Key);
        }
    }
}
