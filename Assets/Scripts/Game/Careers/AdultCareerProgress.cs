using System;
using System.Collections.Generic;

namespace Rootborn.Game.Careers
{
    [Serializable]
    public sealed class PendingFacilityWorkSaveData
    {
        public string RequestId;
        public string WorkId;
        public string MinigameId;
        public int Day;
        public int ReservedTimePoints;
        public int Seed;
        public int FacilityLevel;
        public int RewardPercent;
        public static bool IsEmpty(PendingFacilityWorkSaveData value) => value == null ||
            (string.IsNullOrEmpty(value.RequestId) && string.IsNullOrEmpty(value.WorkId) && string.IsNullOrEmpty(value.MinigameId)
             && value.Day == 0 && value.ReservedTimePoints == 0 && value.Seed == 0 && value.FacilityLevel == 0 && value.RewardPercent == 0);
        public static PendingFacilityWorkSaveData Copy(PendingFacilityWorkSaveData value)
        {
            return IsEmpty(value) ? null : new PendingFacilityWorkSaveData { RequestId = value.RequestId, WorkId = value.WorkId, MinigameId = value.MinigameId,
                Day = value.Day, ReservedTimePoints = value.ReservedTimePoints, Seed = value.Seed, FacilityLevel = value.FacilityLevel, RewardPercent = value.RewardPercent };
        }
    }
    [Serializable]
    public sealed class FacilityWorkProgressSaveData
    {
        public string WorkId;
        public int Mastery;
        public int SuccessfulCompletions;
    }

    [Serializable]
    public sealed class AdultCareerProgressSaveData
    {
        public int Version = 1;
        public string SaveSlot;
        public string PlayerId;
        public string FacilityId;
        public int FacilityLevel;
        public int Reputation;
        public FacilityWorkProgressSaveData[] Works = Array.Empty<FacilityWorkProgressSaveData>();
        public string[] AppliedRequestIds = Array.Empty<string>();
        public string[] EndingIds = Array.Empty<string>();
        public PendingFacilityWorkSaveData PendingWork;
    }

    public sealed class AdultCareerProgress
    {
        private readonly Dictionary<string, FacilityWorkProgressSaveData> _works = new Dictionary<string, FacilityWorkProgressSaveData>(StringComparer.Ordinal);
        private readonly HashSet<string> _requests = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _endings = new HashSet<string>(StringComparer.Ordinal);
        private PendingFacilityWorkSaveData _pendingWork;
        public AdultCareerProgress(string saveSlot, string playerId)
        { SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot; PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId; }
        public string SaveSlot { get; }
        public string PlayerId { get; }
        public bool IsAdult => !string.IsNullOrEmpty(FacilityId) && FacilityLevel > 0;
        public string FacilityId { get; private set; } = string.Empty;
        public int FacilityLevel { get; private set; }
        public int Reputation { get; private set; }
        public string[] EndingIds => Sorted(_endings);
        public bool HasPendingWork => _pendingWork != null;
        public PendingFacilityWorkSaveData PendingWork => PendingFacilityWorkSaveData.Copy(_pendingWork);
        public int GetMastery(FacilityWorkDefinition work) => work != null && _works.TryGetValue(work.Id, out var record) ? record.Mastery : 0;
        public int GetSuccessfulCompletions(FacilityWorkDefinition work) => work != null && _works.TryGetValue(work.Id, out var record) ? record.SuccessfulCompletions : 0;
        public bool HasEnding(FacilityDefinition facility) => facility != null && _endings.Contains(facility.Id);
        internal bool HasAppliedRequest(string request) => !string.IsNullOrEmpty(request) && _requests.Contains(request);
        internal void Select(FacilityDefinition facility, string request)
        { FacilityId = facility.Id; FacilityLevel = 1; _requests.Add(request); }
        internal void ReserveWork(FacilityWorkDefinition work, string request, int day, int seed, int rewardPercent)
        {
            _pendingWork = new PendingFacilityWorkSaveData { RequestId = request, WorkId = work.Id, MinigameId = work.Minigame.Id,
                Day = day, ReservedTimePoints = work.TimePointCost, Seed = seed, FacilityLevel = FacilityLevel, RewardPercent = rewardPercent };
        }
        internal void ForfeitPendingWork()
        {
            if (_pendingWork == null) return;
            _requests.Add(_pendingWork.RequestId); _pendingWork = null;
        }
        internal void CompleteWork(FacilityWorkDefinition work, string request, bool success)
        {
            if (success)
            {
                if (!_works.TryGetValue(work.Id, out var record)) { record = new FacilityWorkProgressSaveData { WorkId = work.Id }; _works.Add(work.Id, record); }
                record.Mastery = (int)Math.Min(work.MaximumMastery, (long)record.Mastery + work.MasteryPerSuccess);
                record.SuccessfulCompletions = (int)Math.Min(int.MaxValue, (long)record.SuccessfulCompletions + 1);
                Reputation = (int)Math.Min(int.MaxValue, (long)Reputation + work.ReputationPerSuccess);
            }
            _requests.Add(request);
            if (_pendingWork != null && _pendingWork.RequestId == request) _pendingWork = null;
        }
        internal void Upgrade(FacilityDefinition facility, FacilityLevelDefinition level, string request)
        { FacilityLevel = level.Level; if (level.Level == 3 && level.CompletesRoute) _endings.Add(facility.Id); _requests.Add(request); }
        public AdultCareerProgressSaveData ToSaveData()
        {
            var keys = new List<string>(_works.Keys); keys.Sort(StringComparer.Ordinal);
            var records = new FacilityWorkProgressSaveData[keys.Count];
            for (int i = 0; i < keys.Count; i++) { var record = _works[keys[i]]; records[i] = new FacilityWorkProgressSaveData { WorkId = record.WorkId, Mastery = record.Mastery, SuccessfulCompletions = record.SuccessfulCompletions }; }
            return new AdultCareerProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, FacilityId = FacilityId, FacilityLevel = FacilityLevel, Reputation = Reputation, Works = records, AppliedRequestIds = Sorted(_requests), EndingIds = Sorted(_endings), PendingWork = PendingFacilityWorkSaveData.Copy(_pendingWork) };
        }
        public static AdultCareerProgress FromSaveData(AdultCareerProgressSaveData data)
        {
            var progress = new AdultCareerProgress(data?.SaveSlot, data?.PlayerId);
            if (data == null) return progress;
            progress.FacilityId = data.FacilityId ?? string.Empty;
            progress.FacilityLevel = string.IsNullOrEmpty(progress.FacilityId) ? 0 : Math.Max(1, Math.Min(3, data.FacilityLevel));
            progress.Reputation = Math.Max(0, data.Reputation);
            progress._pendingWork = PendingFacilityWorkSaveData.Copy(data.PendingWork);
            if (data.Works != null)
            {
                for (int i = 0; i < data.Works.Length; i++)
                {
                    var saved = data.Works[i]; if (saved == null || string.IsNullOrEmpty(saved.WorkId)) continue;
                    if (!progress._works.TryGetValue(saved.WorkId, out var record)) { record = new FacilityWorkProgressSaveData { WorkId = saved.WorkId }; progress._works.Add(saved.WorkId, record); }
                    record.Mastery = Math.Max(record.Mastery, Math.Max(0, saved.Mastery));
                    record.SuccessfulCompletions = Math.Max(record.SuccessfulCompletions, Math.Max(0, saved.SuccessfulCompletions));
                }
            }
            Restore(progress._requests, data.AppliedRequestIds); Restore(progress._endings, data.EndingIds); return progress;
        }
        private static void Restore(HashSet<string> target, string[] values) { if (values == null) return; for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]); }
        private static string[] Sorted(HashSet<string> values) { var result = new string[values.Count]; values.CopyTo(result); Array.Sort(result, StringComparer.Ordinal); return result; }
    }
}
