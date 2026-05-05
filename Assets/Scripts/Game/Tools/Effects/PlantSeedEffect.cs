using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Farming;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Game.Tools.Effects
{
    /// <summary>
    /// 장착된 씨앗(EquippedSeed) 의 SeedFor 작물을 타일에 심는다.
    /// 갈아엎은 빈 셀에서만 동작. 인벤토리에서 씨앗 1개 차감.
    /// </summary>
    [CreateAssetMenu(fileName = "Effect_PlantSeed", menuName = "Rootborn/Tools/Effects/Plant Seed")]
    public sealed class PlantSeedEffect : ToolEffectBase
    {
        public override void Apply(in ToolUseContext ctx)
        {
            var grid = ctx.FarmGrid as FarmGrid;
            var inv = ctx.Inventory as PlayerInventory;
            if (grid == null || inv == null) return;

            var seed = inv.EquippedSeed;
            if (seed == null || seed.Category != ItemCategory.Seed) return;
            CropDefinition crop = seed.SeedFor;
            if (crop == null) return;

            if (!grid.IsTilled(ctx.TargetCell)) return;
            if (grid.HasPlot(ctx.TargetCell)) return;
            if (inv.Inventory.CountOf(seed) <= 0) return;

            if (grid.TryPlant(ctx.TargetCell, crop))
            {
                inv.Inventory.Remove(seed, 1);
            }
        }
    }
}
