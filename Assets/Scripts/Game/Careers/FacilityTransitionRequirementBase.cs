using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    public readonly struct FacilityTransitionContext
    {
        public FacilityTransitionContext(FacilityDefinition facility, StudentLifeProgress student, CareerCandidateProgress candidates)
        { Facility = facility; Student = student; Candidates = candidates; }
        public FacilityDefinition Facility { get; }
        public StudentLifeProgress Student { get; }
        public CareerCandidateProgress Candidates { get; }
    }

    public abstract class FacilityTransitionRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(in FacilityTransitionContext context);
    }
}
