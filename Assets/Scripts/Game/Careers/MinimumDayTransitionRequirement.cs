using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "TransitionRequirement_MinimumDay", menuName = "Rootborn/Careers/Transition Requirements/Minimum Day")]
    public sealed class MinimumDayTransitionRequirement : FacilityTransitionRequirementBase
    {
        [SerializeField] private int _minimumDay = 1;
        public override bool IsSatisfied(in FacilityTransitionContext context) => context.Student != null && context.Student.CurrentDay >= Mathf.Max(1, _minimumDay);
    }
}
