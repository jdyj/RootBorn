using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentDayProgressTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void STUDENT_DAY_001_PerformedActivityRecordsTodayLogAndSaveFields()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests("trait.diligence", "trait.diligence");
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(trait, 1);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests("activity.study-basics", "activity.study-basics", LifeActivityCategory.School, 45, 1, 1, 0, null, new LifeActivityEffectBase[] { effect });
            var progress = new StudentLifeProgress("slot-day", "player-day", 8, 8);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "study-request-1", out var result));

            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual(1, progress.CurrentDay);
            Assert.AreEqual(StudentDayState.InProgress, progress.DayState);
            CollectionAssert.Contains(progress.GetTodayActivityIds(), "activity.study-basics");
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), "activity.study-basics:+trait.diligence=1");
            var saveData = progress.ToSaveData();
            Assert.AreEqual(1, saveData.CurrentDay);
            Assert.AreEqual(StudentDayState.InProgress.ToString(), saveData.DayState);
            CollectionAssert.Contains(saveData.TodayActivityIds, "activity.study-basics");
            CollectionAssert.Contains(saveData.TodayResultLogIds, "activity.study-basics:+trait.diligence=1");
        }

        [Test]
        public void STUDENT_DAY_002_EndDayUsesRegistryRuleAssetOnceAndPersistsResultReadyState()
        {
            var rules = LoadDefaultRuleFromRegistry();
            var progress = new StudentLifeProgress("slot-day", "player-day", 4, 3, 2, 17 * 60);
            progress.RecordActivityCompleted("activity.study-basics", new[] { "activity.study-basics:+skill.basic-study=3" });

            Assert.IsTrue(progress.TryEndDay(rules, out var summary));
            Assert.AreEqual(StudentDayState.ResultReady, progress.DayState);
            Assert.AreEqual(1, summary.DayNumber);
            Assert.AreEqual("home-entry", progress.NextDayEntryPointId);
            Assert.AreEqual(12, progress.Energy);
            Assert.AreEqual(10, progress.Focus);
            CollectionAssert.Contains(summary.CompletedActivityIds, "activity.study-basics");
            CollectionAssert.Contains(summary.ResultLogIds, "activity.study-basics:+skill.basic-study=3");

            Assert.IsFalse(progress.TryEndDay(rules, out var duplicateSummary));
            Assert.AreEqual(1, duplicateSummary.DayNumber);
            Assert.AreEqual(1, progress.CurrentDay);
            Assert.AreEqual(StudentDayState.ResultReady, progress.DayState);
            Assert.AreEqual(1, progress.GetPreviousDayActivityIds().Length);

            var saveData = progress.ToSaveData();
            Assert.AreEqual(1, saveData.CurrentDay);
            Assert.AreEqual(StudentDayState.ResultReady.ToString(), saveData.DayState);
            Assert.AreEqual(1, saveData.LastSettledDay);
            CollectionAssert.Contains(saveData.PreviousDayActivityIds, "activity.study-basics");
            CollectionAssert.Contains(saveData.PreviousDayResultLogIds, "activity.study-basics:+skill.basic-study=3");
        }

        [Test]
        public void STUDENT_DAY_003_NextDayAdvancesDayClearsTodayAndKeepsCumulativeGrowth()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests("trait.diligence", "trait.diligence");
            var progress = new StudentLifeProgress("slot-day", "player-day", 4, 3);
            progress.AddTrait(trait, 2);
            progress.RecordActivityCompleted("activity.study-basics", new[] { "activity.study-basics:+trait.diligence=2" });
            var rules = LoadDefaultRuleFromRegistry();
            Assert.IsTrue(progress.TryEndDay(rules, out _));

            Assert.IsTrue(progress.TryStartNextDay());

            Assert.AreEqual(2, progress.CurrentDay);
            Assert.AreEqual(StudentDayState.InProgress, progress.DayState);
            Assert.AreEqual(0, progress.GetTodayActivityIds().Length);
            Assert.AreEqual(2, progress.GetTraitValue(trait));
            CollectionAssert.Contains(progress.GetPreviousDayActivityIds(), "activity.study-basics");
            Assert.IsFalse(progress.TryStartNextDay(), "Next-day transition should be idempotent while already in progress.");
        }

        [Test]
        public void STUDENT_DAY_004_GameDataRegistryRegistersDefaultDayEndRuleAsset()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.DefaultDayEndRule, "Default day-end rule must be registered in GameDataRegistry.asset.");
            Assert.IsNotNull(registry.DayEndRules, "Day-end rule collection must be exposed by GameDataRegistry.");
            CollectionAssert.Contains(registry.DayEndRules, registry.DefaultDayEndRule, "Default day-end rule must also be present in the registry collection.");
            Assert.AreEqual("day-rule.student-default", registry.DefaultDayEndRule.Id);
            Assert.AreEqual("home-entry", registry.DefaultDayEndRule.NextDayEntryPointId);
            Assert.GreaterOrEqual(registry.DefaultDayEndRule.Rules.Count, 2, "Default day-end rule must contain tunable recovery rule assets.");
            StringAssert.StartsWith("Assets/Data/StudentLife/", AssetDatabase.GetAssetPath(registry.DefaultDayEndRule));
        }

        [Test]
        public void STUDENT_DAY_005_RegistryRuleAppliesTunableEnergyFocusAndNextEntryPoint()
        {
            var rules = LoadDefaultRuleFromRegistry();
            var progress = new StudentLifeProgress("slot-day", "player-day", 1, 1, 5, 21 * 60);

            Assert.IsTrue(progress.TryEndDay(rules, out var summary));

            Assert.AreEqual(12, progress.Energy);
            Assert.AreEqual(10, progress.Focus);
            Assert.AreEqual("home-entry", progress.NextDayEntryPointId);
            Assert.AreEqual("home-entry", summary.NextDayEntryPointId);
        }

        [Test]
        public void STUDENT_DAY_006_MissingRegistryDayEndRuleLeavesRuntimeInteractorInactive()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var board = new GameObject("MissingRuleBoard");
            var panelObject = new GameObject("MissingRulePanel");
            var player = new GameObject("Player");
            try
            {
                var interactor = board.AddComponent<StudentDayEndInteractor>();
                var panel = panelObject.AddComponent<StudentDayResultPanel>();
                var progress = player.AddComponent<StudentLifeProgressComponent>();
                progress.ConfigureForTests("slot-day", "player-day", 2, 2, 0, 8 * 60);
                interactor.Bind(registry.DefaultDayEndRule, panel);

                Assert.IsNull(registry.DefaultDayEndRule);
                Assert.IsFalse(interactor.CanInteract(player), "Missing registry day-end rule must make the runtime interaction inactive.");
                Assert.IsFalse(interactor.TryInteract(player));
                Assert.AreEqual(StudentDayState.InProgress, progress.Progress.DayState);
                Assert.AreEqual(0, progress.Progress.LastSettledDay);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(panelObject);
                Object.DestroyImmediate(board);
                Object.DestroyImmediate(registry);
            }
        }

        [Test]
        public void STUDENT_DAY_007_TownRuntimeInstallerResolvesRegistryDefaultDayEndRuleWithoutRuntimeGeneration()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry);
            Assert.AreSame(registry.DefaultDayEndRule, StudentDayRuntimeInstaller.ResolveDefaultDayEndRulesForTests(registry));

            string source = File.ReadAllText("Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs");
            StringAssert.Contains("DefaultDayEndRule", source);
            StringAssert.DoesNotContain("CreateDefaultDayEndRules", source);
            StringAssert.DoesNotContain("ScriptableObject.CreateInstance<EnergyRestoreDayEndRule>", source);
            StringAssert.DoesNotContain("ScriptableObject.CreateInstance<FocusRestoreDayEndRule>", source);
            StringAssert.DoesNotContain("ScriptableObject.CreateInstance<DayEndRuleDefinition>", source);
        }

        private static DayEndRuleDefinition LoadDefaultRuleFromRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.DefaultDayEndRule, "Default day-end rule must come from the registry asset, not runtime CreateInstance.");
            return registry.DefaultDayEndRule;
        }
    }
}
