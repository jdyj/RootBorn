using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 1: 진행 지점마다 슬롯의 모든 파일을 뽑고, 새 GameSession으로 읽어 명령을 하나 더 한 뒤 다시 저장했을 때
    /// 파일이 줄지 않고 모두 다시 읽히는지 본다. 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class SaveRoundTripTests
    {
        [TestCaseSource(typeof(SlotCheckpoints), nameof(SlotCheckpoints.Names))]
        public void EveryCheckpointLeavesReadableVersionedFiles(string checkpoint)
        {
            var files = SlotCheckpoints.Files(checkpoint);
            Assert.That(files, Is.Not.Empty);
            foreach (var pair in files)
            {
                using var doc = JsonDocument.Parse(pair.Value);
                Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object), pair.Key + "의 맨 위는 객체");
                var version = SlotCheckpoints.TopVersion(pair.Value);
                Assert.That(version, Is.Not.Null, pair.Key + "에 Version이 있다");
                Assert.That(version, Is.GreaterThanOrEqualTo(1), pair.Key + "는 이 프로그램이 쓴 버전");
            }
        }

        [TestCaseSource(typeof(SlotCheckpoints), nameof(SlotCheckpoints.Names))]
        public void ReadingThenOneMoreCommand_KeepsEveryFile_AndEveryFileReadsAgain(string checkpoint)
        {
            using var saves = SlotCheckpoints.Restore(checkpoint);
            var before = SlotCheckpoints.Read(saves);

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True, "읽기만 해서는 아무것도 쓰지 않는다");
            game.VisitLocation("location.town-square");

            var after = SlotCheckpoints.Read(saves);
            Assert.That(after.Keys, Is.SupersetOf(before.Keys), "저장한 뒤 파일이 줄지 않았다");
            Assert.That(after.Keys.Except(before.Keys), Is.Empty.Or.All.StartsWith("house-furniture-layout"), "새로 생기는 파일은 알려진 것뿐");
            foreach (var pair in after)
            {
                Assert.That(SlotCheckpoints.TopVersion(pair.Value), Is.GreaterThanOrEqualTo(1), pair.Key);
                using var doc = JsonDocument.Parse(pair.Value);
                Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object), pair.Key);
            }

            var again = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            Assert.That(again.Student.CurrentDay, Is.EqualTo(game.Student.CurrentDay));
            Assert.That(again.Student.TimeMinutes, Is.EqualTo(game.Student.TimeMinutes));
            Assert.That(again.Adult.IsAdult, Is.EqualTo(game.Adult.IsAdult));
            Assert.That(SlotCheckpoints.Same(after, SlotCheckpoints.Read(saves)), Is.True, "다시 읽기만 해도 쓰지 않는다");
        }

        [Test]
        public void SecondSave_WritesTheSameFilesAsTheFirst()
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            game.VisitLocation("location.town-square");
            var first = SlotCheckpoints.Read(saves);
            game.VisitLocation("location.town-square");
            Assert.That(SlotCheckpoints.Read(saves).Keys, Is.EqualTo(first.Keys));
        }
    }
}
