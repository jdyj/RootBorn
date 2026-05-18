using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class ExplorationInteractionChoiceTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void EXPLORATION_CHOICE_EDIT_001_StrategyAssetsLoadFromRegistryOrDataPath()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.GetExplorationInteractions(), "Exploration interactions must be loadable from registry extension/data path.");
            Assert.IsNotNull(registry.GetExplorationChoices(), "Exploration choices must be loadable from registry extension/data path.");
            Assert.IsNotNull(registry.GetExplorationConditions(), "Exploration conditions must be loadable from registry extension/data path.");
            Assert.IsNotNull(registry.GetExplorationOutcomes(), "Exploration outcomes must be loadable from registry extension/data path.");
            Assert.IsNotNull(registry.GetExplorationRiskPolicies(), "Exploration risk policies must be loadable from registry extension/data path.");
        }

        [Test]
        public void EXPLORATION_CHOICE_EDIT_003_005_ChoiceUsesConditionAndOutcomeStrategyArrays()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            var condition = ScriptableObject.CreateInstance<ExplorationTraitThresholdCondition>();
            var traitOutcome = ScriptableObject.CreateInstance<ExplorationTraitDeltaOutcome>();
            var careerOutcome = ScriptableObject.CreateInstance<ExplorationCareerHintOutcome>();
            var choice = ScriptableObject.CreateInstance<ExplorationChoiceDefinition>();
            var interaction = ScriptableObject.CreateInstance<ExplorationInteractionDefinition>();
            try
            {
                ConfigureItem(item, "item.rusty-key", 10);
                trait.ConfigureForTests("trait.curiosity", "trait.curiosity");
                career.ConfigureForTests("career.investigator", "career.investigator", null);
                condition.ConfigureForTests(trait, 2, "need.curiosity");
                traitOutcome.ConfigureForTests(trait, 1);
                careerOutcome.ConfigureForTests(career);
                choice.ConfigureForTests("choice.inspect", "Inspect", "Inspect carefully", new ExplorationConditionBase[] { condition }, new ExplorationOutcomeBase[] { traitOutcome, careerOutcome }, null, false, "hint.inspect");
                interaction.ConfigureForTests("exploration.alley-crate", "Alley Crate", "A crate in the alley", Vector2.zero, 1f, false, 0, null, new[] { choice });
                var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
                var progress = new ExplorationProgress("slot-a", "player-a");
                var context = new ExplorationInteractionContext(progress, student, new Inventory(), null, null, null, 1, 480);
                var runner = new ExplorationInteractionRunner();

                Assert.IsFalse(runner.TryChoose(interaction, choice, context, out var locked));
                Assert.AreEqual(ExplorationChoiceResultKind.ConditionFailed, locked.Kind);
                Assert.AreEqual("need.curiosity", locked.LockedReasonKey);

                student.AddTrait(trait, 2);
                Assert.IsTrue(runner.TryChoose(interaction, choice, context, out var result));

                Assert.AreEqual(ExplorationChoiceResultKind.Applied, result.Kind);
                Assert.AreEqual(3, student.GetTraitValue(trait));
                Assert.IsTrue(student.IsCareerHintUnlocked(career));
                CollectionAssert.Contains(result.CareerHintIds, career.Id);
                CollectionAssert.Contains(result.OutcomeIds, "trait:" + trait.Id);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(interaction);
                UnityEngine.Object.DestroyImmediate(choice);
                UnityEngine.Object.DestroyImmediate(careerOutcome);
                UnityEngine.Object.DestroyImmediate(traitOutcome);
                UnityEngine.Object.DestroyImmediate(condition);
                UnityEngine.Object.DestroyImmediate(career);
                UnityEngine.Object.DestroyImmediate(trait);
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void EXPLORATION_CHOICE_EDIT_004_006_007_008_ProgressPersistsRepeatPolicyAndDedupe()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var outcome = ScriptableObject.CreateInstance<ExplorationItemGrantOutcome>();
            var risk = ScriptableObject.CreateInstance<ExplorationRiskPolicyDefinition>();
            var choice = ScriptableObject.CreateInstance<ExplorationChoiceDefinition>();
            var interaction = ScriptableObject.CreateInstance<ExplorationInteractionDefinition>();
            try
            {
                ConfigureItem(item, "item.old-coin", 99);
                outcome.ConfigureForTests(item, 1, false);
                risk.ConfigureForTests(2, 1, 0, null, null);
                choice.ConfigureForTests("choice.open", "Open", "Open it", Array.Empty<ExplorationConditionBase>(), new ExplorationOutcomeBase[] { outcome }, risk, false, "hint.open");
                interaction.ConfigureForTests("exploration.alley-crate", "Alley Crate", "A crate in the alley", Vector2.zero, 1f, false, 0, null, new[] { choice, choice, choice });
                var inventory = new Inventory();
                var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
                var progress = new ExplorationProgress("slot-a", "player-a");
                var context = new ExplorationInteractionContext(progress, student, inventory, null, null, null, 3, 615);
                var runner = new ExplorationInteractionRunner();

                Assert.AreEqual(3, interaction.Choices.Count, "One interaction must provide at least three data choices.");
                Assert.IsTrue(runner.TryChoose(interaction, choice, context, out var first));
                Assert.IsFalse(runner.TryChoose(interaction, choice, context, out var duplicate));
                var restored = ExplorationProgress.FromSaveData(progress.ToSaveData());
                var record = restored.GetRecord(interaction.Id);

                Assert.AreEqual(ExplorationChoiceResultKind.Applied, first.Kind);
                Assert.AreEqual(ExplorationChoiceResultKind.DuplicateRequest, duplicate.Kind);
                Assert.AreEqual(1, inventory.CountOf(item), "Item reward must not duplicate.");
                Assert.IsTrue(record.Completed);
                CollectionAssert.Contains(record.SelectedChoiceIds, choice.Id);
                Assert.IsTrue(record.IsRewardClaimed(choice.Id));
                Assert.AreEqual(1, record.RepeatCount);
                Assert.AreEqual(3, record.FirstCompletedDay);
                Assert.AreEqual(615, record.LastCompletedTimeMinutes);
                Assert.AreEqual(2, student.Stress, "Risk fatigue/stress policy must be data-driven.");
                Assert.AreEqual(9, student.Energy, "Risk energy cost must be data-driven.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(interaction);
                UnityEngine.Object.DestroyImmediate(choice);
                UnityEngine.Object.DestroyImmediate(risk);
                UnityEngine.Object.DestroyImmediate(outcome);
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void EXPLORATION_CHOICE_EDIT_009_SummaryBuilderUsesLookupCacheAndDoesNotScanRegistryEveryCall()
        {
            var choice = ScriptableObject.CreateInstance<ExplorationChoiceDefinition>();
            var interaction = ScriptableObject.CreateInstance<ExplorationInteractionDefinition>();
            try
            {
                choice.ConfigureForTests("choice.observe", "Observe", "Look closer", Array.Empty<ExplorationConditionBase>(), Array.Empty<ExplorationOutcomeBase>(), null, false, "quiet hint");
                interaction.ConfigureForTests("exploration.test", "Test", "Desc", Vector2.one, 1.5f, true, 2, null, new[] { choice });
                var cache = new ExplorationLookupCache(new[] { interaction });
                var progress = new ExplorationProgress("slot-a", "player-a");
                var summary = ExplorationSummaryBuilder.Build("exploration.test", cache, progress, null, null, null, null, 1, 0);

                Assert.AreEqual("exploration.test", summary.InteractionId);
                Assert.AreEqual(1, cache.LookupCount);
                Assert.AreEqual(1, summary.Choices.Length);
                Assert.IsTrue(summary.Choices[0].Available);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(interaction);
                UnityEngine.Object.DestroyImmediate(choice);
            }
        }

        [Test]
        public void EXPLORATION_CHOICE_EDIT_SourceAvoidsEntityIdBranchingPatterns()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/ExplorationInteractionDefinition.cs",
                "Assets/Scripts/Game/StudentLife/ExplorationInteractionRunner.cs",
                "Assets/Scripts/Game/StudentLife/ExplorationInteractionOutcomes.cs",
                "Assets/Scripts/Game/StudentLife/ExplorationInteractionConditions.cs",
                "Assets/Scripts/UI/StudentLife/ExplorationChoicePanel.cs",
                "Assets/Scripts/UI/StudentLife/ExplorationInteractionPointInteractor.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("explorationId ==", source);
                StringAssert.DoesNotContain("choiceId ==", source);
                StringAssert.DoesNotContain("siteId ==", source);
                StringAssert.DoesNotContain("switch (explorationId", source);
                StringAssert.DoesNotContain("switch (choiceId", source);
                StringAssert.DoesNotContain("switch (siteId", source);
            }
        }

        private static void ConfigureItem(ItemDefinition item, string id, int maxStack)
        {
            var serialized = new SerializedObject(item);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayKey").stringValue = id;
            serialized.FindProperty("_maxStack").intValue = maxStack;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
