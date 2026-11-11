using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;

namespace Rootborn.Game.Tests
{
    /// <summary>Unity AdultCareerE2EScenarioTests 001~003의 흐름(시설 고르기 → 일 → 올리기)을 명령으로 따라간다. 자동 테스트다.</summary>
    public sealed class AdultCareerTests
    {
        private static GameSession ReadyChefCandidate(TempSaves saves)
        {
            TestWorld.Start(saves.Store).TalkTo("npc.first-guide"); // 첫 저장
            SaveEdits.EditSnapshot(saves.Store, SaveEdits.MakeChefCandidateReady);
            return TestWorld.Start(saves.Store);
        }

        [Test]
        public void Adult001_ChoosingTheChefFacilityMakesTheStudentAnAdultAtLevelOne()
        {
            using var saves = new TempSaves();
            var game = ReadyChefCandidate(saves);
            var traitsBefore = game.Student.GetTraitIds().Select(id => id + "=" + game.Student.GetTraitValue(id)).ToArray();

            Assert.That(game.ChooseFacility("facility.chef", "facility:chef").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.Adult.IsAdult, Is.True);
            Assert.That(game.Adult.FacilityId, Is.EqualTo("facility.chef"));
            Assert.That(game.Adult.FacilityLevel, Is.EqualTo(1));
            Assert.That(game.Student.GetTraitIds().Select(id => id + "=" + game.Student.GetTraitValue(id)), Is.EqualTo(traitsBefore));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Adult.IsAdult, Is.True, "저장하고 읽어도 성인이다");
            Assert.That(again.ChooseFacility("facility.chef", "facility:chef").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
        }

        [Test]
        public void Adult_NotReadyCandidateCannotChooseAFacility()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.ChooseFacility("facility.chef", "facility:chef").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(game.Adult.IsAdult, Is.False);
        }

        [Test]
        public void Adult002_ReservedWorkSettledWithAScoreCountsOneSuccess()
        {
            using var saves = new TempSaves();
            var game = ReadyChefCandidate(saves);
            game.ChooseFacility("facility.chef", "facility:chef");
            var work = game.Definitions.Get<FacilityWork>("work.chef.level1");
            int minutes = game.Student.TimeMinutes;

            Assert.That(game.ReserveFacilityWork(work.Id, "work:chef:1", 11).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            game = TestWorld.Start(saves.Store); // 예약을 저장하고 읽어 정산한다.
            Assert.That(game.SettleFacilityWork("work:chef:1", 100).Kind, Is.EqualTo(AdultCareerResultKind.Applied));

            Assert.That(game.Adult.GetSuccessfulCompletions(work), Is.EqualTo(1));
            Assert.That(game.Adult.GetMastery(work), Is.GreaterThan(0));
            Assert.That(game.Student.TimeMinutes, Is.GreaterThan(minutes), "일한 만큼 시간이 간다");
            Assert.That(game.SettleFacilityWork("work:chef:1", 100).Kind, Is.Not.EqualTo(AdultCareerResultKind.Applied), "같은 일은 한 번만 정산한다");
        }

        [Test]
        public void Adult003_UpgradingToLevelThreePaysTheCostAndEndsTheRoute()
        {
            using var saves = new TempSaves();
            var game = ReadyChefCandidate(saves);
            game.ChooseFacility("facility.chef", "facility:chef");
            SaveEdits.EditSnapshot(saves.Store, snapshot =>
            {
                snapshot.Adult.FacilityLevel = 2;
                snapshot.Adult.Works = new[] { new FacilityWorkProgressSaveData { WorkId = "work.chef.level2", Mastery = 100, SuccessfulCompletions = 5 } };
                SaveEdits.Fill(snapshot, "item.allowance", 300);
                SaveEdits.Fill(snapshot, "item.facility-materials", 10);
            });
            game = TestWorld.Start(saves.Store);

            Assert.That(game.UpgradeFacility("upgrade:chef:3").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.Adult.FacilityLevel, Is.EqualTo(3));
            Assert.That(game.Adult.EndingIds, Does.Contain("facility.chef"));
            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.EqualTo(0));
            Assert.That(game.Inventory.CountOf(game.Item("item.facility-materials")), Is.EqualTo(0));
            Assert.That(game.World.IsActive("world.chef.level3"), Is.True);
            Assert.That(TestWorld.Start(saves.Store).World.IsActive("world.chef.level3"), Is.True, "켠 월드 깃발은 저장된다");
        }

        [Test]
        public void Adult_UpgradeWithoutEnoughItemsChangesNothing()
        {
            using var saves = new TempSaves();
            var game = ReadyChefCandidate(saves);
            game.ChooseFacility("facility.chef", "facility:chef");
            SaveEdits.EditSnapshot(saves.Store, snapshot =>
            {
                snapshot.Adult.FacilityLevel = 2;
                SaveEdits.Fill(snapshot, "item.allowance", 299);
                SaveEdits.Fill(snapshot, "item.facility-materials", 10);
            });
            game = TestWorld.Start(saves.Store);

            Assert.That(game.UpgradeFacility("upgrade:chef:3").Kind, Is.EqualTo(AdultCareerResultKind.InsufficientResources));
            Assert.That(game.Adult.FacilityLevel, Is.EqualTo(2));
            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.EqualTo(299));
        }

        [Test]
        public void Adult_WorkReservedYesterdayIsForfeitedWhenTheNextDayStarts()
        {
            using var saves = new TempSaves();
            var game = ReadyChefCandidate(saves);
            game.ChooseFacility("facility.chef", "facility:chef");
            Assert.That(game.ReserveFacilityWork("work.chef.level1", "work:chef:left", 3).Applied, Is.True);

            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);

            Assert.That(game.Adult.HasPendingWork, Is.False);
            Assert.That(TestWorld.Start(saves.Store).Adult.HasPendingWork, Is.False);
        }
    }
}
