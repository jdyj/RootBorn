using Rootborn.Game.Crops;
using Rootborn.Game.Farming;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.Tools.Effects
{
    /// <summary>
    /// 익은 작물을 수확하여 HarvestItem 을 인벤토리에 추가. 미성숙 셀에는 no-op.
    /// 사용자 결정 #2 — 수확 후 셀은 Tilled 상태 유지 (FarmGrid.TryHarvest 가 처리).
    /// </summary>
    [CreateAssetMenu(fileName = "Effect_HarvestCrop", menuName = "Rootborn/Tools/Effects/Harvest Crop")]
    public sealed class HarvestCropEffect : ToolEffectBase
    {
        public override void Apply(in ToolUseContext ctx)
        {
            var grid = ctx.FarmGrid as FarmGrid;
            var inv = ctx.Inventory as PlayerInventory;
            if (grid == null || inv == null) return;

            if (!grid.TryHarvest(ctx.TargetCell, ctx.Tool, out CropDefinition crop, out int yield))
                return;

            if (crop != null && crop.HarvestItem != null && yield > 0)
            {
                inv.Inventory.Add(crop.HarvestItem, yield);
            }
        }
    }
}
