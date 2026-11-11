using System;
using System.Collections.Generic;

namespace Rootborn.Core.Encyclopedia
{
    /// <summary>세이브 파일에 쓰는 모양. 필드 이름은 기존 세이브 파일과 같아야 한다.</summary>
    [Serializable]
    public sealed class EncyclopediaProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public EncyclopediaProgressRecord[] Records = Array.Empty<EncyclopediaProgressRecord>();
    }

    [Serializable]
    public sealed class EncyclopediaProgressRecord
    {
        public string EntryId;
        public int RevealStage;
        public long FirstDiscoveredUtcTicks;
        public bool IsNew;
    }

    /// <summary>한 플레이어가 해금한 도감 항목. 같은 항목은 한 번만 해금된다.</summary>
    public sealed class EncyclopediaProgress
    {
        private readonly Dictionary<string, EncyclopediaProgressRecord> _records = new Dictionary<string, EncyclopediaProgressRecord>(StringComparer.Ordinal);

        public EncyclopediaProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int UnlockedCount => _records.Count;

        public int NewCount
        {
            get
            {
                int count = 0;
                foreach (var record in _records.Values) if (record.IsNew) count++;
                return count;
            }
        }

        /// <summary>처음 해금할 때만 true. <paramref name="utcTicks"/>가 0 이하면 지금 시각을 쓴다(원본과 같다).</summary>
        public bool TryUnlock(string entryId, int revealStage, long utcTicks)
        {
            if (string.IsNullOrEmpty(entryId) || _records.ContainsKey(entryId)) return false;
            _records.Add(entryId, new EncyclopediaProgressRecord
            {
                EntryId = entryId,
                RevealStage = Math.Max(1, revealStage),
                FirstDiscoveredUtcTicks = utcTicks <= 0L ? DateTime.UtcNow.Ticks : utcTicks,
                IsNew = true,
            });
            return true;
        }

        public bool IsUnlocked(string entryId) => !string.IsNullOrEmpty(entryId) && _records.ContainsKey(entryId);

        public int GetRevealStage(string entryId)
            => !string.IsNullOrEmpty(entryId) && _records.TryGetValue(entryId, out var record) ? Math.Max(1, record.RevealStage) : 0;

        public bool IsNew(string entryId) => !string.IsNullOrEmpty(entryId) && _records.TryGetValue(entryId, out var record) && record.IsNew;

        public bool MarkSeen(string entryId)
        {
            if (string.IsNullOrEmpty(entryId) || !_records.TryGetValue(entryId, out var record) || !record.IsNew) return false;
            record.IsNew = false;
            return true;
        }

        public EncyclopediaProgressSaveData ToSaveData()
        {
            var records = new List<EncyclopediaProgressRecord>(_records.Count);
            foreach (var record in _records.Values)
            {
                records.Add(new EncyclopediaProgressRecord
                {
                    EntryId = record.EntryId,
                    RevealStage = record.RevealStage,
                    FirstDiscoveredUtcTicks = record.FirstDiscoveredUtcTicks,
                    IsNew = record.IsNew,
                });
            }

            return new EncyclopediaProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records.ToArray() };
        }

        public static EncyclopediaProgress FromSaveData(EncyclopediaProgressSaveData saveData)
        {
            var progress = new EncyclopediaProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null || saveData.Records == null) return progress;
            for (int i = 0; i < saveData.Records.Length; i++)
            {
                var record = saveData.Records[i];
                if (record == null || string.IsNullOrEmpty(record.EntryId) || progress._records.ContainsKey(record.EntryId)) continue;
                progress._records.Add(record.EntryId, new EncyclopediaProgressRecord
                {
                    EntryId = record.EntryId,
                    RevealStage = Math.Max(1, record.RevealStage),
                    FirstDiscoveredUtcTicks = record.FirstDiscoveredUtcTicks,
                    IsNew = record.IsNew,
                });
            }
            return progress;
        }
    }

    /// <summary>세이브 파일 읽기·쓰기. 파일이 없거나 읽을 수 없으면 Read가 null을 돌려준다.</summary>
    public interface IEncyclopediaSaveStore
    {
        EncyclopediaProgressSaveData Read(string saveSlot, string fileName);
        void Write(string saveSlot, string fileName, EncyclopediaProgressSaveData data);
    }

    /// <summary>도감 진행의 세이브 파일 이름과 읽기·쓰기 순서. 파일 형식(JSON)은 저장소가 맡는다.</summary>
    public static class EncyclopediaSaveFiles
    {
        private const string FilePrefix = "encyclopedia-";
        private const string FileExtension = ".json";

        /// <summary>플레이어 id를 그대로 붙인다(다른 영역과 달리 문자를 바꾸지 않는다. 원본과 같다).</summary>
        public static string FileNameFor(string playerId)
        {
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            return FilePrefix + player + FileExtension;
        }

        public static EncyclopediaProgress LoadOrCreate(IEncyclopediaSaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            var data = store?.Read(slot, FileNameFor(player));
            return data == null ? new EncyclopediaProgress(slot, player) : EncyclopediaProgress.FromSaveData(data);
        }

        public static void Save(IEncyclopediaSaveStore store, EncyclopediaProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, FileNameFor(progress.PlayerId), progress.ToSaveData());
        }
    }
}
