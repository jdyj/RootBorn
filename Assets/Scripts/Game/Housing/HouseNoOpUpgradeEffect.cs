using UnityEngine;

namespace Rootborn.Game.Housing
{
    [CreateAssetMenu(fileName = "HouseEffect_NoOp", menuName = "Rootborn/Housing/Effects/No Op")]
    public sealed class HouseNoOpUpgradeEffect : HouseUpgradeEffectBase
    {
        public override bool CanApply(in HouseUpgradeContext context) => true;
        public override void Apply(in HouseUpgradeContext context) { }
    }
}