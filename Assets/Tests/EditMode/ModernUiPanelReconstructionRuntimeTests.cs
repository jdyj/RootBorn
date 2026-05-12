using System.Linq;
using NUnit.Framework;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiPanelReconstructionRuntimeTests
    {
        [Test]
        public void RuntimeRecipes_ExposeQuestAndPopupWindows()
        {
            AssertWindowRecipe("QuestWindow", new[] { "commonPanel", "quest-list", "quest-detail", "objective-progress", "reward-row", "claim-button", "scrollbar" });
            AssertWindowRecipe("PopupWindow", new[] { "commonPanel", "item-icon", "item-name", "item-description", "item-count", "action-buttons" });
        }

        private static void AssertWindowRecipe(string fieldName, string[] expectedParts)
        {
            var field = typeof(ModernUiRecipes).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(field, "Missing ModernUiRecipes." + fieldName);

            var recipe = field.GetValue(null) as ModernUiWindowRecipe;
            Assert.IsNotNull(recipe, fieldName + " must be a ModernUiWindowRecipe.");

            var missing = expectedParts.Where(part => !recipe.RequiredParts.Contains(part)).ToArray();
            Assert.IsEmpty(missing, fieldName + " is missing parts: " + string.Join(", ", missing));
        }
    }
}
