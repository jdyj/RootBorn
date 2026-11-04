using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "AutoSkipRequirement_SuccessCount", menuName = "Rootborn/Careers/Auto Skip Requirements/Success Count")]
    public sealed class SuccessCountAutoSkipRequirement : AutoSkipRequirementBase
    {
        [SerializeField] private int _minimumSuccesses = 3;
        public override AutoSkipRequirementCoverage Coverage => AutoSkipRequirementCoverage.SuccessfulWork;
        public override bool IsSatisfied(in AutoSkipContext context) => context.Work != null && context.Progress != null && context.Progress.GetSuccessfulCompletions(context.Work) >= Mathf.Max(1, _minimumSuccesses);
    }
}
