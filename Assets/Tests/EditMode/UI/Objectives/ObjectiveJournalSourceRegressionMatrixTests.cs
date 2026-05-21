using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.UI.Objectives;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalSourceRegressionMatrixTests
    {
        [Test]
        public void OBJECTIVE_JOURNAL_SOURCE_MATRIX_001_MixedSourcesKeepStableKeysUniqueAndOwnedByFirstSource()
        {
            var goals = new ObjectiveJournalCategory("Goals", "Goals");
            var hints = new ObjectiveJournalCategory("Hints", "Hints");
            var quests = new ObjectiveJournalCategory("Quests", "Quests");
            var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[]
            {
                new MatrixSource("daily-plan", goals, new[]
                {
                    new ObjectiveJournalItem("daily-plan:daily.library.self-study", "Goals", "Selected Daily Plan Route", string.Empty, "Selected", "Day 1", "Complete Library route", false, 0),
                }),
                new MatrixSource("completed-route-filter", goals, Array.Empty<ObjectiveJournalItem>()),
                new MatrixSource("daily-plan-route-hints", hints, new[]
                {
                    new ObjectiveJournalItem("daily-plan-route-hint:daily.workshop.practice", "Hints", "Unlocked Workshop Route", string.Empty, "Available", "Unlocked", "Select route", false, 10),
                }),
                new MatrixSource("milestones", hints, new[]
                {
                    new ObjectiveJournalItem("milestone:first-guide-trust", "Hints", "Relationship Milestone", string.Empty, "Available", "0 / 1", "Help First Guide", false, 20),
                }),
                new MatrixSource("career-interests", hints, new[]
                {
                    new ObjectiveJournalItem("career-interest:chef", "Hints", "Career Hint", string.Empty, "Available", "Candidate", "Practice cooking", false, 30),
                }),
                new MatrixSource("quest-chain-log", quests, new[]
                {
                    new ObjectiveJournalItem("quest-chain:daily-plan-route", "Quests", "Quest Chain Route Reward", string.Empty, "Active", "1 / 2", "Claim reward", false, 0),
                }),
                new MatrixSource("duplicate-regression-source", goals, new[]
                {
                    new ObjectiveJournalItem("daily-plan:daily.library.self-study", "Goals", "Duplicate Daily Plan Route", string.Empty, "Active", "Duplicate", "Should not appear", false, 30),
                }),
            });

            var result = aggregator.Build(new ObjectiveJournalContext(null, null, null));

            Assert.AreEqual(1, CountStableKey(result, "daily-plan:daily.library.self-study"), "Selected Daily Plan goal must not be duplicated by later source outputs.");
            Assert.AreEqual(0, CountStableKey(result, "completed-route:daily.library.self-study"), "A completed-route source must not emit active Daily Plan guidance.");
            Assert.AreEqual(1, CountStableKey(result, "daily-plan-route-hint:daily.workshop.practice"), "Unlocked route hints must keep their own stable key namespace.");
            Assert.AreEqual(1, CountStableKey(result, "milestone:first-guide-trust"), "Relationship milestone hints must coexist with route hints.");
            Assert.AreEqual(1, CountStableKey(result, "career-interest:chef"), "Career hints must coexist with route and relationship hints.");
            Assert.AreEqual(1, CountStableKey(result, "quest-chain:daily-plan-route"), "Quest-chain rows must keep their own stable key namespace.");
            Assert.AreEqual("Selected Daily Plan Route", result["Goals"][0].Title, "The first registered source owns duplicate stable-key output.");
            Assert.AreEqual(3, result["Hints"].Length, "Route hints, relationship milestone hints, and career hints must coexist in Hints without collision.");
        }

        private static int CountStableKey(Dictionary<string, ObjectiveJournalItem[]> result, string stableKey)
        {
            int count = 0;
            foreach (var pair in result)
            {
                var items = pair.Value ?? Array.Empty<ObjectiveJournalItem>();
                for (int i = 0; i < items.Length; i++)
                {
                    if (string.Equals(items[i].StableKey, stableKey, StringComparison.Ordinal)) count++;
                }
            }

            return count;
        }

        private sealed class MatrixSource : IObjectiveJournalSource
        {
            private readonly ObjectiveJournalItem[] _items;

            public MatrixSource(string sourceId, ObjectiveJournalCategory category, ObjectiveJournalItem[] items)
            {
                SourceId = sourceId;
                Category = category;
                _items = items ?? Array.Empty<ObjectiveJournalItem>();
            }

            public string SourceId { get; }
            public ObjectiveJournalCategory Category { get; }

            public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
            {
                return _items;
            }
        }
    }
}
