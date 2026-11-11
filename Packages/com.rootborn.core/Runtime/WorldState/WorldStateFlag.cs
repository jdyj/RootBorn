using System;
using System.Collections.Generic;

namespace Rootborn.Core.WorldState
{
    public sealed class WorldStateScope
    {
        public WorldStateScope(string id, WorldStateScopeKind kind, string displayNameKey)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
        }

        public string Id { get; }
        public WorldStateScopeKind Kind { get; }
        public string DisplayNameKey { get; }
    }

    /// <summary>
    /// 월드에 생기는 변화 하나(장소 열림, 물건 드러남 등). 관련 장소·NPC·퀘스트 체인은 id로 가진다.
    /// 범위 정의가 있으면 그 종류가 <paramref name="scope"/>보다 우선한다(원본과 같다).
    /// </summary>
    public sealed class WorldStateFlag
    {
        private readonly string[] _relatedNpcIds;
        private readonly string[] _relatedQuestChainIds;
        private readonly WorldStateSummarySurface[] _visibleSurfaces;
        private readonly WorldStateBadgeKind[] _badges;
        private readonly WorldStateScope _scopeDefinition;
        private readonly WorldStateScopeKind _scope;

        public WorldStateFlag(string id, string displayNameKey, string descriptionKey, string relatedLocationId, string[] relatedNpcIds, string[] relatedQuestChainIds,
            WorldStateScopeKind scope, WorldStateChangeKind changeKind, string nextActionKey, WorldStateSummarySurface[] visibleSurfaces, WorldStateBadgeKind[] badges,
            int sortPriority, int effectVersion, WorldStateScope scopeDefinition = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            RelatedLocationId = relatedLocationId ?? string.Empty;
            _relatedNpcIds = relatedNpcIds ?? Array.Empty<string>();
            _relatedQuestChainIds = relatedQuestChainIds ?? Array.Empty<string>();
            _scope = scope;
            _scopeDefinition = scopeDefinition;
            ChangeKind = changeKind;
            NextActionKey = nextActionKey ?? string.Empty;
            _visibleSurfaces = visibleSurfaces ?? Array.Empty<WorldStateSummarySurface>();
            _badges = badges ?? Array.Empty<WorldStateBadgeKind>();
            SortPriority = sortPriority;
            EffectVersion = Math.Max(1, effectVersion);
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string RelatedLocationId { get; }
        public IReadOnlyList<string> RelatedNpcIds => _relatedNpcIds;
        public IReadOnlyList<string> RelatedQuestChainIds => _relatedQuestChainIds;
        public WorldStateScopeKind Scope => _scopeDefinition != null ? _scopeDefinition.Kind : _scope;
        public WorldStateChangeKind ChangeKind { get; }
        public string NextActionKey { get; }
        public WorldStateBadgeKind[] Badges => _badges;
        public int SortPriority { get; }
        public int EffectVersion { get; }

        /// <summary>보일 화면을 하나도 정하지 않았으면 모든 화면에 보인다.</summary>
        public bool IsVisibleOn(WorldStateSummarySurface surface)
        {
            if (_visibleSurfaces.Length == 0) return true;
            for (int i = 0; i < _visibleSurfaces.Length; i++) if (_visibleSurfaces[i] == surface) return true;
            return false;
        }

        public bool IsRelatedTo(string locationId) => !string.IsNullOrEmpty(locationId) && string.Equals(RelatedLocationId, locationId, StringComparison.Ordinal);

        public string FirstNpcId => _relatedNpcIds.Length > 0 && !string.IsNullOrEmpty(_relatedNpcIds[0]) ? _relatedNpcIds[0] : string.Empty;
    }
}
