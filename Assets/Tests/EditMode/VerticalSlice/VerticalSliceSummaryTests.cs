using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.Game.VerticalSlice;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.VerticalSlice
{
    public sealed class VerticalSliceSummaryTests
    {
        [Test]
        public void VERTICAL_EDIT_001_RegistryLoadsRequiredVerticalSliceSourceData()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            Assert.IsNotNull(registry, "VERTICAL-EDIT-001 failed: GameDataRegistry asset must load from the data path.");
            Assert.IsNotNull(registry.Quests, "VERTICAL-EDIT-001 failed: quest registry array is null.");
            Assert.IsNotNull(registry.Careers, "VERTICAL-EDIT-001 failed: career registry array is null.");
            Assert.IsNotNull(registry.WorldStateUsages, "VERTICAL-EDIT-001 failed: world-state usage registry array is null.");
            Assert.Greater(registry.Quests.Length, 0, "VERTICAL-EDIT-001 failed: no quest data is registered.");
            Assert.Greater(registry.Careers.Length, 0, "VERTICAL-EDIT-001 failed: no career data is registered.");
            Assert.Greater(registry.WorldStateUsages.Length, 0, "VERTICAL-EDIT-001 failed: no world-state usage data is registered.");
        }

        [Test]
        public void VERTICAL_EDIT_003_SummaryCarriesObjectiveGuidanceForNextAction()
        {
            var summary = new VerticalSliceSummary(
                "vertical.day-one",
                new[] { "StudentLife", "Quest" },
                "Choose the next town objective",
                "Review the town board or visit the library",
                "House: interior route can become a later reward",
                "Day 1 changed StudentLife and Quest progress");

            Assert.AreEqual("vertical.day-one", summary.StableKey);
            CollectionAssert.Contains(summary.ChangedDomainIds, "StudentLife");
            StringAssert.Contains("town objective", summary.NextObjectiveText);
            StringAssert.Contains("library", summary.NextActionText);
        }

        [Test]
        public void VERTICAL_EDIT_004_SummaryCarriesDayResultAndHouseMotivation()
        {
            var summary = new VerticalSliceSummary(
                "vertical.day-one",
                new[] { "DiscoveryClue", "Career" },
                "Inspect archive table",
                "Open Objective Journal",
                "House: save money for a larger study room",
                "Clue and career hint unlocked");

            StringAssert.Contains("Clue", summary.DayResultGuideText);
            StringAssert.Contains("House", summary.FollowUpMotivationText);
        }

        [Test]
        public void VERTICAL_EDIT_002_005_006_BuilderSummarizesTwoDomainsAndIsIdempotent()
        {
            var progress = new StudentLifeProgress("slot", "player", 10, 10);
            progress.RecordActivityCompleted("location-activity:self-study", new[] { "growth.focus:+1" });
            progress.UnlockCareerHint(CreateCareer("career.learning"));

            var summary = VerticalSliceSummaryBuilder.Build(progress, questChanged: true, clueChanged: false, worldStateChanged: false, houseMotivation: "House: prepare a study room");
            var duplicate = VerticalSliceSummaryBuilder.Build(progress, questChanged: true, clueChanged: false, worldStateChanged: false, houseMotivation: "House: prepare a study room");

            CollectionAssert.Contains(summary.ChangedDomainIds, "StudentLife");
            CollectionAssert.Contains(summary.ChangedDomainIds, "Quest");
            Assert.GreaterOrEqual(summary.ChangedDomainIds.Length, 2);
            StringAssert.Contains("House", summary.FollowUpMotivationText);
            Assert.AreEqual(summary.StableKey, duplicate.StableKey);
            CollectionAssert.AreEqual(summary.ChangedDomainIds, duplicate.ChangedDomainIds);
        }

        [Test]
        public void VERTICAL_EDIT_008_BuilderDoesNotBranchByKnownEntityIds()
        {
            var source = File.ReadAllText("Assets/Scripts/Game/VerticalSlice/VerticalSliceSummaryBuilder.cs");
            StringAssert.DoesNotContain("career.learning", source);
            StringAssert.DoesNotContain("location-activity:self-study", source);
            StringAssert.DoesNotContain("if (activityId", source);
            StringAssert.DoesNotContain("switch", source);
        }

        [Test]
        public void VERTICAL_EDIT_003_004_UiSurfacesShowVerticalSliceGuidance()
        {
            var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
            var dayGo = new GameObject("StudentDayResultPanel", typeof(RectTransform));
            try
            {
                var journal = panelGo.AddComponent<ObjectiveJournalPanel>();
                var dayResult = dayGo.AddComponent<StudentDayResultPanel>();
                var summary = new VerticalSliceSummary(
                    "vertical.slice.foundation",
                    new[] { "StudentLife", "Quest" },
                    "Choose the next town objective",
                    "Visit the library",
                    "House: prepare a study room",
                    "Today connected StudentLife, Quest");

                journal.SetVerticalSliceSummary(summary);
                journal.Show();
                dayResult.AppendVerticalSliceGuide(summary);

                StringAssert.Contains("Choose the next town objective", journal.VisibleText);
                StringAssert.Contains("Visit the library", journal.VisibleText);
                StringAssert.Contains("House", dayResult.NextGuideTextForTests);
            }
            finally
            {
                Object.DestroyImmediate(panelGo);
                Object.DestroyImmediate(dayGo);
            }
        }

        private static CareerDefinition CreateCareer(string id)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, id);
            return career;
        }
    }
}