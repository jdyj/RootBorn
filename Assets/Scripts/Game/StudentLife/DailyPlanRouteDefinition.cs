using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyPlanRoute_New", menuName = "Rootborn/Student Life/Daily Plan/Route")]
    public sealed class DailyPlanRouteDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LocationDefinition _targetLocation;
        [SerializeField] private LocationGrowthRoute _growthRoute;
        [SerializeField] private LocationActivityDefinition[] _matchingActivities = Array.Empty<LocationActivityDefinition>();
        [SerializeField] private DailyPlanRouteRequirementBase[] _requirements = Array.Empty<DailyPlanRouteRequirementBase>();
        [SerializeField] private DailyPlanRouteOutcomeBase[] _outcomes = Array.Empty<DailyPlanRouteOutcomeBase>();
        [SerializeField] private string _objectiveTitleKey = string.Empty;
        [SerializeField] private string _objectiveDetailKey = string.Empty;
        [SerializeField] private string _nextHintKey = string.Empty;

        public LocationDefinition TargetLocation => _targetLocation;
        public LocationGrowthRoute GrowthRoute => _growthRoute;
        public IReadOnlyList<LocationActivityDefinition> MatchingActivities => _matchingActivities;
        public IReadOnlyList<DailyPlanRouteRequirementBase> Requirements => _requirements;
        public IReadOnlyList<DailyPlanRouteOutcomeBase> Outcomes => _outcomes;
        public string ObjectiveTitleKey => string.IsNullOrEmpty(_objectiveTitleKey) ? DisplayNameKey : _objectiveTitleKey;
        public string ObjectiveDetailKey => string.IsNullOrEmpty(_objectiveDetailKey) ? DisplayNameKey : _objectiveDetailKey;
        public string NextHintKey => _nextHintKey ?? string.Empty;

        public bool IsAvailable(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress, this)) return false;
            }

            return true;
        }

        public bool Matches(LocationActivityDefinition activity)
        {
            if (activity == null) return false;
            for (int i = 0; i < _matchingActivities.Length; i++)
            {
                if (_matchingActivities[i] == activity) return true;
            }

            return _targetLocation != null
                && activity.Location == _targetLocation
                && activity.GrowthRoute == _growthRoute;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
            if (progress == null) return Array.Empty<string>();
            var logs = new List<string>();
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, this) : string.Empty;
                if (!string.IsNullOrEmpty(log)) logs.Add(log);
            }

            return logs.ToArray();
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            LocationDefinition targetLocation,
            LocationGrowthRoute growthRoute,
            LocationActivityDefinition[] matchingActivities,
            DailyPlanRouteRequirementBase[] requirements,
            DailyPlanRouteOutcomeBase[] outcomes,
            string objectiveTitleKey,
            string objectiveDetailKey,
            string nextHintKey)
        {
            ConfigureForTests(id, displayNameKey);
            _targetLocation = targetLocation;
            _growthRoute = growthRoute;
            _matchingActivities = matchingActivities ?? Array.Empty<LocationActivityDefinition>();
            _requirements = requirements ?? Array.Empty<DailyPlanRouteRequirementBase>();
            _outcomes = outcomes ?? Array.Empty<DailyPlanRouteOutcomeBase>();
            _objectiveTitleKey = objectiveTitleKey ?? string.Empty;
            _objectiveDetailKey = objectiveDetailKey ?? string.Empty;
            _nextHintKey = nextHintKey ?? string.Empty;
        }
    }

    [CreateAssetMenu(fileName = "DailyPlanRouteSet_New", menuName = "Rootborn/Student Life/Daily Plan/Route Set")]
    public sealed class DailyPlanRouteSetDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private DailyPlanRouteDefinition[] _routes = Array.Empty<DailyPlanRouteDefinition>();
        [SerializeField] private int _maxSelectionsPerDay = 1;

        public IReadOnlyList<DailyPlanRouteDefinition> Routes => _routes;
        public int MaxSelectionsPerDay => Mathf.Max(1, _maxSelectionsPerDay);

        public DailyPlanRouteDefinition[] GetAvailableRoutes(StudentLifeProgress progress)
        {
            if (progress == null) return Array.Empty<DailyPlanRouteDefinition>();
            var routes = new List<DailyPlanRouteDefinition>();
            for (int i = 0; i < _routes.Length; i++)
            {
                var route = _routes[i];
                if (route != null && route.IsAvailable(progress)) routes.Add(route);
            }

            return routes.ToArray();
        }

        public void ConfigureForTests(string id, string displayNameKey, DailyPlanRouteDefinition[] routes, int maxSelectionsPerDay)
        {
            ConfigureForTests(id, displayNameKey);
            _routes = routes ?? Array.Empty<DailyPlanRouteDefinition>();
            _maxSelectionsPerDay = Mathf.Max(1, maxSelectionsPerDay);
        }
    }

    public abstract class DailyPlanRouteRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress, DailyPlanRouteDefinition route);
    }

    public abstract class DailyPlanRouteOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, DailyPlanRouteDefinition route);
    }
}
