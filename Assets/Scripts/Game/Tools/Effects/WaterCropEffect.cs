using Rootborn.Game.Farming;
using Rootborn.Game.Time;
using UnityEngine;

namespace Rootborn.Game.Tools.Effects
{
    /// <summary>
    /// 물뿌리개 류 도구가 갈아엎은 셀에 물을 준다. Untilled 셀에는 no-op.
    /// </summary>
    [CreateAssetMenu(fileName = "Effect_WaterCrop", menuName = "Rootborn/Tools/Effects/Water Crop")]
    public sealed class WaterCropEffect : ToolEffectBase
    {
        [SerializeField, Range(0f, 1f)] private float _waterLevel01 = 1f;

        public override void Apply(in ToolUseContext ctx)
        {
            var grid = ctx.FarmGrid as FarmGrid;
            if (grid == null) return;
            if (!grid.IsTilled(ctx.TargetCell)) return;
            int day = (ctx.Clock as GameClock)?.Day ?? 1;
            grid.Water(ctx.TargetCell, day, _waterLevel01);
        }
    }
}
