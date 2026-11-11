using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.World;
using Rootborn.Data;

namespace Rootborn.World
{
    /// <summary>
    /// 장면 목록과 새 게임 자리(Core WorldTravelCatalog). 정의 로더(Rootborn.Data)가 data/locations에서 만든 것을 꺼낸다.
    /// JSON을 직접 읽지 않는다.
    /// </summary>
    public static class WorldTravelData
    {
        /// <summary>정의에서 장면 목록 하나를 꺼낸다. 없거나 둘 이상이면 오류.</summary>
        public static WorldTravelCatalog From(GameDefinitions definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            IReadOnlyList<WorldTravelCatalog> all = definitions.All<WorldTravelCatalog>();
            if (all.Count != 1) throw new InvalidDataException("장면 목록 정의(WorldTravelCatalogDefinition)가 하나가 아니다: " + all.Count);
            return all[0];
        }

        /// <summary>옛 진입점(Host가 아직 부른다). 저장소의 data/를 정의 로더로 읽어 From과 같은 값을 낸다.</summary>
        [Obsolete("Host에서 바꿀 곳: WorldTravelData.From(GameContent.Definitions)를 쓴다. 이 진입점은 data/를 한 번 더 읽는다.")]
        public static WorldTravelCatalog Load(string repoRoot) => From(LoadedDefinitions.Of(repoRoot));
    }

    /// <summary>옛 진입점들을 위한 정의 읽기(저장소마다 한 번). 새 코드는 쓰지 않는다 — 정의를 받는다.</summary>
    internal static class LoadedDefinitions
    {
        private static readonly Dictionary<string, GameDefinitions> Cache = new Dictionary<string, GameDefinitions>(StringComparer.OrdinalIgnoreCase);

        public static GameDefinitions Of(string repoRoot)
        {
            string data = Path.GetFullPath(Path.Combine(repoRoot ?? throw new ArgumentNullException(nameof(repoRoot)), "data"));
            lock (Cache)
            {
                if (Cache.TryGetValue(data, out GameDefinitions cached)) return cached;
                LoadResult result = GameDataLoader.Load(data, Rootborn.Data.Builders.RootbornBuilders.Register);
                result.ThrowIfFailed();
                return Cache[data] = result.Definitions;
            }
        }
    }
}
