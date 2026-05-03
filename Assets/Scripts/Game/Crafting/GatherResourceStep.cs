using UnityEngine;

namespace Rootborn.Game.Crafting
{
    [CreateAssetMenu(fileName = "Step_Gather", menuName = "Rootborn/Crafting/Step/Gather Resource")]
    public sealed class GatherResourceStep : CraftStepBase
    {
        [SerializeField] private string _resourceId;
        [SerializeField] private int _requiredCount = 1;

        public string ResourceId => _resourceId;
        public int RequiredCount => _requiredCount;

        public override bool IsSatisfied(in CraftAttemptState state)
        {
            if (state.Inventory == null) return false;
            return state.Inventory.TryGetValue(_resourceId, out int have) && have >= _requiredCount;
        }
    }
}
