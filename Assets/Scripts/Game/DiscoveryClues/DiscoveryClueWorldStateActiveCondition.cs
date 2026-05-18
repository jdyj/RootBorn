using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "DiscoveryClueCondition_WorldStateActive", menuName = "Rootborn/Discovery Clues/Conditions/World State Active")]
    public sealed class DiscoveryClueWorldStateActiveCondition : DiscoveryClueConditionBase
    {
        public override bool Evaluate(in DiscoveryClueContext context, DiscoveryClueDefinition clue)
        {
            return clue != null && context.WorldStateProgress != null && context.WorldStateProgress.IsActive(clue.RelatedWorldState);
        }
    }
}
