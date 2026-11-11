using System;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>탐색 상호작용·선택지·위험 정책·결과, 발견.</summary>
    internal static class ExplorationBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // worldPosition·interactionRadius는 맵 배치와 근접 판정이라 Host·월드 몫이다(Core가 받지 않음).
            b.Add("ExplorationInteractionDefinition", (n, c) => new ExplorationInteraction(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.Bool("repeatable"),
                Math.Max(0, n.Int("cooldownDays")), c.One<ExplorationRiskPolicy>(n, "defaultRiskPolicy"),
                c.All<ExplorationChoice>(n, "choices").ToArray()));
            b.Add("ExplorationChoiceDefinition", BuilderHelpers.AddedLater(new[] { "dismissesInteraction" }, (n, c) => new ExplorationChoice(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"),
                c.All<IExplorationCondition>(n, "conditions").ToArray(),
                c.All<IExplorationOutcome>(n, "outcomes").ToArray(),
                c.One<ExplorationRiskPolicy>(n, "riskPolicy"), n.Bool("repeatable"), n.Str("previewHintKey"), n.Bool("dismissesInteraction"))));
            b.Add("ExplorationRiskPolicyDefinition", (n, c) => new ExplorationRiskPolicy(
                n.Int("stressDelta"), Math.Max(0, n.Int("energyCost")), Math.Max(0, n.Int("focusCost")),
                n.RefId("status"), n.Int("statusDelta")));

            b.Add("ExplorationTraitThresholdCondition", (n, c) => new ExplorationTraitThresholdCondition(
                n.RefId("trait"), n.Int("minimum"), n.Str("lockedReasonKey")));
            b.Add("ExplorationTraitDeltaOutcome", (n, c) => new ExplorationTraitDeltaOutcome(n.RefId("trait"), n.Int("delta")));
            b.Add("ExplorationCareerHintOutcome", (n, c) => new ExplorationCareerHintOutcome(n.RefId("career")));
            b.Add("ExplorationClueOutcome", (n, c) => new ExplorationClueOutcome(n.RefId("clue"), n.RefId("source")));
            // 도감 항목 정의는 아직 Core에 없다(core-world). 공개 단계만 들여다본다(Unity 게터처럼 1 이상).
            b.Add("ExplorationEncyclopediaOutcome", (n, c) =>
            {
                var stage = c.TryPeek(n, "entry", out var entry) ? Math.Max(1, entry.Int("revealStage")) : 1;
                return new ExplorationEncyclopediaOutcome(n.RefId("entry"), stage);
            });
            b.Add("ExplorationFollowUpQuestOutcome", (n, c) => new ExplorationFollowUpQuestOutcome(c.One<QuestDefinition>(n, "quest")));
            // allowDuplicate는 Core 생성자가 받지 않는다(Unity 원본도 Apply에서 쓰지 않았다).
            b.Add("ExplorationItemGrantOutcome", (n, c) => new ExplorationItemGrantOutcome(
                c.One<ItemDefinition>(n, "item"), Math.Max(1, n.Int("count"))));

            b.Add("DiscoveryDefinition", (n, c) => new Discovery(
                n.Id, n.Str("displayNameKey"), n.Enum<DiscoveryScope>("scope"),
                c.All<IDiscoveryRequirement>(n, "requirements").ToArray(),
                c.All<IDiscoveryOutcome>(n, "outcomes").ToArray()));
            b.Add("DiscoveryTraitDeltaOutcome", (n, c) => new DiscoveryTraitDeltaOutcome(n.RefId("trait"), n.Int("delta")));
        }
    }
}
