using System;
using System.Collections.Generic;

namespace Rootborn.Core.WorldState
{
    /// <summary>월드 상태 세이브 파일 읽기·쓰기. 파일이 없거나 읽을 수 없으면 Read가 null을 돌려준다.</summary>
    public interface IWorldStateSaveStore
    {
        WorldStateProgressSaveData Read(string saveSlot, string fileName);
        void Write(string saveSlot, string fileName, WorldStateProgressSaveData data);
    }

    /// <summary>
    /// 플레이어 파일과 슬롯 공유 파일을 나눠 쓰고 합쳐 읽는다(원본 WorldStateProgressPersistence).
    /// 공유 파일에는 Shared·SaveSlot 범위만 들어간다. 파일 형식(JSON)은 저장소가 맡는다.
    /// 원본이 먼저 확인하던 성인 진로 묶음 세이브(AdultCareer*) 경로는 Careers 영역이라 여기 없다. 부르는 쪽이 먼저 확인한다.
    /// </summary>
    public static class WorldStateSaveFiles
    {
        private const string FileName = "world-state-progress.json";
        public const string SharedFileName = "world-state-progress-shared.json";

        public static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == "local-player") return FileName;
            return "world-state-progress-" + Sanitize(playerId) + ".json";
        }

        public static WorldStateProgress LoadOrCreate(IWorldStateSaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            var playerData = store?.Read(slot, FileNameFor(player));
            var sharedData = store?.Read(slot, SharedFileName);
            var merged = MergeSaveData(slot, player, sharedData, playerData);
            return WorldStateProgress.FromSaveData(merged, slot, player);
        }

        public static void Save(IWorldStateSaveStore store, WorldStateProgress progress)
        {
            if (store == null || progress == null) return;
            var saveData = progress.ToSaveData();
            store.Write(progress.SaveSlot, FileNameFor(progress.PlayerId), saveData);
            store.Write(progress.SaveSlot, SharedFileName, FilterByScope(saveData, WorldStateScopeKind.Shared, WorldStateScopeKind.SaveSlot));
        }

        public static WorldStateProgressSaveData FilterByScope(WorldStateProgressSaveData source, params WorldStateScopeKind[] scopes)
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

        /// <summary>뒤에 오는 원본이 같은 상태의 기록을 덮는다(공유 다음 플레이어 순으로 부른다).</summary>
        public static WorldStateProgressSaveData MergeSaveData(string saveSlot, string playerId, params WorldStateProgressSaveData[] sources)
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

        private static bool HasScope(string rawScope, WorldStateScopeKind[] scopes)
        {
            if (!Enum.TryParse(rawScope, out WorldStateScopeKind parsed)) parsed = WorldStateScopeKind.Shared;
            if (scopes == null) return false;
            for (int i = 0; i < scopes.Length; i++) if (parsed == scopes[i]) return true;
            return false;
        }

        internal static string Sanitize(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }
}
