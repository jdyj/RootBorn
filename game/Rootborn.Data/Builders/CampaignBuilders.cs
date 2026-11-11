using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>캠페인: 캠페인, 날, 경로, 목표, 보상.</summary>
    internal static class CampaignBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("CampaignDefinition", (n, c) => new Campaign(
                n.Id, n.Str("descriptionKey"), c.All<CampaignDay>(n, "days").ToArray(), c.All<CampaignReward>(n, "rewards").ToArray()));
            // Unity CampaignDayDefinition.DayNumber는 1 이상.
            b.Add("CampaignDayDefinition", (n, c) => new CampaignDay(
                n.Id, Math.Max(1, n.Int("dayNumber")), n.Str("themeKey"), n.Str("directionText"), n.Str("nextGuideText"),
                c.All<CampaignRoute>(n, "routes").ToArray()));
            b.Add("CampaignRouteDefinition", (n, c) => new CampaignRoute(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), c.All<CampaignObjective>(n, "objectives").ToArray()));

            b.Add("CampaignActivityLogObjective", (n, c) => new CampaignActivityLogObjective(
                n.Id, n.Str("requiredLogPrefix"), n.Int("requiredCount"), n.Bool("requiresSchoolClass")));
            // Core의 장소 방문 목표는 학교 수업을 요구하지 않는다(생성자가 받지 않음). 데이터의 requiresSchoolClass가 1이면 오류로 드러낸다.
            b.Add("CampaignLocationVisitObjective", (n, c) =>
            {
                if (n.Bool("requiresSchoolClass"))
                    n.Report("wrong-type", "장소 방문 목표의 requiresSchoolClass=1은 Core가 받지 않는다");
                return new CampaignLocationVisitObjective(n.Id, n.RefId("location"), n.Int("requiredCount"));
            });
            b.Add("CampaignCareerHintReward", (n, c) => new CampaignCareerHintReward(n.Id, n.RefId("career")));
        }
    }
}
