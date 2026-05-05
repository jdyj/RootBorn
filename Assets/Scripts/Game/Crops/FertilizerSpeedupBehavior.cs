using UnityEngine;

namespace Rootborn.Game.Crops
{
    /// <summary>
    /// 비료가 적용된 셀에서 성장 속도 배수를 적용. 실제 multiplier 값은 FarmGrid 가
    /// per-cell 로 관리하고 CropGrowthContext.FertilizerMultiplier 로 전달. 본 SO 는
    /// "비료 반응성" 플래그 + 상한선 역할.
    /// </summary>
    [CreateAssetMenu(fileName = "Behavior_FertilizerSpeedup", menuName = "Rootborn/Crops/Behaviors/Fertilizer Speedup")]
    public sealed class FertilizerSpeedupBehavior : GrowthBehaviorBase
    {
        [SerializeField, Tooltip("비료로 얻을 수 있는 최대 배수 상한. ctx.FertilizerMultiplier 가 이를 초과해도 클램프.")]
        private float _maxBoost = 1.5f;

        public float MaxBoost => _maxBoost;

        public override float ModifyGrowthRate(in CropGrowthContext ctx)
        {
            float fert = ctx.FertilizerMultiplier <= 0f ? 1f : ctx.FertilizerMultiplier;
            return Mathf.Clamp(fert, 1f, Mathf.Max(1f, _maxBoost));
        }
    }
}
