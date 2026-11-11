using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>장소, 장소 분류, 장소 정체성, 장소 활동과 결과, 장소 상태.</summary>
    public static class LocationBuilders
    {
        /// <summary>
        /// Unity enum LocationGrowthRoute의 숫자 → Core LocationGrowthRoutes 문자열. 순서는 원본 선언 순서다
        /// (Assets/Scripts/Game/StudentLife/LocationActivityDefinition.cs, EnumOrderTests가 고정).
        /// </summary>
        public static readonly IReadOnlyList<string> GrowthRouteNames = new[]
        {
            LocationGrowthRoutes.FormalStudy,
            LocationGrowthRoutes.SelfStudy,
            LocationGrowthRoutes.Work,
            LocationGrowthRoutes.Commerce,
            LocationGrowthRoutes.SocialHelp,
            LocationGrowthRoutes.Rest,
            LocationGrowthRoutes.Exploration,
        };

        /// <summary>Unity LocationActivityProgressDeltaOutcome의 target. Core에는 대상마다 클래스가 따로 있다.</summary>
        public enum ProgressTarget
        {
            Trait,
            Skill,
            Relationship,
            Status,
            CareerHint,
        }

        internal static string GrowthRoute(NodeReader n, string field)
        {
            var index = n.Int(field);
            if (index >= 0 && index < GrowthRouteNames.Count) return GrowthRouteNames[index];
            n.Report("enum-range", $"필드 '{field}'의 값 {index}는 LocationGrowthRoute에 없다");
            return GrowthRouteNames[0];
        }

        internal static void Register(DefinitionBuilders b)
        {
            // worldPosition은 맵 배치라 Host·월드 몫이다.
            b.Add("LocationDefinition", (n, c) => new Location(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.RefId("category"), n.Str("worldAnchorId"),
                n.RefIds("relatedRoles"), n.RefIds("relatedNpcs"), n.RefIds("discoveries"),
                c.All<ILocationVisitRule>(n, "visitRules").ToArray()));
            b.Add("LocationCategoryDefinition", (n, c) => new LocationCategory(n.Id, n.Str("displayName"), n.Int("sortOrder")));

            b.Add("LocationIdentityDefinition", (n, c) =>
            {
                var hints = new List<LocationHintEntry>();
                foreach (var hint in n.Items("hints"))
                    hints.Add(new LocationHintEntry(hint.Str("displayKey"), GrowthRoute(hint, "growthRoute"), hint.RefId("milestone")));

                // Unity LocationIdentityDefinition 게터: 시작은 0 이상, 끝은 시작 이상.
                var start = Math.Max(0, n.Int("recommendedVisitStartMinute"));
                return new LocationIdentity(
                    n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.RefId("location"),
                    c.All<LocationActivity>(n, "availableActivities").ToArray(), hints.ToArray(),
                    start, Math.Max(start, n.Int("recommendedVisitEndMinute")));
            });

            b.Add("LocationActivityDefinition", BuilderHelpers.AddedLater(new[] { "resultTextKey" }, (n, c) => new LocationActivity(
                n.Id, n.Str("displayNameKey"), n.Str("resultTextKey"), n.RefId("location"), GrowthRoute(n, "growthRoute"),
                c.One<ActivityTimePolicy>(n, "timePolicy"),
                Math.Max(0, n.Int("timeCostMinutes")), Math.Max(0, n.Int("energyCost")), Math.Max(0, n.Int("focusCost")),
                n.Int("stressDelta"),
                c.All<ILocationActivityRequirement>(n, "requirements").ToArray(),
                c.All<ILocationActivityOutcome>(n, "outcomes").ToArray())));

            // 한 Unity 클래스가 target 값에 따라 다섯 가지로 쓰였다. Core의 대상별 결과로 나눈다.
            b.Add("LocationActivityProgressDeltaOutcome", BuilderHelpers.AddedLater(new[] { "logCareerHintEveryTime" }, (n, c) => ProgressDelta(n)));
            b.Add("LocationActivityTraitDeltaOutcome", (n, c) => new LocationActivityTraitDeltaOutcome(n.RefId("trait"), n.Int("delta")));
            // 레시피 공개는 레거시 레시피 영역의 실행 서비스가 필요하다. 아직 없어 null을 넘기며, 그동안 이 결과는 아무것도 하지 않는다.
            b.Add("LocationActivityResourceRecipeOutcome", (n, c) => new LocationActivityResourceRecipeOutcome(
                n.RefId("resource"), n.RefId("recipe"), n.Str("nextHintKey"), null));

            b.Add("LocationStateDefinition", (n, c) => new LocationState(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.RefId("location"), n.Int("priority"),
                c.All<ILocationStateCondition>(n, "conditions").ToArray(),
                c.All<ILocationStateEffect>(n, "effects").ToArray()));
            b.Add("LocationStateActivityLogCondition", (n, c) => new LocationStateActivityLogCondition(
                n.Str("requiredActivityId"), n.Int("minimumCount")));
            b.Add("LocationStateTimeSlotCondition", (n, c) => new LocationStateTimeSlotCondition(n.RefIds("timeSlots")));
            b.Add("LocationStateAvailabilityEffect", (n, c) => new LocationStateAvailabilityEffect(
                n.RefIds("activities"), n.RefIds("npcs"), n.RefIds("clues"),
                n.Strings("interactableObjectIds"), n.Strings("portalIds"), n.Strings("shopItemIds")));
        }

        private static ILocationActivityOutcome ProgressDelta(NodeReader n)
        {
            var delta = n.Int("delta");
            switch (n.Enum<ProgressTarget>("target"))
            {
                case ProgressTarget.Trait: return new LocationActivityTraitDeltaOutcome(n.RefId("trait"), delta);
                case ProgressTarget.Skill: return new LocationActivitySkillDeltaOutcome(n.RefId("skill"), delta);
                case ProgressTarget.Relationship: return new LocationActivityRelationshipDeltaOutcome(n.RefId("relationship"), delta);
                case ProgressTarget.Status: return new LocationActivityStatusDeltaOutcome(n.RefId("status"), delta);
                default: return new LocationActivityCareerHintOutcome(n.RefId("career"), n.Bool("logCareerHintEveryTime"));
            }
        }
    }
}
