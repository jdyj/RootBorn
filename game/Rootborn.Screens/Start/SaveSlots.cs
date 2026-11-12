using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Screens.Start
{
    /// <summary>슬롯 한 칸의 요약: 슬롯 id, 이야기가 있는지, 슬롯 정보(없으면 null).</summary>
    public sealed class SaveSlotSummary
    {
        public SaveSlotSummary(string slotId, SaveSlotMetadata metadata, bool unreadable)
        {
            SlotId = slotId;
            Metadata = metadata;
            Unreadable = unreadable;
        }

        public string SlotId { get; }
        /// <summary>읽힌 슬롯 정보. 없거나 읽을 수 없으면 null.</summary>
        public SaveSlotMetadata Metadata { get; }
        /// <summary>슬롯 정보 파일이 있는데 읽을 수 없다(깨짐·더 새 버전). 빈 슬롯이 아니다: 새 이야기를 쓰지 않고, 지우려면 확인을 거친다.</summary>
        public bool Unreadable { get; }
        public bool Exists => Metadata != null;
    }

    /// <summary>
    /// 화면이 쓰는 세이브 슬롯(Unity SaveService의 UI 슬롯 부분): 슬롯 수, 슬롯 목록, 새 이야기 시작, 지우기, 새 슬롯 정보 만들기.
    /// 파일은 ISaveStore로만 읽고 쓴다. 슬롯 폴더를 통째로 지우는 일은 ISaveStore에 없어 만든 쪽이 넘긴다(보고서 "필요한 변경").
    /// </summary>
    public sealed class SaveSlots
    {
        /// <summary>Unity SaveService.MaxUiSlots. 슬롯 수는 여기에만 둔다.</summary>
        public const int MaxUiSlots = 3;

        // Unity SaveSlotSelectPanel.ConfirmSpumCreator의 시드 곱수.
        private const int WorldSeedFactor = 397, TileSeedFactor = 491;

        private readonly ISaveStore _store;
        private readonly Action<string> _deleteSlotFolder;

        /// <param name="deleteSlotFolder">슬롯 폴더와 그 안의 파일을 모두 지운다(없으면 아무것도 하지 않는다).</param>
        public SaveSlots(ISaveStore store, Action<string> deleteSlotFolder)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _deleteSlotFolder = deleteSlotFolder ?? throw new ArgumentNullException(nameof(deleteSlotFolder));
        }

        public static string SlotId(int index) => "slot-" + index;

        public static bool IsUiSlot(string slotId)
        {
            for (int i = 0; i < MaxUiSlots; i++) if (slotId == SlotId(i)) return true;
            return false;
        }

        /// <summary>UI 슬롯 전부(Unity ListUiSlots). 읽을 수 없는 슬롯 정보는 Unreadable로 알린다(Unity 판은 빈 슬롯으로 보였다).</summary>
        public IReadOnlyList<SaveSlotSummary> List()
        {
            var result = new List<SaveSlotSummary>(MaxUiSlots);
            for (int i = 0; i < MaxUiSlots; i++) result.Add(Read(SlotId(i)));
            return result;
        }

        public SaveSlotSummary Read(string slotId)
        {
            try
            {
                if (_store.TryRead(slotId, SaveFileNames.Metadata, out SaveSlotMetadata metadata) && metadata != null && !string.IsNullOrEmpty(metadata.SlotId))
                {
                    metadata.FillMissing();
                    return new SaveSlotSummary(slotId, metadata, false);
                }
                return new SaveSlotSummary(slotId, null, false);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                return new SaveSlotSummary(slotId, null, true);
            }
        }

        /// <summary>
        /// 새 이야기의 슬롯 정보(Unity CreateUiMetadata + ConfirmSpumCreator): 시드는 지금 시각 값과 슬롯 id로 만든다(새 이야기마다 다르다).
        /// 이름은 앞뒤 공백을 지운 값, 비면 슬롯 id. 겉모습은 만들기 화면이 고른 부위.
        /// </summary>
        public static SaveSlotMetadata CreateMetadata(string slotId, string playerName, CharacterAppearanceSnapshotSaveData snapshot, long nowUtcTicks, int tickCount)
        {
            if (!IsUiSlot(slotId)) throw new ArgumentException("UI 슬롯은 slot-0부터 slot-" + (MaxUiSlots - 1) + "까지다: " + slotId, nameof(slotId));
            int slotHash = SaveSlotMetadata.StableHash(slotId, 0);
            var metadata = new SaveSlotMetadata
            {
                Version = SaveSlotMetadata.KnownVersion,
                SlotId = slotId,
                DisplayName = string.IsNullOrWhiteSpace(playerName) ? slotId : playerName.Trim(),
                CreatedAtUtcTicks = nowUtcTicks,
                UpdatedAtUtcTicks = nowUtcTicks,
                WorldSeed = unchecked(tickCount * WorldSeedFactor) ^ slotHash,
                TileSeed = unchecked(tickCount * TileSeedFactor) ^ (slotHash << 1),
                CharacterAppearanceSnapshot = snapshot,
            };
            metadata.FillMissing();
            return metadata;
        }

        /// <summary>
        /// 이야기가 없는 슬롯에 새 이야기를 쓴다(Unity StartNewSlot): 읽히는 이야기가 있거나 슬롯 정보를 읽을 수 없으면 false이고 아무것도 바꾸지 않는다
        /// (Unity 판은 읽을 수 없는 슬롯을 지우고 새로 썼다 — 깨졌거나 더 새 버전인 기록이 하루 사본까지 사라졌다).
        /// 슬롯 정보가 없으면 슬롯에 남은 파일을 먼저 지우고 슬롯 정보를 쓴다.
        /// </summary>
        public bool StartNewSlot(SaveSlotMetadata metadata)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (!IsUiSlot(metadata.SlotId)) return false;
            SaveSlotSummary now = Read(metadata.SlotId);
            if (now.Exists || now.Unreadable) return false;
            _deleteSlotFolder(metadata.SlotId);
            _store.Write(metadata.SlotId, SaveFileNames.Metadata, metadata);
            return true;
        }

        /// <summary>슬롯을 지운다(Unity DeleteSlot: 슬롯 폴더 통째로).</summary>
        public void Delete(string slotId)
        {
            if (!IsUiSlot(slotId)) throw new ArgumentException("UI 슬롯이 아니다: " + slotId, nameof(slotId));
            _deleteSlotFolder(slotId);
        }
    }
}
