using System.Collections.Generic;
using Rootborn.Core.Characters;
using Rootborn.Core.Save;

namespace Rootborn.World
{
    /// <summary>
    /// 플레이어의 옷 선택을 세이브 슬롯에서 읽는다(Unity WearableWardrobeStore.Load + GrowingCharacterStageDriver.Wearables).
    /// 옛 선택(만들기 화면이 슬롯 정보에 남긴 "outfit:몸" 부위)은 처음 한 번 옮기고, 옮긴 결과를 써서 다시 옮기지 않는다.
    /// Unity 판의 몸별 옛 옷장 파일(GrowingWardrobeStore)은 읽지 않는다.
    /// </summary>
    public static class PlayerWearables
    {
        /// <summary>Unity GrowingCharacterStageSelector.OutfitCategoryPrefix.</summary>
        public const string LegacyOutfitCategoryPrefix = "outfit:";

        public static WearableState Load(ISaveStore store, string slotId, string playerId, SaveSlotMetadata metadata, WearableCatalog catalog, CharacterArt art)
        {
            if (store == null || string.IsNullOrEmpty(slotId)) return new WearableState();
            string file = SaveFileNames.Wearables(playerId);
            store.TryRead(slotId, file, out WearableSaveData saved);
            var creator = new List<KeyValuePair<string, string>>();
            if (metadata?.CharacterAppearanceSnapshot?.selectedParts != null)
                foreach (CharacterAppearancePartSaveData part in metadata.CharacterAppearanceSnapshot.selectedParts)
                    if (part != null) creator.Add(new KeyValuePair<string, string>(part.categoryId, part.partId));
            List<string> legacyIds = LegacyWardrobe.LookIds(null, creator, LegacyOutfitCategoryPrefix);
            WearableLoadResult result = WearableSaveRules.Load(catalog, saved, legacyIds, art.LegacyAppearanceMap());
            if (result.NeedsSave) store.Write(slotId, file, result.ToSaveData());
            return result.State;
        }
    }
}
