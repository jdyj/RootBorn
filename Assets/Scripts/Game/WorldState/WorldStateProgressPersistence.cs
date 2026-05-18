using System;
using System.Collections.Generic;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    public static class WorldStateProgressPersistence
    {
        private const string FileName = "world-state-progress.json";
        private const string SharedFileName = "world-state-progress-shared.json";

        public static WorldStateProgress LoadOrCreate(string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string fileName = FileNameFor(player);
            var playerData = ReadSaveData(slot, fileName);
            var sharedData = ReadSaveData(slot, SharedFileName);
            var merged = MergeSaveData(slot, player, sharedData, playerData);
            return WorldStateProgress.FromSaveData(merged, slot, player);
        }

        public static void Save(WorldStateProgress progress)
        {
            if (progress == null) return;
            var saveData = progress.ToSaveData();
            string fileName = FileNameFor(progress.PlayerId);
            WriteSaveData(progress.SaveSlot, fileName, saveData);
            WriteSaveData(progress.SaveSlot, SharedFileName, FilterByScope(saveData, WorldStateScopeKind.Shared, WorldStateScopeKind.SaveSlot));
        }

        public static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == "local-player") return FileName;
            return "world-state-progress-" + Sanitize(playerId) + ".json";
        }

        private static WorldStateProgressSaveData ReadSaveData(string saveSlot, string fileName)
        {
            string json = new SaveService(saveSlot).ReadJson(fileName);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<WorldStateProgressSaveData>(json);
        }

        private static void WriteSaveData(string saveSlot, string fileName, WorldStateProgressSaveData saveData)
        {
            string json = JsonUtility.ToJson(saveData ?? new WorldStateProgressSaveData(), true);
            new SaveService(saveSlot).WriteJson(fileName, json);
        }

        private static WorldStateProgressSaveData FilterByScope(WorldStateProgressSaveData source, params WorldStateScopeKind[] scopes)
        {
            if (source == null) return new WorldStateProgressSaveData();
            var records = new List<WorldStateFlagRecordSaveData>();
            var activeIds = new HashSet<string>(StringComparer.Ordinal);
            if (source.Records != null)
            {
                for (int i = 0; i < source.Records.Length; i++)
                {
                    var record = source.Records[i];
                    if (record == null || string.IsNullOrEmpty(record.FlagId) || !HasScope(record.Scope, scopes)) continue;
                    records.Add(record);
                    activeIds.Add(record.FlagId);
                }
            }

            var today = new List<string>();
            if (source.TodayWorldChangeFlagIds != null)
            {
                for (int i = 0; i < source.TodayWorldChangeFlagIds.Length; i++)
                {
                    string flagId = source.TodayWorldChangeFlagIds[i];
                    if (!string.IsNullOrEmpty(flagId) && activeIds.Contains(flagId) && !today.Contains(flagId)) today.Add(flagId);
                }
            }

            return new WorldStateProgressSaveData
            {
                SaveSlot = source.SaveSlot,
                PlayerId = string.Empty,
                Records = records.ToArray(),
                TodayWorldChangeFlagIds = today.ToArray(),
            };
        }

        private static bool HasScope(string rawScope, WorldStateScopeKind[] scopes)
        {
            if (!Enum.TryParse(rawScope, out WorldStateScopeKind parsed)) parsed = WorldStateScopeKind.Shared;
            if (scopes == null) return false;
            for (int i = 0; i < scopes.Length; i++) if (parsed == scopes[i]) return true;
            return false;
        }

        private static WorldStateProgressSaveData MergeSaveData(string saveSlot, string playerId, params WorldStateProgressSaveData[] sources)
        {
            var records = new Dictionary<string, WorldStateFlagRecordSaveData>(StringComparer.Ordinal);
            var today = new List<string>();
            if (sources != null)
            {
                for (int i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (source == null) continue;
                    if (source.Records != null)
                    {
                        for (int r = 0; r < source.Records.Length; r++)
                        {
                            var record = source.Records[r];
                            if (record == null || string.IsNullOrEmpty(record.FlagId)) continue;
                            records[record.FlagId] = record;
                        }
                    }

                    if (source.TodayWorldChangeFlagIds != null)
                    {
                        for (int t = 0; t < source.TodayWorldChangeFlagIds.Length; t++)
                        {
                            string flagId = source.TodayWorldChangeFlagIds[t];
                            if (!string.IsNullOrEmpty(flagId) && !today.Contains(flagId)) today.Add(flagId);
                        }
                    }
                }
            }

            return new WorldStateProgressSaveData
            {
                SaveSlot = saveSlot,
                PlayerId = playerId,
                Records = new List<WorldStateFlagRecordSaveData>(records.Values).ToArray(),
                TodayWorldChangeFlagIds = today.ToArray(),
            };
        }

        private static string Sanitize(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }
}
