using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Inventory;

namespace Rootborn.Data.Builders
{
    /// <summary>도감 분류, 항목, 해금 조건.</summary>
    internal static class EncyclopediaBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("EncyclopediaCategoryDefinition", (n, c) => new EncyclopediaCategory(n.Id, n.Str("displayName"), n.Int("sortOrder")));

            b.Add("EncyclopediaEntryDefinition", (n, c) => new EncyclopediaEntry(
                n.Id, c.One<EncyclopediaCategory>(n, "category"), n.Str("displayName"), n.Str("lockedDisplayName"),
                n.Str("description"), n.Str("lockedHint"), n.Strings("detailLines"),
                c.All<IEncyclopediaUnlockCondition>(n, "unlockConditions").ToArray(),
                n.Int("revealStage")));

            // 해금 조건은 대상을 id로 가진다. data/에는 발견 완료만 있다. 나머지 필드 이름은 Unity EncyclopediaDefinitions.cs.
            b.Add("EncyclopediaDiscoveryCompletedCondition", (n, c) => new EncyclopediaDiscoveryCompletedCondition(n.RefId("discovery")));
            b.Add("EncyclopediaLocationUnlockedCondition", (n, c) => new EncyclopediaLocationUnlockedCondition(n.RefId("location")));
            b.Add("EncyclopediaNpcMetCondition", (n, c) => new EncyclopediaNpcMetCondition(n.RefId("npc")));
            b.Add("EncyclopediaActivityCompletedCondition", (n, c) => new EncyclopediaActivityCompletedCondition(n.RefId("activity")));
            b.Add("EncyclopediaCareerHintUnlockedCondition", (n, c) => new EncyclopediaCareerHintUnlockedCondition(n.RefId("career")));
            b.Add("EncyclopediaKnowledgeUnlockedCondition", (n, c) => new EncyclopediaKnowledgeUnlockedCondition(n.RefId("knowledge")));
            b.Add("EncyclopediaItemPossessedCondition", (n, c) => new EncyclopediaItemPossessedCondition(c.One<ItemDefinition>(n, "item")));
        }
    }
}
