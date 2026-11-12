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
    /// 플레이어 인벤토리(ItemInventory, 아이템 = 정의)를 성인 직업 인벤토리(아이템 = id)로 쓴다. id는 받은 찾기 함수로 정의로 바꾼다.
    /// 정의가 없는 id가 섞이면 교환하지 않는다(아무것도 바뀌지 않는다). 판정은 ItemInventory가 한다.
    /// </summary>
    public sealed class ItemInventoryCareerInventory : ICareerInventory
    {
        private readonly ItemInventory _inventory;
        private readonly Func<string, ItemDefinition> _findItem;

        public ItemInventoryCareerInventory(ItemInventory inventory, Func<string, ItemDefinition> findItem)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _findItem = findItem ?? throw new ArgumentNullException(nameof(findItem));
        }

        public bool CanExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards)
            => TryConvert(costs, out var c) && TryConvert(rewards, out var r) && _inventory.CanExchange(c, r);

        public bool TryExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards, Action commit)
            => TryConvert(costs, out var c) && TryConvert(rewards, out var r) && _inventory.TryExchange(c, r, commit);

        public int CountOf(string itemId)
        {
            var item = string.IsNullOrEmpty(itemId) ? null : _findItem(itemId);
            return item != null ? _inventory.CountOf(item) : 0;
        }

        private bool TryConvert(IReadOnlyList<ItemAmount<string>> amounts, out List<InventoryGrant> grants)
        {
            grants = null;
            if (amounts == null) return true;
            grants = new List<InventoryGrant>(amounts.Count);
            for (int i = 0; i < amounts.Count; i++)
            {
                var item = string.IsNullOrEmpty(amounts[i].Item) ? null : _findItem(amounts[i].Item);
                if (item == null) return false;
                grants.Add(new InventoryGrant(item, amounts[i].Count));
            }

            return true;
        }
    }

    /// <summary>일을 하면 오르는 기술 하나.</summary>
    public readonly struct SkillGain
    {
        public SkillGain(string skillId, int amount)
        {
            SkillId = skillId;
            Amount = Math.Max(0, amount);
        }

        public string SkillId { get; }
        public int Amount { get; }
    }

    /// <summary>공간이 일 정산에 더해 주는 것. default는 아무것도 더하지 않는다.</summary>
    public readonly struct WorkExtras
    {
        private readonly IReadOnlyList<SkillGain> _skillGains;

        public WorkExtras(int rewardPercentBonus, int reputationBonus, IReadOnlyList<SkillGain> skillGains)
        {
            RewardPercentBonus = Math.Max(0, rewardPercentBonus);
            ReputationBonus = Math.Max(0, reputationBonus);
            _skillGains = skillGains;
        }

        public int RewardPercentBonus { get; }
        public int ReputationBonus { get; }
        public IReadOnlyList<SkillGain> SkillGains => _skillGains ?? Array.Empty<SkillGain>();
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
