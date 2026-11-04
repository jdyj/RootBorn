using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "Facility_New", menuName = "Rootborn/Careers/Facility")]
    public sealed class FacilityDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private CareerPracticeDefinition _practice;
        [SerializeField] private CareerDefinition _career;
        [SerializeField] private CareerCandidateDefinition _candidate;
        [SerializeField] private FacilityTransitionRequirementBase[] _transitionRequirements = Array.Empty<FacilityTransitionRequirementBase>();
        [SerializeField] private FacilityLevelDefinition[] _levels = Array.Empty<FacilityLevelDefinition>();
        [SerializeField] private FacilityWorkDefinition[] _works = Array.Empty<FacilityWorkDefinition>();
        public string Id => _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public LocationDefinition Location => _location;
        public CareerPracticeDefinition Practice => _practice;
        public CareerDefinition Career => _career;
        public CareerCandidateDefinition Candidate => _candidate;
        public IReadOnlyList<FacilityLevelDefinition> Levels => _levels ?? Array.Empty<FacilityLevelDefinition>();
        public IReadOnlyList<FacilityWorkDefinition> Works => _works ?? Array.Empty<FacilityWorkDefinition>();
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(Id) || Career == null || Candidate == null || Levels.Count != 3) return false;
                for (int level = 1; level <= 3; level++)
                {
                    int matches = 0;
                    for (int i = 0; i < Levels.Count; i++) if (Levels[i] != null && Levels[i].IsValid && Levels[i].Level == level) matches++;
                    if (matches != 1) return false;
                }
                return GetLevel(3).CompletesRoute;
            }
        }
        public FacilityLevelDefinition GetLevel(int level)
        {
            for (int i = 0; i < Levels.Count; i++) if (Levels[i] != null && Levels[i].Level == level) return Levels[i];
            return null;
        }
        public bool ContainsWork(FacilityWorkDefinition work)
        {
            for (int i = 0; i < Works.Count; i++) if (ReferenceEquals(Works[i], work)) return true;
            return false;
        }
        public bool CanTransition(in FacilityTransitionContext context)
        {
            bool hasHint = false, hasCandidate = false;
            if (_transitionRequirements == null) return false;
            for (int i = 0; i < _transitionRequirements.Length; i++)
            {
                var rule = _transitionRequirements[i];
                if (rule == null || !rule.IsSatisfied(in context)) return false;
                hasHint |= rule is CareerHintTransitionRequirement;
                hasCandidate |= rule is CandidateReadyTransitionRequirement;
            }
            return hasHint && hasCandidate;
        }
    }
}
