using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Placement
{
    /// <summary>
    /// 놓인 가구 하나. 필드 이름은 Unity 판 FurniturePlacementSaveData와 같다.
    /// StateJson과 TileName은 판정이 쓰지 않지만 옛 세이브의 값을 그대로 지켜 다시 쓴다.
    /// </summary>
    [Serializable]
    public sealed class FurniturePlacementSaveData
    {
        public string FurnitureId = string.Empty;
        public string SurfaceId = string.Empty;
        public int AnchorX;
        public int AnchorY;
        public FurniturePlacementDirection Direction;
        public string StateJson = string.Empty;
        public string TileName = string.Empty;

        /// <summary>상자의 내용물(MonoGame 판이 더한 필드, 배치 파일 버전 2). 상자가 아니거나 한 번도 쓰지 않았으면 null.</summary>
        public FurnitureStorageSaveData Storage;

        public FurniturePlacementSaveData Clone()
        {
            return new FurniturePlacementSaveData
            {
                FurnitureId = FurnitureId,
                SurfaceId = SurfaceId,
                AnchorX = AnchorX,
                AnchorY = AnchorY,
                Direction = Direction,
                StateJson = StateJson,
                TileName = TileName,
                Storage = Storage?.Clone(),
            };
        }
    }

    /// <summary>집 단계 하나의 가구 배치 파일(house-furniture-layout-stage-*.json)의 맨 위 모양.</summary>
    [Serializable]
    public sealed class FurniturePlacementLayoutSaveData : IVersionedSave
    {
        /// <summary>2: 놓인 가구 기록에 상자 내용물(Storage)이 생겼다. 1 파일은 그대로 읽힌다(내용물 없음).</summary>
        public const int KnownVersion = 2;

        public int Version;

        /// <summary>파일에 없으면 null로 남는다. 그런 파일은 빈 방이 아니라 읽을 수 없는 파일로 본다.</summary>
        public FurniturePlacementSaveData[] Items;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>가구 해금 파일(furniture-unlocks*.json)과 성인 직업 스냅샷의 FurnitureUnlocks. 필드 이름은 Unity 판과 같다.</summary>
    [Serializable]
    public sealed class FurnitureUnlockProgressSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public string[] UnlockTokens = Array.Empty<string>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>
    /// 가구 세이브 파일을 읽고 쓴다. 파일 이름은 SaveFileNames, 형식은 저장소가 맡는다.
    /// 성인 직업 스냅샷이 있으면 해금은 스냅샷의 FurnitureUnlocks가 원본이다(원본 FurnitureUnlockProgressPersistence).
    /// 스냅샷 확인은 Careers 영역이라 여기 없다. 부르는 쪽이 먼저 확인하고 <see cref="FurnitureUnlockProgress.FromSaveData"/>를 쓴다.
    /// </summary>
    public static class FurnitureSaveFiles
    {
        public static FurnitureUnlockProgress LoadUnlocksOrCreate(ISaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? SaveFileNames.LegacyPlayerId : playerId;
            if (store == null || !store.TryRead(slot, SaveFileNames.FurnitureUnlocks(player), out FurnitureUnlockProgressSaveData data))
                return new FurnitureUnlockProgress(slot, player);
            return FurnitureUnlockProgress.FromSaveData(data, slot, player);
        }

        public static void SaveUnlocks(ISaveStore store, FurnitureUnlockProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, SaveFileNames.FurnitureUnlocks(progress.PlayerId), progress.ToSaveData());
        }

        /// <summary>
        /// 그 단계의 배치 기록. 파일이 없으면 빈 목록이다. 0단계는 단계 파일이 없으면 단계가 생기기 전의 파일
        /// (house-furniture-layout.json)을 읽는다. Items가 없는 파일은 예외를 낸다(빈 방으로 덮어쓰지 않게).
        /// </summary>
        public static FurniturePlacementSaveData[] LoadHouseLayout(ISaveStore store, string saveSlot, int stageIndex)
        {
            if (store == null) return Array.Empty<FurniturePlacementSaveData>();
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string fileName = SaveFileNames.HouseFurnitureLayout(stageIndex);
            if (!store.TryRead(slot, fileName, out FurniturePlacementLayoutSaveData layout))
            {
                if (Math.Max(0, stageIndex) != 0 || !store.TryRead(slot, SaveFileNames.HouseFurnitureLayoutBeforeStages, out layout))
                    return Array.Empty<FurniturePlacementSaveData>();
                fileName = SaveFileNames.HouseFurnitureLayoutBeforeStages;
            }

            if (layout.Items == null) throw new InvalidOperationException($"가구 배치 파일 {slot}/{fileName}에 Items가 없다");
            var records = new List<FurniturePlacementSaveData>(layout.Items.Length);
            for (int i = 0; i < layout.Items.Length; i++) if (layout.Items[i] != null) records.Add(layout.Items[i]);
            return records.ToArray();
        }

        /// <summary>지금 집 단계(Unity HouseStatePersistence.Load). 파일이 없으면 0, 음수는 0이다.</summary>
        public static int LoadHouseStage(ISaveStore store, string saveSlot)
        {
            if (store == null) return 0;
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            return store.TryRead(slot, SaveFileNames.HouseState, out HouseStateSaveData state) ? Math.Max(0, state.CurrentStageIndex) : 0;
        }

        public static void SaveHouseLayout(ISaveStore store, string saveSlot, int stageIndex, IReadOnlyList<FurniturePlacementSaveData> records)
        {
            if (store == null) return;
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            var items = new List<FurniturePlacementSaveData>(records == null ? 0 : records.Count);
            if (records != null) for (int i = 0; i < records.Count; i++) if (records[i] != null) items.Add(records[i].Clone());
            store.Write(slot, SaveFileNames.HouseFurnitureLayout(stageIndex), new FurniturePlacementLayoutSaveData { Items = items.ToArray() });
        }
    }
}
