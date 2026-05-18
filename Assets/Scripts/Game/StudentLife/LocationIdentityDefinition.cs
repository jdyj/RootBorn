using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [Serializable]
    public struct LocationHintEntry
    {
        [SerializeField] private string _displayKey;
        [SerializeField] private LocationGrowthRoute _growthRoute;
        [SerializeField] private MilestoneDefinition _milestone;

        public string DisplayKey => string.IsNullOrEmpty(_displayKey) ? string.Empty : _displayKey;
        public LocationGrowthRoute GrowthRoute => _growthRoute;
        public MilestoneDefinition Milestone => _milestone;

        public LocationHintEntry(string displayKey, LocationGrowthRoute growthRoute, MilestoneDefinition milestone)
        {
            _displayKey = string.IsNullOrEmpty(displayKey) ? string.Empty : displayKey;
            _growthRoute = growthRoute;
            _milestone = milestone;
        }
    }

    [CreateAssetMenu(fileName = "LocationIdentity_New", menuName = "Rootborn/Student Life/Location Identity/Identity")]
    public sealed class LocationIdentityDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private LocationActivityDefinition[] _availableActivities = Array.Empty<LocationActivityDefinition>();
        [SerializeField] private LocationHintEntry[] _hints = Array.Empty<LocationHintEntry>();
        [SerializeField] private int _recommendedVisitStartMinute = 8 * 60;
        [SerializeField] private int _recommendedVisitEndMinute = 19 * 60;

        public LocationDefinition Location => _location;
        public string LocationId => _location != null ? _location.Id : string.Empty;
        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public IReadOnlyList<LocationActivityDefinition> AvailableActivities => _availableActivities;
        public IReadOnlyList<LocationHintEntry> Hints => _hints;
        public int RecommendedVisitStartMinute => Mathf.Max(0, _recommendedVisitStartMinute);
        public int RecommendedVisitEndMinute => Mathf.Max(RecommendedVisitStartMinute, _recommendedVisitEndMinute);

        public bool HasGrowthRoute(LocationGrowthRoute route)
        {
            for (int i = 0; i < _availableActivities.Length; i++)
            {
                var activity = _availableActivities[i];
                if (activity != null && activity.GrowthRoute == route) return true;
            }

            for (int i = 0; i < _hints.Length; i++) if (_hints[i].GrowthRoute == route) return true;
            return false;
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            LocationDefinition location,
            string descriptionKey,
            LocationActivityDefinition[] availableActivities,
            LocationHintEntry[] hints,
            int recommendedVisitStartMinute,
            int recommendedVisitEndMinute)
        {
            ConfigureForTests(id, displayNameKey);
            _location = location;
            _descriptionKey = string.IsNullOrEmpty(descriptionKey) ? displayNameKey : descriptionKey;
            _availableActivities = availableActivities ?? Array.Empty<LocationActivityDefinition>();
            _hints = hints ?? Array.Empty<LocationHintEntry>();
            _recommendedVisitStartMinute = recommendedVisitStartMinute;
            _recommendedVisitEndMinute = recommendedVisitEndMinute;
        }
    }
}
