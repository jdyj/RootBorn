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
        public void RuleForCategoryWithoutFurniture_IsReportedAsProblem()
        {
            // 분류 비교는 대소문자를 가린다: "kitchen" 규칙은 "Kitchen" 가구에 맞지 않고, 맞는 가구가 하나도 없으면 문제다.
            var table = new FurnitureQualityTable(
                new[] { CategoryRule("kitchen", "cozy", 3), CategoryRule("Kitchen", "tidy", 1) },
                new[] { Furniture("a", "Kitchen") });

            Assert.That(table.Problems.Count, Is.EqualTo(1));
            Assert.That(table.Problems[0], Does.Contain("kitchen"));
            Assert.That(table.ScoresOf("a").Count, Is.EqualTo(1), "맞는 분류 규칙은 그대로 들어간다");
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

        // ---------- 성능 측정(출력만, 실패 조건이 아니다) ----------

        // 지금 가구 수(data/ FurnitureDefinition 1,503개)와 같은 수의 시험용 가구, 분류 규칙 6개(data/workspaces와 같은 모양).
        private const int FurnitureCount = 1503;
        // 데이터의 가장 큰 일터 한도: 레벨 3 한도 14 + 인테리어 단계 규칙 +2 +4(둘 다 닿으면 더한다).
        private const int MaxCapacity = 20;
        private static readonly string[] Categories = { "Kitchen", "LivingRoom", "Bedroom", "Bathroom", "Plant", "Decor", "Other" };

        private static FurnitureDefinition[] ManyFurniture()
        {
            var all = new FurnitureDefinition[FurnitureCount];
            for (int i = 0; i < all.Length; i++) all[i] = Furniture("f" + i, Categories[i % Categories.Length]);
            return all;
        }

        private static FurnitureQualityRule[] DataShapedRules() => new[]
        {
            CategoryRule("Kitchen", "hygiene", 2), CategoryRule("LivingRoom", "cozy", 2), CategoryRule("Bedroom", "cozy", 1),
            CategoryRule("Bathroom", "hygiene", 1), CategoryRule("Plant", "cozy", 3), CategoryRule("Decor", "cozy", 3),
        };

        [Test]
        public void SpaceScore_AtMaxCapacity_IsFast()
        {
            var table = new FurnitureQualityTable(DataShapedRules(), ManyFurniture());
            var placed = new string[MaxCapacity];
            for (int i = 0; i < placed.Length; i++) placed[i] = "f" + (i % 9); // 되풀이 줄임도 거치게 같은 가구를 섞는다
            Score(table, placed, 70, 125); // 데우기

            var watch = System.Diagnostics.Stopwatch.StartNew();
            SpaceScores last = null;
            for (int i = 0; i < 1000; i++) last = Score(table, placed, 70, 125);
            watch.Stop();
            TestContext.Out.WriteLine($"[성능] 가구 {MaxCapacity}개 점수 계산 1,000번: {watch.Elapsed.TotalMilliseconds:0.00}ms (한 번 평균 {watch.Elapsed.TotalMilliseconds:0.00}µs)");
            Assert.That(last.All, Is.Not.Empty);
        }

        [Test]
        public void QualityTable_ForAllFurniture_BuildTime()
        {
            var furniture = ManyFurniture();
            var rules = DataShapedRules();
            new FurnitureQualityTable(rules, furniture); // 데우기

            var watch = System.Diagnostics.Stopwatch.StartNew();
            FurnitureQualityTable table = null;
            for (int i = 0; i < 10; i++) table = new FurnitureQualityTable(rules, furniture);
            watch.Stop();
            TestContext.Out.WriteLine($"[성능] 가구 {FurnitureCount}개 성질 표 만들기: 한 번 평균 {watch.Elapsed.TotalMilliseconds / 10:0.000}ms");
            Assert.That(table.Problems, Is.Empty);
        }
    }
}
