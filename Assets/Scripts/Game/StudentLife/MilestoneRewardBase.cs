using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public abstract class MilestoneRewardBase : ScriptableObject
    {
        public abstract bool CanApply(Inventory inventory);
        public abstract void Apply(Inventory inventory);
    }

    [CreateAssetMenu(fileName = "MilestoneReward_Item", menuName = "Rootborn/Student Life/Milestones/Rewards/Item")]
    public sealed class ItemMilestoneReward : MilestoneRewardBase
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;

        public ItemDefinition Item => _item;
        public int Count => Mathf.Max(1, _count);

        public void ConfigureForTests(ItemDefinition item, int count)
        {
            _item = item;
            _count = Mathf.Max(1, count);
        }

        public override bool CanApply(Inventory inventory)
        {
            return inventory != null && _item != null && inventory.CanAdd(_item, Count);
        }

        public override void Apply(Inventory inventory)
        {
            if (CanApply(inventory)) inventory.Add(_item, Count);
        }
    }
}
