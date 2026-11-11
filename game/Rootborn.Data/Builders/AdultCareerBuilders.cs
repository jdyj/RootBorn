using System;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>성인 직업: 시설, 시설 단계, 시설 일, 단계 전환 조건, 자동 넘김 조건.</summary>
    internal static class AdultCareerBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("FacilityDefinition", (n, c) => new Facility(
                n.Id, n.Str("displayNameKey"), n.RefId("location"), n.RefId("practice"), n.RefId("career"),
                c.One<CareerCandidate>(n, "candidate"),
                c.All<IFacilityTransitionRequirement>(n, "transitionRequirements").ToArray(),
                c.All<FacilityLevel>(n, "levels").ToArray(),
                c.All<FacilityWork>(n, "works").ToArray()));

            // costItems와 costCounts는 짝이다. Unity는 길이가 다르거나 빈 칸이 있으면 그 단계를 쓸 수 없는 것으로 봤다.
            // 여기서는 오류로 드러낸다. achievementSprite는 그림.
            b.Add("FacilityLevelDefinition", (n, c) => new FacilityLevel(
                n.Id, n.Str("displayNameKey"), n.Int("level"),
                BuilderHelpers.StrictItemAmounts(n, "costItems", "costCounts"),
                n.RefIds("worldFlags"), n.Int("rewardPercent"), n.Bool("completesRoute")));

            b.Add("FacilityWorkDefinition", (n, c) => new FacilityWork(
                n.Id, n.Str("displayNameKey"), n.RefId("facility"), c.One<Minigame>(n, "minigame"),
                BuilderHelpers.StrictItemAmounts(n, "rewardItems", "maximumRewardCounts"),
                c.All<IAutoSkipRequirement>(n, "autoSkipRequirements").ToArray(),
                n.Int("minimumFacilityLevel"), n.Int("timePointCost"),
                Math.Max(0, n.Int("timeCostMinutes")), // Unity FacilityWorkDefinition.TimeCostMinutes와 같게
                n.Int("maximumMastery"), n.Int("masteryPerSuccess"), n.Int("minimumSuccessScore"), n.Int("reputationPerSuccess")));

            b.Add("CareerHintTransitionRequirement", (n, c) => new CareerHintTransitionRequirement());
            b.Add("CandidateReadyTransitionRequirement", (n, c) => new CandidateReadyTransitionRequirement());
            b.Add("MinimumDayTransitionRequirement", (n, c) => new MinimumDayTransitionRequirement(n.Int("minimumDay")));

            b.Add("MasteryAutoSkipRequirement", (n, c) => new MasteryAutoSkipRequirement());
            b.Add("SuccessCountAutoSkipRequirement", (n, c) => new SuccessCountAutoSkipRequirement(n.Int("minimumSuccesses")));
            b.Add("FacilityEquipmentAutoSkipRequirement", (n, c) => new FacilityEquipmentAutoSkipRequirement(
                n.Int("minimumLevel"), n.RefId("equipment")));
        }
    }
}
