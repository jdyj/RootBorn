using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Objectives;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalSourceCatalogTests
    {
        [Test]
        public void SourceCatalog_CreatesAllDefaultSourceIdsInStableOrder()
        {
            var type = Type.GetType("Rootborn.UI.Objectives.ObjectiveJournalSourceCatalog, Rootborn.UI");
            Assert.IsNotNull(type, "Objective Journal source composition must be owned by a catalog outside ObjectiveJournalRuntimeProvider.");
            var method = type.GetMethod("CreateDefaultSources", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(method, "ObjectiveJournalSourceCatalog must expose CreateDefaultSources for composition tests.");

            var sources = (IObjectiveJournalSource[])method.Invoke(null, null);
            var ids = new List<string>();
            for (int i = 0; i < sources.Length; i++) ids.Add(sources[i].SourceId);

            CollectionAssert.AreEqual(
                new[] { "student-day-guide", "daily-plan", "daily-plan-route-hints", "resource-recipes", "status-activity", "quest-log", "quest-chain-log", "campaign-progress", "milestones", "career-interests" },
                ids,
                "Default Objective Journal source order is part of the player-facing category and tracked-restore behavior.");
        }

        [Test]
        public void SourceCatalog_DefaultSourcesExposeExpectedCategories()
        {
            var sources = ObjectiveJournalSourceCatalog.CreateDefaultSources();
            var categoriesById = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Length; i++)
            {
                categoriesById.Add(sources[i].SourceId, sources[i].Category.CategoryId);
            }

            Assert.AreEqual("Goals", categoriesById["student-day-guide"]);
            Assert.AreEqual("Goals", categoriesById["daily-plan"]);
            Assert.AreEqual("Hints", categoriesById["daily-plan-route-hints"]);
            Assert.AreEqual("Hints", categoriesById["resource-recipes"]);
            Assert.AreEqual("Hints", categoriesById["status-activity"]);
            Assert.AreEqual("Quests", categoriesById["quest-log"]);
            Assert.AreEqual("Quests", categoriesById["quest-chain-log"]);
            Assert.AreEqual("Campaign", categoriesById["campaign-progress"]);
            Assert.AreEqual("Hints", categoriesById["milestones"]);
            Assert.AreEqual("Hints", categoriesById["career-interests"]);
        }

        [Test]
        public void SourceCatalog_DefaultSourcesDegradeWithNullContext()
        {
            var sources = ObjectiveJournalSourceCatalog.CreateDefaultSources();
            var context = new ObjectiveJournalContext(null, null, null);

            for (int i = 0; i < sources.Length; i++)
            {
                Assert.DoesNotThrow(() => sources[i].BuildItems(context), sources[i].SourceId + " must tolerate null registry/player/save context.");
            }
        }

        [Test]
        public void SourceTypes_AreStandaloneClassesOutsideRuntimeProvider()
        {
            string[] typeNames =
            {
                "Rootborn.UI.Objectives.StudentDayGuideObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.DailyPlanObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.DailyPlanRouteHintObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.ResourceRecipeObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.StatusActivityObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.QuestLogObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.QuestChainObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.CampaignObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.MilestoneObjectiveSource, Rootborn.UI",
                "Rootborn.UI.Objectives.CareerInterestObjectiveSource, Rootborn.UI"
            };

            for (int i = 0; i < typeNames.Length; i++)
            {
                var type = Type.GetType(typeNames[i]);
                Assert.IsNotNull(type, typeNames[i] + " must exist as a standalone Objective Journal source class.");
                Assert.IsNull(type.DeclaringType, typeNames[i] + " must not remain nested in ObjectiveJournalRuntimeProvider.");
                Assert.IsTrue(typeof(IObjectiveJournalSource).IsAssignableFrom(type), typeNames[i] + " must implement IObjectiveJournalSource.");
            }
        }
    }
}
