using System;
using System.Collections.Generic;

namespace Rootborn.Core.WorldState
{
    /// <summary>켜진 상태를 화면별 요약으로 만든다. 우선순위가 높은 것이 앞이다.</summary>
    public static class WorldStateSummaryBuilder
    {
        public static WorldStateSummaryModel[] BuildForSurface(IReadOnlyList<WorldStateFlag> flags, WorldStateProgress progress, WorldStateSummarySurface surface, IWorldStateDisplayNames names)
            => Build(flags, progress, surface, null, names);

        public static WorldStateSummaryModel[] BuildForLocation(IReadOnlyList<WorldStateFlag> flags, WorldStateProgress progress, string locationId, WorldStateSummarySurface surface, IWorldStateDisplayNames names)
            => Build(flags, progress, surface, locationId, names);

        /// <param name="locationId">null이면 장소로 거르지 않는다. 빈 문자열은 어느 장소와도 맞지 않는다.</param>
        private static WorldStateSummaryModel[] Build(IReadOnlyList<WorldStateFlag> flags, WorldStateProgress progress, WorldStateSummarySurface surface, string locationId, IWorldStateDisplayNames names)
        {
            if (flags == null || progress == null) return Array.Empty<WorldStateSummaryModel>();
            var result = new List<WorldStateSummaryModel>();
            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (flag == null || !progress.IsActive(flag) || !flag.IsVisibleOn(surface)) continue;
                if (locationId != null && !flag.IsRelatedTo(locationId)) continue;
                var record = progress.GetRecord(flag);
                string locationName = names != null && !string.IsNullOrEmpty(flag.RelatedLocationId) ? names.LocationName(flag.RelatedLocationId) : string.Empty;
                string npcName = names != null && !string.IsNullOrEmpty(flag.FirstNpcId) ? names.NpcName(flag.FirstNpcId) : string.Empty;
                result.Add(new WorldStateSummaryModel(flag.Id, flag.DisplayNameKey, flag.DescriptionKey, locationName, npcName, flag.NextActionKey,
                    flag.ChangeKind, record.Scope, flag.Badges, flag.SortPriority, record.SeenNotification));
            }

            result.Sort((left, right) => right.SortPriority.CompareTo(left.SortPriority));
            return result.ToArray();
        }
    }

    /// <summary>id → 상태 정의 조회. 처음 찾을 때 한 번만 만든다.</summary>
    public sealed class WorldStateFlagLookupCache
    {
        private readonly IReadOnlyList<WorldStateFlag> _flags;
        private Dictionary<string, WorldStateFlag> _byId;

        public WorldStateFlagLookupCache(IReadOnlyList<WorldStateFlag> flags)
        {
            _flags = flags ?? Array.Empty<WorldStateFlag>();
        }

        public int BuildCount { get; private set; }

        public bool TryGetFlag(string id, out WorldStateFlag flag)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(id))
            {
                flag = null;
                return false;
            }

            return _byId.TryGetValue(id, out flag);
        }

        private void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, WorldStateFlag>(StringComparer.Ordinal);
            for (int i = 0; i < _flags.Count; i++)
            {
                var flag = _flags[i];
                if (flag != null && !string.IsNullOrEmpty(flag.Id)) _byId[flag.Id] = flag;
            }

            BuildCount++;
        }
    }
}
