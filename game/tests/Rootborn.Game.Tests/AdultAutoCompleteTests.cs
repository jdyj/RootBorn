// 원래 파일: Assets/Tests/PlayMode/EndToEnd/AdultCareerE2EScenarioTests.cs (ADULT_E2E_003 뒷부분, ADULT_E2E_004)
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 성인 셰프 시설에서 마지막으로 통과한 일이 자동 완료를 열고, 자동 완료는 만점 보상을 한 번만 준다.
    /// 시작 상태는 Unity 판처럼 스냅샷을 고쳐 만든다(준비 단계). 명령마다 새 GameSession으로 다시 읽는다. 자동 테스트다.
    /// </summary>
    public sealed class AdultAutoCompleteTests
    {
        private const string FirstWork = "work.chef.level1";

        private static GameSession ReadyLevelTwoChef(TempSaves saves, int mastery, int successes)
        {
            TestWorld.Start(saves.Store).TalkTo("npc.first-guide"); // 첫 저장
            SaveEdits.EditSnapshot(saves.Store, snapshot =>
            {
                SaveEdits.MakeChefCandidateReady(snapshot);
                snapshot.Adult.FacilityId = "facility.chef";
                snapshot.Adult.FacilityLevel = 2;
                snapshot.Adult.Works = new[] { new FacilityWorkProgressSaveData { WorkId = FirstWork, Mastery = mastery, SuccessfulCompletions = successes } };
            });
            return TestWorld.Start(saves.Store);
        }

        private static int[] Counts(GameSession game, Rootborn.Core.Inventory.ItemAmount<string>[] rewards)
            => rewards.Select(r => game.Inventory.CountOf(game.Item(r.Item))).ToArray();

        [Test]
        public void Adult004_TheFifthPassedWorkUnlocksAutoComplete_WhichPaysTheTopRewardOnce()
        {
            using var saves = new TempSaves();
            var game = ReadyLevelTwoChef(saves, mastery: 80, successes: 4);
            var work = game.Definitions.Get<FacilityWork>(FirstWork);
            var facility = game.Definitions.Get<Facility>("facility.chef");

            Assert.That(game.CompleteFacilityWork(FirstWork, "auto:too-early", 100, autoSkip: true).Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed),
                "숙련이 차기 전에는 자동 완료가 없다");
            game = TestWorld.Start(saves.Store);

            // 다섯 번째 일: 미니게임으로 예약하고 통과 점수로 정산한다.
            Assert.That(game.ReserveFacilityWork(FirstWork, "work:chef:5", 7).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            game = TestWorld.Start(saves.Store);
            Assert.That(game.SettleFacilityWork("work:chef:5", 100).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Adult.GetSuccessfulCompletions(work), Is.EqualTo(5));
            Assert.That(game.Adult.GetMastery(work), Is.EqualTo(work.MaximumMastery));

            Assert.That(work.TryBuildRewards(100, facility.GetLevel(2).RewardPercent, out var top), Is.True);
            Assert.That(top, Is.Not.Empty);
            var before = Counts(game, top);

            Assert.That(game.CompleteFacilityWork(FirstWork, "auto:chef:1", 0, autoSkip: true).Kind, Is.EqualTo(AdultCareerResultKind.Applied),
                "자동 완료는 점수와 상관없이 만점으로 정산한다");
            game = TestWorld.Start(saves.Store);
            var paid = Counts(game, top);
            Assert.That(paid, Is.EqualTo(before.Select((b, i) => b + top[i].Count).ToArray()), "만점 보상을 한 번 준다");
            Assert.That(game.Adult.GetSuccessfulCompletions(work), Is.EqualTo(6));
            // 흐르는 시간: 하루에 몇 번이라는 한도는 없다. 오늘을 마감(잠)한 뒤에는 일을 못 한다.
            Assert.That(game.Sleep().Ended, Is.True);

            Assert.That(game.CompleteFacilityWork(FirstWork, "auto:chef:2", 100, autoSkip: true).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientTime));
            Assert.That(game.ReserveFacilityWork(FirstWork, "work:chef:6", 7).Kind, Is.Not.EqualTo(AdultCareerResultKind.Applied),
                "마감한 날에는 예약도 안 된다(예약은 하루 진행 여부를 요구 조건에서 먼저 본다)");
            Assert.That(game.CompleteFacilityWork(FirstWork, "auto:chef:1", 100, autoSkip: true).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Adult.GetSuccessfulCompletions(work), Is.EqualTo(6));
            Assert.That(Counts(again, top), Is.EqualTo(paid), "다시 읽어도 보상은 그대로다");
        }

        [Test]
        public void Adult003_AfterTheRouteEnds_AutoCompleteStillCountsAWork()
        {
            using var saves = new TempSaves();
            ReadyLevelTwoChef(saves, mastery: 100, successes: 5);
            SaveEdits.EditSnapshot(saves.Store, snapshot =>
            {
                SaveEdits.Fill(snapshot, "item.allowance", 300);
                SaveEdits.Fill(snapshot, "item.facility-materials", 10);
            });
            var game = TestWorld.Start(saves.Store);
            var work = game.Definitions.Get<FacilityWork>(FirstWork);

            Assert.That(game.UpgradeFacility("upgrade:chef:3").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Adult.EndingIds, Does.Contain("facility.chef"));
            Assert.That(game.UpgradeFacility("upgrade:chef:4").Kind, Is.EqualTo(AdultCareerResultKind.AtMaximumLevel), "가장 높은 단계는 더 올리지 않는다");

            Assert.That(game.CompleteFacilityWork(FirstWork, "auto:after-ending", 100, autoSkip: true).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(TestWorld.Start(saves.Store).Adult.GetSuccessfulCompletions(work), Is.EqualTo(6), "끝난 뒤에도 일은 이어진다");
        }
    }
}
