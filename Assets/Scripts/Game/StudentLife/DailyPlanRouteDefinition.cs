using System;
using System.Collections.Generic;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public readonly struct DailyPlanRouteContext
    {
        public DailyPlanRouteContext(StudentLifeProgress studentProgress)
            : this(studentProgress, null, null, null)
        {
        }

        public DailyPlanRouteContext(
            StudentLifeProgress studentProgress,
            WorldStateProgress worldStateProgress,
            DiscoveryClueProgress discoveryClueProgress,
            ClueInterpretationProgress interpretationProgress)
        {
            StudentProgress = studentProgress;
            WorldStateProgress = worldStateProgress;
            DiscoveryClueProgress = discoveryClueProgress;
            InterpretationProgress = interpretationProgress;
        }

        public StudentLifeProgress StudentProgress { get; }
        public WorldStateProgress WorldStateProgress { get; }
        public DiscoveryClueProgress DiscoveryClueProgress { get; }
        public ClueInterpretationProgress InterpretationProgress { get; }
    }

    [CreateAssetMenu(fileName = "DailyPlanRoute_New", menuName = "Rootborn/Student Life/Daily Plan/Route")]
    public sealed class DailyPlanRouteDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LocationDefinition _targetLocation;
        [SerializeField] private LocationGrowthRoute _growthRoute;
        [SerializeField] private LocationActivityDefinition[] _matchingActivities = Array.Empty<LocationActivityDefinition>();
        [SerializeField] private CareerPracticeDefinition[] _matchingPractices = Array.Empty<CareerPracticeDefinition>();
        [SerializeField] private PartTimeWorkDefinition[] _matchingWorks = Array.Empty<PartTimeWorkDefinition>();
        [SerializeField] private DailyPlanRouteRequirementBase[] _requirements = Array.Empty<DailyPlanRouteRequirementBase>();
        [SerializeField] private DailyPlanRouteOutcomeBase[] _outcomes = Array.Empty<DailyPlanRouteOutcomeBase>();
        [SerializeField] private string _objectiveTitleKey = string.Empty;
        [SerializeField] private string _objectiveDetailKey = string.Empty;
        [SerializeField] private string _nextHintKey = string.Empty;

        public LocationDefinition TargetLocation => _targetLocation;
        public LocationGrowthRoute GrowthRoute => _growthRoute;
        public IReadOnlyList<LocationActivityDefinition> MatchingActivities => _matchingActivities;
        public IReadOnlyList<CareerPracticeDefinition> MatchingPractices => _matchingPractices;
        public IReadOnlyList<PartTimeWorkDefinition> MatchingWorks => _matchingWorks;
        public IReadOnlyList<DailyPlanRouteRequirementBase> Requirements => _requirements;
        public IReadOnlyList<DailyPlanRouteOutcomeBase> Outcomes => _outcomes;
        public string ObjectiveTitleKey => string.IsNullOrEmpty(_objectiveTitleKey) ? DisplayNameKey : _objectiveTitleKey;
        public string ObjectiveDetailKey => string.IsNullOrEmpty(_objectiveDetailKey) ? DisplayNameKey : _objectiveDetailKey;
        public string NextHintKey => _nextHintKey ?? string.Empty;

        public bool IsAvailable(StudentLifeProgress progress)
        {
            return IsAvailable(new DailyPlanRouteContext(progress));
        }

        public bool IsAvailable(in DailyPlanRouteContext context)
        {
            if (context.StudentProgress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(in context, this)) return false;
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

            // Explicit typed plans require their real practice/work settlement. Explicit location references above remain valid.
            if ((_matchingPractices != null && _matchingPractices.Length > 0)
                || (_matchingWorks != null && _matchingWorks.Length > 0)) return false;

            return _targetLocation != null
                && activity.Location == _targetLocation
                && activity.GrowthRoute == _growthRoute;
        }

        public bool MatchesPractice(CareerPracticeDefinition practice)
        {
            if (practice == null || _matchingPractices == null) return false;
            foreach (var candidate in _matchingPractices) if (candidate == practice) return true;
            return false;
        }
        public bool MatchesWork(PartTimeWorkDefinition work)
        {
            if (work == null || _matchingWorks == null) return false;
            foreach (var candidate in _matchingWorks) if (candidate == work) return true;
            return false;
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

    public abstract class DailyPlanRouteRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress, DailyPlanRouteDefinition route);

        public virtual bool IsSatisfied(in DailyPlanRouteContext context, DailyPlanRouteDefinition route)
        {
            return IsSatisfied(context.StudentProgress, route);
        }
    }

    public abstract class DailyPlanRouteOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, DailyPlanRouteDefinition route);
    }
}