using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "TransitionRequirement_CareerHint", menuName = "Rootborn/Careers/Transition Requirements/Career Hint")]
    public sealed class CareerHintTransitionRequirement : FacilityTransitionRequirementBase
    {
        public override bool IsSatisfied(in FacilityTransitionContext context) => context.Student != null && context.Facility != null && context.Facility.Career != null && context.Student.IsCareerHintUnlocked(context.Facility.Career);
    }
}
