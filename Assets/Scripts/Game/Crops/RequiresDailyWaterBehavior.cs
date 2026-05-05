using UnityEngine;

namespace Rootborn.Game.Crops
{
    /// <summary>
    /// 매일 물을 줘야 성장하는 작물의 GrowthBehavior. 물이 없고 비도 안 오면 성장률 0.
    /// </summary>
    [CreateAssetMenu(fileName = "Behavior_RequiresDailyWater", menuName = "Rootborn/Crops/Behaviors/Requires Daily Water")]
    public sealed class RequiresDailyWaterBehavior : GrowthBehaviorBase
    {
        [SerializeField, Tooltip("물이 없는 날 성장률 배수. 0=완전 정지.")]
        private float _drySoilMultiplier = 0f;

        public float DrySoilMultiplier => _drySoilMultiplier;

        public override float ModifyGrowthRate(in CropGrowthContext ctx)
        {
            return (ctx.WaterLevel01 > 0f || ctx.IsRaining) ? 1f : Mathf.Max(0f, _drySoilMultiplier);
        }
    }
}
