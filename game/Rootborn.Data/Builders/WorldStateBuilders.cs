using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.WorldState;

namespace Rootborn.Data.Builders
{
    /// <summary>월드 상태(깃발, 범위, 조건, 효과)와 월드 상태 사용(정의, 조건, 결과, 반복 정책).</summary>
    internal static class WorldStateBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("WorldStateScopeDefinition", (n, c) => new WorldStateScope(
                n.Id, n.Enum<WorldStateScopeKind>("kind"), n.Str("displayNameKey")));

            b.Add("WorldStateFlagDefinition", (n, c) => new WorldStateFlag(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.RefId("relatedLocation"),
                n.RefIds("relatedNpcs"), n.RefIds("relatedQuestChains"),
                n.Enum<WorldStateScopeKind>("scope"), n.Enum<WorldStateChangeKind>("changeKind"), n.Str("nextActionKey"),
                BuilderHelpers.EnumArray<WorldStateSummarySurface>(n, "visibleSurfaces"),
                BuilderHelpers.EnumArray<WorldStateBadgeKind>(n, "badges"),
                n.Int("sortPriority"), n.Int("effectVersion"),
                c.One<WorldStateScope>(n, "scopeDefinition")));

            // data/에는 아직 없지만 Core에 있는 조건과 효과. 필드 이름은 Unity WorldStateConditionBase.cs, WorldStateEffectBase.cs.
            b.Add("WorldStateFlagActiveCondition", (n, c) => new WorldStateFlagActiveCondition(n.RefId("flag"), n.Bool("expectedActive")));
            b.Add("WorldStateSetObjectActiveEffect", (n, c) => new WorldStateSetObjectActiveEffect(n.Bool("active")));

            b.Add("WorldStateUsageDefinition", (n, c) => new WorldStateUsage(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), c.One<WorldStateFlag>(n, "sourceFlag"), n.RefId("relatedLocation"),
                n.Str("interactionPromptKey"), n.Str("nextActionKey"),
                c.All<IWorldStateUsageCondition>(n, "conditions").ToArray(),
                c.All<IWorldStateUsageOutcome>(n, "outcomes").ToArray(),
                c.One<WorldStateUsageRepeatPolicy>(n, "repeatPolicy"),
                BuilderHelpers.EnumArray<WorldStateUsageSummarySurface>(n, "visibleSurfaces"),
                n.Int("sortPriority")));
            b.Add("WorldStateUsageRepeatPolicyDefinition", (n, c) => new WorldStateUsageRepeatPolicy(
                n.Enum<WorldStateUsageRepeatMode>("mode"), n.Int("cooldownDays"), n.Int("maxUses"), n.Bool("allowDuplicateRewards")));

            b.Add("WorldStateUsageFlagActiveCondition", (n, c) => new WorldStateUsageFlagActiveCondition());
            b.Add("WorldStateUsageActivityUnlockOutcome", (n, c) => new WorldStateUsageActivityUnlockOutcome(n.RefIds("activities")));
            b.Add("WorldStateUsageCareerHintOutcome", (n, c) => new WorldStateUsageCareerHintOutcome(n.RefIds("careers")));
            b.Add("WorldStateUsageEncyclopediaOutcome", (n, c) => new WorldStateUsageEncyclopediaOutcome(
                c.All<EncyclopediaEntry>(n, "entries").ToArray()));
            b.Add("WorldStateUsageQuestUnlockOutcome", (n, c) => new WorldStateUsageQuestUnlockOutcome(
                c.All<QuestDefinition>(n, "quests").ToArray()));
        }
    }
}
