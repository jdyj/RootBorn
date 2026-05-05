using Rootborn.Game.Common;
using Rootborn.Game.Farming;
using Rootborn.Game.Player;
using Rootborn.Game.Time;
using UnityEngine;

namespace Rootborn.Game.Tools.Effects
{
    /// <summary>
    /// 비료 도구가 갈아엎은 셀에 비료 효과를 부여. Inventory 의 _consumesItem 을 1개 소비.
    /// 일정 일수 동안 성장률 multiplier 적용. 사용자 결정 #3 — 전용 도구 + Item_Fertilizer 소비.
    /// </summary>
    [CreateAssetMenu(fileName = "Effect_FertilizeCrop", menuName = "Rootborn/Tools/Effects/Fertilize Crop")]
    public sealed class FertilizeCropEffect : ToolEffectBase
    {
        [SerializeField] private float _multiplier = 1.5f;
        [SerializeField] private int _durationDays = 1;
        [SerializeField] private ItemDefinition _consumesItem;

        public ItemDefinition ConsumesItem => _consumesItem;

        public override void Apply(in ToolUseContext ctx)
        {
            var grid = ctx.FarmGrid as FarmGrid;
            var inv = ctx.Inventory as PlayerInventory;
            if (grid == null || inv == null) return;
            if (!grid.IsTilled(ctx.TargetCell)) return;
            if (_consumesItem != null && inv.Inventory.CountOf(_consumesItem) <= 0) return;

            int day = (ctx.Clock as GameClock)?.Day ?? 1;
            grid.Fertilize(ctx.TargetCell, day, _multiplier, _durationDays);

            if (_consumesItem != null) inv.Inventory.Remove(_consumesItem, 1);
        }
    }
}
