using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class SelfStudyLibraryGrowthTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string SelfStudyCategoryId = "outside.category.self-study";
        private const string LibraryStudyActivityId = "outside.self-study.library";

        [Test]
        public void STUDY_EDIT_001_SelfStudyActivityAndCategoryLoadFromRegistryDataPath()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            var category = FindCategory(registry, SelfStudyCategoryId);
            var activity = FindActivity(registry, LibraryStudyActivityId);

            Assert.IsNotNull(category, "Self-study category must be registered in GameDataRegistry.");
            Assert.IsNotNull(activity, "Library self-study activity must be registered in GameDataRegistry.");
            Assert.AreSame(category, activity.Category, "Library self-study activity must reference the registered self-study category asset.");
            StringAssert.StartsWith("Assets/Data/StudentLife/OutsideSchool/", AssetDatabase.GetAssetPath(category));
            StringAssert.StartsWith("Assets/Data/StudentLife/OutsideSchool/", AssetDatabase.GetAssetPath(activity));
        }

        [Test]
        public void STUDY_EDIT_002_SelfStudyCostAndOutcomesApplyWithoutEntityIdBranching()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var activity = FindActivity(registry, LibraryStudyActivityId);
            Assert.IsNotNull(activity, "Library self-study activity must be registered before execution can be tested.");
            Assert.Greater(activity.Outcomes.Count, 0, "Library self-study must define data-driven outcome strategy assets.");

            var progress = new StudentLifeProgress("slot-study", "player-study", 8, 8);
            var runner = new OutsideSchoolActivityRunner();
            int startEnergy = progress.Energy;
            int startFocus = progress.Focus;
            int startStress = progress.Stress;

            Assert.IsTrue(runner.TryPerform(activity, progress, "study-edit-request-1", out var result));

            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual(LibraryStudyActivityId, result.ActivityId);
            Assert.AreEqual(SelfStudyCategoryId, result.CategoryId);
            Assert.AreEqual(startEnergy - activity.EnergyCost, progress.Energy);
            Assert.AreEqual(startFocus - activity.FocusCost, progress.Focus);
            Assert.AreEqual(startStress + activity.StressDelta, progress.Stress);
            Assert.Greater(result.OutcomeLogIds.Length, 0, "Self-study must emit at least one growth result log.");
            CollectionAssert.Contains(progress.GetTodayActivityIds(), LibraryStudyActivityId);
        }

        [Test]
        public void STUDY_EDIT_003_SelfStudyFailsClearlyWhenFocusOrEnergyIsInsufficient()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var activity = FindActivity(registry, LibraryStudyActivityId);
            Assert.IsNotNull(activity, "Library self-study activity must be registered before failure behavior can be tested.");

            var progress = new StudentLifeProgress("slot-study", "player-study", 0, 0);
            var runner = new OutsideSchoolActivityRunner();

            Assert.IsFalse(runner.TryPerform(activity, progress, "study-edit-request-low-resource", out var result));

            Assert.AreEqual(LifeActivityResultKind.InsufficientResources, result.Kind);
            Assert.AreEqual(0, progress.GetTodayActivityIds().Length);
            Assert.AreEqual(0, progress.GetTodayResultLogIds().Length);
        }

        [Test]
        public void STUDY_EDIT_004_SelfStudyChangesTraitSkillRelationshipOrConditionState()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var activity = FindActivity(registry, LibraryStudyActivityId);
            Assert.IsNotNull(activity, "Library self-study activity must be registered before growth behavior can be tested.");

            var progress = new StudentLifeProgress("slot-study", "player-study", 8, 8);
            var runner = new OutsideSchoolActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "study-edit-request-growth", out var result));

            bool changedAnyGrowthState = progress.GetTraitIds().Length > 0 ||
                                         progress.GetSkillIds().Length > 0 ||
                                         progress.GetRelationshipIds().Length > 0 ||
                                         progress.GetStatusIds().Length > 0;
            Assert.IsTrue(changedAnyGrowthState, "Self-study must change at least one trait, skill, relationship, or condition state.");
            Assert.Greater(result.OutcomeLogIds.Length, 0);
        }

        [Test]
        public void STUDY_EDIT_005_SameSelfStudyResultDoesNotApplyTwiceAfterSaveLoad()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var activity = FindActivity(registry, LibraryStudyActivityId);
            Assert.IsNotNull(activity, "Library self-study activity must be registered before idempotency can be tested.");

            var runner = new OutsideSchoolActivityRunner();
            var progress = new StudentLifeProgress("slot-study", "player-study", 8, 8);

            Assert.IsTrue(runner.TryPerform(activity, progress, "study-edit-request-idempotent", out var first));
            var saveData = progress.ToSaveData();
            var restored = StudentLifeProgress.FromSaveData(saveData, registry.StudentLifeTraits, registry.StudentLifeSkills, registry.Careers, registry.Relationships, registry.StudentConditionStatuses);
            Assert.IsFalse(runner.TryPerform(activity, restored, "study-edit-request-idempotent", out var duplicate));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(progress.Energy, restored.Energy);
            Assert.AreEqual(progress.Focus, restored.Focus);
            Assert.AreEqual(progress.Stress, restored.Stress);
            Assert.AreEqual(progress.GetTodayResultLogIds().Length, restored.GetTodayResultLogIds().Length);
        }

        [Test]
        public void STUDY_EDIT_006_SelfStudyRuntimeSourcesAvoidEntityIdBranchingPatterns()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/OutsideSchoolActivityDefinition.cs",
                "Assets/Scripts/Game/StudentLife/OutsideSchoolActivityRunner.cs",
                "Assets/Scripts/Game/StudentLife/OutsideSchoolOutcomes.cs",
                "Assets/Scripts/Game/StudentLife/OutsideSchoolRequirements.cs",
                "Assets/Scripts/UI/StudentLife/OutsideSchoolRuntimeInstaller.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("activityId ==", source);
                StringAssert.DoesNotContain("categoryId ==", source);
                StringAssert.DoesNotContain("locationId ==", source);
                StringAssert.DoesNotContain("switch (activityId", source);
                StringAssert.DoesNotContain("switch (categoryId", source);
                StringAssert.DoesNotContain("switch (locationId", source);
            }
        }

        private static OutsideSchoolActivityCategoryDefinition FindCategory(GameDataRegistry registry, string id)
        {
            var categories = registry != null ? registry.OutsideSchoolActivityCategories : null;
            if (categories == null) return null;
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i] != null && categories[i].Id == id)
                {
                    return categories[i];
                }
            }

            return null;
        }

        private static OutsideSchoolActivityDefinition FindActivity(GameDataRegistry registry, string id)
        {
            var activities = registry != null ? registry.OutsideSchoolActivities : null;
            if (activities == null) return null;
            for (int i = 0; i < activities.Length; i++)
            {
                if (activities[i] != null && activities[i].Id == id)
                {
                    return activities[i];
                }
            }

            return null;
        }
    }
}
