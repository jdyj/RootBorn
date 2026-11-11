using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>학생 생활 중심부: 생활 활동, 시간 정책, 달력, 튜토리얼 단계, 하루 끝 규칙.</summary>
    internal static class StudentLifeBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // 컨디션 상태. 값은 진행 상태가 id로 갖는다. persistsToNextDay의 Unity 초기값은 true다.
            b.Add("StatusDefinition", (n, c) => new StatusDefinition(n.Id, n.Str("displayNameKey"), n.Bool("persistsToNextDay")));
            b.Add("RelationshipDefinition", (n, c) => new RelationshipDefinition(n.Id, n.Str("displayNameKey"), n.Str("targetId")));
            b.Add("TimeSlotDefinition", (n, c) => new TimeSlotDefinition(n.Id, n.Str("displayNameKey"), n.Int("order")));

            // displayNameKey는 화면 문구라 Core가 받지 않는다. 비용은 Unity 게터처럼 0 이상.
            b.Add("LifeActivityDefinition", (n, c) => new LifeActivity(
                n.Id,
                n.Enum<LifeActivityCategory>("category"),
                c.One<ActivityTimePolicy>(n, "timePolicy"),
                Math.Max(0, n.Int("timeCostMinutes")),
                Math.Max(0, n.Int("energyCost")),
                Math.Max(0, n.Int("focusCost")),
                n.Int("stressDelta"),
                c.All<ILifeActivityRequirement>(n, "requirements").ToArray(),
                c.All<ILifeActivityEffect>(n, "effects").ToArray(),
                c.All<LifeChoice>(n, "choices").ToArray()));

            b.Add("TraitDeltaActivityEffect", (n, c) => new TraitDeltaEffect(n.RefId("trait"), n.Int("delta")));
            b.Add("SkillProgressActivityEffect", (n, c) => new SkillProgressEffect(n.RefId("skill"), n.Int("delta")));
            b.Add("CareerHintUnlockActivityEffect", (n, c) => new CareerHintUnlockEffect(n.RefId("career")));

            b.Add("ActivityTimePolicyDefinition", (n, c) => new ActivityTimePolicy(
                n.Id, Math.Max(0, n.Int("timePointCost")), n.Int("allowedDaysMask"), n.Bool("oncePerDay")));

            // Unity LifeCalendarDefinition.GetBudget은 0 아래로 내려가지 않는다.
            b.Add("LifeCalendarDefinition", (n, c) => new LifeCalendar(
                Math.Max(0, n.Int("weekdayBudget")), Math.Max(0, n.Int("weekendBudget")), n.Int("weekendDaysMask")));

            b.Add("TutorialStageDefinition", (n, c) => new TutorialStage(n.Id, n.Str("guideText"), n.Str("displayNameKey")));

            b.Add("DayEndRuleDefinition", (n, c) => new DayEndRuleSet(
                n.Id, n.Str("nextDayEntryPointId"), c.All<IDayEndRule>(n, "rules").ToArray()));
            b.Add("EnergyRestoreDayEndRule", (n, c) => new EnergyRestoreRule(n.Int("restoredEnergy")));
            b.Add("FocusRestoreDayEndRule", (n, c) => new FocusRestoreRule(n.Int("restoredFocus")));
            b.Add("TutorialStageAdvanceDayEndRule", (n, c) => new TutorialStageAdvanceRule(
                n.Str("fromStageId"), c.One<TutorialStage>(n, "targetStage"), n.Str("nextObjectiveId")));
        }
    }
}
