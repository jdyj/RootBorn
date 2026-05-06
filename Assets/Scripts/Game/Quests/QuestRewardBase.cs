using UnityEngine;

namespace Rootborn.Game.Quests
{
    public abstract class QuestRewardBase : ScriptableObject
    {
        public abstract bool CanApply(in RewardRuntimeContext context);
        public abstract void Apply(in RewardRuntimeContext context);
    }
}
