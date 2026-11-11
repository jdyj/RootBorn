namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 규칙이지만 Core에 아직 없는 종류. 영역을 옮길 때마다 여기서 빼고 만들기 함수를 등록한다.
    /// 이 목록이 줄어드는 것이 이전 진행 상황이다.
    /// </summary>
    internal static class NotPortedYet
    {
        private const string CoreWorld = "core-world";
        private const string Legacy = "legacy";
        private const string Status = "status";
        private const string House = "house";

        public static void Register(DefinitionBuilders b)
        {
            foreach (var type in CoreWorldTypes) b.NotPortedYet(type, CoreWorld);
            foreach (var type in LegacyTypes) b.NotPortedYet(type, Legacy);

            b.NotPortedYet("StatusDefinition", Status);
            b.NotPortedYet("StatusEffectDefinition", Status);

            // 가구 배치 칸·놓을 수 있는 면·이동 막음은 집 꾸미기 규칙이다.
            b.NotPortedYet("FurnitureDefinition", House);

            // Core에는 LocationStateConflictMode 열거형과 그것을 받는 LocationStateResolver만 있고 정의 클래스가 없다.
            b.NotPortedYet("LocationStateConflictPolicyDefinition", "student-life(정의 클래스 없음)");
        }

        private static readonly string[] CoreWorldTypes =
        {
            "WorldStateFlagDefinition", "WorldStateScopeDefinition",
            "WorldStateUsageDefinition", "WorldStateUsageCareerHintOutcome", "WorldStateUsageEncyclopediaOutcome",
            "WorldStateUsageFlagActiveCondition", "WorldStateUsageQuestUnlockOutcome", "WorldStateUsageRepeatPolicyDefinition",
            "EncyclopediaCategoryDefinition", "EncyclopediaEntryDefinition", "EncyclopediaDiscoveryCompletedCondition",
            "DiscoveryClueDefinition", "DiscoveryClueSourceDefinition", "DiscoveryClueCareerHintOutcome",
            "DiscoveryClueEncyclopediaOutcome", "DiscoveryClueLocationVisitCompletion", "DiscoveryClueQuestUnlockOutcome",
            "DiscoveryClueWorldStateActiveCondition", "DiscoveryClueWorldStateRevealOutcome",
            "ClueInterpretationDefinition", "ClueInterpretationSourceDefinition", "ClueInterpretationPolicyDefinition",
            "ClueInterpretationClueSeenCondition", "ClueInterpretationRelationshipThresholdCondition",
            "ClueInterpretationCareerHintOutcome", "ClueInterpretationEncyclopediaOutcome", "ClueInterpretationQuestUnlockOutcome",
            "ClueInterpretationRelationshipDeltaOutcome", "ClueInterpretationStatusDeltaOutcome", "ClueInterpretationWorldStateOutcome",
            "DialogueDefinition", "DialogueChoiceDefinition",
            "NpcDefinition", "NpcRoleDefinition", "NpcScheduleDefinition",
            "TimeSlotDefinition", "TimeSlotScheduleRule",
            "RelationshipDefinition",
            "WorldDestinationDefinition", "WorldTravelCatalogDefinition",
        };

        private static readonly string[] LegacyTypes =
        {
            "KnowledgeNode", "HitGroundWithRockTrigger", "ToolDefinition", "FishingAnimationEffect",
            "ResourceNodeDefinition", "RecipeDefinition", "GatherResourceStep", "HeirTrait",
        };
    }
}
