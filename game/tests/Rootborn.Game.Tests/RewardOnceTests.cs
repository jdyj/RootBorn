using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>보상과 진행은 한 번만 적용되고, 인벤토리가 가득 차면 아무것도 바뀌지 않는다. 자동 테스트다.</summary>
    public sealed class RewardOnceTests
    {
        private const string Work = "work.shift.corner-store";
        private const string Request = "work:corner-store:1";

        [Test]
        public void SettlingTheSameWorkTwice_PaysOnce_EvenAfterSaveAndLoad()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.BeginPartTimeWork(Work, Request, 1).Applied, Is.True);
            Assert.That(game.SettleMinigame(Request, true).Applied, Is.True);
            int allowance = game.Inventory.CountOf(game.Item("item.allowance"));
            Assert.That(allowance, Is.EqualTo(5));

            Assert.That(game.SettleMinigame(Request, true).Applied, Is.False);
            Assert.That(game.BeginPartTimeWork(Work, Request, 1).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.SettleMinigame(Request, true).Applied, Is.False);
            Assert.That(again.BeginPartTimeWork(Work, Request, 1).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(again.Inventory.CountOf(again.Item("item.allowance")), Is.EqualTo(allowance));
        }

        [Test]
        public void FullInventory_RefusesThePaidWorkAndChangesNothing()
        {
            using var saves = new TempSaves();
            TestWorld.Start(saves.Store).TalkTo("npc.first-guide");
            var stone = TestWorld.Definitions.Get<Rootborn.Core.Inventory.ItemDefinition>("Stone");
            SaveEdits.EditSnapshot(saves.Store, snapshot => SaveEdits.Fill(snapshot, "Stone", stone.MaxStack * Rootborn.Core.Inventory.ItemInventory.MaxSlots));
            var game = TestWorld.Start(saves.Store);
            var before = game.Student.ToSaveData();

            Assert.That(game.BeginPartTimeWork(Work, Request, 1).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));

            var after = game.Student.ToSaveData();
            Assert.That(after.Energy, Is.EqualTo(before.Energy));
            Assert.That(after.Focus, Is.EqualTo(before.Focus));
            Assert.That(after.TimeMinutes, Is.EqualTo(before.TimeMinutes));
            Assert.That(after.TimePointsSpentToday, Is.EqualTo(before.TimePointsSpentToday));
            Assert.That(game.Student.HasPendingMinigame, Is.False);
            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.EqualTo(0));
        }

        [Test]
        public void TheSameLocationActivityRequestTwice_GrowsOnce()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.PerformLocationActivity("opening.clue.archive-door", "archive:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            var once = game.Student.ToSaveData();

            Assert.That(game.PerformLocationActivity("opening.clue.archive-door", "archive:1").Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            var again = TestWorld.Start(saves.Store);
            Assert.That(again.PerformLocationActivity("opening.clue.archive-door", "archive:1").Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(again.Student.Focus, Is.EqualTo(once.Focus));
            Assert.That(again.Student.TimePointsSpentToday, Is.EqualTo(once.TimePointsSpentToday));
        }

        [Test]
        public void EndingTheSameDayTwice_AppliesTheDayResultOnce()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.EndDay().Ended, Is.False);
            Assert.That(TestWorld.Start(saves.Store).EndDay().Ended, Is.False, "읽은 뒤에도 같은 날은 다시 끝나지 않는다");
        }
    }
}
