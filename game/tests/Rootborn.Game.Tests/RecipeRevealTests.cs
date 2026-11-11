using NUnit.Framework;
using Rootborn.Core.Gathering;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>광장 도움 활동의 레시피 공개가 세이브에 남는다. 정의는 그 저장소의 RuleServices로 읽어야 한다(자동 테스트).</summary>
    public sealed class RecipeRevealTests
    {
        private const string SquareHelp = "location.activity.square-help-request";
        private const string Recipe = "recipe.town.field-notes";

        [Test]
        public void SquareHelp_RevealsTheFieldNotesRecipe_AndItIsSaved()
        {
            using var saves = new TempSaves();
            var game = GameSession.StartFromData(TestWorld.DataDirectory, saves.Store, TestWorld.Slot, TestWorld.Player);
            Assert.That(game.ResourceRecipes().IsRecipeRevealed(Recipe), Is.False);

            Assert.That(game.PerformLocationActivity(SquareHelp, "location-activity:square-help:day1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));

            Assert.That(game.ResourceRecipes().IsRecipeRevealed(Recipe), Is.True);
            Assert.That(game.ResourceRecipes().IsResourceSeen("Rock"), Is.True);
            Assert.That(saves.Store.TryRead<ResourceRecipeProgressSaveData>(TestWorld.Slot, SaveFileNames.ResourceRecipe(TestWorld.Player), out _), Is.True);
            var again = GameSession.StartFromData(TestWorld.DataDirectory, saves.Store, TestWorld.Slot, TestWorld.Player);
            Assert.That(again.ResourceRecipes().IsRecipeRevealed(Recipe), Is.True);
        }

        [Test]
        public void StartFromData_UsesTheRegistryDefaults()
        {
            using var saves = new TempSaves();
            var game = GameSession.StartFromData(TestWorld.DataDirectory, saves.Store, TestWorld.Slot, TestWorld.Player);
            var reference = TestWorld.Start(saves.Store);

            Assert.That(game.Student.DailyTimePointBudget, Is.EqualTo(reference.Student.DailyTimePointBudget));
            Assert.That(game.Student.TutorialStageId, Is.EqualTo(reference.Student.TutorialStageId));
            Assert.That(game.Campaign, Is.Not.Null);
        }
    }
}
