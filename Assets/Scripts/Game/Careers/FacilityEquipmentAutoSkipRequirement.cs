using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "AutoSkipRequirement_FacilityEquipment", menuName = "Rootborn/Careers/Auto Skip Requirements/Facility Equipment")]
    public sealed class FacilityEquipmentAutoSkipRequirement : AutoSkipRequirementBase
    {
        [SerializeField] private int _minimumLevel = 1;
        [SerializeField] private ItemDefinition _equipment;
        public override AutoSkipRequirementCoverage Coverage => AutoSkipRequirementCoverage.FacilityEquipment;
        public override bool IsSatisfied(in AutoSkipContext context) => context.Progress != null && context.Progress.FacilityLevel >= Mathf.Max(1, _minimumLevel) && (_equipment == null || (context.Inventory != null && context.Inventory.CountOf(_equipment) > 0));
    }
}
