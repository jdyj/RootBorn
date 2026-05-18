using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public enum LocationGrowthRoute
    {
        FormalStudy,
        SelfStudy,
        Work,
        Commerce,
        SocialHelp,
        Rest,
        Exploration
    }

    [CreateAssetMenu(fileName = "LocationActivity_New", menuName = "Rootborn/Student Life/Location Identity/Activity")]
    public sealed class LocationActivityDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private LocationGrowthRoute _growthRoute;
        [SerializeField] private int _timeCostMinutes;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _focusCost;
        [SerializeField] private int _stressDelta;
        [SerializeField] private LocationActivityRequirementBase[] _requirements = Array.Empty<LocationActivityRequirementBase>();
        [SerializeField] private LocationActivityOutcomeBase[] _outcomes = Array.Empty<LocationActivityOutcomeBase>();

        public LocationDefinition Location => _location;
        public string LocationId => _location != null ? _location.Id : string.Empty;
        public LocationGrowthRoute GrowthRoute => _growthRoute;
        public int TimeCostMinutes => Mathf.Max(0, _timeCostMinutes);
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public int StressDelta => _stressDelta;
        public IReadOnlyList<LocationActivityRequirementBase> Requirements => _requirements;
        public IReadOnlyList<LocationActivityOutcomeBase> Outcomes => _outcomes;

        public bool HasSatisfiedRequirements(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress, this)) return false;
            }

            return true;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
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
            LocationDefinition location,
            LocationGrowthRoute growthRoute,
            int timeCostMinutes,
            int energyCost,
            int focusCost,
            int stressDelta,
            LocationActivityRequirementBase[] requirements,
            LocationActivityOutcomeBase[] outcomes)
        {
            ConfigureForTests(id, displayNameKey);
            _location = location;
            _growthRoute = growthRoute;
            _timeCostMinutes = timeCostMinutes;
            _energyCost = energyCost;
            _focusCost = focusCost;
            _stressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<LocationActivityRequirementBase>();
            _outcomes = outcomes ?? Array.Empty<LocationActivityOutcomeBase>();
        }
    }

    public abstract class LocationActivityRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress, LocationActivityDefinition activity);
    }

    public abstract class LocationActivityOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, LocationActivityDefinition activity);
    }
}
