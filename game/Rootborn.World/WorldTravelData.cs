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
    }
}
