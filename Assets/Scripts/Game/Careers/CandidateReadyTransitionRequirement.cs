using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "TransitionRequirement_CandidateReady", menuName = "Rootborn/Careers/Transition Requirements/Candidate Ready")]
    public sealed class CandidateReadyTransitionRequirement : FacilityTransitionRequirementBase
    {
        public override bool IsSatisfied(in FacilityTransitionContext context)
        {
            var candidate = context.Facility != null ? context.Facility.Candidate : null;
            return candidate != null && context.Student != null && context.Candidates != null && candidate.GetState(context.Candidates) == CareerCandidateState.ReadyForChoice && candidate.AreRequirementsSatisfied(context.Candidates, context.Student);
        }
    }
}
