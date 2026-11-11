using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>마일스톤, 마일스톤 경로, 마일스톤 목표.</summary>
    internal static class MilestoneBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("MilestoneDefinition", BuilderHelpers.AddedLater(new[] { "prerequisiteMilestones" }, (n, c) => new Milestone(
                n.Id, n.Str("displayNameKey"),
                c.All<MilestoneObjective>(n, "objectives").ToArray(),
                c.All<IMilestoneReward>(n, "rewards").ToArray(),
                c.All<MilestoneRoute>(n, "routes").ToArray(),
                n.RefIds("prerequisiteMilestones"),
                n.Int("requiredObjectiveCount"))));
            b.Add("MilestoneRouteDefinition", (n, c) => new MilestoneRoute(
                n.Id, c.One<MilestoneObjective>(n, "objective"), n.Str("recommendedActionId")));

            // Unity MilestoneObjectiveBase.RequiredValue는 1 이상.
            b.Add("RelationshipMilestoneObjective", (n, c) => new RelationshipMilestoneObjective(
                n.Id, n.RefId("relationship"), Math.Max(1, n.Int("requiredValue"))));
            b.Add("OutsideSchoolLogMilestoneObjective", (n, c) => new OutsideSchoolLogMilestoneObjective(
                n.Id, n.Str("targetId"), Math.Max(1, n.Int("requiredValue"))));
        }
    }
}
