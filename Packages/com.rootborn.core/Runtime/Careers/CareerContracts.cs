using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Careers
{
    /// <summary>
    /// 성인 직업이 쓰는 인벤토리. 아이템은 id로 가리킨다. 교환은 전부 되거나 전부 안 되고,
    /// commit은 교환과 함께 안에서 실행된다(실패하면 되돌린다).
    /// </summary>
    public interface ICareerInventory
    {
        bool CanExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards);
        bool TryExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards, Action commit);
        int CountOf(string itemId);
    }

    /// <summary>Core 칸 인벤토리(아이템 = id 문자열)를 성인 직업 인벤토리로 쓴다.</summary>
    public sealed class ItemSlotsCareerInventory<TSlot> : ICareerInventory where TSlot : ItemSlot<string>, new()
    {
        private readonly ItemSlots<string, TSlot> _slots;

        public ItemSlotsCareerInventory(ItemSlots<string, TSlot> slots) { _slots = slots ?? throw new ArgumentNullException(nameof(slots)); }

        public bool CanExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards) => _slots.CanExchange(costs, rewards);
        public bool TryExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards, Action commit) => _slots.TryExchange(costs, rewards, commit);
        public int CountOf(string itemId) => _slots.CountOf(itemId);
    }

    /// <summary>
    /// 세계 상태(아직 Core에 없는 영역). 같은 세이브 슬롯·플레이어인지 확인하고, 깃발을 켠다.
    /// 그 영역을 옮기면 그쪽 진행 상태가 이 인터페이스를 구현해 잇는다.
    /// </summary>
    public interface ICareerWorldState
    {
        string SaveSlot { get; }
        string PlayerId { get; }
        bool IsActive(string flagId);

        /// <summary>원본 WorldStateActivationSource(빈 장소, 빈 NPC, 요청 id, 날짜)에 해당하는 값을 받는다.</summary>
        bool TryActivate(string flagId, string sourceRequestId, int day);
    }
}
