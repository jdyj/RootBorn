using UnityEngine;

namespace Rootborn.Game.Housing
{
    [CreateAssetMenu(fileName = "HouseCondition_Always", menuName = "Rootborn/Housing/Conditions/Always")]
    public sealed class HouseAlwaysCondition : HouseUpgradeConditionBase
    {
        public override bool IsMet(in HouseUpgradeContext context) => true;
    }
}