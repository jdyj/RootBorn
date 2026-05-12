using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class OutsideSchoolGrowthFoundationTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_001_DefinitionsAndCategoriesLoadFromRegistryDataPath()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.OutsideSchoolActivities, "Outside-school activities must be exposed by GameDataRegistry.");
            Assert.IsNotNull(registry.OutsideSchoolActivityCategories, "Outside-school categories must be exposed by GameDataRegistry.");
            Assert.GreaterOrEqual(registry.OutsideSchoolActivities.Length, 1, "At least one sample outside-school activity must be registered.");
            Assert.GreaterOrEqual(registry.OutsideSchoolActivityCategories.Length, 1, "At least one outside-school category must be registered.");
            StringAssert.StartsWith("Assets/Data/StudentLife/OutsideSchool/", AssetDatabase.GetAssetPath(registry.OutsideSchoolActivities[0]));
            StringAssert.StartsWith("Assets/Data/StudentLife/OutsideSchool/", AssetDatabase.GetAssetPath(registry.OutsideSchoolActivityCategories[0]));
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_002_RequirementsEvaluateWithoutEntityIdBranching()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var requirement = ScriptableObject.CreateInstance<OutsideSchoolTraitThresholdRequirement>();
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);
            try
            {
                trait.ConfigureForTests("trait.helpfulness", "trait.helpfulness");
                requirement.ConfigureForTests(trait, 2);

                Assert.IsFalse(requirement.IsSatisfied(progress));
                progress.AddTrait(trait, 2);
                Assert.IsTrue(requirement.IsSatisfied(progress));
            }
            finally
            {
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(trait);
            }
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_003_OutcomeRecordsGenericOutsideSchoolResultLog()
        {
            var category = ScriptableObject.CreateInstance<OutsideSchoolActivityCategoryDefinition>();
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<OutsideSchoolTraitDeltaOutcome>();
            var activity = ScriptableObject.CreateInstance<OutsideSchoolActivityDefinition>();
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);
            var runner = new OutsideSchoolActivityRunner();
            try
            {
                category.ConfigureForTests("outside.category.help", "outside.category.help");
                trait.ConfigureForTests("trait.helpfulness", "trait.helpfulness");
                outcome.ConfigureForTests(trait, 3);
                activity.ConfigureForTests("outside.help-neighbor", "outside.help-neighbor", category, 30, 1, 0, 0, null, new OutsideSchoolOutcomeBase[] { outcome });

                Assert.IsTrue(runner.TryPerform(activity, progress, "outside-request-1", out var result));

                string expected = "outside-school:outside.help-neighbor:+trait.helpfulness=3";
                Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
                Assert.AreEqual("outside.help-neighbor", result.ActivityId);
                Assert.AreEqual("outside.category.help", result.CategoryId);
                Assert.AreEqual(3, progress.GetTraitValue(trait));
                CollectionAssert.Contains(result.OutcomeLogIds, expected);
                CollectionAssert.Contains(OutsideSchoolDayLog.FromResultLogs(progress.GetTodayResultLogIds()).ResultLogIds, expected);
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), expected);
            }
            finally
            {
                Object.DestroyImmediate(activity);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
                Object.DestroyImmediate(category);
            }
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_004_DuplicateRequestIdDoesNotApplyTwice()
        {
            var category = ScriptableObject.CreateInstance<OutsideSchoolActivityCategoryDefinition>();
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<OutsideSchoolTraitDeltaOutcome>();
            var activity = ScriptableObject.CreateInstance<OutsideSchoolActivityDefinition>();
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);
            var runner = new OutsideSchoolActivityRunner();
            try
            {
                category.ConfigureForTests("outside.category.help", "outside.category.help");
                trait.ConfigureForTests("trait.helpfulness", "trait.helpfulness");
                outcome.ConfigureForTests(trait, 2);
                activity.ConfigureForTests("outside.help-neighbor", "outside.help-neighbor", category, 30, 0, 0, 0, null, new OutsideSchoolOutcomeBase[] { outcome });

                Assert.IsTrue(runner.TryPerform(activity, progress, "same-outside-request", out var first));
                Assert.IsFalse(runner.TryPerform(activity, progress, "same-outside-request", out var duplicate));

                Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
                Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
                Assert.AreEqual(2, progress.GetTraitValue(trait));
                Assert.AreEqual(1, OutsideSchoolDayLog.FromResultLogs(progress.GetTodayResultLogIds()).ActivityIds.Length);
            }
            finally
            {
                Object.DestroyImmediate(activity);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
                Object.DestroyImmediate(category);
            }
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_005_TodayAndPreviousOutsideSchoolLogsPersistInSaveData()
        {
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);
            progress.RecordActivityCompleted("outside.help-neighbor", new[] { "outside-school:outside.help-neighbor:+trait.helpfulness=1" });

            var saveData = progress.ToSaveData();
            var restored = StudentLifeProgress.FromSaveData(saveData, null, null, null);

            CollectionAssert.Contains(saveData.TodayActivityIds, "outside.help-neighbor");
            CollectionAssert.Contains(saveData.TodayResultLogIds, "outside-school:outside.help-neighbor:+trait.helpfulness=1");
            CollectionAssert.Contains(OutsideSchoolDayLog.FromResultLogs(restored.GetTodayResultLogIds()).ActivityIds, "outside.help-neighbor");
            Assert.IsTrue(restored.TryEndDay(null, out var summary));
            CollectionAssert.Contains(OutsideSchoolDayLog.FromResultLogs(restored.GetPreviousDayResultLogIds()).ActivityIds, "outside.help-neighbor");
            CollectionAssert.Contains(summary.ResultLogIds, "outside-school:outside.help-neighbor:+trait.helpfulness=1");
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_006_SummaryDtoUsesCommonEntriesWithoutRouteSpecificFields()
        {
            var log = new OutsideSchoolDayLog(
                new[] { "outside.help-neighbor" },
                new[] { "outside-school:outside.help-neighbor:+trait.helpfulness=1", "outside-school:outside.help-neighbor:+skill.community=2" },
                new[] { "request-1" });

            var summary = OutsideSchoolGrowthSummary.FromDayLog(log);

            Assert.AreEqual(2, summary.Entries.Length);
            Assert.AreEqual("outside.help-neighbor", summary.Entries[0].ActivityId);
            Assert.AreEqual("trait.helpfulness", summary.Entries[0].TargetId);
            Assert.AreEqual(1, summary.Entries[0].Delta);
            Assert.AreEqual("skill.community", summary.Entries[1].TargetId);
            Assert.AreEqual(2, summary.Entries[1].Delta);
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_007_FollowUpRoutesCanReuseSameModel()
        {
            var help = ScriptableObject.CreateInstance<OutsideSchoolActivityCategoryDefinition>();
            var study = ScriptableObject.CreateInstance<OutsideSchoolActivityCategoryDefinition>();
            var work = ScriptableObject.CreateInstance<OutsideSchoolActivityCategoryDefinition>();
            var exploration = ScriptableObject.CreateInstance<OutsideSchoolActivityCategoryDefinition>();
            try
            {
                help.ConfigureForTests("outside.category.help", "outside.category.help");
                study.ConfigureForTests("outside.category.self-study", "outside.category.self-study");
                work.ConfigureForTests("outside.category.work", "outside.category.work");
                exploration.ConfigureForTests("outside.category.exploration", "outside.category.exploration");

                var categories = new[] { help, study, work, exploration };
                for (int i = 0; i < categories.Length; i++)
                {
                    var activity = ScriptableObject.CreateInstance<OutsideSchoolActivityDefinition>();
                    try
                    {
                        activity.ConfigureForTests("outside.sample." + i, "outside.sample." + i, categories[i], 5, 0, 0, 0, null, null);
                        Assert.AreSame(categories[i], activity.Category);
                        Assert.AreEqual(categories[i].Id, activity.CategoryId);
                    }
                    finally
                    {
                        Object.DestroyImmediate(activity);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(help);
                Object.DestroyImmediate(study);
                Object.DestroyImmediate(work);
                Object.DestroyImmediate(exploration);
            }
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_008_SourceAvoidsEntityIdBranchingPatterns()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/OutsideSchoolActivityDefinition.cs",
                "Assets/Scripts/Game/StudentLife/OutsideSchoolActivityRunner.cs",
                "Assets/Scripts/Game/StudentLife/OutsideSchoolOutcomes.cs",
                "Assets/Scripts/Game/StudentLife/OutsideSchoolRequirements.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("activityId ==", source);
                StringAssert.DoesNotContain("routeId ==", source);
                StringAssert.DoesNotContain("locationId ==", source);
                StringAssert.DoesNotContain("switch (activityId", source);
                StringAssert.DoesNotContain("switch (routeId", source);
                StringAssert.DoesNotContain("switch (locationId", source);
            }
        }
    }
}
