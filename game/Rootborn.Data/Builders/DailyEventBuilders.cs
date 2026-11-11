using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>하루 이벤트: 이벤트, 종류, 선택지, 나타나는 조건, 결과.</summary>
    internal static class DailyEventBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("DailyEventDefinition", (n, c) => new DailyEvent(
                n.Id, n.Str("descriptionKey"), n.RefId("location"), n.RefId("npc"), c.One<DailyEventKind>(n, "kind"),
                c.All<IDailyEventAvailabilityRule>(n, "availabilityRules").ToArray(),
                c.All<DailyEventChoice>(n, "choices").ToArray(), n.Str("displayNameKey")));

            // Unity DailyEventKindDefinition 게터: 미루는 날과 마감 시각은 0 이상.
            b.Add("DailyEventKindDefinition", (n, c) => new DailyEventKind(
                n.Id, n.Bool("isOptional"), n.Bool("isDeferrable"), n.Bool("isTimed"), n.Bool("isRequired"),
                n.Bool("isRepeatable"), n.Bool("isConditional"), Math.Max(0, n.Int("deferDays")), Math.Max(0, n.Int("dueTimeMinutes"))));

            b.Add("DailyEventChoiceDefinition", (n, c) => new DailyEventChoice(
                n.Id, n.Str("resultSummaryKey"), c.One<ActivityTimePolicy>(n, "timePolicy"),
                Math.Max(0, n.Int("timeCostMinutes")), Math.Max(0, n.Int("energyCost")), Math.Max(0, n.Int("focusCost")),
                n.Int("stressDelta"), n.Bool("isDeferChoice"), n.Bool("isDeclineChoice"),
                c.All<IDailyEventAvailabilityRule>(n, "availabilityRules").ToArray(),
                c.All<IDailyEventOutcome>(n, "outcomes").ToArray(), n.Str("displayNameKey")));

            b.Add("DailyEventLocationAvailabilityRule", (n, c) => new DailyEventLocationAvailabilityRule(n.RefId("location")));
            b.Add("DailyEventTraitDeltaOutcome", (n, c) => new DailyEventTraitDeltaOutcome(n.RefId("trait"), n.Int("delta")));
            b.Add("DailyEventRelationshipDeltaOutcome", (n, c) => new DailyEventRelationshipDeltaOutcome(n.RefId("relationship"), n.Int("delta")));
            b.Add("DailyEventStatusDeltaOutcome", (n, c) => new DailyEventStatusDeltaOutcome(n.RefId("status"), n.Int("delta")));
        }
    }
}
