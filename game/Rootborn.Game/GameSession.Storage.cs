using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;

namespace Rootborn.Game
{
    /// <summary>
    /// 상자: 놓인 가구 가운데 보관 칸이 있는 것(데이터 FurnitureStorageDefinition). 안 쓰는 물건을 넣어 두고 꺼낸다.
    /// - 혼자 하기와 집을 각자 쓰는 방: 내용물은 놓인 가구 기록(배치 파일)에 묶인다. 옮기기는 가방과 상자를 함께 확인하고 함께 바꾸며,
    ///   가방(스냅샷)과 배치 파일을 한 저장 묶음으로 쓴다(도중에 꺼져도 전부 옛것이거나 전부 새것). 같은 요청 id는 한 번만.
    /// - 집을 같이 쓰는 방: 상자의 내용물은 방의 것(RoomChests). 넣기·꺼내기는 잡기 요청만 적고(<see cref="WaitingForStorage"/>),
    ///   방이 허락하면 그때 가방을 바꾸며 같은 바꾸기 안에서 쓰기를 적는다. 가방이 못 받으면 풀기를 적는다 — 되돌리는 흐름 없음.
    /// 가방은 늘 각자다. 가방끼리 주고받는 길은 없다.
    /// </summary>
    public sealed partial class GameSession
    {
        private static readonly string[] StorageTopics = { GameChangeTopics.Storage, GameChangeTopics.Inventory };
        private FurnitureStorageCatalog _storageCatalog;
        private bool _houseLayoutDirty;
        private RoomChestsClient _roomChests;
        private string _storageWaiting;

        private FurnitureStorageCatalog StorageCatalog => _storageCatalog ??= new FurnitureStorageCatalog(_definitions.All<FurnitureStorageDefinition>());

        /// <summary>그 가구가 상자다(데이터).</summary>
        public bool IsStorage(FurnitureDefinition furniture) => StorageCatalog.IsStorage(furniture);

        /// <summary>같이 쓰는 상자의 넣기·꺼내기가 방의 허락을 기다린다(화면은 그동안 옮기기를 막는다).</summary>
        public bool WaitingForStorage => _storageWaiting != null;

        /// <summary>마지막 옮기기의 결과(같이 쓰는 상자는 허락·거절이 온 뒤의 결과). 화면이 안내 문구를 고른다.</summary>
        public StorageTransferResult? LastStorageResult { get; private set; }

        /// <summary>집의 그 가구가 상자면 그 내용물(같이 쓰는 집이면 방이 알린 것). 상자가 아니거나 없으면 null.</summary>
        public StorageContents StorageAt(string instanceId)
        {
            var placed = Find(instanceId);
            if (placed == null) return null;
            int slots = StorageCatalog.SlotsOf(placed.Definition.StableId);
            if (slots <= 0) return null;
            if (_roomChests == null) return StorageContents.From(House.StorageOf(instanceId), slots, FindItem);

            var stacks = _roomChests.ContentsOf(instanceId);
            var items = new PlayerInventoryItemSaveData[stacks.Count];
            for (int i = 0; i < items.Length; i++) items[i] = new PlayerInventoryItemSaveData { ItemId = stacks[i].Item, Count = stacks[i].Count };
            return StorageContents.From(new FurnitureStorageSaveData { Items = items }, slots, FindItem);
        }

        /// <summary>가방에서 상자로 넣는다. requestId가 없으면 새로 만든다(같은 요청을 다시 보내면 한 번만 반영된다).</summary>
        public StorageTransferResult PutIntoStorage(string instanceId, string itemId, int count, string requestId = null)
            => MoveStorage(instanceId, StorageDirection.Put, itemId, count, requestId);

        /// <summary>상자에서 가방으로 꺼낸다.</summary>
        public StorageTransferResult TakeFromStorage(string instanceId, string itemId, int count, string requestId = null)
            => MoveStorage(instanceId, StorageDirection.Take, itemId, count, requestId);

        private void UseSharedChests(RoomShares shares) => _roomChests = shares?.Find<RoomChestsClient>();

        private void ListenToSharedChests()
        {
            if (_roomChests != null) _roomChests.Answered += OnStorageAnswered;
        }

        private StorageTransferResult MoveStorage(string instanceId, StorageDirection direction, string itemId, int count, string requestId)
        {
            var chest = StorageAt(instanceId);
            if (chest == null) return Result(StorageTransferResult.NotStorage);
            var item = string.IsNullOrEmpty(itemId) ? null : FindItem(itemId);
            string request = string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString("N") : requestId;
            if (_roomChests != null) return RequestSharedStorage(instanceId, chest, direction, item, count);

            var result = StorageTransfer.TryMove(Inventory, chest, direction, item, count, request, next =>
            {
                House.SetStorage(instanceId, next);
                _houseLayoutDirty = true;
            });
            if (result == StorageTransferResult.Moved) SaveAndNotify(StorageTopics);
            return Result(result);
        }

        // 같이 쓰는 상자: 지금 보이는 것으로 될지 먼저 보고(방이 최종 판정한다) 잡기 요청을 적는다. 한 번에 하나만 기다린다.
        private StorageTransferResult RequestSharedStorage(string instanceId, StorageContents chest, StorageDirection direction, ItemDefinition item, int count)
        {
            if (_storageWaiting != null) return StorageTransferResult.Busy;
            var check = StorageTransfer.Check(Inventory, chest, direction, item, count);
            if (check != StorageTransferResult.Moved) return Result(check);
            LastStorageResult = null;
            _storageWaiting = direction == StorageDirection.Put
                ? _roomChests.RequestPut(instanceId, item.Id, count)
                : _roomChests.RequestTake(instanceId, item.Id, count);
            SaveAndNotify(StorageTopics, GameChangeTopics.Room);
            return StorageTransferResult.Requested;
        }

        // 방이 잡기에 답했다: 허락이면 가방을 바꾸고 그 바꾸기 안에서 쓰기를 적는다. 가방이 못 하면(그 사이 바뀜) 풀기를 적는다.
        private void OnStorageAnswered(RoomRequestSaveData request, bool applied)
        {
            if (request.RequestId == _storageWaiting) _storageWaiting = null;
            bool put = request.Kind == RoomChests.Put;
            if (!applied)
            {
                LastStorageResult = put ? StorageTransferResult.NoRoom : StorageTransferResult.NotEnough;
                SaveAndNotify(StorageTopics, GameChangeTopics.Room);
                return;
            }

            var item = request.Ids != null && request.Ids.Length > 1 ? FindItem(request.Ids[1]) : null;
            var grants = item == null ? null : new[] { new InventoryGrant(item, request.Amount) };
            string hold = request.RequestId;
            bool done = grants != null && (put
                ? Inventory.TryExchange(grants, null, () => _roomChests.AddUse(hold))
                : Inventory.TryExchange(null, grants, () => _roomChests.AddUse(hold)));
            if (!done) _roomChests.AddRelease(hold);
            LastStorageResult = done ? StorageTransferResult.Moved : put ? StorageTransferResult.NotEnough : StorageTransferResult.NoRoom;
            SaveAndNotify(StorageTopics, GameChangeTopics.Room);
        }

        private StorageTransferResult Result(StorageTransferResult result)
        {
            LastStorageResult = result;
            return result;
        }

        // 저장 묶음 안에서: 상자 내용물이 바뀌었으면 배치 파일도 같은 묶음으로 쓴다(가방과 함께).
        private void SaveHouseLayoutIfDirty(Rootborn.Core.Save.ISaveStore batch)
        {
            if (!_houseLayoutDirty || House == null || _roomHouse != null) return;
            FurnitureSaveFiles.SaveHouseLayout(batch, SaveSlot, HouseStage, House.BuildSaveRecords());
        }
    }
}
