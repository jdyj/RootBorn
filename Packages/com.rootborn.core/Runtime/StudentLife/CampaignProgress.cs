using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.StudentLife
{
    /// <summary>세이브 파일 모양. 필드 이름을 바꾸지 않는다.</summary>
    [Serializable]
    public sealed class CampaignProgressSaveData : IVersionedSave
    {
        /// <summary>이 프로그램이 아는 가장 높은 형식 버전. 0은 Unity 판이 쓴 파일(Version 필드 없음)이다.</summary>
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public string ActiveCampaignId;
        public int CurrentDayNumber = 1;
        public string[] CompletedObjectiveIds = Array.Empty<string>();
        public SelectedRouteSaveData[] SelectedRoutes = Array.Empty<SelectedRouteSaveData>();
        public string[] RewardClaimedIds = Array.Empty<string>();
        public string[] AppliedResultIds = Array.Empty<string>();
        public string[] LastProgressLogIds = Array.Empty<string>();

        [Serializable]
        public struct SelectedRouteSaveData
        {
            public string DayId;
            public string RouteId;
        }

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>한 플레이어의 캠페인 진행. 끝낸 목표, 날마다 고른 경로, 받은 보상, 적용한 결과 id.</summary>
    public sealed class CampaignProgress
    {
        private readonly HashSet<string> _completedObjectiveIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _selectedRouteIdsByDay = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _rewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedResultIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _lastProgressLogIds = new List<string>();

        public CampaignProgress(string saveSlot, string playerId, Campaign campaign)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActiveCampaignId = campaign != null ? campaign.Id : string.Empty;
            CurrentDayNumber = 1;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public string ActiveCampaignId { get; private set; }
        public int CurrentDayNumber { get; private set; }
        public string[] LastProgressLogIds => _lastProgressLogIds.ToArray();

        public void SetCurrentDayNumber(int dayNumber) => CurrentDayNumber = Math.Max(1, dayNumber);
        public bool MarkResultApplied(string resultId) => !string.IsNullOrEmpty(resultId) && _appliedResultIds.Add(resultId);
        public bool IsResultApplied(string resultId) => !string.IsNullOrEmpty(resultId) && _appliedResultIds.Contains(resultId);
        public bool IsObjectiveCompleted(string objectiveId) => !string.IsNullOrEmpty(objectiveId) && _completedObjectiveIds.Contains(objectiveId);

        public bool CompleteObjective(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId)) return false;
            bool added = _completedObjectiveIds.Add(objectiveId);
            if (added && !_lastProgressLogIds.Contains(objectiveId)) _lastProgressLogIds.Add(objectiveId);
            return added;
        }

        public bool SelectRoute(CampaignDay day, CampaignRoute route)
        {
            if (day == null || route == null || string.IsNullOrEmpty(day.Id) || string.IsNullOrEmpty(route.Id)) return false;
            _selectedRouteIdsByDay[day.Id] = route.Id;
            return true;
        }

        public string GetSelectedRouteId(string dayId) => !string.IsNullOrEmpty(dayId) && _selectedRouteIdsByDay.TryGetValue(dayId, out string routeId) ? routeId : string.Empty;

        /// <summary>
        /// 모든 보상이 적용 가능할 때만 한꺼번에 준다. 같은 requestId는 한 번만.
        /// 진행 상태가 없으면 거절하고 아무것도 기록하지 않는다(원본은 받은 것으로 기록만 했다).
        /// </summary>
        public bool TryClaimRewards(Campaign campaign, StudentLifeProgress studentProgress, string requestId)
        {
            if (campaign == null || studentProgress == null || string.IsNullOrEmpty(requestId) || _rewardClaimedIds.Contains(requestId)) return false;
            IReadOnlyList<CampaignReward> rewards = campaign.Rewards;
            for (int i = 0; i < rewards.Count; i++) if (rewards[i] != null && !rewards[i].CanApply(studentProgress)) return false;
            _rewardClaimedIds.Add(requestId);
            for (int i = 0; i < rewards.Count; i++)
            {
                CampaignReward reward = rewards[i];
                if (reward == null) continue;
                if (!string.IsNullOrEmpty(reward.Id)) _rewardClaimedIds.Add(reward.Id);
                reward.Apply(studentProgress);
            }
            return true;
        }

        public CampaignProgressSaveData ToSaveData()
        {
            var selected = new List<CampaignProgressSaveData.SelectedRouteSaveData>();
            foreach (var pair in _selectedRouteIdsByDay) selected.Add(new CampaignProgressSaveData.SelectedRouteSaveData { DayId = pair.Key, RouteId = pair.Value });
            return new CampaignProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                ActiveCampaignId = ActiveCampaignId,
                CurrentDayNumber = CurrentDayNumber,
                CompletedObjectiveIds = ToArray(_completedObjectiveIds),
                SelectedRoutes = selected.ToArray(),
                RewardClaimedIds = ToArray(_rewardClaimedIds),
                AppliedResultIds = ToArray(_appliedResultIds),
                LastProgressLogIds = _lastProgressLogIds.ToArray(),
            };
        }

        public static CampaignProgress FromSaveData(CampaignProgressSaveData saveData, Campaign campaign)
        {
            var progress = new CampaignProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player", campaign);
            if (saveData == null) return progress;
            progress.ActiveCampaignId = string.IsNullOrEmpty(saveData.ActiveCampaignId) ? (campaign != null ? campaign.Id : string.Empty) : saveData.ActiveCampaignId;
            progress.CurrentDayNumber = Math.Max(1, saveData.CurrentDayNumber);
            AddAll(progress._completedObjectiveIds, saveData.CompletedObjectiveIds);
            AddAll(progress._rewardClaimedIds, saveData.RewardClaimedIds);
            AddAll(progress._appliedResultIds, saveData.AppliedResultIds);
            if (saveData.SelectedRoutes != null)
                for (int i = 0; i < saveData.SelectedRoutes.Length; i++)
                    if (!string.IsNullOrEmpty(saveData.SelectedRoutes[i].DayId)) progress._selectedRouteIdsByDay[saveData.SelectedRoutes[i].DayId] = saveData.SelectedRoutes[i].RouteId;
            if (saveData.LastProgressLogIds != null)
                for (int i = 0; i < saveData.LastProgressLogIds.Length; i++)
                    if (!string.IsNullOrEmpty(saveData.LastProgressLogIds[i])) progress._lastProgressLogIds.Add(saveData.LastProgressLogIds[i]);
            return progress;
        }

        private static void AddAll(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }
    }
}
