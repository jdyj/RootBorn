using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_ItemGrant", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Item Grant")]
    public sealed class ExplorationItemGrantOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;
        [SerializeField] private bool _allowDuplicate;
        public ItemDefinition Item => _item;
        public int Count => Mathf.Max(1, _count);
        public override string OutcomeId => _item != null ? "item-grant:" + _item.Id : string.Empty;
        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice)
        {
            return context.Inventory == null || _item == null || context.Inventory.CanAdd(_item, Count);
        }
        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (context.Inventory == null || _item == null) return false;
            int count = Count;
            context.Inventory.Add(_item, count);
            collector?.AddItemGrant(_item.Id + ":" + count.ToString());
            return true;
        }
        public void ConfigureForTests(ItemDefinition item, int count, bool allowDuplicate) { _item = item; _count = count; _allowDuplicate = allowDuplicate; }
    }
}
