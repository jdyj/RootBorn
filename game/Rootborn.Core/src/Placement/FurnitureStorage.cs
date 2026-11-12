using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Placement
{
    /// <summary>
    /// 상자(보관 칸이 있는 가구)의 데이터: 어느 가구가 몇 칸을 담는가. 데이터 한 곳(data/interiors/FurnitureStorageDefinition.json)에 둔다 —
    /// 어느 그림을 상자로 쓸지, 몇 종류를 둘지는 그 파일만 고친다. 코드는 가구 id를 모른다.
    /// </summary>
    public sealed class FurnitureStorageDefinition
    {
        public FurnitureStorageDefinition(string id, FurnitureDefinition furniture, int slots)
        {
            Id = id ?? string.Empty;
            Furniture = furniture ?? throw new ArgumentNullException(nameof(furniture));
            if (slots < 1) throw new ArgumentOutOfRangeException(nameof(slots), "상자 칸은 1 이상이다");
            Slots = slots;
        }

        public string Id { get; }
        public FurnitureDefinition Furniture { get; }

        /// <summary>담을 수 있는 칸 수(가방 칸과 같은 쌓기 규칙).</summary>
        public int Slots { get; }
    }

    /// <summary>가구 id → 상자 칸 수. 같은 가구가 두 번 있으면 앞의 것.</summary>
    public sealed class FurnitureStorageCatalog
    {
        private readonly Dictionary<string, int> _slots = new Dictionary<string, int>(StringComparer.Ordinal);

        public static readonly FurnitureStorageCatalog Empty = new FurnitureStorageCatalog(null);

        public FurnitureStorageCatalog(IEnumerable<FurnitureStorageDefinition> definitions)
        {
            if (definitions == null) return;
            foreach (var d in definitions)
                if (d != null && !_slots.ContainsKey(d.Furniture.StableId)) _slots.Add(d.Furniture.StableId, d.Slots);
        }

        /// <summary>그 가구의 상자 칸 수. 상자가 아니면 0.</summary>
        public int SlotsOf(string furnitureId) => furnitureId != null && _slots.TryGetValue(furnitureId, out int slots) ? slots : 0;

        public bool IsStorage(FurnitureDefinition furniture) => furniture != null && SlotsOf(furniture.StableId) > 0;
    }

    /// <summary>
    /// 놓인 상자 하나의 내용물(놓인 가구 기록 <see cref="FurniturePlacementSaveData.Storage"/>에 묶인다). 필드는 MonoGame 판이 더했다.
    /// Items는 칸 순서의 찬 칸(가방 세이브와 같은 모양), Requests는 반영한 최근 요청 id(같은 요청을 두 번 반영하지 않게).
    /// </summary>
    [Serializable]
    public sealed class FurnitureStorageSaveData
    {
        public PlayerInventoryItemSaveData[] Items = Array.Empty<PlayerInventoryItemSaveData>();
        public string[] Requests = Array.Empty<string>();

        public FurnitureStorageSaveData Clone() => new FurnitureStorageSaveData
        {
            Items = Items == null ? Array.Empty<PlayerInventoryItemSaveData>() : (PlayerInventoryItemSaveData[])Items.Clone(),
            Requests = Requests == null ? Array.Empty<string>() : (string[])Requests.Clone(),
        };

        /// <summary>물건이 하나라도 들었다(치울 수 없다).</summary>
        public static bool HoldsItems(FurnitureStorageSaveData data)
        {
            if (data?.Items == null) return false;
            foreach (var item in data.Items) if (!string.IsNullOrEmpty(item.ItemId) && item.Count > 0) return true;
            return false;
        }
    }

    /// <summary>
    /// 상자 내용물을 읽은 것(가방과 같은 쌓기·칸 수 규칙). 정의에 없는 아이템 id(데이터에서 빠진 것)는 버리지 않고 칸을 차지한 채 그대로 다시 쓴다.
    /// </summary>
    public sealed class StorageContents
    {
        private readonly List<ItemStack<ItemDefinition>> _stacks;
        private readonly HashSet<ItemDefinition> _unknown;
        private readonly List<string> _requests;

        private StorageContents(int maxSlots, List<ItemStack<ItemDefinition>> stacks, HashSet<ItemDefinition> unknown, List<string> requests)
        {
            MaxSlots = maxSlots;
            _stacks = stacks;
            _unknown = unknown;
            _requests = requests;
        }

        /// <param name="maxSlots">그 상자의 칸 수(데이터).</param>
        /// <param name="findItem">아이템 id → 정의(없으면 null).</param>
        public static StorageContents From(FurnitureStorageSaveData data, int maxSlots, Func<string, ItemDefinition> findItem)
        {
            var stacks = new List<ItemStack<ItemDefinition>>(Math.Max(0, maxSlots));
            var unknown = new HashSet<ItemDefinition>();
            var requests = new List<string>();
            if (data?.Items != null)
            {
                foreach (var saved in data.Items)
                {
                    if (string.IsNullOrEmpty(saved.ItemId) || saved.Count <= 0) continue;
                    var item = findItem?.Invoke(saved.ItemId);
                    if (item == null)
                    {
                        // 칸 하나를 그대로 차지하는 자리표. 진짜 아이템과 같지 않으므로 꺼내거나 쌓이지 않는다.
                        item = new ItemDefinition(saved.ItemId, maxStack: saved.Count);
                        unknown.Add(item);
                    }

                    stacks.Add(new ItemStack<ItemDefinition>(item, saved.Count));
                }
            }

            if (data?.Requests != null)
                foreach (var id in data.Requests)
                    if (!string.IsNullOrEmpty(id) && !requests.Contains(id)) requests.Add(id);
            return new StorageContents(Math.Max(0, maxSlots), stacks, unknown, requests);
        }

        public int MaxSlots { get; }
        public IReadOnlyList<ItemStack<ItemDefinition>> Stacks => _stacks;

        /// <summary>물건이 든 칸 수.</summary>
        public int UsedSlots
        {
            get
            {
                int used = 0;
                foreach (var s in _stacks) if (s.Item != null && s.Count > 0) used++;
                return used;
            }
        }

        public bool IsEmpty => UsedSlots == 0;

        /// <summary>정의에 없는 아이템의 자리표인지(화면은 이름 대신 id를 보인다).</summary>
        public bool IsUnknown(ItemDefinition item) => item != null && _unknown.Contains(item);

        public int CountOf(ItemDefinition item)
        {
            if (item == null) return 0;
            int sum = 0;
            foreach (var s in _stacks) if (ReferenceEquals(s.Item, item)) sum += s.Count;
            return sum;
        }

        /// <summary>이 요청 id를 이미 반영했다.</summary>
        public bool Knows(string requestId) => !string.IsNullOrEmpty(requestId) && _requests.Contains(requestId);

        internal IReadOnlyList<string> Requests => _requests;

        public FurnitureStorageSaveData ToSaveData() => Save(_stacks, _requests, null);

        internal static FurnitureStorageSaveData Save(IReadOnlyList<ItemStack<ItemDefinition>> stacks, IReadOnlyList<string> requests, string newRequest)
        {
            var items = new List<PlayerInventoryItemSaveData>(stacks.Count);
            foreach (var s in stacks)
                if (s.Item != null && s.Count > 0 && !string.IsNullOrEmpty(s.Item.Id)) items.Add(new PlayerInventoryItemSaveData { ItemId = s.Item.Id, Count = s.Count });
            var ids = new List<string>(requests.Count + 1);
            ids.AddRange(requests);
            if (!string.IsNullOrEmpty(newRequest) && !ids.Contains(newRequest)) ids.Add(newRequest);
            int drop = ids.Count - StorageTransfer.RecentRequestCapacity;
            if (drop > 0) ids.RemoveRange(0, drop);
            return new FurnitureStorageSaveData { Items = items.ToArray(), Requests = ids.ToArray() };
        }
    }

    /// <summary>가방에서 상자로 넣기(Put)인지 상자에서 가방으로 꺼내기(Take)인지. 숫자는 요청에 쓰이므로 바꾸지 않는다.</summary>
    public enum StorageDirection
    {
        Put = 0,
        Take = 1,
    }

    /// <summary>옮기기의 결과. 화면은 이것으로 안내 문구를 고른다.</summary>
    public enum StorageTransferResult
    {
        /// <summary>옮겼다.</summary>
        Moved,
        /// <summary>같은 요청을 이미 반영했다(아무것도 바꾸지 않았다).</summary>
        AlreadyDone,
        /// <summary>그 가구는 상자가 아니거나 없다.</summary>
        NotStorage,
        /// <summary>읽을 수 없는 요청(수량 0 이하, 아이템·요청 id 없음).</summary>
        Invalid,
        /// <summary>돈은 상자에 넣지 않는다(지갑에 있다).</summary>
        Currency,
        /// <summary>내놓는 쪽에 그만큼 없다.</summary>
        NotEnough,
        /// <summary>받는 쪽에 자리가 없다(일부만 들어가도 거절).</summary>
        NoRoom,
        /// <summary>같이 쓰는 상자: 방에 요청했다. 허락이 오면 바뀐다.</summary>
        Requested,
        /// <summary>같이 쓰는 상자: 앞의 요청을 아직 기다린다.</summary>
        Busy,
    }

    /// <summary>
    /// 가방 ↔ 상자 옮기기(혼자 하기와 각자 쓰는 집). 둘을 함께 확인하고 함께 바꾼다: 가방 교환 안에서 상자의 다음 내용물을 넘긴다(commit은
    /// 메모리만 바꾸고 던지지 않아야 한다 — 저장은 부르는 쪽이 가방과 같은 묶음으로 한다). 실패하면 아무것도 바뀌지 않는다.
    /// 같은 요청 id는 한 번만 반영한다(상자가 최근 요청 id를 기억한다). 돈(가방의 통화 아이템)은 넣지 않는다.
    /// </summary>
    public static class StorageTransfer
    {
        /// <summary>상자마다 기억하는 최근 요청 id 수.</summary>
        public const int RecentRequestCapacity = 64;

        /// <summary>바꾸지 않고 될지만 본다. 되면 Moved.</summary>
        public static StorageTransferResult Check(ItemInventory bag, StorageContents chest, StorageDirection direction, ItemDefinition item, int count)
            => Plan(bag, chest, direction, item, count, out _);

        public static StorageTransferResult TryMove(ItemInventory bag, StorageContents chest, StorageDirection direction, ItemDefinition item, int count,
            string requestId, Action<FurnitureStorageSaveData> commit)
        {
            if (string.IsNullOrEmpty(requestId) || commit == null) return StorageTransferResult.Invalid;
            if (chest != null && chest.Knows(requestId)) return StorageTransferResult.AlreadyDone;
            var result = Plan(bag, chest, direction, item, count, out var next);
            if (result != StorageTransferResult.Moved) return result;

            var saved = StorageContents.Save(next, chest.Requests, requestId);
            bool moved = direction == StorageDirection.Put
                ? bag.TryExchange(Grants(item, count), null, () => commit(saved))
                : bag.TryExchange(null, Grants(item, count), () => commit(saved));
            return moved ? StorageTransferResult.Moved : StorageTransferResult.NotEnough;
        }

        private static StorageTransferResult Plan(ItemInventory bag, StorageContents chest, StorageDirection direction, ItemDefinition item, int count,
            out List<ItemStack<ItemDefinition>> next)
        {
            next = null;
            if (bag == null || chest == null) return StorageTransferResult.NotStorage;
            if (item == null || count <= 0 || chest.IsUnknown(item)) return StorageTransferResult.Invalid;
            if (bag.Currency != null && ReferenceEquals(item, bag.Currency)) return StorageTransferResult.Currency;
            var amount = new[] { new ItemAmount<ItemDefinition>(item, count) };
            if (direction == StorageDirection.Put)
            {
                if (bag.CountOf(item) < count) return StorageTransferResult.NotEnough;
                if (!InventoryPlanner.TryPlanExchange(chest.Stacks, chest.MaxSlots, null, amount, i => i.MaxStack, out next)) return StorageTransferResult.NoRoom;
                return bag.CanExchange(Grants(item, count), null) ? StorageTransferResult.Moved : StorageTransferResult.NotEnough;
            }

            if (chest.CountOf(item) < count) return StorageTransferResult.NotEnough;
            if (!InventoryPlanner.TryPlanExchange(chest.Stacks, chest.MaxSlots, amount, null, i => i.MaxStack, out next)) return StorageTransferResult.NotEnough;
            return bag.CanExchange(null, Grants(item, count)) ? StorageTransferResult.Moved : StorageTransferResult.NoRoom;
        }

        private static InventoryGrant[] Grants(ItemDefinition item, int count) => new[] { new InventoryGrant(item, count) };
    }
}
