namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 규칙이지만 Core에 아직 없는 종류. 영역을 옮길 때마다 여기서 빼고 만들기 함수를 등록한다.
    /// 이 목록이 줄어드는 것이 이전 진행 상황이다.
    /// </summary>
    internal static class NotPortedYet
    {
        private const string World = "world(장면 이동, Runtime/World)";
        private const string Legacy = "legacy";
        private const string Status = "status";
        private const string House = "house";

        public static void Register(DefinitionBuilders b)
        {
            // 월드 영역에서 Core에 정의 클래스가 없는 것. 규칙은 이것들을 id로만 가리킨다.
            b.NotPortedYet("TimeSlotDefinition", "time(시간대 정의 클래스 없음, 규칙은 시간대 id만 쓴다)");
            b.NotPortedYet("RelationshipDefinition", "student-life(관계 정의 클래스 없음, 규칙은 관계 id만 쓴다)");
            // 장면 이동 목적지와 새 게임 시작 위치는 월드(맵) 프로젝트가 Runtime/World에서 옮긴다.
            b.NotPortedYet("WorldDestinationDefinition", World);
            b.NotPortedYet("WorldTravelCatalogDefinition", World);
            foreach (var type in LegacyTypes) b.NotPortedYet(type, Legacy);

            b.NotPortedYet("StatusDefinition", Status);
            b.NotPortedYet("StatusEffectDefinition", Status);

            // 가구 배치 칸·놓을 수 있는 면·이동 막음은 집 꾸미기 규칙이다.
            b.NotPortedYet("FurnitureDefinition", House);
        }

        private static readonly string[] LegacyTypes =
        {
            "KnowledgeNode", "HitGroundWithRockTrigger", "ToolDefinition", "FishingAnimationEffect",
            "ResourceNodeDefinition", "RecipeDefinition", "GatherResourceStep", "HeirTrait",
        };
    }
}
