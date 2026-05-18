using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateUsageCondition_FlagActive", menuName = "Rootborn/World State/Usage/Conditions/Flag Active")]
    public sealed class WorldStateUsageFlagActiveCondition : WorldStateUsageConditionBase
    {
        public override bool Evaluate(in WorldStateUsageContext context, WorldStateUsageDefinition usage)
        {
            return usage != null && context.WorldStateProgress != null && context.WorldStateProgress.IsActive(usage.SourceFlag);
        }
    }
}
