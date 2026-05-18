using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationCondition_ItemHeld", menuName = "Rootborn/Student Life/Exploration Choices/Conditions/Item Held")]
    public sealed class ExplorationItemHeldCondition : ExplorationConditionBase
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;

        public override bool Evaluate(in ExplorationInteractionContext context, ExplorationChoiceDefinition choice)
        {
            return context.Inventory != null && _item != null && context.Inventory.CountOf(_item) >= Mathf.Max(1, _count);
        }

        public void ConfigureForTests(ItemDefinition item, int count, string lockedReasonKey)
        {
            _item = item;
            _count = count;
            SetLockedReasonForTests(lockedReasonKey);
        }
    }
}
