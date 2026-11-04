using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    // Staged domain/source tests only. No scene loading, persistence I/O or global save override.
    // The proposed API is deliberately reached through its serialized field so RED compiles before implementation.
    public sealed class ExplorationDismissChoiceTests
    {
        private const string LeavePath = "Assets/Data/StudentLife/ExplorationInteractions/Choices/Choice_Exploration_TestLeave.asset";
        private const string Flag = "_dismissesInteraction";
        private readonly List<ScriptableObject> _owned = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [Test]
        public void AuthoredLeaveAloneExplicitlySerializesDismissalWithoutRiskOrOutcomeData()
        {
            var leave = AssetDatabase.LoadAssetAtPath<ExplorationChoiceDefinition>(LeavePath);
            Assert.That(leave, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(LeavePath), Is.EqualTo("2650a04f8aebf9c4f8f865947e8b6ac6"));
            Assert.That(SerializedDismisses(leave), Is.True, "The actual authored Leave must opt in; a runtime-only flag does not fix the saved source.");
            string yaml = File.ReadAllText(LeavePath).Replace("\r\n", "\n");
            Assert.That(Regex.Matches(yaml, @"(?m)^  _dismissesInteraction: 1$").Count, Is.EqualTo(1));
            Assert.That(leave.Id, Is.EqualTo("choice.test.leave"));
            Assert.That(leave.Conditions, Is.Empty);
            Assert.That(leave.Outcomes, Is.Empty);
            Assert.That(leave.RiskPolicy, Is.Null);
            Assert.That(leave.Repeatable, Is.True);
            var enabled = AssetDatabase.FindAssets("t:ExplorationChoiceDefinition", new[] { "Assets/Data" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => SerializedDismisses(AssetDatabase.LoadAssetAtPath<ExplorationChoiceDefinition>(path)))
                .ToArray();
            Assert.That(enabled, Is.EqualTo(new[] { LeavePath }), "Only the existing Leave SO is authored as dismissing in this change.");
        }

        [Test]
        public void OmittedFlagDefaultsFalseAndPreviouslySerializedResultOrdinalsRemainStable()
        {
            // This existing eight-argument call must remain source-compatible with an optional final bool.
            var choice = Choice("fixture.default");
            Assert.That(SerializedDismisses(choice), Is.False);
            SetDismisses(choice);
            choice.ConfigureForTests("fixture.reset-default", "Reset", "Reset", Array.Empty<ExplorationConditionBase>(),
                Array.Empty<ExplorationOutcomeBase>(), null, false, "Reset preview");
            Assert.That(SerializedDismisses(choice), Is.False, "Omitting the optional bool must reset a reused test definition to its normal default.");
            Assert.That((int)ExplorationChoiceResultKind.Applied, Is.EqualTo(0));
            Assert.That((int)ExplorationChoiceResultKind.InvalidRequest, Is.EqualTo(1));
            Assert.That((int)ExplorationChoiceResultKind.ConditionFailed, Is.EqualTo(2));
            Assert.That((int)ExplorationChoiceResultKind.DuplicateRequest, Is.EqualTo(3));
            Assert.That((int)ExplorationChoiceResultKind.CooldownActive, Is.EqualTo(4));
            Assert.That((int)ExplorationChoiceResultKind.InsufficientResources, Is.EqualTo(5));
            Assert.That(Enum.GetNames(typeof(ExplorationChoiceResultKind)), Does.Contain("Dismissed"));
            Assert.That((int)Enum.Parse(typeof(ExplorationChoiceResultKind), "Dismissed"), Is.EqualTo(6));
        }

        [Test]
        public void DismissAtZeroEnergyBypassesDefaultRiskAndCreatesNoRecordActivityOrOutcome()
        {
            var dismiss = Choice("fixture.any-dismiss-name");
            SetDismisses(dismiss);
            var interaction = Interaction("fixture.generic-site", dismiss, Risk(3, 4, 2));
            var context = Context(0, 0);
            string before = Snapshot(context);
            int vitals = 0, inventoryEvents = 0;
            context.StudentProgress.VitalsChanged += () => vitals++;
            context.Inventory.OnChanged += () => inventoryEvents++;

            bool handled = new ExplorationInteractionRunner().TryChoose(interaction, dismiss, context, out var result);

            Assert.That(handled, Is.True);
            AssertDismissed(result, interaction.Id, dismiss.Id);
            Assert.That(Snapshot(context), Is.EqualTo(before));
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty, "CanAttempt/GetRecord must not run before a pure dismissal.");
            Assert.That(vitals, Is.Zero);
            Assert.That(inventoryEvents, Is.Zero);
        }

        [Test]
        public void RepeatedDismissalStillWorksAfterCompletionAndDuringCooldownWithoutChangingPriorRecords()
        {
            // Cover both normal blockers without changing their normal-action behavior.
            foreach (bool cooldown in new[] { false, true })
            {
                var dismiss = Choice(cooldown ? "fixture.dismiss.cooldown" : "fixture.dismiss.completed");
                SetDismisses(dismiss); // Choice.Repeatable remains false; it cannot block cancellation.
                var interaction = Interaction(cooldown ? "fixture.cooldown" : "fixture.once", dismiss,
                    Risk(1, 1, 1), repeatable: cooldown, cooldownDays: cooldown ? 5 : 0);
                var context = Context(0, 0, day: 3);
                context.ExplorationProgress.MarkCompleted(interaction.Id, "fixture.previously-earned", 3, 450, new ExplorationOutcomeCollector());
                string before = Snapshot(context);
                var runner = new ExplorationInteractionRunner();
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Assert.That(runner.TryChoose(interaction, dismiss, context, out var result), Is.True);
                    AssertDismissed(result, interaction.Id, dismiss.Id);
                    Assert.That(Snapshot(context), Is.EqualTo(before));
                }
                var record = context.ExplorationProgress.ToSaveData().Records.Single();
                Assert.That(record.RepeatCount, Is.EqualTo(1));
                Assert.That(record.SelectedChoiceIds, Is.EqualTo(new[] { "fixture.previously-earned" }));
            }
        }

        [Test]
        public void DismissalStillHonorsAnAuthoredTraitConditionWithoutMutatingRejectedOrAcceptedState()
        {
            var trait = Own<TraitDefinition>();
            trait.ConfigureForTests("fixture.trait", "Fixture trait");
            var condition = Own<ExplorationTraitThresholdCondition>();
            condition.ConfigureForTests(trait, 2, "fixture.requires-trait");
            var dismiss = Choice("fixture.conditioned", new ExplorationConditionBase[] { condition });
            SetDismisses(dismiss);
            var interaction = Interaction("fixture.conditioned-site", dismiss, Risk(1, 1, 0));
            var context = Context(0, 0);
            var runner = new ExplorationInteractionRunner();
            string beforeRejected = Snapshot(context);

            Assert.That(runner.TryChoose(interaction, dismiss, context, out var rejected), Is.False);
            Assert.That(rejected.Kind, Is.EqualTo(ExplorationChoiceResultKind.ConditionFailed));
            Assert.That(rejected.LockedReasonKey, Is.EqualTo("fixture.requires-trait"));
            AssertNoOutcomes(rejected);
            Assert.That(Snapshot(context), Is.EqualTo(beforeRejected));

            context.StudentProgress.AddTrait(trait, 2); // Transient unit setup only; no saved progression is modified.
            string beforeAccepted = Snapshot(context);
            Assert.That(runner.TryChoose(interaction, dismiss, context, out var accepted), Is.True);
            AssertDismissed(accepted, interaction.Id, dismiss.Id);
            Assert.That(Snapshot(context), Is.EqualTo(beforeAccepted));
        }

        [Test]
        public void MissingPreviousChoicePrerequisiteDoesNotCreateARecordAndEarnedPrerequisiteStillAllowsDismissal()
        {
            var priorChoice = Choice("fixture.prerequisite-choice");
            var priorSite = Interaction("fixture.prerequisite-site", priorChoice);
            var condition = Own<ExplorationPreviousChoiceCondition>();
            condition.ConfigureForTests(priorSite, priorChoice, "fixture.requires-previous");
            var dismiss = Choice("fixture.previous-conditioned", new ExplorationConditionBase[] { condition });
            SetDismisses(dismiss);
            var interaction = Interaction("fixture.current-site", dismiss);
            var context = Context(0, 0);
            var runner = new ExplorationInteractionRunner();
            string missing = Snapshot(context);

            Assert.That(runner.TryChoose(interaction, dismiss, context, out var rejected), Is.False);
            Assert.That(rejected.Kind, Is.EqualTo(ExplorationChoiceResultKind.ConditionFailed));
            Assert.That(rejected.LockedReasonKey, Is.EqualTo("fixture.requires-previous"));
            Assert.That(Snapshot(context), Is.EqualTo(missing), "Condition queries must use a non-creating record lookup.");
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);

            context.ExplorationProgress.MarkCompleted(priorSite.Id, priorChoice.Id, 1, 420, new ExplorationOutcomeCollector());
            string earned = Snapshot(context);
            Assert.That(runner.TryChoose(interaction, dismiss, context, out var result), Is.True);
            AssertDismissed(result, interaction.Id, dismiss.Id);
            Assert.That(Snapshot(context), Is.EqualTo(earned));
        }

        [Test]
        public void SummaryQueryDoesNotCreateRecordsAndStillReportsExistingCompletion()
        {
            var choice = Choice("fixture.summary-choice");
            var interaction = Interaction("fixture.summary-site", choice, Risk(1, 5, 5));
            var context = Context(0, 0);
            var cache = new ExplorationLookupCache(new[] { interaction });
            string before = Snapshot(context);

            var summary = ExplorationSummaryBuilder.Build(interaction.Id, cache, context.ExplorationProgress,
                context.StudentProgress, context.Inventory, context.QuestLog, context.DiscoveryClueProgress, 1, 480);

            Assert.That(summary.Completed, Is.False);
            Assert.That(summary.Choices.Single().Available, Is.True, "Summary availability remains condition-only; no risk/resource redesign.");
            Assert.That(Snapshot(context), Is.EqualTo(before));
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);
            context.ExplorationProgress.MarkCompleted(interaction.Id, choice.Id, 1, 480, new ExplorationOutcomeCollector());
            string completed = Snapshot(context);
            var after = ExplorationSummaryBuilder.Build(interaction.Id, cache, context.ExplorationProgress,
                context.StudentProgress, context.Inventory, context.QuestLog, context.DiscoveryClueProgress, 1, 480);
            Assert.That(after.Completed, Is.True);
            Assert.That(cache.LookupCount, Is.EqualTo(2));
            Assert.That(Snapshot(context), Is.EqualTo(completed));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MisconfiguredDismissalWithItsOwnRiskOrOutcomeIsRejectedWithoutDiscardingAuthoredEffects(bool ownRisk)
        {
            var trait = Own<TraitDefinition>();
            trait.ConfigureForTests("fixture.reward-trait", "Reward trait");
            var outcome = Own<ExplorationTraitDeltaOutcome>();
            outcome.ConfigureForTests(trait, 3);
            var dismiss = Choice("fixture.misconfigured", outcomes: ownRisk ? null : new ExplorationOutcomeBase[] { outcome },
                risk: ownRisk ? Risk(2, 1, 1) : null);
            SetDismisses(dismiss);
            var interaction = Interaction("fixture.invalid-dismiss", dismiss);
            var context = Context(10, 10);
            string before = Snapshot(context);

            Assert.That(new ExplorationInteractionRunner().TryChoose(interaction, dismiss, context, out var result), Is.False);

            Assert.That(result.Kind, Is.EqualTo(ExplorationChoiceResultKind.InvalidRequest));
            AssertNoOutcomes(result);
            Assert.That(Snapshot(context), Is.EqualTo(before));
            Assert.That(context.ExplorationProgress.ToSaveData().Records, Is.Empty);
        }

        [Test]
        public void OrdinaryChoiceStillPaysFallbackRiskAppliesOutcomeRecordsActivityAndDeduplicates()
        {
            var trait = Own<TraitDefinition>();
            trait.ConfigureForTests("fixture.normal-trait", "Normal trait");
            var outcome = Own<ExplorationTraitDeltaOutcome>();
            outcome.ConfigureForTests(trait, 3);
            var choice = Choice("fixture.normal-choice", outcomes: new ExplorationOutcomeBase[] { outcome });
            var interaction = Interaction("fixture.normal-site", choice, Risk(2, 1, 1));
            var context = Context(10, 10);
            var runner = new ExplorationInteractionRunner();

            Assert.That(runner.TryChoose(interaction, choice, context, out var first), Is.True);
            Assert.That(first.Kind, Is.EqualTo(ExplorationChoiceResultKind.Applied));
            Assert.That(context.StudentProgress.Energy, Is.EqualTo(9));
            Assert.That(context.StudentProgress.Focus, Is.EqualTo(9));
            Assert.That(context.StudentProgress.Stress, Is.EqualTo(9)); // Context starts at7.
            Assert.That(context.StudentProgress.GetTraitValue(trait), Is.EqualTo(3));
            Assert.That(context.StudentProgress.GetTodayActivityIds(), Is.EqualTo(new[] { interaction.Id }));
            Assert.That(first.OutcomeIds, Does.Contain(outcome.OutcomeId));
            var saved = context.ExplorationProgress.ToSaveData().Records.Single();
            Assert.That(saved.Completed, Is.True);
            Assert.That(saved.RepeatCount, Is.EqualTo(1));
            Assert.That(saved.RewardClaimedChoiceIds, Is.EqualTo(new[] { choice.Id }));
            string applied = Snapshot(context);

            Assert.That(runner.TryChoose(interaction, choice, context, out var duplicate), Is.False);
            Assert.That(duplicate.Kind, Is.EqualTo(ExplorationChoiceResultKind.DuplicateRequest));
            Assert.That(Snapshot(context), Is.EqualTo(applied));
        }

        private T Own<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            _owned.Add(value);
            return value;
        }

        private ExplorationChoiceDefinition Choice(string id, ExplorationConditionBase[] conditions = null,
            ExplorationOutcomeBase[] outcomes = null, ExplorationRiskPolicyDefinition risk = null)
        {
            var value = Own<ExplorationChoiceDefinition>();
            value.ConfigureForTests(id, id, id + ".desc", conditions ?? Array.Empty<ExplorationConditionBase>(),
                outcomes ?? Array.Empty<ExplorationOutcomeBase>(), risk, false, id + ".preview");
            return value;
        }

        private ExplorationInteractionDefinition Interaction(string id, ExplorationChoiceDefinition choice,
            ExplorationRiskPolicyDefinition risk = null, bool repeatable = false, int cooldownDays = 0)
        {
            var value = Own<ExplorationInteractionDefinition>();
            value.ConfigureForTests(id, id, id + ".desc", Vector2.zero, 1f, repeatable, cooldownDays, risk, new[] { choice });
            return value;
        }

        private ExplorationRiskPolicyDefinition Risk(int stress, int energy, int focus)
        {
            var value = Own<ExplorationRiskPolicyDefinition>();
            value.ConfigureForTests(stress, energy, focus, null, null);
            return value;
        }

        private static ExplorationInteractionContext Context(int energy, int focus, int day = 1)
        {
            return new ExplorationInteractionContext(new ExplorationProgress("fixture-slot", "fixture-player"),
                new StudentLifeProgress("fixture-slot", "fixture-player", energy, focus, 7, 480), new Inventory(),
                new QuestLog(Array.Empty<QuestDefinition>()), new DiscoveryClueProgress("fixture-slot", "fixture-player"),
                new EncyclopediaProgress("fixture-slot", "fixture-player"), day, 480);
        }

        private static bool SerializedDismisses(ExplorationChoiceDefinition choice)
        {
            Assert.That(choice, Is.Not.Null);
            var data = new SerializedObject(choice);
            var property = data.FindProperty(Flag);
            Assert.That(property, Is.Not.Null, "Required serialized dismissal field is missing: " + Flag);
            Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.Boolean));
            return property.boolValue;
        }

        private static void SetDismisses(ExplorationChoiceDefinition choice)
        {
            Assert.That(AssetDatabase.GetAssetPath(choice), Is.Empty, "Only fixture-owned transient choices may be configured.");
            SerializedDismisses(choice); // Explicit red assertion; never turn a missing field into NullReferenceException.
            var data = new SerializedObject(choice);
            data.FindProperty(Flag).boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string Snapshot(in ExplorationInteractionContext context)
        {
            return string.Join("\n", JsonUtility.ToJson(context.StudentProgress.ToSaveData()),
                JsonUtility.ToJson(context.ExplorationProgress.ToSaveData()),
                string.Join("|", context.Inventory.Slots.Select(slot => (slot.Item != null ? slot.Item.Id : "<empty>") + ":" + slot.Count)),
                JsonUtility.ToJson(context.QuestLog.ToSaveData()), JsonUtility.ToJson(context.DiscoveryClueProgress.ToSaveData()),
                JsonUtility.ToJson(context.EncyclopediaProgress.ToSaveData()));
        }

        private static void AssertDismissed(ExplorationChoiceResult result, string interactionId, string choiceId)
        {
            Assert.That(result.Kind.ToString(), Is.EqualTo("Dismissed")); // Compiles before the enum member exists.
            Assert.That(result.InteractionId, Is.EqualTo(interactionId));
            Assert.That(result.ChoiceId, Is.EqualTo(choiceId));
            Assert.That(result.LockedReasonKey, Is.Empty);
            AssertNoOutcomes(result);
        }

        private static void AssertNoOutcomes(ExplorationChoiceResult result)
        {
            Assert.That(new[] { result.OutcomeIds, result.ItemGrantIds, result.ItemSpendIds, result.ClueIds,
                result.EncyclopediaEntryIds, result.CareerHintIds, result.FollowUpQuestIds, result.FatigueStatusIds }, Is.All.Empty);
        }
    }
}
