// 원래 파일: Assets/Tests/PlayMode/EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs (KNOWLEDGE_STATUS_E2E_001),
//            Assets/Tests/PlayMode/EndToEnd/TownResourceRecipeActivityE2EScenarioTests.cs (TOWN_RESOURCE_RECIPE_E2E_001)
using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 장소 활동의 결과가 저장과 다시 읽기를 거쳐 한 번만 남는다: 작업장 일은 피로 상태와 오늘 결과 기록을 한 번씩 더하고,
    /// 광장 도움은 레시피를 한 번만 공개한다. 수첩 줄(화면)은 여기서 보지 않는다. 자동 테스트다.
    /// </summary>
    public sealed class ActivityStatusAndRecipeTests
    {
        private const string WorkshopShift = "location.activity.workshop-shift";
        private const string Fatigue = "status.fatigue";
        private const string SquareHelp = "location.activity.square-help-request";
        private const string Recipe = "recipe.town.field-notes";

        [Test]
        public void KnowledgeStatus001_WorkshopShift_AddsFatigueAndItsLogOnce_PerRequest()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.GetStatusValue(Fatigue), Is.EqualTo(0));

            game.VisitLocation("location.workshop");
            var result = game.PerformLocationActivity(WorkshopShift, "location-activity:workshop:1");
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            var statusLog = result.OutcomeLogIds.FirstOrDefault(id => id.Contains(Fatigue));
            Assert.That(statusLog, Is.Not.Null, "결과 기록에 상태 변화가 있다: " + string.Join(",", result.OutcomeLogIds));
            Assert.That(game.Student.GetStatusValue(Fatigue), Is.EqualTo(1));

            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out var saved), Is.True);
            Assert.That(saved.Student.StudentConditionStatuses.Count(e => e.Id == Fatigue && e.Value == 1), Is.EqualTo(1));
            Assert.That(saved.Student.TodayResultLogIds.Count(id => id == statusLog), Is.EqualTo(1));

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.GetStatusValue(Fatigue), Is.EqualTo(1), "다시 읽어도 한 번");
            Assert.That(game.Student.GetTodayResultLogIds().Count(id => id == statusLog), Is.EqualTo(1));
            Assert.That(game.PerformLocationActivity(WorkshopShift, "location-activity:workshop:1").Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(game.Student.GetStatusValue(Fatigue), Is.EqualTo(1));

            Assert.That(game.PerformLocationActivity(WorkshopShift, "location-activity:workshop:2").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(TestWorld.Start(saves.Store).Student.GetStatusValue(Fatigue), Is.EqualTo(2), "새 요청은 한 번 더");
        }

        [Test]
        public void TownResourceRecipe001_SquareHelpAgain_DoesNotRevealTheRecipeTwice()
        {
            using var saves = new TempSaves();
            var game = GameSession.StartFromData(TestWorld.DataDirectory, saves.Store, TestWorld.Slot, TestWorld.Player);
            var first = game.PerformLocationActivity(SquareHelp, "location-activity:square-help:1");
            Assert.That(first.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(first.OutcomeLogIds.Any(id => id.Contains(Recipe)), Is.True, "첫 결과에 레시피 공개 기록: " + string.Join(",", first.OutcomeLogIds));
            var progress = game.ResourceRecipes();
            Assert.That(progress.RevealedRecipeIds.Count(id => id == Recipe), Is.EqualTo(1));
            Assert.That(progress.SeenResourceIds.Length, Is.EqualTo(1));

            game = GameSession.StartFromData(TestWorld.DataDirectory, saves.Store, TestWorld.Slot, TestWorld.Player);
            Assert.That(game.ResourceRecipes().RevealedRecipeIds.Count(id => id == Recipe), Is.EqualTo(1));
            var second = game.PerformLocationActivity(SquareHelp, "location-activity:square-help:2");
            Assert.That(second.Kind, Is.EqualTo(LifeActivityResultKind.Applied), "돕기는 다시 할 수 있다");
            Assert.That(second.OutcomeLogIds.Any(id => id.Contains(Recipe)), Is.False, "두 번째에는 공개 기록이 없다: " + string.Join(",", second.OutcomeLogIds));
            Assert.That(game.ResourceRecipes().RevealedRecipeIds.Count(id => id == Recipe), Is.EqualTo(1));
            Assert.That(game.ResourceRecipes().SeenResourceIds.Length, Is.EqualTo(1));
        }
    }
}
