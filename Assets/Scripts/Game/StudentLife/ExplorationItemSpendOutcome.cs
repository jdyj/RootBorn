using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_ItemSpend", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Item Spend")]
    public sealed class ExplorationItemSpendOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _count = 1;
        public override string OutcomeId => _item != null ? "item-spend:" + _item.Id : string.Empty;
        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice)
        {
            return context.Inventory != null && _item != null && context.Inventory.CountOf(_item) >= Mathf.Max(1, _count);
        }
        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (!CanApply(context, interaction, choice)) return false;
            int count = Mathf.Max(1, _count);
            bool removed = context.Inventory.Remove(_item, count);
            if (removed) collector?.AddItemSpend(_item.Id + ":" + count.ToString());
            return removed;
        }
        public void ConfigureForTests(ItemDefinition item, int count) { _item = item; _count = count; }
    }
}
