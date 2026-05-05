using Rootborn.Game.Farming;
using UnityEngine;

namespace Rootborn.Game.Tools.Effects
{
    /// <summary>
    /// 호미 류 도구가 잔디 셀을 갈아엎는 효과. 이미 갈아엎은 셀에는 no-op (멱등).
    /// </summary>
    [CreateAssetMenu(fileName = "Effect_TillSoil", menuName = "Rootborn/Tools/Effects/Till Soil")]
    public sealed class TillSoilEffect : ToolEffectBase
    {
        public override void Apply(in ToolUseContext ctx)
        {
            var grid = ctx.FarmGrid as FarmGrid;
            if (grid == null) return;
            if (grid.IsTilled(ctx.TargetCell)) return; // idempotent
            grid.Till(ctx.TargetCell);
        }
    }
}
