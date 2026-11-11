using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Game;

namespace Rootborn.Screens.Inventory
{
    /// <summary>가방 탭 하나: 이름 문구 키, 아이콘 키, 보여 줄 분류(null이면 전체).</summary>
    public sealed record InventoryTab(string CategoryKey, string IconKey, ItemCategory[] Categories);

    /// <summary>보이는 가방 줄 하나: 아이템과 수량.</summary>
    public readonly record struct InventoryRow(ItemDefinition Item, int Count);

    /// <summary>
    /// 가방 탭 표와 거르기 규칙(Unity InventoryUiFilter). 탭은 데이터 표 하나이고, 분류와 이름 검색만 본다(아이템 id로 분기하지 않는다).
    /// Unity 판: 탭 0 전체, 1 도구, 2 자원과 씨앗, 3 기타. 아이콘 키는 Unity ModernUiInventoryPanel.TabThemeIconKeys.
    /// </summary>
    public static class InventoryFilter
    {
        public static readonly InventoryTab[] Tabs =
        {
            new InventoryTab("ui.inventory.category.all", "icon:inventory-all", null),
            new InventoryTab("ui.inventory.category.tools", "icon:inventory-tools", new[] { ItemCategory.Tool }),
            new InventoryTab("ui.inventory.category.resources", "icon:inventory-consumables", new[] { ItemCategory.Resource, ItemCategory.Seed }),
            new InventoryTab("ui.inventory.category.misc", "icon:inventory-misc", new[] { ItemCategory.Misc }),
        };

        /// <summary>탭의 분류에 들고 이름이 검색어(앞뒤 공백 뺀 것)를 포함하면 true. 대소문자는 가리지 않는다. 검색어가 비면 이름은 보지 않는다.</summary>
        public static bool Matches(ItemCategory category, string displayName, int tab, string query)
        {
            InventoryTab entry = Tabs[Math.Max(0, Math.Min(tab, Tabs.Length - 1))];
            if (entry.Categories != null && Array.IndexOf(entry.Categories, category) < 0) return false;
            string trimmed = query?.Trim();
            return string.IsNullOrEmpty(trimmed)
                || (displayName ?? string.Empty).IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// 가방 화면의 표시 모델: 가방 칸 순서대로 탭·검색어에 맞는 줄. 빈 칸과 수량 0인 칸은 뺀다(Unity Refresh와 같음).
    /// 이름은 displayName(문구 키 → 문구)으로 받아 검색에만 쓴다.
    /// </summary>
    public sealed class InventoryModel
    {
        public static readonly InventoryModel Empty = new InventoryModel(Array.Empty<InventoryRow>());

        public InventoryModel(IReadOnlyList<InventoryRow> rows) { Rows = rows; }

        public IReadOnlyList<InventoryRow> Rows { get; }

        public static InventoryModel From(GameSession session, Func<string, string> displayName, int tab, string query)
        {
            if (session == null) return Empty;
            IReadOnlyList<ItemInventory.Slot> slots = session.Inventory.Slots;
            var rows = new List<InventoryRow>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                ItemInventory.Slot slot = slots[i];
                if (slot.Item == null || slot.Count <= 0) continue;
                if (!InventoryFilter.Matches(slot.Item.Category, displayName(slot.Item.DisplayKey), tab, query)) continue;
                rows.Add(new InventoryRow(slot.Item, slot.Count));
            }
            return new InventoryModel(rows);
        }
    }
}
