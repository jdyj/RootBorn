namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 규칙이지만 Core에 아직 없는 종류. 영역을 옮길 때마다 여기서 빼고 만들기 함수를 등록한다.
    /// 이 목록이 줄어드는 것이 이전 진행 상황이다.
    /// </summary>
    internal static class NotPortedYet
    {
        private const string World = "world(장면 이동, Runtime/World)";

        public static void Register(DefinitionBuilders b)
        {
            // 장면 이동 목적지와 새 게임 시작 위치는 월드(맵) 프로젝트가 Runtime/World에서 옮긴다.
            b.NotPortedYet("WorldDestinationDefinition", World);
            b.NotPortedYet("WorldTravelCatalogDefinition", World);
        }
    }
}
