using Rootborn.Game.Placement;
using UnityEngine;

namespace Rootborn.Game.Quests.Rewards
{
    [CreateAssetMenu(fileName = "Reward_FurnitureUnlock", menuName = "Rootborn/Quests/Rewards/Furniture Unlock")]
    public sealed class FurnitureUnlockQuestReward : QuestRewardBase
    {
        [SerializeField] private string _unlockToken = string.Empty;

        public string UnlockToken => _unlockToken;

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.FurnitureUnlockProgress != null
                && !string.IsNullOrEmpty(_unlockToken)
                && !context.FurnitureUnlockProgress.IsUnlocked(_unlockToken);
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            context.FurnitureUnlockProgress?.TryUnlock(_unlockToken);
        }

        public void ConfigureForTests(string unlockToken)
        {
            _unlockToken = unlockToken ?? string.Empty;
        }
    }
}
