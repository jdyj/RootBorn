using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>학교 밖 활동, 마을 돕기, 알바(일터와 일).</summary>
    internal static class TownLifeBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("OutsideSchoolActivityDefinition", (n, c) => new OutsideSchoolActivity(
                n.Id, n.Str("displayNameKey"), n.RefId("category"), c.One<ActivityTimePolicy>(n, "timePolicy"),
                Math.Max(0, n.Int("timeCostMinutes")), Math.Max(0, n.Int("energyCost")), Math.Max(0, n.Int("focusCost")),
                n.Int("stressDelta"),
                c.All<IOutsideSchoolRequirement>(n, "requirements").ToArray(),
                c.All<IOutsideSchoolOutcome>(n, "outcomes").ToArray()));
            b.Add("OutsideSchoolTraitDeltaOutcome", (n, c) => new OutsideSchoolTraitDeltaOutcome(n.RefId("trait"), n.Int("delta")));
            b.Add("OutsideSchoolSkillDeltaOutcome", (n, c) => new OutsideSchoolSkillDeltaOutcome(n.RefId("skill"), n.Int("delta")));

            b.Add("TownHelpActionDefinition", (n, c) => new TownHelpAction(
                n.Id, n.Str("displayNameKey"), n.Str("npcId"), n.Str("locationId"), c.One<ActivityTimePolicy>(n, "timePolicy"),
                Math.Max(0, n.Int("timeCostMinutes")), Math.Max(0, n.Int("energyCost")), Math.Max(0, n.Int("focusCost")),
                n.Int("stressDelta"),
                c.All<IHelpActionRequirement>(n, "requirements").ToArray(),
                c.All<IHelpActionOutcome>(n, "outcomes").ToArray()));
            b.Add("HelpActionTraitDeltaOutcome", (n, c) => new HelpActionTraitDeltaOutcome(n.RefId("trait"), n.Int("delta")));
            b.Add("HelpActionRelationshipDeltaOutcome", (n, c) => new HelpActionRelationshipDeltaOutcome(n.RefId("relationship"), n.Int("delta")));

            b.Add("WorkplaceDefinition", (n, c) => new Workplace(
                n.Id, n.Str("displayNameKey"), n.Str("locationId"), n.Str("npcId"), n.RefIds("availableWorks")));
            b.Add("PartTimeWorkDefinition", (n, c) => new PartTimeWork(
                n.Id, n.Str("displayNameKey"), c.One<Minigame>(n, "minigame"), c.One<Workplace>(n, "workplace"),
                c.One<ActivityTimePolicy>(n, "timePolicy"),
                Math.Max(0, n.Int("timeCostMinutes")), Math.Max(0, n.Int("energyCost")), Math.Max(0, n.Int("focusCost")),
                n.Int("stressDelta"), Rewards(n, c),
                c.All<IWorkRequirement>(n, "requirements").ToArray(),
                c.All<IWorkOutcome>(n, "outcomes").ToArray()));
            b.Add("WorkTraitDeltaOutcome", (n, c) => new WorkTraitDeltaOutcome(n.RefId("trait"), n.Int("delta")));
        }

        /// <summary>Unity PartTimeWorkDefinition과 같게: 개수가 모자라면 1개, 빈 아이템이나 0 이하는 빼고 짝짓는다.</summary>
        private static InventoryGrant[] Rewards(NodeReader n, BuildContext c)
        {
            var items = c.Slots<ItemDefinition>(n, "rewardItems");
            var counts = n.IntArray("rewardCounts");
            var rewards = new List<InventoryGrant>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var count = i < counts.Length ? counts[i] : 1;
                if (items[i] != null && count > 0) rewards.Add(new InventoryGrant(items[i], count));
            }

            return rewards.ToArray();
        }
    }
}
