using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Placement
{
    /// <summary>
    /// 플레이어가 얻은 가구 해금(원본 Assets/Scripts/Game/Placement/FurnitureUnlockProgress.cs).
    /// 퀘스트의 가구 해금 보상은 <see cref="IQuestFurnitureUnlocks"/>로 이것을 쓴다.
    /// </summary>
    public sealed class FurnitureUnlockProgress : IQuestFurnitureUnlocks
    {
        private readonly HashSet<string> _unlockTokens = new HashSet<string>(StringComparer.Ordinal);

        public FurnitureUnlockProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int UnlockedCount => _unlockTokens.Count;
        public IReadOnlyCollection<string> UnlockTokens => _unlockTokens;

        public bool IsUnlocked(string unlockToken) => !string.IsNullOrEmpty(unlockToken) && _unlockTokens.Contains(unlockToken);

        /// <summary>새로 얻었으면 true. 빈 해금과 이미 가진 해금은 false.</summary>
        public bool TryUnlock(string unlockToken) => !string.IsNullOrEmpty(unlockToken) && _unlockTokens.Add(unlockToken);

        /// <summary>해금은 글자 순서로 정렬해 쓴다(원본과 같다).</summary>
        public FurnitureUnlockProgressSaveData ToSaveData()
        {
            var tokens = new List<string>(_unlockTokens);
            tokens.Sort(StringComparer.Ordinal);
            return new FurnitureUnlockProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, UnlockTokens = tokens.ToArray() };
        }

        /// <summary>같은 슬롯·플레이어의 기록일 때만 지금 해금을 그 기록으로 바꾼다.</summary>
        public bool RestoreFromSaveData(FurnitureUnlockProgressSaveData data)
        {
            if (!IsMine(data)) return false;
            _unlockTokens.Clear();
            AddAll(data.UnlockTokens);
            return true;
        }

        /// <summary>같은 슬롯·플레이어의 기록일 때만 그 해금을 더한다.</summary>
        public bool MergeFromSaveData(FurnitureUnlockProgressSaveData data)
        {
            if (!IsMine(data)) return false;
            AddAll(data.UnlockTokens);
            return true;
        }

        public static FurnitureUnlockProgress FromSaveData(FurnitureUnlockProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId)
        {
            var progress = new FurnitureUnlockProgress(
                saveData != null && !string.IsNullOrEmpty(saveData.SaveSlot) ? saveData.SaveSlot : fallbackSaveSlot,
                saveData != null && !string.IsNullOrEmpty(saveData.PlayerId) ? saveData.PlayerId : fallbackPlayerId);
            if (saveData != null) progress.AddAll(saveData.UnlockTokens);
            return progress;
        }

        private bool IsMine(FurnitureUnlockProgressSaveData data)
            => data != null && data.SaveSlot == SaveSlot && data.PlayerId == PlayerId && data.UnlockTokens != null;

        private void AddAll(string[] tokens)
        {
            if (tokens == null) return;
            for (int i = 0; i < tokens.Length; i++) TryUnlock(tokens[i]);
        }
    }

    public enum FurniturePurchaseResult { Purchased, NotForSale, AlreadyOwned, NotEnoughMoney, Unavailable }

    /// <summary>가게에서 가구를 사면 값을 내고 그 해금을 얻는다. 둘은 함께 바뀌고, 실패하면 아무것도 바뀌지 않는다.</summary>
    public static class FurniturePurchase
    {
        public static FurniturePurchaseResult Try(FurnitureDefinition furniture, ItemInventory inventory, FurnitureUnlockProgress unlocks)
        {
            if (furniture == null || !furniture.IsForSale) return FurniturePurchaseResult.NotForSale;
            if (inventory == null || unlocks == null) return FurniturePurchaseResult.Unavailable;
            if (unlocks.IsUnlocked(furniture.UnlockToken)) return FurniturePurchaseResult.AlreadyOwned;
            var cost = new[] { new InventoryGrant(furniture.PriceItem, furniture.Price) };
            return inventory.TryExchange(cost, Array.Empty<InventoryGrant>(), () => unlocks.TryUnlock(furniture.UnlockToken))
                ? FurniturePurchaseResult.Purchased
                : FurniturePurchaseResult.NotEnoughMoney;
        }
    }

    /// <summary>배치 목록에 보이는 가구(원본 InteriorFurnitureCatalog.LoadFurniture의 거르기와 순서).</summary>
    public static class FurnitureCatalog
    {
        /// <summary>
        /// 해금이 필요 없는 가구와 해금한 가구만. 얻은(해금이 필요했던) 가구가 먼저 오고, 그다음 분류와 id 글자 순서.
        /// 해금 상태가 없으면 해금이 필요한 가구는 보이지 않는다.
        /// </summary>
        public static List<FurnitureDefinition> Available(IEnumerable<FurnitureDefinition> catalog, FurnitureUnlockProgress unlocks)
        {
            var result = new List<FurnitureDefinition>();
            if (catalog == null) return result;
            foreach (var definition in catalog)
            {
                if (definition == null) continue;
                if (IsAvailable(definition, unlocks)) result.Add(definition);
            }

            result.Sort(Compare);
            return result;
        }

        /// <summary>목록에 보이는(놓을 수 있는) 가구인가: 해금이 필요 없거나 해금했다.</summary>
        public static bool IsAvailable(FurnitureDefinition definition, FurnitureUnlockProgress unlocks)
            => definition != null && (definition.UnlockToken.Length == 0 || (unlocks != null && unlocks.IsUnlocked(definition.UnlockToken)));

        /// <summary>그 장소에서 파는 가구(가게 화면의 살 목록). 목록 순서 그대로다.</summary>
        public static List<FurnitureDefinition> ForSaleAt(IEnumerable<FurnitureDefinition> catalog, string locationId)
        {
            var result = new List<FurnitureDefinition>();
            if (catalog == null || string.IsNullOrEmpty(locationId)) return result;
            foreach (var definition in catalog)
                if (definition != null && definition.IsForSale && string.Equals(definition.PurchaseLocationId, locationId, StringComparison.Ordinal)) result.Add(definition);
            return result;
        }

        private static int Compare(FurnitureDefinition a, FurnitureDefinition b)
        {
            int byUnlock = (a.UnlockToken.Length == 0 ? 1 : 0).CompareTo(b.UnlockToken.Length == 0 ? 1 : 0);
            if (byUnlock != 0) return byUnlock;
            int byCategory = string.CompareOrdinal(a.Category, b.Category);
            return byCategory != 0 ? byCategory : string.CompareOrdinal(a.StableId, b.StableId);
        }
    }
}
