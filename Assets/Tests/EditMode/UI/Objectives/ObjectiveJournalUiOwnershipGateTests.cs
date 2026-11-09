using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalUiOwnershipGateTests
    {
        private static readonly Regex ObjectiveUiTypePattern = new Regex(
            @"\bclass\s+(?<type>[A-Za-z0-9_]*(Goal|Quest|Campaign|Hint|Milestone)[A-Za-z0-9_]*(Hud|Panel|Widget))\b",
            RegexOptions.Compiled);

        private static readonly Regex FactoryPattern = new Regex(
            @"EnsureInScene\s*\(|new\s+GameObject\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex ObjectiveKeywordPattern = new Regex(
            @"Goal|Quest|Campaign|Hint|Milestone|TrackedObjective",
            RegexOptions.Compiled);

        private static readonly HashSet<string> AllowedObjectiveUiTypes = new HashSet<string>
        {
            "Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs :: ObjectiveJournalPanel",
            "Assets/Scripts/UI/Objectives/TrackedObjectiveHud.cs :: TrackedObjectiveHud",

            // Reviewed legacy or non-always-visible surfaces. New objective-like HUD/Panel/Widget types must not be added here without design review.
            "Assets/Scripts/UI/Quests/QuestChainLogPanel.cs :: QuestChainLogPanel",
            "Assets/Scripts/UI/StudentLife/CareerInterestPanel.cs :: CareerInterestPanel"
        };

        private static readonly HashSet<string> AllowedObjectiveUiFactories = new HashSet<string>
        {
            "Assets/Scripts/UI/Objectives/TrackedObjectiveHud.cs :: _root = new GameObject(\"TrackedObjectiveHudRoot\", typeof(RectTransform), typeof(ModernUiTileImage), typeof(CanvasGroup));",
            "Assets/Scripts/UI/Quests/QuestChainLogPanel.cs :: public static QuestChainLogPanel EnsureInScene(Canvas canvas)",
            "Assets/Scripts/UI/Quests/QuestChainLogPanel.cs :: var go = new GameObject(\"QuestChainLogPanel\", typeof(RectTransform));",
            "Assets/Scripts/UI/Quests/QuestChainLogPanel.cs :: _root = new GameObject(\"QuestChainLogRoot\", typeof(RectTransform), typeof(ModernUiTileImage));",
            "Assets/Scripts/UI/Quests/QuestChainRuntimeInstaller.cs :: _panel = QuestChainLogPanel.EnsureInScene(canvas);",
            "Assets/Scripts/UI/StudentLife/CareerInterestPanel.cs :: public static CareerInterestPanel EnsureInScene(Canvas canvas)",
            "Assets/Scripts/UI/StudentLife/CareerInterestRuntimeInstaller.cs :: _panel = CareerInterestPanel.EnsureInScene(canvas);",
            "Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs :: go = new GameObject(\"CampaignResultSummary\", typeof(RectTransform), typeof(Text));",
            "Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs :: go = new GameObject(\"QuestChainResultSummary\", typeof(RectTransform), typeof(Text));"
        };

        [Test]
        public void OBJECTIVE_UI_GATE_001_ObjectiveLikeHudPanelTypesStayInsideReviewedOwners()
        {
            var unexpected = new List<string>();
            foreach (string file in Directory.GetFiles("Assets/Scripts/UI", "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                string source = File.ReadAllText(file);
                foreach (Match match in ObjectiveUiTypePattern.Matches(source))
                {
                    string usage = normalized + " :: " + match.Groups["type"].Value;
                    if (!AllowedObjectiveUiTypes.Contains(usage))
                    {
                        unexpected.Add(usage);
                    }
                }
            }

            Assert.IsEmpty(unexpected, "Objective-like UI must be owned by ObjectiveJournalPanel, with only TrackedObjectiveHud as the compact always-visible HUD. Unexpected types:\n" + string.Join("\n", unexpected));
        }

        [Test]
        public void OBJECTIVE_UI_GATE_002_ObjectiveLikeAlwaysVisibleFactoriesStayReviewed()
        {
            var unexpected = new List<string>();
            foreach (string file in Directory.GetFiles("Assets/Scripts/UI", "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (!FactoryPattern.IsMatch(trimmed) || !ObjectiveKeywordPattern.IsMatch(trimmed))
                    {
                        continue;
                    }

                    string usage = normalized + " :: " + trimmed;
                    if (!AllowedObjectiveUiFactories.Contains(usage))
                    {
                        unexpected.Add(normalized + ":" + (i + 1) + ": " + trimmed);
                    }
                }
            }

            Assert.IsEmpty(unexpected, "New objective-like EnsureInScene/new GameObject factories require Objective Journal ownership review. Unexpected factories:\n" + string.Join("\n", unexpected));
        }
    }
}
