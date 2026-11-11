using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.WorldState;

namespace Rootborn.Data.Builders
{
    internal static class QuestBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("QuestDefinition", (n, c) => new QuestDefinition(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"),
                c.All<QuestObjective>(n, "objectives"),
                c.All<QuestReward>(n, "rewards"),
                c.All<QuestCompletionEffect>(n, "completionEffects"),
                c.All<QuestCondition>(n, "providerConditions"),
                c.All<QuestCondition>(n, "prerequisites")));

            // startNpc는 NPC 정의(아직 Core에 없음)를 id로만 가리킨다.
            b.Add("QuestChainDefinition", BuilderHelpers.AddedLater(new[] { "failureConditions", "expirationConditions", "startNpc" }, (n, c) => new QuestChainDefinition(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.Str("relatedCareerInterestId"),
                c.All<QuestStepDefinition>(n, "steps"),
                c.All<QuestCondition>(n, "startConditions"),
                c.All<QuestCondition>(n, "unblockConditions"),
                c.All<QuestCondition>(n, "blockConditions"),
                c.All<QuestReward>(n, "completionRewards"),
                n.Int("sortPriority"),
                c.All<QuestCondition>(n, "failureConditions"),
                c.All<QuestCondition>(n, "expirationConditions"),
                n.RefId("startNpc"),
                n.Bool("developerOnly"))));

            b.Add("QuestStepDefinition", BuilderHelpers.AddedLater(new[] { "legacyStepIds" }, (n, c) => new QuestStepDefinition(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"),
                c.All<QuestObjective>(n, "objectives"),
                c.All<QuestCondition>(n, "completionConditions"),
                c.All<QuestCondition>(n, "blockConditions"),
                c.All<QuestReward>(n, "rewards"),
                c.All<QuestChainTransition>(n, "transitions"),
                n.Strings("legacyStepIds"))));

            // 목표. Unity QuestObjectiveBase.RequiredCount는 1 이상으로 다듬는다.
            b.Add("AnyOfQuestObjective", (n, c) => new AnyOfQuestObjective(n.Str("displayKey"), Count(n), c.All<QuestObjective>(n, "alternatives")));
            b.Add("GatherQuestObjective", (n, c) => new GatherQuestObjective(n.Str("displayKey"), Count(n), n.RefId("targetResource")));
            b.Add("TalkQuestObjective", (n, c) => new TalkQuestObjective(n.Str("displayKey"), Count(n), n.RefId("targetNpc")));
            b.Add("LocationVisitQuestObjective", (n, c) => new LocationVisitQuestObjective(n.Str("displayKey"), Count(n), n.RefId("targetLocation")));
            b.Add("LocationActivityQuestObjective", (n, c) => new LocationActivityQuestObjective(n.Str("displayKey"), Count(n), n.RefId("targetActivity")));
            b.Add("PartTimeWorkQuestObjective", (n, c) => new PartTimeWorkQuestObjective(n.Str("displayKey"), Count(n), n.RefIds("acceptedWorks")));
            b.Add("FurniturePlacedQuestObjective", (n, c) => new FurniturePlacedQuestObjective(
                n.Str("displayKey"), Count(n), n.RefIds("acceptedFurniture"), n.Str("requiredSurfaceId")));
            b.Add("DailyPlanSelectedQuestObjective", (n, c) => new DailyPlanSelectedQuestObjective(n.Str("displayKey"), Count(n)));
            b.Add("CareerInterestSelectedQuestObjective", (n, c) => new CareerInterestSelectedQuestObjective(
                n.Str("displayKey"), Count(n), n.RefIds("acceptedInterests")));

            // 보상과 완료 효과.
            b.Add("ItemQuestReward", (n, c) => new ItemQuestReward(c.One<ItemDefinition>(n, "item"), Math.Max(1, n.Int("count"))));
            b.Add("FurnitureUnlockQuestReward", (n, c) => new FurnitureUnlockQuestReward(n.Str("unlockToken")));
            b.Add("WorldStateChangeQuestReward", (n, c) => new WorldStateChangeQuestReward(
                WorldStateFlags(n, c), n.Str("sourceQuestChainId"), n.Str("sourceQuestStepId"), n.Str("sourceEventId"), n.Int("activationDay")));
            b.Add("SetStoryFlagCompletionEffect", (n, c) => new SetStoryFlagCompletionEffect(n.RefId("flag")));
            b.Add("TraitDeltaCompletionEffect", (n, c) => new TraitDeltaCompletionEffect(n.RefId("trait"), n.Int("delta"), n.RefId("careerHint")));

            // 조건. name은 Unity 에셋 이름(BlockedSummary의 기본값).
            b.Add("DayAtLeastCondition", (n, c) => new DayAtLeastCondition(n.Int("minimumDay"), n.Str("blockedSummaryKey"), n.AssetName));
            b.Add("LocationActivityCompletedCondition", (n, c) => new LocationActivityCompletedCondition(
                n.RefId("activity"), n.Str("blockedReasonId"), n.Str("blockedSummary"), n.AssetName));
        }

        private static int Count(NodeReader n) => Math.Max(1, n.Int("requiredCount"));

        /// <summary>
        /// 퀘스트 보상은 월드 상태 깃발의 id와 효과 버전만 쓴다(Quests는 WorldState 영역을 참조하지 않는다).
        /// 효과 버전은 WorldStateFlag가 1 이상으로 다듬은 값이다.
        /// </summary>
        private static List<QuestWorldStateFlag> WorldStateFlags(NodeReader n, BuildContext c)
        {
            var flags = new List<QuestWorldStateFlag>();
            foreach (var flag in c.All<WorldStateFlag>(n, "flags")) flags.Add(new QuestWorldStateFlag(flag.Id, flag.EffectVersion));
            return flags;
        }
    }
}
