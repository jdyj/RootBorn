using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 2: 슬롯의 파일 하나를 망가뜨리고 GameSession을 시작한다. 망가진 파일은 "세이브 없음"이 아니므로
    /// 시작이 예외로 거절되고(어느 파일인지 메시지에 나온다) 슬롯의 어떤 파일도 바뀌지 않아야 한다. 자동 테스트다.
    /// </summary>
    public sealed class CorruptSaveStartTests
    {
        public enum Damage { Empty, Truncated, ArrayAtTop, NumberAtTop, HugeVersion }

        // 집·일터에 들어올 때 읽는 배치는 이 시험의 대상이 아니다(ADamagedHouseLayout_..., ADamagedWorkspaceLayout_...). 월드 상태 사본도 시작할 때 확인한다.
        private static bool ReadAtStart(string file)
            => !file.StartsWith("house-furniture-layout", StringComparison.Ordinal) && !file.StartsWith("workspace-layout", StringComparison.Ordinal);

        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (var checkpoint in SlotCheckpoints.Names)
                foreach (var file in SlotCheckpoints.FileNames(checkpoint).Where(ReadAtStart))
                    foreach (Damage damage in Enum.GetValues(typeof(Damage)))
                    {
                        if (damage == Damage.HugeVersion && SlotCheckpoints.TopVersion(SlotCheckpoints.Files(checkpoint)[file]) == null) continue;
                        yield return new TestCaseData(checkpoint, file, damage).SetName($"Start_Refuses_{checkpoint}_{Short(file)}_{damage}");
                    }
        }

        private static string Short(string file) => file.StartsWith("adult-career-snapshot", StringComparison.Ordinal) ? "adult-career-snapshot" : file.Replace(".json", "");

        internal static byte[] Apply(byte[] original, Damage damage)
        {
            switch (damage)
            {
                case Damage.Empty: return new byte[0];
                case Damage.Truncated: return original.Take(original.Length / 2).ToArray();
                case Damage.ArrayAtTop: return Encoding.UTF8.GetBytes("[1, 2, 3]");
                case Damage.NumberAtTop: return Encoding.UTF8.GetBytes("42");
                default:
                    int version = SlotCheckpoints.TopVersion(original).Value;
                    var text = Encoding.UTF8.GetString(original);
                    var changed = text.Replace("\"Version\": " + version, "\"Version\": 999999");
                    Assert.That(changed, Is.Not.EqualTo(text), "Version 자리를 찾지 못했다");
                    return Encoding.UTF8.GetBytes(changed);
            }
        }

        [TestCaseSource(nameof(Cases))]
        public void Start_RefusesADamagedFile_NamesIt_AndChangesNothing(string checkpoint, string file, Damage damage)
        {
            using var saves = SlotCheckpoints.Restore(checkpoint);
            File.WriteAllBytes(Path.Combine(saves.Path, SlotCheckpoints.Slot, file), Apply(SlotCheckpoints.Files(checkpoint)[file], damage));
            var before = SlotCheckpoints.Read(saves);

            var error = Assert.Catch<Exception>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));

            Assert.That(error.ToString(), Does.Contain(file), "어느 파일인지 알린다");
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True, "거절한 시작이 슬롯의 파일을 바꾸지 않는다");
        }

        [TestCase("day-3")]
        [TestCase("adult-chosen")]
        public void ADamagedFile_IsNotOverwrittenByANewGame_EvenAfterTryingAgain(string checkpoint)
        {
            using var saves = SlotCheckpoints.Restore(checkpoint);
            var snapshotFile = SlotCheckpoints.FileNames(checkpoint).Single(f => f.StartsWith("adult-career-snapshot", StringComparison.Ordinal));
            File.WriteAllBytes(Path.Combine(saves.Path, SlotCheckpoints.Slot, snapshotFile), Encoding.UTF8.GetBytes("{ \"Student\": "));
            var before = SlotCheckpoints.Read(saves);

            for (int attempt = 0; attempt < 3; attempt++)
                Assert.Catch<Exception>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot).VisitLocation("location.town-square"));

            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True);
        }

        [TestCase("")]
        [TestCase("[1]")]
        [TestCase("{\"CurrentStageIndex\": ")]
        [TestCase("{\"Version\": 999999}")]
        public void AHouseStateFileThatCannotBeRead_RefusesToStart_AndStaysAsItWas(string content)
        {
            using var saves = SlotCheckpoints.Restore("furniture-placed");
            File.WriteAllText(Path.Combine(saves.Path, SlotCheckpoints.Slot, "house-state.json"), content);
            var before = SlotCheckpoints.Read(saves);

            var error = Assert.Catch<Exception>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));

            Assert.That(error.ToString(), Does.Contain("house-state.json"));
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True);
        }

        [TestCase(Damage.Empty)]
        [TestCase(Damage.Truncated)]
        [TestCase(Damage.ArrayAtTop)]
        [TestCase(Damage.NumberAtTop)]
        [TestCase(Damage.HugeVersion)]
        public void ADamagedHouseLayout_RefusesToEnterTheHouse_AndStaysAsItWas(Damage damage)
        {
            using var saves = SlotCheckpoints.Restore("furniture-placed");
            var layout = SlotCheckpoints.FileNames("furniture-placed").Single(f => f.StartsWith("house-furniture-layout", StringComparison.Ordinal));
            File.WriteAllBytes(Path.Combine(saves.Path, SlotCheckpoints.Slot, layout), Apply(SlotCheckpoints.Files("furniture-placed")[layout], damage));
            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            var before = SlotCheckpoints.Read(saves);

            var error = Assert.Catch<Exception>(() => game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId));

            Assert.That(error.ToString(), Does.Contain(layout));
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True);
        }

        [TestCase(Damage.Empty)]
        [TestCase(Damage.Truncated)]
        [TestCase(Damage.ArrayAtTop)]
        [TestCase(Damage.NumberAtTop)]
        [TestCase(Damage.HugeVersion)]
        public void ADamagedWorkspaceLayout_RefusesToEnterTheWorkspace_AndStaysAsItWas(Damage damage)
        {
            const string checkpoint = "workspace-entered";
            using var saves = SlotCheckpoints.Restore(checkpoint);
            var layout = SlotCheckpoints.FileNames(checkpoint).Single(f => f.StartsWith("workspace-layout", StringComparison.Ordinal));
            File.WriteAllBytes(Path.Combine(saves.Path, SlotCheckpoints.Slot, layout), Apply(SlotCheckpoints.Files(checkpoint)[layout], damage));
            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            var before = SlotCheckpoints.Read(saves);

            var error = Assert.Catch<Exception>(() => SlotCheckpoints.EnterChefWorkspace(game));

            Assert.That(error.ToString(), Does.Contain(layout));
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True, "망가진 일터 배치를 기본 템플릿으로 덮지 않는다");
        }

        [Test]
        public void ADamagedFileInOneSlot_DoesNotStopAnotherSlot()
        {
            using var saves = SlotCheckpoints.Restore("day-3", "slot-broken");
            SlotCheckpoints.WriteAll(saves, SlotCheckpoints.Files("day-3"), "slot-good");
            var snapshotFile = SlotCheckpoints.FileNames("day-3").Single(f => f.StartsWith("adult-career-snapshot", StringComparison.Ordinal));
            File.WriteAllBytes(Path.Combine(saves.Path, "slot-broken", snapshotFile), new byte[0]);

            Assert.Catch<Exception>(() => TestWorld.Start(saves.Store, "slot-broken"));
            Assert.That(TestWorld.Start(saves.Store, "slot-good").Student.CurrentDay, Is.EqualTo(3));
        }
    }
}
