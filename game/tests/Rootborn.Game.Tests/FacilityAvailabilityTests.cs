// 원래 흐름: Assets/Scripts/UI/Careers/FacilityManagementPanel.cs Refresh(단추마다 CanTransition/CanWork/CanAutoSkip/CanUpgrade/CanRequestPractice),
//            Assets/Scripts/UI/Careers/CareerFacilityInteractor.cs TryInteract(시설 화면을 열면 장소 방문 이벤트 "facility-visit").
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>시설 조회는 명령과 같은 이유를 주고 아무것도 바꾸지 않는다. 시설 방문은 퀘스트·체인에만 알린다. 자동 테스트다.</summary>
    public sealed class FacilityAvailabilityTests
    {
        private const string Chef = "facility.chef";

        private static GameSession ReadyChefCandidate(TempSaves saves)
        {
            TestWorld.Start(saves.Store).TalkTo("npc.first-guide");
            SaveEdits.EditSnapshot(saves.Store, SaveEdits.MakeChefCandidateReady);
            return TestWorld.Start(saves.Store);
        }

        [Test]
        public void NewStudent_CannotChooseOrUpgradeOrWork_ButCanTryThePractice()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var status = game.FacilityStatus(Chef);

            Assert.That(status.Facility.Id, Is.EqualTo(Chef));
            Assert.That(status.Transition, Is.EqualTo(AdultCareerResultKind.RequirementFailed), "진로 후보가 아직 준비되지 않았다");
            Assert.That(status.Transition, Is.EqualTo(game.ChooseFacility(Chef, "facility:chef").Kind), "명령도 같은 이유");
            Assert.That(status.Upgrade, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(status.Works, Is.Not.Empty);
            Assert.That(status.Works.All(w => !w.CanWork && !w.CanAutoSkip), Is.True, "성인이 아니면 일할 수 없다");
            Assert.That(game.FacilityStatus("facility.none"), Is.Null);

            Assert.That(status.Practice, Is.EqualTo(AdultCareerResultKind.Applied), "학생은 진로 체험을 할 수 있다");
            Assert.That(game.BeginPractice(status.Facility.PracticeId, "practice:chef", 5).Applied, Is.True, "명령도 된다");
            Assert.That(game.FacilityStatus(Chef).Practice, Is.EqualTo(AdultCareerResultKind.RequirementFailed), "한 판이 예약돼 있다");
        }

        [Test]
        public void ReadyCandidate_CanChoose_ThenWorkButNotAutoSkip_AndQueriesChangeNothing()
        {
            using var saves = new TempSaves();
            var game = ReadyChefCandidate(saves);
            Assert.That(game.CanChooseFacility(Chef), Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.Adult.IsAdult, Is.False, "조회는 바꾸지 않는다");

            Assert.That(game.ChooseFacility(Chef, "facility:chef").Applied, Is.True);
            int timeBefore = game.Student.TimePointsRemaining;
            var status = game.FacilityStatus(Chef);
            Assert.That(status.Transition, Is.EqualTo(AdultCareerResultKind.AlreadyAdult));
            Assert.That(status.Practice, Is.EqualTo(AdultCareerResultKind.RequirementFailed), "성인은 진로 체험을 하지 않는다");
            var level1 = status.Works.Single(w => w.Work.Id == "work.chef.level1");
            Assert.That(level1.Direct, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(level1.AutoSkip, Is.EqualTo(AdultCareerResultKind.RequirementFailed), "숙련과 성공 횟수가 아직 없다");
            Assert.That(level1.AutoSkip, Is.EqualTo(game.CompleteFacilityWork(level1.Work.Id, "skip:1", 100, true).Kind));
            Assert.That(game.Student.TimePointsRemaining, Is.EqualTo(timeBefore));

            Assert.That(game.ReserveFacilityWork(level1.Work.Id, "work:1", 3).Applied, Is.True);
            Assert.That(game.FacilityStatus(Chef).Works.Single(w => w.Work.Id == "work.chef.level1").Direct,
                Is.EqualTo(AdultCareerResultKind.RequirementFailed), "예약이 남아 있다");
            Assert.That(game.CanUpgradeFacility(), Is.EqualTo(AdultCareerResultKind.RequirementFailed));
        }

        [Test]
        public void Upgrade_IsAllowedOnlyWithTheCosts()
        {
            using var saves = new TempSaves();
            var game = ReadyChefCandidate(saves);
            game.ChooseFacility(Chef, "facility:chef");
            Assert.That(game.CanUpgradeFacility(), Is.EqualTo(AdultCareerResultKind.InsufficientResources));
            Assert.That(game.UpgradeFacility("upgrade:1").Kind, Is.EqualTo(AdultCareerResultKind.InsufficientResources));

            SaveEdits.EditSnapshot(saves.Store, snapshot =>
            {
                SaveEdits.Fill(snapshot, "item.allowance", 300);
                SaveEdits.Fill(snapshot, "item.facility-materials", 10);
            });
            game = TestWorld.Start(saves.Store);
            Assert.That(game.CanUpgradeFacility(), Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.FacilityStatus(Chef).Upgrade, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.FacilityStatus("facility.soldier").Upgrade, Is.EqualTo(AdultCareerResultKind.RequirementFailed), "일하지 않는 시설");
            Assert.That(game.Adult.FacilityLevel, Is.EqualTo(1));
            Assert.That(game.UpgradeFacility("upgrade:2").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
        }

        [Test]
        public void VisitingTheFacilities_CountsAsVisitingTheirPlaces_ForTheTourStep_WithoutRecordingAVisit()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.TalkTo("npc.first-guide");
            Assert.That(game.SelectDailyPlanRoute(game.AvailableDailyPlanRoutes().First().Id), Is.True);
            game.TalkTo("npc.teacher");
            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            var chain = game.Chain(MainChainTests.ChainId);
            Assert.That(game.QuestChains.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.AfterSchoolTour"));
            var visitedBefore = new LocationVisitProgress(game.Student).VisitedLocationIds.ToArray();

            foreach (var facility in new[] { "facility.chef", "facility.emergency-care", "facility.interior", "facility.soldier" })
                Assert.That(game.VisitFacility(facility), Is.True, facility);

            Assert.That(game.QuestChains.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.FirstPartTimeJob"), "시설 앞 방문도 일터 둘러보기에 든다(Unity와 같음)");
            Assert.That(new LocationVisitProgress(game.Student).VisitedLocationIds, Is.EqualTo(visitedBefore), "장소 방문 기록은 남기지 않는다");
            Assert.That(TestWorld.Start(saves.Store).QuestChains.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.FirstPartTimeJob"), "저장된다");
            Assert.That(game.VisitFacility("facility.none"), Is.False);
        }
    }
}
