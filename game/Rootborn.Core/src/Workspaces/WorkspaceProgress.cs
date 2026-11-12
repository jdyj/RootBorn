using System;
using System.Collections.Generic;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Core.Workspaces
{
    /// <summary>workspace-progress-*.json의 모양. MonoGame 판이 더한 파일이다. 필드 이름을 바꾸지 않는다.</summary>
    [Serializable]
    public sealed class WorkspaceProgressSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        /// <summary>형식 버전. 새로 만든 모양은 0(버전 없는 파일과 같다)이고 쓸 때 <see cref="KnownVersion"/>을 넣는다.</summary>
        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public string[] AppliedRequestIds = Array.Empty<string>();
        public string[] SeededWorkspaceIds = Array.Empty<string>();
        public int GrowthDay;
        public int GrowthCountToday;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>
    /// 한 플레이어의 일터 진행: 쓴 템플릿 적용 요청, 기본 템플릿을 깐 일터, 오늘 꾸미며 배운 횟수.
    /// 바꾸는 일은 <see cref="WorkspaceService"/>만 한다. 목록은 글자 순서로 쓴다.
    /// </summary>
    public sealed class WorkspaceProgress
    {
        private readonly HashSet<string> _requests = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _seeded = new HashSet<string>(StringComparer.Ordinal);

        public WorkspaceProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? SaveFileNames.LegacyPlayerId : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int GrowthDay { get; private set; }
        public int GrowthCountToday { get; private set; }

        public bool HasApplied(string requestId) => !string.IsNullOrEmpty(requestId) && _requests.Contains(requestId);
        public bool IsSeeded(string workspaceId) => !string.IsNullOrEmpty(workspaceId) && _seeded.Contains(workspaceId);

        /// <summary>그날 더 셀 수 있는 횟수. 센 날이 다르면 한도 전부.</summary>
        public int GrowthLeftToday(int day, int dailyLimit)
        {
            int limit = Math.Max(0, dailyLimit);
            if (day != GrowthDay) return limit;
            return Math.Max(0, limit - GrowthCountToday);
        }

        internal void MarkApplied(string requestId) => _requests.Add(requestId);
        internal void MarkSeeded(string workspaceId) => _seeded.Add(workspaceId);

        internal void CountGrowth(int day)
        {
            if (day != GrowthDay)
            {
                GrowthDay = day;
                GrowthCountToday = 0;
            }
            GrowthCountToday = GrowthCountToday == int.MaxValue ? int.MaxValue : GrowthCountToday + 1;
        }

        public WorkspaceProgressSaveData ToSaveData() => new WorkspaceProgressSaveData
        {
            Version = WorkspaceProgressSaveData.KnownVersion,
            SaveSlot = SaveSlot,
            PlayerId = PlayerId,
            AppliedRequestIds = Sorted(_requests),
            SeededWorkspaceIds = Sorted(_seeded),
            GrowthDay = GrowthDay,
            GrowthCountToday = GrowthCountToday,
        };

        /// <summary>기록의 슬롯·플레이어가 비면 받은 값을 쓴다. 빈 id는 버리고 음수는 0으로 둔다.</summary>
        public static WorkspaceProgress FromSaveData(WorkspaceProgressSaveData data, string fallbackSaveSlot, string fallbackPlayerId)
        {
            var progress = new WorkspaceProgress(
                data != null && !string.IsNullOrEmpty(data.SaveSlot) ? data.SaveSlot : fallbackSaveSlot,
                data != null && !string.IsNullOrEmpty(data.PlayerId) ? data.PlayerId : fallbackPlayerId);
            if (data == null) return progress;
            AddRange(progress._requests, data.AppliedRequestIds);
            AddRange(progress._seeded, data.SeededWorkspaceIds);
            progress.GrowthDay = Math.Max(0, data.GrowthDay);
            progress.GrowthCountToday = Math.Max(0, data.GrowthCountToday);
            return progress;
        }

        private static void AddRange(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] Sorted(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }

    /// <summary>
    /// 일터 진행 파일과 일터 배치 파일 읽기·쓰기. 파일 이름은 SaveFileNames.WorkspaceProgress·WorkspaceLayout.
    /// 배치 파일의 모양은 집 배치 파일(<see cref="FurniturePlacementLayoutSaveData"/>)과 같다.
    /// 높은 버전의 파일은 저장소가 읽지 않는다(예외) — 그러면 새 진행을 만들지 않으므로 덮어쓰지도 않는다.
    /// </summary>
    public static class WorkspaceSaveFiles
    {
        public static WorkspaceProgress LoadOrCreate(ISaveStore store, string saveSlot, string playerId)
        {
            string slot = Slot(saveSlot);
            string player = Player(playerId);
            if (store == null || !store.TryRead(slot, SaveFileNames.WorkspaceProgress(player), out WorkspaceProgressSaveData data))
                return new WorkspaceProgress(slot, player);
            return WorkspaceProgress.FromSaveData(data, slot, player);
        }

        public static void Save(ISaveStore store, WorkspaceProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, SaveFileNames.WorkspaceProgress(progress.PlayerId), progress.ToSaveData());
        }

        /// <summary>그 일터 면의 배치 기록. 파일이 없으면 null(아직 기본 템플릿을 깔지 않은 일터). Items가 없는 파일은 예외를 낸다.</summary>
        public static FurniturePlacementSaveData[] LoadLayout(ISaveStore store, string saveSlot, string surfaceId, string playerId)
        {
            if (store == null) return null;
            string slot = Slot(saveSlot);
            string fileName = SaveFileNames.WorkspaceLayout(surfaceId, Player(playerId));
            if (!store.TryRead(slot, fileName, out FurniturePlacementLayoutSaveData layout)) return null;
            if (layout.Items == null) throw new InvalidOperationException($"일터 배치 파일 {slot}/{fileName}에 Items가 없다");
            var records = new List<FurniturePlacementSaveData>(layout.Items.Length);
            for (int i = 0; i < layout.Items.Length; i++) if (layout.Items[i] != null) records.Add(layout.Items[i]);
            return records.ToArray();
        }

        public static void SaveLayout(ISaveStore store, string saveSlot, string surfaceId, string playerId, IReadOnlyList<FurniturePlacementSaveData> records)
        {
            if (store == null) return;
            var items = new List<FurniturePlacementSaveData>(records == null ? 0 : records.Count);
            if (records != null) for (int i = 0; i < records.Count; i++) if (records[i] != null) items.Add(records[i].Clone());
            store.Write(Slot(saveSlot), SaveFileNames.WorkspaceLayout(surfaceId, Player(playerId)), new FurniturePlacementLayoutSaveData { Items = items.ToArray() });
        }

        private static string Slot(string saveSlot) => string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
        private static string Player(string playerId) => string.IsNullOrEmpty(playerId) ? SaveFileNames.LegacyPlayerId : playerId;
    }
}
