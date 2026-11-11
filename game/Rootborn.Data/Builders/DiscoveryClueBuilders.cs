using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.WorldState;

namespace Rootborn.Data.Builders
{
    /// <summary>발견 단서(출처, 조건, 완료, 결과)와 단서 해석(출처, 정책, 조건, 결과).</summary>
    internal static class DiscoveryClueBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            RegisterClues(b);
            RegisterInterpretations(b);
        }

        private static void RegisterClues(DefinitionBuilders b)
        {
            b.Add("DiscoveryClueSourceDefinition", (n, c) => new DiscoveryClueSource(
                n.Id, n.Enum<DiscoveryClueSourceKind>("kind"), n.Str("displayNameKey"), n.Str("hintTextKey"),
                n.RefId("relatedNpc"), c.One<WorldStateFlag>(n, "relatedWorldState"), n.Int("strength")));

            b.Add("DiscoveryClueDefinition", (n, c) => new DiscoveryClue(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), c.One<WorldStateFlag>(n, "relatedWorldState"),
                n.RefId("relatedQuest"), n.RefId("relatedLocation"), n.Int("strength"),
                n.Str("publicTextKey"), n.Str("hiddenTextKey"),
                c.All<DiscoveryClueSource>(n, "sources").ToArray(),
                c.All<IDiscoveryClueCondition>(n, "conditions").ToArray(),
                c.All<IDiscoveryClueCompletion>(n, "completions").ToArray(),
                c.All<IDiscoveryClueOutcome>(n, "outcomes").ToArray(),
                BuilderHelpers.EnumArray<DiscoveryClueSummarySurface>(n, "visibleSurfaces"),
                n.Int("sortPriority")));

            b.Add("DiscoveryClueWorldStateActiveCondition", (n, c) => new DiscoveryClueWorldStateActiveCondition());
            b.Add("DiscoveryClueLocationVisitCompletion", (n, c) => new DiscoveryClueLocationVisitCompletion(n.Str("locationId")));
            b.Add("DiscoveryClueEncyclopediaOutcome", (n, c) => new DiscoveryClueEncyclopediaOutcome(c.All<EncyclopediaEntry>(n, "entries").ToArray()));
            b.Add("DiscoveryClueWorldStateRevealOutcome", (n, c) => new DiscoveryClueWorldStateRevealOutcome(c.All<WorldStateFlag>(n, "flags").ToArray()));
            b.Add("DiscoveryClueCareerHintOutcome", (n, c) => new DiscoveryClueCareerHintOutcome(n.RefIds("careers")));
            b.Add("DiscoveryClueQuestUnlockOutcome", (n, c) => new DiscoveryClueQuestUnlockOutcome(c.All<QuestDefinition>(n, "quests").ToArray()));
        }

        private static void RegisterInterpretations(DefinitionBuilders b)
        {
            b.Add("ClueInterpretationSourceDefinition", (n, c) => new ClueInterpretationSource(
                n.Id, n.Enum<ClueInterpretationSourceKind>("kind"), n.Str("targetId"), n.Str("displayNameKey"), n.Str("hintTextKey"), n.Int("strength")));
            b.Add("ClueInterpretationPolicyDefinition", (n, c) => new ClueInterpretationPolicy(
                n.Id, n.Enum<ClueInterpretationPolicyKind>("kind"), n.Str("groupId"), n.Int("sequenceOrder"), n.Int("cooldownDays"), n.Bool("consumeItem")));

            b.Add("ClueInterpretationDefinition", (n, c) => new ClueInterpretation(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.RefId("clue"), n.Str("methodKey"),
                n.Str("publicTextKey"), n.Str("hiddenTextKey"),
                c.All<ClueInterpretationSource>(n, "sources").ToArray(),
                c.All<IClueInterpretationCondition>(n, "conditions").ToArray(),
                c.All<IClueInterpretationOutcome>(n, "outcomes").ToArray(),
                c.One<ClueInterpretationPolicy>(n, "policy"),
                n.Int("sortPriority")));

            // 조건은 모두 lockedReasonKey를 가진다(Unity ClueInterpretationConditionBase).
            b.Add("ClueInterpretationClueSeenCondition", (n, c) => new ClueInterpretationClueSeenCondition(n.Str("lockedReasonKey")));
            b.Add("ClueInterpretationRelationshipThresholdCondition", (n, c) => new ClueInterpretationRelationshipThresholdCondition(
                n.RefId("relationship"), n.Int("minimumValue"), n.Str("lockedReasonKey")));
            b.Add("ClueInterpretationWorldStateActiveCondition", (n, c) => new ClueInterpretationWorldStateActiveCondition(
                c.One<WorldStateFlag>(n, "flag"), n.Str("lockedReasonKey")));

            b.Add("ClueInterpretationCareerHintOutcome", (n, c) => new ClueInterpretationCareerHintOutcome(n.RefIds("careers")));
            b.Add("ClueInterpretationEncyclopediaOutcome", (n, c) => new ClueInterpretationEncyclopediaOutcome(c.All<EncyclopediaEntry>(n, "entries").ToArray()));
            b.Add("ClueInterpretationQuestUnlockOutcome", (n, c) => new ClueInterpretationQuestUnlockOutcome(c.All<QuestDefinition>(n, "quests").ToArray()));
            b.Add("ClueInterpretationRelationshipDeltaOutcome", (n, c) => new ClueInterpretationRelationshipDeltaOutcome(n.RefId("relationship"), n.Int("delta")));
            b.Add("ClueInterpretationStatusDeltaOutcome", (n, c) => new ClueInterpretationStatusDeltaOutcome(n.RefId("status"), n.Int("delta")));
            b.Add("ClueInterpretationWorldStateOutcome", (n, c) => new ClueInterpretationWorldStateOutcome(c.All<WorldStateFlag>(n, "flags").ToArray()));
        }
    }
}
