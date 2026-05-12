using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class ModernUiStyle2RuntimeSourceAuditTests
    {
        [Test]
        public void FirstScopeRuntimeUi_DoesNotReferenceStyle1Sprites()
        {
            var files = new[]
            {
                "Assets/Scripts/UI/Modern/ModernUiRecipes.cs",
                "Assets/Scripts/Game/Common/ModernHudSpriteKeys.cs",
                "Assets/Scripts/UI/HUD/StatusHud.cs",
                "Assets/Scripts/UI/Quests/QuestLogPanel.cs"
            };

            var violations = new List<string>();
            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                AddIfContains(violations, file, source, "ModernUI_16_Style1");
                AddIfContains(violations, file, source, "ModernUI_32_Style1");
                AddIfContains(violations, file, source, "sprites/ui/modern/16/style-1");
                AddIfContains(violations, file, source, "sprites/ui/modern/32/style-1");
                AddIfContains(violations, file, source, "ModernUISpriteAddresses.Style16,");
                AddIfContains(violations, file, source, "ModernUISpriteAddresses.Style32,");
            }

            Assert.IsEmpty(violations, "First-scope runtime UI still references Style1: " + string.Join(", ", violations));
        }

        [Test]
        public void RuntimeUi_UsesSemanticStyle2SpriteCatalogInsteadOfRawCoordinates()
        {
            var files = new[]
            {
                "Assets/Scripts/UI/Modern/ModernUiRecipes.cs",
                "Assets/Scripts/Game/Common/ModernHudSpriteKeys.cs",
                "Assets/Scripts/Game/Common/ModernUISpriteAddresses.cs",
            };

            var violations = new List<string>();
            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                AddIfContains(violations, file, source, "ModernUI_16_Style2_r");
            }

            Assert.IsEmpty(violations, "Runtime UI must use ModernUiStyle2Sprites semantic aliases, not raw Style2 slice coordinates: " + string.Join(", ", violations));
        }

        [Test]
        public void ModernHudSpriteKeys_UsesSpecificStyle2SemanticGroups()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Common/ModernHudSpriteKeys.cs");

            StringAssert.DoesNotContain("ModernUiStyle2Sprites.Hud.", source);
            StringAssert.Contains("ModernUiStyle2Sprites.Button.", source);
            StringAssert.Contains("ModernUiStyle2Sprites.Slot.", source);
            StringAssert.Contains("ModernUiStyle2Sprites.Ribbon.", source);
            StringAssert.Contains("ModernUiStyle2Sprites.Tab.", source);
        }

        private static void AddIfContains(List<string> violations, string file, string source, string token)
        {
            if (source.Contains(token))
            {
                violations.Add(file + " -> " + token);
            }
        }
    }
}
