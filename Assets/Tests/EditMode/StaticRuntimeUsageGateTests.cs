using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class StaticRuntimeUsageGateTests
    {
        private static readonly Regex EntityBranchingPattern = new Regex(
            @"crop(Id|\.Id|\.id)\s*==|tool(Id|\.Id|\.id)\s*==|switch\s*\(\s*(crop|tool|knowledge).*\)|enum\s+(CropId|ToolId|KnowledgeId)|class\s+(Wheat|Carrot|StonePickaxe|StoneAxe|StoneHoe)",
            RegexOptions.Compiled);

        private static readonly Regex RuntimeResourceOrFindPattern = new Regex(
            @"Resources\.Load\s*(<|\()|GameObject\.Find\s*\(|FindObjectOfType\s*(<|\()|FindObjectsOfType\s*(<|\()|FindFirstObjectByType\s*(<|\()|FindObjectsByType\s*(<|\()",
            RegexOptions.Compiled);

        private static readonly HashSet<string> AllowedRuntimeResourceOrFindUsages = new HashSet<string>
        {
            "Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs :: var fromResources = UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs :: var allRegistries = UnityEngine.Resources.FindObjectsOfTypeAll<GameDataRegistry>();",
            "Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs :: var allSprites = UnityEngine.Resources.FindObjectsOfTypeAll<Sprite>();",
            "Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs :: if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)",
            "Assets/Scripts/Game/Bootstrap/SceneDiagnostics.cs :: int cameraCount = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;",
            "Assets/Scripts/Game/Bootstrap/SceneDiagnostics.cs :: int rendererCount = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length;",
            "Assets/Scripts/Game/Bootstrap/SceneDiagnostics.cs :: int tilemapCount = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None).Length;",
            "Assets/Scripts/Game/Farming/FarmGrid.cs :: _clock = GameClock.Instance != null ? GameClock.Instance : Object.FindFirstObjectByType<GameClock>();",
            "Assets/Scripts/Game/Managers/DataManager.cs :: Registry = UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/Game/Managers/Managers.cs :: var existing = GameObject.Find(\"@Managers\");",
            "Assets/Scripts/Game/Player/GatherInteractor.cs :: var all = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);",
            "Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs :: if (Object.FindFirstObjectByType<SeededFarmWorldApplier>() != null)",
            "Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs :: registry = global::UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs :: var tilemap = Object.FindFirstObjectByType<Tilemap>();",
            "Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs :: var root = GameObject.Find(\"[Resources]\");",
            "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs :: var farmCanvas = GameObject.Find(\"[FarmCanvas]\");",
            "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs :: foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (candidate != null && candidate.name == \"[FarmCanvas]\") return candidate;",
            "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs :: var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);",
            "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs :: var registry = Rootborn.Game.Managers.Managers.Data?.Registry ?? Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/UI/HUD/StatusHud.cs :: _cached = Resources.Load<Font>(\"Fonts/VaultUI\");",
            "Assets/Scripts/UI/HUD/StatusHud.cs :: var playerGo = GameObject.Find(\"Player\");",
            "Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs :: var existing = Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include);",
            "Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs :: var canvas = Object.FindFirstObjectByType<Canvas>();",
            "Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs :: if (Object.FindFirstObjectByType<EventSystem>() != null)"
        };

        [Test]
        public void STATIC_001_RuntimeCode_HasNoEntityIdBranchingPatterns()
        {
            var hits = FindMatches(EntityBranchingPattern, "Assets/Scripts/Game", "Assets/Scripts/UI", "Assets/Scripts/Network");

            Assert.IsEmpty(hits, "Entity-specific branches must stay data-driven. Matches:\n" + string.Join("\n", hits));
        }

        [Test]
        public void STATIC_002_RuntimeResourcesAndFindUsages_StayInsideReviewedAllowlist()
        {
            var hits = FindMatches(RuntimeResourceOrFindPattern, "Assets/Scripts/Game", "Assets/Scripts/UI", "Assets/Scripts/Network");
            var unexpected = new List<string>();
            foreach (string hit in hits)
            {
                string usageKey = ToUsageKey(hit);
                if (!AllowedRuntimeResourceOrFindUsages.Contains(usageKey))
                    unexpected.Add(hit);
            }

            Assert.IsEmpty(unexpected, "New runtime Resources.Load/Find usage requires explicit review or removal:\n" + string.Join("\n", unexpected));
        }

        private static List<string> FindMatches(Regex pattern, params string[] roots)
        {
            var hits = new List<string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string[] lines = File.ReadAllLines(file);
                    string normalized = file.Replace('\\', '/');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string trimmed = lines[i].Trim();
                        if (trimmed.StartsWith("//")) continue;
                        if (pattern.IsMatch(trimmed))
                            hits.Add($"{normalized}:{i + 1}: {trimmed}");
                    }
                }
            }
            return hits;
        }

        private static string ToUsageKey(string hit)
        {
            int firstColon = hit.IndexOf(':');
            int secondColon = hit.IndexOf(':', firstColon + 1);
            string path = hit.Substring(0, firstColon);
            string line = hit.Substring(secondColon + 2);
            return path + " :: " + line;
        }
    }
}
