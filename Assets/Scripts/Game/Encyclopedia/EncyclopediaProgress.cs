using System;
using System.Collections.Generic;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Encyclopedia
{
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
                foreach (var record in _records.Values)
                {
                    if (record.IsNew) count++;
                }
                return count;
            }
        }

        public bool TryUnlock(string entryId, int revealStage, long utcTicks)
        {
            if (string.IsNullOrEmpty(entryId) || _records.ContainsKey(entryId))
            {
                return false;
            }

            _records.Add(entryId, new EncyclopediaProgressRecord
            {
                EntryId = entryId,
                RevealStage = Mathf.Max(1, revealStage),
                FirstDiscoveredUtcTicks = utcTicks <= 0L ? DateTime.UtcNow.Ticks : utcTicks,
                IsNew = true,
            });
            return true;
        }

        public bool IsUnlocked(string entryId)
        {
            return !string.IsNullOrEmpty(entryId) && _records.ContainsKey(entryId);
        }

        public int GetRevealStage(string entryId)
        {
            return !string.IsNullOrEmpty(entryId) && _records.TryGetValue(entryId, out var record) ? Mathf.Max(1, record.RevealStage) : 0;
        }

        public bool IsNew(string entryId)
        {
            return !string.IsNullOrEmpty(entryId) && _records.TryGetValue(entryId, out var record) && record.IsNew;
        }

        public bool MarkSeen(string entryId)
        {
            if (string.IsNullOrEmpty(entryId) || !_records.TryGetValue(entryId, out var record) || !record.IsNew)
            {
                return false;
            }

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

            return new EncyclopediaProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Records = records.ToArray(),
            };
        }

        public static EncyclopediaProgress FromSaveData(EncyclopediaProgressSaveData saveData)
        {
            var progress = new EncyclopediaProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null || saveData.Records == null)
            {
                return progress;
            }

            for (int i = 0; i < saveData.Records.Length; i++)
            {
                var record = saveData.Records[i];
                if (record == null || string.IsNullOrEmpty(record.EntryId) || progress._records.ContainsKey(record.EntryId))
                {
                    continue;
                }

                progress._records.Add(record.EntryId, new EncyclopediaProgressRecord
                {
                    EntryId = record.EntryId,
                    RevealStage = Mathf.Max(1, record.RevealStage),
                    FirstDiscoveredUtcTicks = record.FirstDiscoveredUtcTicks,
                    IsNew = record.IsNew,
                });
            }

            return progress;
        }
    }

    public static class EncyclopediaProgressPersistence
    {
        private const string FilePrefix = "encyclopedia-";
        private const string FileExtension = ".json";

        public static EncyclopediaProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(slot).ReadJson(FileNameFor(player));
            if (string.IsNullOrEmpty(json))
            {
                return new EncyclopediaProgress(slot, player);
            }

            try
            {
                return EncyclopediaProgress.FromSaveData(JsonUtility.FromJson<EncyclopediaProgressSaveData>(json));
            }
            catch (ArgumentException)
            {
                return new EncyclopediaProgress(slot, player);
            }
        }

        public static void Save(EncyclopediaProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        private static string FileNameFor(string playerId)
        {
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            return FilePrefix + player + FileExtension;
        }
    }
}
