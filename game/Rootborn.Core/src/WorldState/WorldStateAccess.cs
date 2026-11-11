using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.WorldState
{
    /// <summary>
    /// 다른 영역이 id로 월드 상태를 읽고 켜는 창구. 퀘스트·일과·성인 직업·장소 상태가 각자 둔 인터페이스를 여기서 한 번에 구현한다.
    /// id → 정의 해석은 정의 목록이 맡는다. 목록에 없는 id는 켤 수 없다(범위를 알 수 없다).
    /// </summary>
    public sealed class WorldStateAccess : IQuestWorldState, IDailyPlanWorldState, ICareerWorldState, ILocationStateWorldFlags
    {
        private readonly WorldStateProgress _progress;
        private readonly WorldStateFlagLookupCache _flags;

        public WorldStateAccess(WorldStateProgress progress, IReadOnlyList<WorldStateFlag> flags)
            : this(progress, new WorldStateFlagLookupCache(flags))
        {
        }

        public WorldStateAccess(WorldStateProgress progress, WorldStateFlagLookupCache flags)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _flags = flags ?? new WorldStateFlagLookupCache(null);
        }

        public WorldStateProgress Progress => _progress;
        public string SaveSlot => _progress.SaveSlot;
        public string PlayerId => _progress.PlayerId;

        public bool IsActive(string flagId) => _progress.IsActive(flagId);

        public bool TryActivate(string flagId, in WorldStateActivationSource source)
        {
            return _flags.TryGetFlag(flagId, out var flag) && _progress.TryActivate(flag, source);
        }

        bool IQuestWorldState.TryActivate(string flagId, QuestWorldStateSource source)
            => TryActivate(flagId, new WorldStateActivationSource(source.QuestChainId, source.QuestStepId, source.EventId, source.Day));

        /// <summary>원본 성인 직업처럼 퀘스트 체인·단계는 비우고 요청 id를 이벤트 id로 남긴다.</summary>
        bool ICareerWorldState.TryActivate(string flagId, string sourceRequestId, int day)
            => TryActivate(flagId, new WorldStateActivationSource(string.Empty, string.Empty, sourceRequestId, day));

        public bool MarkEffectVersionApplied(string flagId, int version)
        {
            return _flags.TryGetFlag(flagId, out var flag) && _progress.MarkEffectVersionApplied(flag, version);
        }
    }
}
