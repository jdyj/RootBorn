using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    public interface ILocationVisitRule
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    public sealed class LocationCategory
    {
        public LocationCategory(string id, string displayName, int sortOrder)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            SortOrder = sortOrder;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int SortOrder { get; }
    }

    /// <summary>
    /// 마을 장소 하나. 분류·NPC 역할·NPC·발견은 id로 가리킨다. 월드 위치는 화면 배치라 여기 두지 않는다.
    /// 방문 규칙을 모두 만족해야 방문할 수 있다.
    /// </summary>
    public sealed class Location
    {
        private readonly string[] _relatedRoleIds;
        private readonly string[] _relatedNpcIds;
        private readonly string[] _discoveryIds;
        private readonly ILocationVisitRule[] _visitRules;

        public Location(string id, string displayNameKey, string descriptionKey, string categoryId, string worldAnchorId,
            string[] relatedRoleIds, string[] relatedNpcIds, string[] discoveryIds, ILocationVisitRule[] visitRules)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            CategoryId = categoryId ?? string.Empty;
            WorldAnchorId = string.IsNullOrEmpty(worldAnchorId) ? Id : worldAnchorId;
            _relatedRoleIds = relatedRoleIds ?? Array.Empty<string>();
            _relatedNpcIds = relatedNpcIds ?? Array.Empty<string>();
            _discoveryIds = discoveryIds ?? Array.Empty<string>();
            _visitRules = visitRules ?? Array.Empty<ILocationVisitRule>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string CategoryId { get; }
        public string WorldAnchorId { get; }
        public IReadOnlyList<string> RelatedRoleIds => _relatedRoleIds;
        public IReadOnlyList<string> RelatedNpcIds => _relatedNpcIds;
        public IReadOnlyList<string> DiscoveryIds => _discoveryIds;
        public IReadOnlyList<ILocationVisitRule> VisitRules => _visitRules;

        public bool CanVisit(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _visitRules.Length; i++)
            {
                var rule = _visitRules[i];
                if (rule != null && !rule.IsSatisfied(progress)) return false;
            }
            return true;
        }
    }
}
