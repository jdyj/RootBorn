using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 거절하거나 같은 요청을 다시 한 행동은 퀘스트·체인을 진행시키지 않고 세이브에 아무것도 쓰지 않는다. 자동 테스트다.
    /// 옮겨 옴: Assets/Tests/EditMode/StudentLife/QuestGameplayEventAdapterTests.cs
    /// (RejectedLocationInteraction_DoesNotPublishQuestProgress, WorkRejectedForResources_DoesNotPublishQuestProgressOrPay,
    /// WorkSuccess_..._AndDuplicateRequestPublishesNothing). GameSession 명령으로 진행한다(화면 근거 아님).
    /// </summary>
    public sealed class RefusedActionQuestTests
    {
        private const string Activity = "opening.clue.archive-door";
        private const string Work = "work.shift.corner-store";
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        private static string Quests(GameSession game)
            => JsonSerializer.Serialize(game.QuestLog.ToSaveData(), Json) + JsonSerializer.Serialize(game.QuestChains.ToSaveData(), Json);

        [Test]
        public void TheSameLocationActivityRequestAgain_AdvancesNoQuest_AndWritesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.PerformLocationActivity(Activity, "archive:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            string quests = Quests(game);
            var files = SlotCheckpoints.Read(saves, TestWorld.Slot);

            Assert.That(game.PerformLocationActivity(Activity, "archive:1").Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));

            Assert.That(Quests(game), Is.EqualTo(quests));
            Assert.That(SlotCheckpoints.Same(files, SlotCheckpoints.Read(saves, TestWorld.Slot)), Is.True);
        }

        [Test]
        public void ALocationActivityRefusedOnASettledDay_AdvancesNoQuest_AndWritesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.EndDay().Ended, Is.True);
            string quests = Quests(game);
            var files = SlotCheckpoints.Read(saves, TestWorld.Slot);

            Assert.That(game.PerformLocationActivity(Activity, "archive:after-end").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied));

            Assert.That(Quests(game), Is.EqualTo(quests));
            Assert.That(SlotCheckpoints.Same(files, SlotCheckpoints.Read(saves, TestWorld.Slot)), Is.True);
            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Student.HasAppliedRequest("archive:after-end"), Is.False, "거절한 요청은 받은 것으로 남지 않는다");
        }

        [Test]
        public void AWorkRefusedForTime_PaysNothing_AdvancesNoQuest_AndWritesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.EndDay().Ended, Is.True);
            string quests = Quests(game);
            int allowance = game.Inventory.CountOf(game.Item("item.allowance"));
            var files = SlotCheckpoints.Read(saves, TestWorld.Slot);

            Assert.That(game.BeginPartTimeWork(Work, "work:after-end", 1).Applied, Is.False);
            Assert.That(game.SettleMinigame("work:after-end", true).Applied, Is.False);

            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.EqualTo(allowance));
            Assert.That(Quests(game), Is.EqualTo(quests));
            Assert.That(SlotCheckpoints.Same(files, SlotCheckpoints.Read(saves, TestWorld.Slot)), Is.True);
        }

        [Test]
        public void ASettledWorkSettledAgain_AdvancesNoQuest()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.BeginPartTimeWork(Work, "work:1", 1).Applied, Is.True);
            Assert.That(game.SettleMinigame("work:1", true).Applied, Is.True);
            string quests = Quests(game);
            var files = SlotCheckpoints.Read(saves, TestWorld.Slot);

            Assert.That(game.SettleMinigame("work:1", true).Applied, Is.False);
            Assert.That(game.BeginPartTimeWork(Work, "work:1", 1).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));

            Assert.That(Quests(game), Is.EqualTo(quests));
            Assert.That(SlotCheckpoints.Same(files, SlotCheckpoints.Read(saves, TestWorld.Slot)), Is.True);
        }
    }
}
