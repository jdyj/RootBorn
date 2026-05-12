using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiPanelReconstructionManifestTests
    {
        private const string ManifestPath = "docs/art/modern-ui-reconstruction-manifest.json";

        [Test]
        public void Manifest_DeclaresInventoryStatusQuestAndPopupRecipes()
        {
            string json = File.ReadAllText(ManifestPath);
            string recipesJson = ExtractRecipesBlock(json);

            StringAssert.Contains("\"inventory\"", recipesJson);
            StringAssert.Contains("\"status\"", recipesJson);
            StringAssert.Contains("\"quest\"", recipesJson);
            StringAssert.Contains("\"popup\"", recipesJson);
        }

        [Test]
        public void Manifest_MapsPanelReconstructionRolesToConcreteSubSprites()
        {
            string json = File.ReadAllText(ManifestPath);
            var missing = RequiredRoles
                .Where(role => !json.Contains("\"role\": \"" + role + "\""))
                .ToArray();

            Assert.IsEmpty(missing, "Manifest is missing required Modern UI panel roles: " + string.Join(", ", missing));
        }

        [Test]
        public void InventoryStatusQuestPopupRecipes_DeclareExpectedParts()
        {
            string recipesJson = ExtractRecipesBlock(File.ReadAllText(ManifestPath));

            AssertRecipeContains(recipesJson, "inventory", new[] { "commonPanel", "title-tabs", "slot-grid", "scrollbar", "selection-cursor", "popup" });
            AssertRecipeContains(recipesJson, "status", new[] { "commonPanel", "title-tabs", "portrait-frame", "gauge-row", "icon-frame", "bottom-buttons" });
            AssertRecipeContains(recipesJson, "quest", new[] { "commonPanel", "quest-list", "quest-detail", "objective-progress", "reward-row", "claim-button", "scrollbar" });
            AssertRecipeContains(recipesJson, "popup", new[] { "commonPanel", "item-icon", "item-name", "item-description", "item-count", "action-buttons" });
        }

        private static void AssertRecipeContains(string json, string recipeName, IReadOnlyList<string> expectedParts)
        {
            string body = ExtractRecipeBody(json, recipeName);
            Assert.IsNotEmpty(body, "Missing recipe: " + recipeName);

            var missing = expectedParts
                .Where(part => !body.Contains("\"" + part + "\""))
                .ToArray();
            Assert.IsEmpty(missing, recipeName + " recipe is missing parts: " + string.Join(", ", missing));
        }

        private static string ExtractRecipesBlock(string json)
        {
            const string marker = "\"recipes\"";
            int markerIndex = json.IndexOf(marker, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(markerIndex, 0, "Missing recipes block.");
            return json.Substring(markerIndex);
        }

        private static string ExtractRecipeBody(string json, string recipeName)
        {
            string marker = "\"" + recipeName + "\":";
            int markerIndex = json.IndexOf(marker, System.StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return string.Empty;
            }

            int start = json.IndexOf('[', markerIndex);
            int end = json.IndexOf(']', start);
            if (start < 0 || end < start)
            {
                return string.Empty;
            }

            return json.Substring(start + 1, end - start - 1);
        }

        private static readonly string[] RequiredRoles =
        {
            "corner-tl",
            "edge-t",
            "corner-tr",
            "edge-l",
            "fill",
            "edge-r",
            "corner-bl",
            "edge-b",
            "corner-br",
            "title-tab",
            "slot",
            "selection-cursor",
            "scrollbar-track",
            "scrollbar-thumb",
            "popup-frame",
            "item-action-button",
            "portrait-frame",
            "icon-frame",
            "gauge-track",
            "gauge-fill",
            "quest-row",
            "quest-detail-frame",
            "reward-frame",
            "claim-button"
        };
    }
}
