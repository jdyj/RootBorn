using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiRecipeRuntimeTests
    {
        [Test]
        public void CommonPanel_ContainsNineTileRoles()
        {
            Assert.AreEqual(9, ModernUiRecipes.CommonPanel.Tiles.Count);
            Assert.IsTrue(ModernUiRecipes.CommonPanel.Tiles.Any(t => t.Role == "corner-tl"));
            Assert.IsTrue(ModernUiRecipes.CommonPanel.Tiles.Any(t => t.Role == "fill"));
            Assert.IsTrue(ModernUiRecipes.CommonPanel.Tiles.Any(t => t.Role == "corner-br"));
        }

        [Test]
        public void TargetWindowRecipes_DeclareRequiredParts()
        {
            CollectionAssert.Contains(ModernUiRecipes.SettingsWindow.RequiredParts, "toggle-row");
            CollectionAssert.Contains(ModernUiRecipes.InventoryWindow.RequiredParts, "slot-grid");
            CollectionAssert.Contains(ModernUiRecipes.StatusWindow.RequiredParts, "gauge-row");
        }

        [Test]
        public void SpriteResolver_DoesNotUseRuntimeResourcesLoad()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Modern/ModernUiSpriteResolver.cs");
            StringAssert.DoesNotContain("Resources.Load", source);
        }
    }
}
