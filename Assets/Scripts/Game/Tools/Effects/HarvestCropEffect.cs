using Rootborn.Game.Crops;
using Rootborn.Game.Farming;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Tools.Effects
{
    /// <summary>
    /// 성숙 작물을 수확하여 HarvestItem 을 인벤토리에 추가한다. 미성숙 작물은 no-op.
    /// 수확 후 밭 상태는 Tilled 로 유지된다.
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
                ctx.QuestEvents?.Record(new QuestEvent(
                    QuestEventKind.Harvest,
                    $"{ctx.TargetCell}:{UnityEngine.Time.frameCount}",
                    count: yield,
                    crop: crop,
                    item: crop.HarvestItem,
                    tool: ctx.Tool));
            }
        }
    }
}
