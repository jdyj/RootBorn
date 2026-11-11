// 원래 파일: Assets/Tests/PlayMode/EndToEnd/TownExplorationInteractionChoiceE2EScenarioTests.cs (EXPLORATION_CHOICE_E2E_001_011),
//            Assets/Tests/PlayMode/EndToEnd/ExplorationInteractionChoiceE2EScenarioTests.cs (EXPLORATION_CHOICE_E2E_005_011),
//            Assets/Tests/PlayMode/EndToEnd/EncyclopediaDiscoveryUiE2EScenarioTests.cs (ENCYCLOPEDIA_E2E_001_008 중 규칙·저장 부분)
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 마을 탐색 지점(exploration.test.alley-crate)의 선택: 조건이 안 되는 선택은 잠겨 있고, 고른 선택은 결과를 한 번 주고 저장되며 다시 고르면 거절된다.
    /// 발견 선택은 도감 항목을 새 표시와 함께 한 번 연다. 도감 화면(분류·잠긴 줄·본 표시 지우기)은 여기서 보지 않는다. 자동 테스트다.
    /// </summary>
    public sealed class ExplorationAndEncyclopediaTests
    {
        private const string Crate = "exploration.test.alley-crate";
        private const string Entry = "ency.location.first-discovery";

        [Test]
        public void Exploration001_LockedChoiceIsRefused_TheChosenOneIsSavedWithItsIds_AndRepeatingIsRefused()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var wood = game.Item("Wood");
            int energy = game.Student.Energy;

            Assert.That(game.ChooseExploration(Crate, "choice.test.careful").Kind, Is.EqualTo(ExplorationChoiceResultKind.ConditionFailed), "집중이 없으면 잠긴 선택");
            Assert.That(game.Student.Energy, Is.EqualTo(energy), "잠긴 선택은 비용을 쓰지 않는다");
            Assert.That(game.Exploration.GetRecord(Crate)?.Completed ?? false, Is.False);

            Assert.That(game.ChooseExploration(Crate, "choice.test.inspect").Kind, Is.EqualTo(ExplorationChoiceResultKind.Applied));
            Assert.That(saves.Store.TryRead<ExplorationProgressSaveData>(TestWorld.Slot, SaveFileNames.Exploration(TestWorld.Player), out var saved), Is.True);
            var record = saved.Records.Single(r => r.InteractionId == Crate);
            Assert.That(record.Completed, Is.True);
            Assert.That(record.SelectedChoiceIds, Does.Contain("choice.test.inspect"));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Exploration.GetRecord(Crate).Completed, Is.True);
            Assert.That(again.ChooseExploration(Crate, "choice.test.inspect").Kind, Is.EqualTo(ExplorationChoiceResultKind.DuplicateRequest));
            Assert.That(again.Inventory.CountOf(wood), Is.EqualTo(1), "다시 골라도 물건을 또 주지 않는다");
        }

        [Test]
        public void Exploration_SelfStudyOutsideSchool_OpensTheCarefulChoice()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.ChooseExploration(Crate, "choice.test.careful").Kind, Is.EqualTo(ExplorationChoiceResultKind.ConditionFailed));

            Assert.That(game.PerformOutsideSchoolActivity("outside.self-study.library", "outside:library:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            game = TestWorld.Start(saves.Store);
            Assert.That(game.ChooseExploration(Crate, "choice.test.careful").Kind, Is.EqualTo(ExplorationChoiceResultKind.Applied),
                "도서관 자습으로 자란 집중이 조심스러운 선택을 연다");
            Assert.That(TestWorld.Start(saves.Store).Exploration.GetRecord(Crate).SelectedChoiceIds, Does.Contain("choice.test.careful"));
        }

        [Test]
        public void Encyclopedia_DiscoveryChoiceUnlocksTheEntryOnce_WithANewMark_AndKeepsItAfterReading()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.Encyclopedia.UnlockedCount, Is.EqualTo(0), "새 슬롯은 도감이 비어 있다");

            Assert.That(game.ChooseExploration(Crate, "choice.test.discovery").Kind, Is.EqualTo(ExplorationChoiceResultKind.Applied));
            Assert.That(game.Encyclopedia.IsUnlocked(Entry), Is.True);
            Assert.That(game.Encyclopedia.UnlockedCount, Is.EqualTo(1));
            Assert.That(game.Encyclopedia.NewCount, Is.EqualTo(1), "처음 연 항목은 새 표시가 하나");
            Assert.That(game.DiscoveryClues.GetRecord("clue.discovery.library-archive-rumor").Seen, Is.True, "단서도 함께 드러난다");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Encyclopedia.IsUnlocked(Entry), Is.True);
            Assert.That(again.Encyclopedia.NewCount, Is.EqualTo(1), "본 표시를 지우는 명령이 없어 새 표시는 남는다");
            Assert.That(again.ChooseExploration(Crate, "choice.test.discovery").Kind, Is.EqualTo(ExplorationChoiceResultKind.DuplicateRequest));
            Assert.That(again.Encyclopedia.UnlockedCount, Is.EqualTo(1), "다시 골라도 항목이 늘지 않는다");
            Assert.That(again.Encyclopedia.NewCount, Is.EqualTo(1));
        }
    }
}
