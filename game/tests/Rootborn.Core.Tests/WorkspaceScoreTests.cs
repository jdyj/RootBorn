using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;

namespace Rootborn.Core.Tests
{
    public sealed class WorkspaceScoreTests
    {
        private static FurnitureDefinition Furniture(string id, string category)
            => new FurnitureDefinition(id, id, null, category, null, null, null, true, FurniturePlacementPreference.Any);

        private static FurnitureQualityRule CategoryRule(string category, string quality, int score)
            => new FurnitureQualityRule("rule." + category + "." + quality, category, null, quality, score);

        private static FurnitureQualityRule FurnitureRule(string furnitureId, string quality, int score)
            => new FurnitureQualityRule("rule." + furnitureId + "." + quality, null, furnitureId, quality, score);

        private static SpaceScores Score(FurnitureQualityTable table, string[] placed, int repeatPercent, int scorePercent)
            => SpaceScoreCalculator.Calculate(
                new ISpaceQualitySource[] { new FurnitureQualitySource(placed, table, repeatPercent) }, scorePercent);

        [Test]
        public void CategoryRule_GivesScoreToEveryFurnitureOfThatCategory()
        {
            var table = new FurnitureQualityTable(
                new[] { CategoryRule("Kitchen", "cozy", 3) },
                new[] { Furniture("a", "Kitchen"), Furniture("b", "Kitchen") });

            var scores = Score(table, new[] { "a", "b" }, 100, 100);

            Assert.That(scores.Get("cozy"), Is.EqualTo(6));
        }

        [Test]
        public void FurnitureRule_OverridesCategoryRule()
        {
            var table = new FurnitureQualityTable(
                new[] { CategoryRule("Kitchen", "cozy", 3), FurnitureRule("a", "cozy", 10) },
                new[] { Furniture("a", "Kitchen"), Furniture("b", "Kitchen") });

            Assert.That(Score(table, new[] { "a" }, 100, 100).Get("cozy"), Is.EqualTo(10));
            Assert.That(Score(table, new[] { "b" }, 100, 100).Get("cozy"), Is.EqualTo(3));
        }

        [Test]
        public void RepeatedFurniture_Falls()
        {
            var table = new FurnitureQualityTable(
                new[] { FurnitureRule("a", "cozy", 10) },
                new[] { Furniture("a", "Kitchen") });

            var scores = Score(table, new[] { "a", "a", "a" }, 50, 100);

            Assert.That(scores.Get("cozy"), Is.EqualTo(17), "10 + 5 + 2");
        }

        [Test]
        public void ScorePercent_MultipliesTotal()
        {
            var table = new FurnitureQualityTable(
                new[] { FurnitureRule("a", "cozy", 10) },
                new[] { Furniture("a", "Kitchen") });

            Assert.That(Score(table, new[] { "a" }, 100, 150).Get("cozy"), Is.EqualTo(15));
        }

        [Test]
        public void FurnitureWithoutRule_ScoresZero()
        {
            var table = new FurnitureQualityTable(
                new[] { FurnitureRule("a", "cozy", 10) },
                new[] { Furniture("a", "Kitchen"), Furniture("plain", "Other") });

            var scores = Score(table, new[] { "plain" }, 100, 100);

            Assert.That(table.ScoresOf("plain"), Is.Empty);
            Assert.That(scores.Get("cozy"), Is.EqualTo(0));
            Assert.That(scores.All, Is.Empty);
        }

        [Test]
        public void UnknownQuality_ReturnsZero()
        {
            Assert.That(SpaceScores.Empty.Get("nothing"), Is.EqualTo(0));
            var table = new FurnitureQualityTable(
                new[] { FurnitureRule("a", "cozy", 10) },
                new[] { Furniture("a", "Kitchen") });
            Assert.That(Score(table, new[] { "a" }, 100, 100).Get("tidy"), Is.EqualTo(0));
        }

        [Test]
        public void RuleForUnknownFurniture_IsReportedAsProblem()
        {
            var table = new FurnitureQualityTable(
                new[] { FurnitureRule("ghost", "cozy", 10) },
                new[] { Furniture("a", "Kitchen") });

            Assert.That(table.Problems.Count, Is.EqualTo(1));
            Assert.That(table.ScoresOf("ghost"), Is.Empty);
        }

        [Test]
        public void RuleWithBothCategoryAndFurniture_IsInvalid()
        {
            var both = new FurnitureQualityRule("r", "Kitchen", "a", "cozy", 3);
            var neither = new FurnitureQualityRule("r2", null, null, "cozy", 3);
            var negative = new FurnitureQualityRule("r3", "Kitchen", null, "cozy", -1);

            Assert.That(both.IsValid, Is.False);
            Assert.That(neither.IsValid, Is.False);
            Assert.That(negative.IsValid, Is.False);
            Assert.That(CategoryRule("Kitchen", "cozy", 3).IsValid, Is.True);

            var table = new FurnitureQualityTable(new[] { both }, new[] { Furniture("a", "Kitchen") });
            Assert.That(table.Problems.Count, Is.EqualTo(1));
            Assert.That(table.ScoresOf("a"), Is.Empty);
        }

        [Test]
        public void Scores_AreSortedByQualityIdAndClampedToIntMax()
        {
            var table = new FurnitureQualityTable(
                new[] { FurnitureRule("a", "tidy", int.MaxValue), FurnitureRule("a", "cozy", 1) },
                new[] { Furniture("a", "Kitchen") });

            var scores = Score(table, new[] { "a" }, 100, 200);

            Assert.That(scores.All[0].QualityId, Is.EqualTo("cozy"));
            Assert.That(scores.All[1].QualityId, Is.EqualTo("tidy"));
            Assert.That(scores.Get("tidy"), Is.EqualTo(int.MaxValue));
            Assert.That(scores.Get("cozy"), Is.EqualTo(2));
        }
    }
}
