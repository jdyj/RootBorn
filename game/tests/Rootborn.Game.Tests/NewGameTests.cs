using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 새 게임 초기값. 값의 출처: 시작 에너지·집중·시각은 Unity StudentLifeProgressComponent 직렬화 기본값(12, 10, 480분),
    /// 달력·하루 끝 규칙·튜토리얼 시작 단계·캠페인은 registry.json의 기본값 키. 자동 테스트다.
    /// </summary>
    public sealed class NewGameTests
    {
        [Test]
        public void NewGame_StartsWithTheUnityStartingValues()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var student = game.Student;

            Assert.That(student.SaveSlot, Is.EqualTo(TestWorld.Slot));
            Assert.That(student.PlayerId, Is.EqualTo(TestWorld.Player));
            Assert.That(student.Energy, Is.EqualTo(12));
            Assert.That(student.Focus, Is.EqualTo(10));
            Assert.That(student.Stress, Is.EqualTo(0));
            Assert.That(student.TimeMinutes, Is.EqualTo(420), "흐르는 시간: 하루는 07:00에 시작한다(game/time.json, Unity 판은 8:00)");
            Assert.That(student.CurrentDay, Is.EqualTo(1));
            Assert.That(student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(student.TutorialStageId, Is.EqualTo("tutorial.day1"));
            Assert.That(student.DailyTimePointBudget, Is.EqualTo(2), "Calendar_Default 평일 2");
            Assert.That(student.DailyFocus, Is.EqualTo(10), "day-rule.student-default 집중 회복 10");
            Assert.That(Rootborn.Core.Inventory.ItemInventory.MaxSlots, Is.EqualTo(32));
            Assert.That(game.Inventory.Slots.All(slot => slot.Item == null), Is.True, "Unity 판은 시작 아이템을 주지 않는다");
            Assert.That(game.Adult.IsAdult, Is.False);
            Assert.That(game.Campaign.ActiveCampaignId, Is.EqualTo("campaign.school-opening-three-days"));
            Assert.That(game.QuestChains.GetState(game.Chain("main-story.school-opening")), Is.EqualTo(QuestChainState.Available),
                "메인 체인은 엄마(npc.first-guide)와 이야기할 때 시작한다");
        }

        [Test]
        public void NewGame_WritesNothingUntilTheFirstCommand()
        {
            using var saves = new TempSaves();
            TestWorld.Start(saves.Store);
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out _), Is.False);
        }

        [Test]
        public void SavedGame_ContinuesFromTheSave()
        {
            using var saves = new TempSaves();
            var first = TestWorld.Start(saves.Store);
            first.TalkTo("npc.first-guide");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.QuestChains.GetState(again.Chain("main-story.school-opening")), Is.EqualTo(QuestChainState.Tracked));
            Assert.That(again.Student.DailyFocus, Is.EqualTo(10), "읽은 진행 상태에도 하루 집중을 다시 넣는다");
            Assert.That(again.Student.DailyTimePointBudget, Is.EqualTo(2), "읽은 진행 상태에도 달력을 다시 넣는다");
        }
    
        [Test]
        public void StartSettings_FromTheDefinitions_MatchTheRegistryFile_AndStartAtTheDayStart()
        {
            var settings = GameStartSettings.FromDefinitions(TestWorld.Definitions);
            var errors = new System.Collections.Generic.List<Rootborn.Data.LoadError>();
            var table = Rootborn.Data.EntityTable.ReadDirectory(TestWorld.DataDirectory, errors);
            Assert.That(errors, Is.Empty);
            string First(string key) => table.RegistryIds(key).Count > 0 ? table.RegistryIds(key)[0] : string.Empty;

            Assert.That((settings.CalendarId, settings.DayEndRuleId, settings.TutorialStageId, settings.CampaignId, settings.LocationStateConflictPolicyId),
                Is.EqualTo((First("studentLifeCalendar"), First("defaultDayEndRule"), First("defaultTutorialStage"), First("defaultStudentCampaign"), First("locationStateConflictPolicies"))));
            Assert.That(settings.CalendarId, Is.Not.Empty);
            Assert.That(settings.CampaignId, Is.Not.Empty);
            Assert.That(settings.StartTimeMinutes, Is.EqualTo(TestWorld.Definitions.TimeRules.DayStartMinutes), "새 게임은 흐르는 시간의 하루 시작(07:00)에서");
            Assert.That(settings.StartTimeMinutes, Is.EqualTo(7 * 60));
        }
    }
}
