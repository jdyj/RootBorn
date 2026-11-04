using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestRewardBase : ScriptableObject
    {
        public abstract bool CanApply(in RewardRuntimeContext context);
        public abstract void Apply(in RewardRuntimeContext context);

        // Return true only when this strategy's entire effect is the returned item grant.
        // Batch callers commit these grants together and do not invoke Apply a second time.
        public virtual bool TryGetInventoryGrant(out InventoryGrant grant)
        {
            grant = default;
            return false;
        }
    }
}
