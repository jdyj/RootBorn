using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "AutoSkipRequirement_Mastery", menuName = "Rootborn/Careers/Auto Skip Requirements/Mastery")]
    public sealed class MasteryAutoSkipRequirement : AutoSkipRequirementBase
    {
        public override AutoSkipRequirementCoverage Coverage => AutoSkipRequirementCoverage.Mastery;
        public override bool IsSatisfied(in AutoSkipContext context) => context.Work != null && context.Progress != null && context.Progress.GetMastery(context.Work) >= context.Work.MaximumMastery;
    }
}
