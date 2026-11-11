using Rootborn.Core.World;

namespace Rootborn.Data.Builders
{
    /// <summary>장면 이동 목적지와 장면 목록(새 게임 시작 자리 포함). Core World의 값 클래스로 만든다.</summary>
    internal static class WorldBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("WorldDestinationDefinition", (n, c) => new WorldDestination(n.Str("sceneName"), n.Strings("spawnIds")));

            b.Add("WorldTravelCatalogDefinition", (n, c) => new WorldTravelCatalog(
                c.All<WorldDestination>(n, "destinations"),
                c.One<WorldDestination>(n, "newGameDestination")?.SceneName,
                n.Str("newGameSpawnId")));
        }
    }
}
