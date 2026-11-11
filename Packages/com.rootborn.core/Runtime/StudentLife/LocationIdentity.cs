using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>장소 정체성의 힌트 한 줄. 성장 길과 이어지는 마일스톤은 id로 가리킨다.</summary>
    public readonly struct LocationHintEntry
    {
        public LocationHintEntry(string displayKey, string growthRoute, string milestoneId)
        {
            DisplayKey = displayKey ?? string.Empty;
            GrowthRoute = growthRoute ?? string.Empty;
            MilestoneId = milestoneId ?? string.Empty;
        }

        public string DisplayKey { get; }
        public string GrowthRoute { get; }
        public string MilestoneId { get; }
    }

    /// <summary>장소의 성격: 할 수 있는 활동, 성장 길 힌트, 추천 방문 시간.</summary>
    public sealed class LocationIdentity
    {
        private readonly LocationActivity[] _availableActivities;
        private readonly LocationHintEntry[] _hints;

        public LocationIdentity(string id, string displayNameKey, string descriptionKey, string locationId, LocationActivity[] availableActivities, LocationHintEntry[] hints,
            int recommendedVisitStartMinute, int recommendedVisitEndMinute)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            LocationId = locationId ?? string.Empty;
            _availableActivities = availableActivities ?? Array.Empty<LocationActivity>();
            _hints = hints ?? Array.Empty<LocationHintEntry>();
            RecommendedVisitStartMinute = Math.Max(0, recommendedVisitStartMinute);
            RecommendedVisitEndMinute = Math.Max(RecommendedVisitStartMinute, recommendedVisitEndMinute);
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string LocationId { get; }
        public IReadOnlyList<LocationActivity> AvailableActivities => _availableActivities;
        public IReadOnlyList<LocationHintEntry> Hints => _hints;
        public int RecommendedVisitStartMinute { get; }
        public int RecommendedVisitEndMinute { get; }

        public bool HasGrowthRoute(string route)
        {
            if (string.IsNullOrEmpty(route)) return false;
            for (int i = 0; i < _availableActivities.Length; i++)
            {
                var activity = _availableActivities[i];
                if (activity != null && activity.GrowthRoute == route) return true;
            }
            for (int i = 0; i < _hints.Length; i++) if (_hints[i].GrowthRoute == route) return true;
            return false;
        }
    }
}
