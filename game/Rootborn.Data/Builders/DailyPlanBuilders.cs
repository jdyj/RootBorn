using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>일과(하루 계획) 경로와 경로 묶음, 경로 조건.</summary>
    internal static class DailyPlanBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("DailyPlanRouteDefinition", BuilderHelpers.AddedLater(new[] { "matchingPractices", "matchingWorks" }, (n, c) => new DailyPlanRoute(
                n.Id, n.Str("displayNameKey"), n.RefId("targetLocation"),
                LocationBuilders.GrowthRoute(n, "growthRoute"),
                c.All<IDailyPlanLocationActivity>(n, "matchingActivities").ToArray(),
                c.All<CareerPractice>(n, "matchingPractices").ToArray(),
                c.All<IMinigamePartTimeWork>(n, "matchingWorks").ToArray(),
                c.All<IDailyPlanRouteRequirement>(n, "requirements").ToArray(),
                c.All<IDailyPlanRouteOutcome>(n, "outcomes").ToArray(),
                n.Str("objectiveTitleKey"), n.Str("objectiveDetailKey"), n.Str("nextHintKey"))));

            // Unity DailyPlanRouteSetDefinition.MaxSelectionsPerDay는 1 이상.
            b.Add("DailyPlanRouteSetDefinition", (n, c) => new DailyPlanRouteSet(
                n.Id, n.Str("displayNameKey"), c.All<DailyPlanRoute>(n, "routes").ToArray(), Math.Max(1, n.Int("maxSelectionsPerDay"))));

            b.Add("DailyPlanCareerHintRequirement", (n, c) => new DailyPlanCareerHintRequirement(n.RefId("career"), n.Bool("expectedUnlocked")));
            // 알바 기록 읽기는 Core의 순수 해석기(PartTimeWorkLogReader)를 쓴다.
            b.Add("DailyPlanPartTimeWorkRequirement", (n, c) => new DailyPlanPartTimeWorkRequirement(
                n.RefId("work"), n.Bool("expectedCompleted"), new PartTimeWorkLogReader()));
            b.Add("DailyPlanWorldStateRequirement", (n, c) => new DailyPlanWorldStateRequirement(n.RefId("flag"), n.Bool("expectedActive")));
        }
    }
}
