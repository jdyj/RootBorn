using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class SpumResourcesUsageGateTests
    {
        private static readonly Regex RuntimeResourcesPattern = new Regex(
            @"\bResources\.(Load(All|Async)?|GetBuiltinResource)\s*(<|\()|\bUnityEngine\.Resources\.(Load(All|Async)?|GetBuiltinResource)\s*(<|\()|\bglobal::UnityEngine\.Resources\.(Load(All|Async)?|GetBuiltinResource)\s*(<|\()",
            RegexOptions.Compiled);

        private static readonly HashSet<string> AllowedRuntimeResourcesUsages = new HashSet<string>
        {
            "Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs :: var fromResources = UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/Game/Managers/DataManager.cs :: Registry = UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/Game/StudentLife/MilestoneGrowth.cs :: var fallback = UnityEngine.Resources.Load<Rootborn.Game.Common.GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs :: registry = global::UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs :: var registry = Rootborn.Game.Managers.Managers.Data?.Registry ?? Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/UI/HUD/StatusHud.cs :: _cached = Resources.Load<Font>(\"Fonts/VaultUI\");",
            "Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs :: return Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
            "Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs :: return Resources.Load<GameDataRegistry>(\"GameDataRegistry\");"
        };

        [Test]
        public void SPUM_RESOURCES_001_RuntimeResourcesLoadStaysInsideReviewedNonSpumAllowlist()
        {
            var hits = FindMatches(RuntimeResourcesPattern, "Assets/Scripts/Game", "Assets/Scripts/UI", "Assets/Scripts/Network");
            var unexpected = new List<string>();
            var spumRuntimeLoads = new List<string>();

            foreach (string hit in hits)
            {
                if (IsAllowedBuiltinFontUsage(hit) || AllowedRuntimeResourcesUsages.Contains(ToUsageKey(hit)))
                    continue;

                unexpected.Add(hit);
                if (hit.ToLowerInvariant().Contains("spum"))
                    spumRuntimeLoads.Add(hit);
            }

            Assert.IsEmpty(spumRuntimeLoads, "SPUM runtime visuals must use Addressables and registry data, not Resources.Load*. Matches:\n" + string.Join("\n", spumRuntimeLoads));
            Assert.IsEmpty(unexpected, "New runtime Resources.Load*, Resources.LoadAll, Resources.LoadAsync, or non-LegacyRuntime builtin resource usage requires explicit review or removal:\n" + string.Join("\n", unexpected));
        }

        private static bool IsAllowedBuiltinFontUsage(string hit)
        {
            return hit.Contains("Resources.GetBuiltinResource<Font>(\"LegacyRuntime.ttf\")")
                || hit.Contains("UnityEngine.Resources.GetBuiltinResource<Font>(\"LegacyRuntime.ttf\")");
        }

        private static List<string> FindMatches(Regex pattern, params string[] roots)
        {
            var hits = new List<string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string[] lines = File.ReadAllLines(file);
                    string normalized = file.Replace('\\', '/');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string trimmed = lines[i].Trim();
                        if (trimmed.StartsWith("//"))
                            continue;

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
