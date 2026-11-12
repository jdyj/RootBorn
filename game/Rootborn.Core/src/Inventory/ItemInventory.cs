using System;
using System.Collections.Generic;
using Rootborn.Core.Rewards;

namespace Rootborn.Core.Inventory
{
    /// <summary>
    /// The player's slot inventory: 32 slots, stacking by each item's max stack, all-or-nothing exchanges.
    /// Same rules and API as the game's Inventory; the slot logic is <see cref="ItemSlots{TItem,TSlot}"/>.
    /// 지갑과 통화 아이템을 받으면 그 아이템은 칸에 두지 않고 지갑으로 보낸다(멀티플레이 설계 2.2절). 주고받기는 칸과 지갑을 함께 확인하고
    /// 함께 바꾼다. 통화 아이템을 묻는 CountOf는 지갑 잔액이다. 지갑이 없으면 지금까지처럼 모든 아이템이 칸에 든다.
    /// </summary>
    public sealed class ItemInventory : IRewardInventory<ItemDefinition>
    {
        public const int MaxSlots = 32;

        public sealed class Slot : ItemSlot<ItemDefinition>
        {
        }

        private readonly ItemSlots<ItemDefinition, Slot> _slots = new ItemSlots<ItemDefinition, Slot>(MaxSlots, item => item.MaxStack);
        private readonly IWallet _wallet;
        private bool _walletInsideExchange;

        // 세이브에서 읽었지만 가방에 넣지 못한 것: 지금 데이터에 없는 아이템, 칸·묶음이 모자라 안 들어간 수량.
        // 가방 규칙(칸, 세기, 주고받기)에는 보이지 않고 다음 저장에 그대로 다시 쓴다. 데이터가 돌아오거나 자리가 나면 다음에 읽을 때 가방에 든다.
        private readonly List<PlayerInventoryItemSaveData> _kept = new List<PlayerInventoryItemSaveData>();

        public ItemInventory()
        {
            _slots.Changed += RaiseChanged;
        }

        /// <param name="wallet">돈을 주는 쪽.</param>
        /// <param name="currency">지갑으로 갈 아이템(데이터가 정한 통화). 둘 중 하나라도 null이면 지갑 없이 쓴다.</param>
        public ItemInventory(IWallet wallet, ItemDefinition currency) : this()
        {
            if (wallet == null || currency == null) return;
            _wallet = wallet;
            Currency = currency;
            _wallet.Changed += OnWalletChanged;
        }

        public IReadOnlyList<Slot> Slots => _slots.Slots;

        /// <summary>지갑으로 가는 통화 아이템. 지갑이 없으면 null.</summary>
        public ItemDefinition Currency { get; }

        public event Action OnChanged;

        public bool CanExchange(IReadOnlyList<InventoryGrant> costs, IReadOnlyList<InventoryGrant> rewards)
            => CanExchangeAmounts(ToAmounts(costs), ToAmounts(rewards));

        public bool CanReceive(IReadOnlyList<ItemAmount<ItemDefinition>> grants) => CanExchangeAmounts(null, grants);

        public bool TryReceive(IReadOnlyList<ItemAmount<ItemDefinition>> grants, Action commit) => TryExchangeAmounts(null, grants, commit);

        public bool TryAddAll(IReadOnlyList<InventoryGrant> rewards, Action commitState = null) => TryExchange(null, rewards, commitState);

        // commitState must contain only prevalidated, nonthrowing progress changes and no I/O.
        // Observers run after both sides commit; the reentry guard stays held through notification.
        public bool TryExchange(IReadOnlyList<InventoryGrant> costs, IReadOnlyList<InventoryGrant> rewards, Action commitState = null)
            => TryExchangeAmounts(ToAmounts(costs), ToAmounts(rewards), commitState);

        public void Add(ItemDefinition item, int count = 1)
        {
            if (!IsCurrency(item))
            {
                _slots.Add(item, count);
                return;
            }

            // 칸에 넣는 Add처럼 들어가지 않는 만큼은 버린다(먼저 CanAdd로 확인한다).
            if (count <= 0) return;
            long room = (long)PersonalWallet.MaxBalance - _wallet.Balance;
            _wallet.TryExchange(0, (int)Math.Min(count, room), null);
        }

        public bool CanAdd(ItemDefinition item, int count = 1)
            => IsCurrency(item) ? count > 0 && _wallet.CanExchange(0, count) : _slots.CanAdd(item, count);

        public bool CanAddAll(IReadOnlyList<InventoryGrant> grants) => CanExchangeAmounts(null, ToAmounts(grants));

        public bool TryMoveSlot(int sourceIndex, int targetIndex) => _slots.TryMoveSlot(sourceIndex, targetIndex);

        public bool Remove(ItemDefinition item, int count = 1)
            => IsCurrency(item) ? count > 0 && _wallet.TryExchange(count, 0, null) : _slots.Remove(item, count);

        public int CountOf(ItemDefinition item) => IsCurrency(item) ? _wallet.Balance : _slots.CountOf(item);

        /// <summary>가방 칸을 비운다. 지갑은 그대로다.</summary>
        public void Clear() => _slots.Clear();

        /// <summary>Saves filled slots by item id, in slot order. Empty slots and items without an id are left out. 지갑은 따로 저장한다.</summary>
        public PlayerInventorySaveData ToSaveData(string equippedToolItemId)
        {
            var slots = _slots.Slots;
            var items = new List<PlayerInventoryItemSaveData>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Item == null || slot.Count <= 0 || string.IsNullOrEmpty(slot.Item.Id)) continue;
                items.Add(new PlayerInventoryItemSaveData { ItemId = slot.Item.Id, Count = slot.Count });
            }

            // 읽을 때 가방에 넣지 못한 것은 칸 뒤에 그대로 붙여 쓴다.
            for (int i = 0; i < _kept.Count; i++) items.Add(_kept[i]);
            return new PlayerInventorySaveData { Items = items.ToArray(), EquippedToolItemId = equippedToolItemId ?? string.Empty };
        }

        /// <summary>
        /// Replaces the contents with the saved items. A null save changes nothing. The equipped tool id is left to the caller.
        /// Ids <paramref name="findById"/> cannot resolve, and counts that no longer fit (the data shrank a stack size), are not dropped:
        /// they stay out of the slots and <see cref="ToSaveData"/> writes them back. 전에는 버려서 다음 저장에 사라졌다.
        /// 지갑이 있으면 옛 세이브의 통화 아이템 칸은 지갑에 더한다(지갑을 먼저 읽어 둔다). 다음 저장부터 가방에는 돈 칸이 없다.
        /// </summary>
        public void LoadFromSaveData(PlayerInventorySaveData saveData, Func<string, ItemDefinition> findById)
        {
            if (saveData == null) return;
            Clear();
            _kept.Clear();
            if (saveData.Items == null || findById == null) return;
            for (int i = 0; i < saveData.Items.Length; i++)
            {
                var saved = saveData.Items[i];
                if (string.IsNullOrEmpty(saved.ItemId) || saved.Count <= 0) continue;
                var item = findById(saved.ItemId);
                if (item == null)
                {
                    Keep(saved.ItemId, saved.Count);
                    continue;
                }

                int before = CountOf(item);
                Add(item, saved.Count);
                Keep(saved.ItemId, saved.Count - (CountOf(item) - before));
            }
        }

        private void Keep(string itemId, int count)
        {
            if (count <= 0) return;
            for (int i = 0; i < _kept.Count; i++)
            {
                if (_kept[i].ItemId != itemId) continue;
                long sum = (long)_kept[i].Count + count;
                _kept[i] = new PlayerInventoryItemSaveData { ItemId = itemId, Count = (int)Math.Min(int.MaxValue, sum) };
                return;
            }

            _kept.Add(new PlayerInventoryItemSaveData { ItemId = itemId, Count = count });
        }

        private bool IsCurrency(ItemDefinition item) => _wallet != null && item != null && ReferenceEquals(item, Currency);

        private bool CanExchangeAmounts(IReadOnlyList<ItemAmount<ItemDefinition>> costs, IReadOnlyList<ItemAmount<ItemDefinition>> rewards)
        {
            if (_wallet == null) return _slots.CanExchange(costs, rewards);
            return Split(costs, out var itemCosts, out int moneyCost) && Split(rewards, out var itemRewards, out int moneyReward)
                   && _wallet.CanExchange(moneyCost, moneyReward) && _slots.CanExchange(itemCosts, itemRewards);
        }

        private bool TryExchangeAmounts(IReadOnlyList<ItemAmount<ItemDefinition>> costs, IReadOnlyList<ItemAmount<ItemDefinition>> rewards, Action commitState)
        {
            if (_wallet == null) return _slots.TryExchange(costs, rewards, commitState);
            if (!Split(costs, out var itemCosts, out int moneyCost) || !Split(rewards, out var itemRewards, out int moneyReward)) return false;
            if (!_wallet.CanExchange(moneyCost, moneyReward) || !_slots.CanExchange(itemCosts, itemRewards)) return false;

            // 칸을 바꾸는 안에서 지갑을 바꾸고, 지갑 안에서 진행 상태를 바꾼다. 어느 단계가 던지면 둘 다 되돌린다.
            // 알림은 칸 쪽이 끝에 한 번 낸다(지갑 알림은 이 동안 넘기지 않는다). 칸의 다시 들어오기 막기가 알림까지 이어진다.
            try
            {
                return _slots.TryExchange(itemCosts, itemRewards, () =>
                {
                    _walletInsideExchange = true;
                    try
                    {
                        if (!_wallet.TryExchange(moneyCost, moneyReward, commitState)) throw new WalletRefused();
                    }
                    finally { _walletInsideExchange = false; }
                });
            }
            catch (WalletRefused)
            {
                return false;
            }
        }

        // 통화 아이템과 나머지를 나눈다. 수량이 0 이하이거나 통화 합이 int를 넘으면 false(칸 쪽 계획과 같이 잘못된 수량은 전체 실패).
        private bool Split(IReadOnlyList<ItemAmount<ItemDefinition>> amounts, out IReadOnlyList<ItemAmount<ItemDefinition>> items, out int money)
        {
            items = amounts;
            money = 0;
            if (amounts == null) return true;
            long sum = 0;
            bool any = false;
            for (int i = 0; i < amounts.Count; i++)
            {
                if (!IsCurrency(amounts[i].Item)) continue;
                if (amounts[i].Count <= 0) return false;
                sum += amounts[i].Count;
                if (sum > int.MaxValue) return false;
                any = true;
            }

            if (!any) return true;
            var rest = new List<ItemAmount<ItemDefinition>>(amounts.Count);
            for (int i = 0; i < amounts.Count; i++)
                if (!IsCurrency(amounts[i].Item)) rest.Add(amounts[i]);
            items = rest;
            money = (int)sum;
            return true;
        }

        private void OnWalletChanged()
        {
            if (!_walletInsideExchange) RaiseChanged();
        }

        private void RaiseChanged() => OnChanged?.Invoke();

        private static List<ItemAmount<ItemDefinition>> ToAmounts(IReadOnlyList<InventoryGrant> grants)
        {
            if (grants == null) return null;
            var amounts = new List<ItemAmount<ItemDefinition>>(grants.Count);
            for (int i = 0; i < grants.Count; i++) amounts.Add(new ItemAmount<ItemDefinition>(grants[i].Item, grants[i].Count));
            return amounts;
        }

        /// <summary>지갑이 칸 교환 안에서 거절했을 때 칸 쪽을 되돌리려고 던진다. 밖으로 나가지 않는다.</summary>
        private sealed class WalletRefused : Exception
        {
        }
    }

    /// <summary>Same field names as the game's save file (written by JsonUtility).</summary>
    [Serializable]
    public sealed class PlayerInventorySaveData
    {
        public PlayerInventoryItemSaveData[] Items = Array.Empty<PlayerInventoryItemSaveData>();
        public string EquippedToolItemId;
    }

    [Serializable]
    public struct PlayerInventoryItemSaveData
    {
        public string ItemId;
        public int Count;
    }
}
