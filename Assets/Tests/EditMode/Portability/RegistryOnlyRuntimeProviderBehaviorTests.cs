using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Portability
{
    public sealed class RegistryOnlyRuntimeProviderBehaviorTests
    {
        [Test]
        public void PORTABILITY_REGISTRY_004_TestOnlyExplorationDataIsReturnedFromRegistryFields()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var interaction = ScriptableObject.CreateInstance<ExplorationInteractionDefinition>();
            var choice = ScriptableObject.CreateInstance<ExplorationChoiceDefinition>();
            var riskPolicy = ScriptableObject.CreateInstance<ExplorationRiskPolicyDefinition>();
            try
            {
                Set(registry, "_explorationInteractions", new[] { interaction });
                Set(registry, "_explorationChoices", new[] { choice });
                Set(registry, "_explorationRiskPolicies", new[] { riskPolicy });

                Assert.AreSame(interaction, registry.GetExplorationInteractions()[0]);
                Assert.AreSame(choice, registry.GetExplorationChoices()[0]);
                Assert.AreSame(riskPolicy, registry.GetExplorationRiskPolicies()[0]);
            }
            finally
            {
                Destroy(registry, interaction, choice, riskPolicy);
            }
        }

        [Test]
        public void PORTABILITY_REGISTRY_005_TestOnlyLocationStateDataIsReturnedFromRegistryFields()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var state = ScriptableObject.CreateInstance<LocationStateDefinition>();
            var policy = ScriptableObject.CreateInstance<LocationStateConflictPolicyDefinition>();
            try
            {
                Set(registry, "_locationStates", new[] { state });
                Set(registry, "_locationStateConflictPolicies", new[] { policy });

                Assert.AreSame(state, GameDataRegistryLocationStateExtensions.GetLocationStates(registry)[0]);
                Assert.AreSame(policy, GameDataRegistryLocationStateExtensions.GetLocationStateConflictPolicies(registry)[0]);
            }
            finally
            {
                Destroy(registry, state, policy);
            }
        }

        [Test]
        public void PORTABILITY_REGISTRY_006_TestOnlyWorldStateUsageDataIsReturnedFromRegistryFields()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var usage = ScriptableObject.CreateInstance<WorldStateUsageDefinition>();
            var condition = ScriptableObject.CreateInstance<WorldStateUsageFlagActiveCondition>();
            var outcome = ScriptableObject.CreateInstance<WorldStateUsageQuestUnlockOutcome>();
            var policy = ScriptableObject.CreateInstance<WorldStateUsageRepeatPolicyDefinition>();
            try
            {
                Set(registry, "_worldStateUsages", new[] { usage });
                Set(registry, "_worldStateUsageConditions", new WorldStateUsageConditionBase[] { condition });
                Set(registry, "_worldStateUsageOutcomes", new WorldStateUsageOutcomeBase[] { outcome });
                Set(registry, "_worldStateUsageRepeatPolicies", new[] { policy });

                Assert.AreSame(usage, GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsages(registry)[0]);
                Assert.AreSame(condition, GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsageConditions(registry)[0]);
                Assert.AreSame(outcome, GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsageOutcomes(registry)[0]);
                Assert.AreSame(policy, GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsageRepeatPolicies(registry)[0]);
            }
            finally
            {
                Destroy(registry, usage, condition, outcome, policy);
            }
        }

        [Test]
        public void PORTABILITY_REGISTRY_007_TestOnlyDiscoveryClueDataIsReturnedFromRegistryFields()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var clue = ScriptableObject.CreateInstance<DiscoveryClueDefinition>();
            var source = ScriptableObject.CreateInstance<DiscoveryClueSourceDefinition>();
            var condition = ScriptableObject.CreateInstance<DiscoveryClueWorldStateActiveCondition>();
            var completion = ScriptableObject.CreateInstance<DiscoveryClueLocationVisitCompletion>();
            var outcome = ScriptableObject.CreateInstance<DiscoveryClueQuestUnlockOutcome>();
            try
            {
                Set(registry, "_discoveryClues", new[] { clue });
                Set(registry, "_discoveryClueSources", new[] { source });
                Set(registry, "_discoveryClueConditions", new DiscoveryClueConditionBase[] { condition });
                Set(registry, "_discoveryClueCompletions", new DiscoveryClueCompletionBase[] { completion });
                Set(registry, "_discoveryClueOutcomes", new DiscoveryClueOutcomeBase[] { outcome });

                Assert.AreSame(clue, GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClues(registry)[0]);
                Assert.AreSame(source, GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueSources(registry)[0]);
                Assert.AreSame(condition, GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueConditions(registry)[0]);
                Assert.AreSame(completion, GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueCompletions(registry)[0]);
                Assert.AreSame(outcome, GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueOutcomes(registry)[0]);
            }
            finally
            {
                Destroy(registry, clue, source, condition, completion, outcome);
            }
        }

        [Test]
        public void PORTABILITY_REGISTRY_008_TestOnlyClueInterpretationDataIsReturnedFromRegistryFields()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var interpretation = ScriptableObject.CreateInstance<ClueInterpretationDefinition>();
            var source = ScriptableObject.CreateInstance<ClueInterpretationSourceDefinition>();
            var condition = ScriptableObject.CreateInstance<ClueInterpretationWorldStateActiveCondition>();
            var outcome = ScriptableObject.CreateInstance<ClueInterpretationQuestUnlockOutcome>();
            var policy = ScriptableObject.CreateInstance<ClueInterpretationPolicyDefinition>();
            try
            {
                Set(registry, "_clueInterpretations", new[] { interpretation });
                Set(registry, "_clueInterpretationSources", new[] { source });
                Set(registry, "_clueInterpretationConditions", new ClueInterpretationConditionBase[] { condition });
                Set(registry, "_clueInterpretationOutcomes", new ClueInterpretationOutcomeBase[] { outcome });
                Set(registry, "_clueInterpretationPolicies", new[] { policy });

                Assert.AreSame(interpretation, GameDataRegistryClueInterpretationExtensions.GetClueInterpretations(registry)[0]);
                Assert.AreSame(source, GameDataRegistryClueInterpretationExtensions.GetClueInterpretationSources(registry)[0]);
                Assert.AreSame(condition, GameDataRegistryClueInterpretationExtensions.GetClueInterpretationConditions(registry)[0]);
                Assert.AreSame(outcome, GameDataRegistryClueInterpretationExtensions.GetClueInterpretationOutcomes(registry)[0]);
                Assert.AreSame(policy, GameDataRegistryClueInterpretationExtensions.GetClueInterpretationPolicies(registry)[0]);
            }
            finally
            {
                Destroy(registry, interpretation, source, condition, outcome, policy);
            }
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            }
        }
    }
}
