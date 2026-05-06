using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Quests.Rewards
{
    [CreateAssetMenu(fileName = "Reward_Item", menuName = "Rootborn/Quests/Rewards/Item")]
    public sealed class ItemQuestReward : QuestRewardBase
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;

        public ItemDefinition Item => _item;
        public int Count => Mathf.Max(1, _count);

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.Inventory != null
                && _item != null
                && context.Inventory.CanAdd(_item, Count);
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context))
            {
                return;
            }

            context.Inventory.Add(_item, Count);
        }
    }
}
