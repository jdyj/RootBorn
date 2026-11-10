using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>세이브 파일에 쓰는 모양. 필드 이름은 기존 세이브 파일과 같아야 한다.</summary>
    [Serializable]
    public sealed class CareerCandidateProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public CandidateEntry[] Candidates = Array.Empty<CandidateEntry>();
        public string[] AppliedSourceRequestIds = Array.Empty<string>();
        public string[] ClaimedRewardIds = Array.Empty<string>();
        public string[] TodayHintResultLines = Array.Empty<string>();

        [Serializable]
        public struct CandidateEntry
        {
            public string CandidateId;
            public int UnderstandingScore;
            public string[] HintIds;
            public string LastHintSourceId;
        }
    }

    public readonly struct CareerCandidateHintApplyResult
    {
        public readonly bool Applied;
        public readonly string CandidateId;
        public readonly string HintId;
        public readonly CareerCandidateState NewState;
        public readonly int UnderstandingScore;

        public CareerCandidateHintApplyResult(bool applied, string candidateId, string hintId, CareerCandidateState newState, int understandingScore)
        {
            Applied = applied;
            CandidateId = string.IsNullOrEmpty(candidateId) ? string.Empty : candidateId;
            HintId = string.IsNullOrEmpty(hintId) ? string.Empty : hintId;
            NewState = newState;
            UnderstandingScore = Math.Max(0, understandingScore);
        }
    }

    /// <summary>
    /// 한 플레이어의 진로 후보 진행. 같은 출처 요청은 한 번만 센다. 맞지 않은 요청은 쓰지 않은 것으로 되돌린다.
    /// 오늘의 힌트 결과 줄은 다음 날 시작에 지운다.
    /// </summary>
    public sealed class CareerCandidateProgress
    {
        private readonly Dictionary<string, CandidateStateData> _candidates = new Dictionary<string, CandidateStateData>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedSourceRequestIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _claimedRewardIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _todayHintResultLines = new List<string>();

        public CareerCandidateProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public string[] TodayHintResultLines => _todayHintResultLines.ToArray();

        public CareerCandidateHintApplyResult ApplyHint(CareerHint hint, StudentLifeProgress studentProgress, string sourceRequestId, string[] resultLogIds)
        {
            if (hint == null || hint.Candidate == null) return new CareerCandidateHintApplyResult(false, string.Empty, string.Empty, CareerCandidateState.Locked, 0);
            string requestId = string.IsNullOrEmpty(sourceRequestId) ? hint.Id : sourceRequestId;
            if (string.IsNullOrEmpty(requestId) || !_appliedSourceRequestIds.Add(requestId))
                return new CareerCandidateHintApplyResult(false, hint.Candidate.Id, hint.Id, hint.Candidate.GetState(this), GetUnderstandingScore(hint.Candidate));

            var context = new CareerCandidateEvaluationContext(hint.Candidate, this, studentProgress, requestId, resultLogIds);
            if (!hint.Matches(context))
            {
                _appliedSourceRequestIds.Remove(requestId);
                return new CareerCandidateHintApplyResult(false, hint.Candidate.Id, hint.Id, hint.Candidate.GetState(this), GetUnderstandingScore(hint.Candidate));
            }

            CandidateStateData state = GetOrCreate(hint.Candidate.Id);
            if (hint.AppliesOnce && state.HintIds.Contains(hint.Id))
            {
                _appliedSourceRequestIds.Remove(requestId);
                return new CareerCandidateHintApplyResult(false, hint.Candidate.Id, hint.Id, hint.Candidate.GetState(this), state.UnderstandingScore);
            }

            CareerText.AddUnique(state.HintIds, hint.Id);
            state.UnderstandingScore = Math.Max(0, state.UnderstandingScore + hint.UnderstandingDelta);
            state.LastHintSourceId = requestId;
            var newState = hint.Candidate.GetState(this);
            CareerText.AddUnique(_todayHintResultLines, hint.Candidate.Id + ":" + hint.Id + ":" + newState);
            return new CareerCandidateHintApplyResult(true, hint.Candidate.Id, hint.Id, newState, state.UnderstandingScore);
        }

        public CareerCandidateHintApplyResult ApplyFirstMatchingHint(IReadOnlyList<CareerHint> hints, StudentLifeProgress studentProgress, string sourceRequestId, string[] resultLogIds)
        {
            if (hints == null) return new CareerCandidateHintApplyResult(false, string.Empty, string.Empty, CareerCandidateState.Locked, 0);
            for (int i = 0; i < hints.Count; i++)
            {
                var result = ApplyHint(hints[i], studentProgress, sourceRequestId, resultLogIds);
                if (result.Applied) return result;
            }
            return new CareerCandidateHintApplyResult(false, string.Empty, string.Empty, CareerCandidateState.Locked, 0);
        }

        /// <summary>깊어짐 단계부터 후보 보상을 받는다. 보상마다 한 번만 준다.</summary>
        public bool TryClaimRewards(CareerCandidate candidate, StudentLifeProgress studentProgress)
        {
            if (candidate == null || candidate.GetState(this) < CareerCandidateState.Deepening) return false;
            var context = new CareerCandidateEvaluationContext(candidate, this, studentProgress, string.Empty, Array.Empty<string>());
            bool appliedAny = false;
            for (int i = 0; i < candidate.Rewards.Count; i++)
            {
                var reward = candidate.Rewards[i];
                if (reward == null || string.IsNullOrEmpty(reward.RewardId) || !_claimedRewardIds.Add(candidate.Id + ":" + reward.RewardId)) continue;
                reward.Apply(context);
                appliedAny = true;
            }
            return appliedAny;
        }

        public bool HasAppliedSource(string sourceRequestId) => !string.IsNullOrEmpty(sourceRequestId) && _appliedSourceRequestIds.Contains(sourceRequestId);
        public bool HasClaimedReward(CareerCandidate candidate, ICareerCandidateReward reward) => candidate != null && reward != null && _claimedRewardIds.Contains(candidate.Id + ":" + reward.RewardId);
        public int GetUnderstandingScore(CareerCandidate candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.UnderstandingScore : 0;
        public int GetHintCount(CareerCandidate candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.HintIds.Count : 0;
        public string[] GetHintIds(CareerCandidate candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.HintIds.ToArray() : Array.Empty<string>();
        public string GetLastHintSourceId(CareerCandidate candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.LastHintSourceId : string.Empty;
        public void ClearTodayHintResults() => _todayHintResultLines.Clear();

        public CareerCandidateProgressSaveData ToSaveData()
        {
            var entries = new CareerCandidateProgressSaveData.CandidateEntry[_candidates.Count];
            int index = 0;
            foreach (var pair in _candidates)
            {
                entries[index++] = new CareerCandidateProgressSaveData.CandidateEntry
                {
                    CandidateId = pair.Key,
                    UnderstandingScore = pair.Value.UnderstandingScore,
                    HintIds = pair.Value.HintIds.ToArray(),
                    LastHintSourceId = pair.Value.LastHintSourceId,
                };
            }
            return new CareerCandidateProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Candidates = entries,
                AppliedSourceRequestIds = CareerText.ToArray(_appliedSourceRequestIds),
                ClaimedRewardIds = CareerText.ToArray(_claimedRewardIds),
                TodayHintResultLines = _todayHintResultLines.ToArray(),
            };
        }

        public static CareerCandidateProgress FromSaveData(CareerCandidateProgressSaveData saveData)
        {
            if (saveData == null) return new CareerCandidateProgress("default", "player");
            var progress = new CareerCandidateProgress(saveData.SaveSlot, saveData.PlayerId);
            if (saveData.Candidates != null)
            {
                for (int i = 0; i < saveData.Candidates.Length; i++)
                {
                    var entry = saveData.Candidates[i];
                    if (string.IsNullOrEmpty(entry.CandidateId)) continue;
                    var state = progress.GetOrCreate(entry.CandidateId);
                    state.UnderstandingScore = Math.Max(0, entry.UnderstandingScore);
                    state.LastHintSourceId = string.IsNullOrEmpty(entry.LastHintSourceId) ? string.Empty : entry.LastHintSourceId;
                    if (entry.HintIds != null)
                        for (int hintIndex = 0; hintIndex < entry.HintIds.Length; hintIndex++) CareerText.AddUnique(state.HintIds, entry.HintIds[hintIndex]);
                }
            }
            CareerText.RestoreSet(progress._appliedSourceRequestIds, saveData.AppliedSourceRequestIds);
            CareerText.RestoreSet(progress._claimedRewardIds, saveData.ClaimedRewardIds);
            CareerText.RestoreOrdered(progress._todayHintResultLines, saveData.TodayHintResultLines);
            return progress;
        }

        private CandidateStateData GetOrCreate(string candidateId)
        {
            string key = string.IsNullOrEmpty(candidateId) ? string.Empty : candidateId;
            if (!_candidates.TryGetValue(key, out var state))
            {
                state = new CandidateStateData();
                _candidates[key] = state;
            }
            return state;
        }

        private sealed class CandidateStateData
        {
            public int UnderstandingScore;
            public string LastHintSourceId = string.Empty;
            public readonly List<string> HintIds = new List<string>();
        }
    }

    public readonly struct CareerCandidateSummary
    {
        public readonly string CandidateId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly CareerCandidateState State;
        public readonly int HintCount;
        public readonly int UnderstandingScore;
        public readonly string[] HintIds;
        public readonly string[] RelatedLocationKeys;
        public readonly string[] RelatedActivityKeys;
        public readonly string[] RequirementLines;
        public readonly string[] RecommendedActionKeys;

        public CareerCandidateSummary(string candidateId, string displayName, string description, CareerCandidateState state, int hintCount, int understandingScore,
            string[] hintIds, string[] relatedLocationKeys, string[] relatedActivityKeys, string[] requirementLines, string[] recommendedActionKeys)
        {
            CandidateId = string.IsNullOrEmpty(candidateId) ? string.Empty : candidateId;
            DisplayName = string.IsNullOrEmpty(displayName) ? "???" : displayName;
            Description = string.IsNullOrEmpty(description) ? string.Empty : description;
            State = state;
            HintCount = Math.Max(0, hintCount);
            UnderstandingScore = Math.Max(0, understandingScore);
            HintIds = hintIds ?? Array.Empty<string>();
            RelatedLocationKeys = relatedLocationKeys ?? Array.Empty<string>();
            RelatedActivityKeys = relatedActivityKeys ?? Array.Empty<string>();
            RequirementLines = requirementLines ?? Array.Empty<string>();
            RecommendedActionKeys = recommendedActionKeys ?? Array.Empty<string>();
        }
    }

    /// <summary>진로 후보 화면에 보일 요약. 잠긴 후보는 이름 대신 예고 문구와 부분 힌트를 보인다.</summary>
    public static class CareerCandidateSummaryBuilder
    {
        public static CareerCandidateSummary Build(CareerCandidate candidate, CareerCandidateProgress progress, StudentLifeProgress studentProgress)
        {
            if (candidate == null) return default;
            var state = candidate.GetState(progress);
            string displayName = state == CareerCandidateState.Locked ? candidate.LockedTeaserKey : candidate.DisplayNameKey;
            string description = state == CareerCandidateState.Locked ? "Partial hints point toward " + FirstRecommended(candidate) : candidate.DescriptionKey;
            return new CareerCandidateSummary(
                candidate.Id,
                displayName,
                description,
                state,
                progress != null ? progress.GetHintCount(candidate) : 0,
                progress != null ? progress.GetUnderstandingScore(candidate) : 0,
                progress != null ? progress.GetHintIds(candidate) : Array.Empty<string>(),
                DisplayKeys(candidate.RelatedLocations),
                DisplayKeys(candidate.RelatedActivities),
                RequirementLines(candidate, progress, studentProgress),
                Recommended(candidate));
        }

        public static CareerCandidateSummary[] BuildAll(IReadOnlyList<CareerCandidate> candidates, CareerCandidateProgress progress, StudentLifeProgress studentProgress)
        {
            if (candidates == null) return Array.Empty<CareerCandidateSummary>();
            var summaries = new List<CareerCandidateSummary>();
            for (int i = 0; i < candidates.Count; i++) if (candidates[i] != null) summaries.Add(Build(candidates[i], progress, studentProgress));
            return summaries.ToArray();
        }

        internal static string[] DisplayKeys(IReadOnlyList<DefinitionRef> refs)
        {
            var values = new List<string>();
            for (int i = 0; i < refs.Count; i++) if (!string.IsNullOrEmpty(refs[i].Id)) values.Add(refs[i].DisplayNameKey);
            return values.ToArray();
        }

        private static string[] RequirementLines(CareerCandidate candidate, CareerCandidateProgress progress, StudentLifeProgress studentProgress)
        {
            var values = new List<string>();
            for (int i = 0; i < candidate.Requirements.Count; i++)
            {
                var requirement = candidate.Requirements[i];
                if (requirement == null) continue;
                var context = new CareerCandidateEvaluationContext(candidate, progress, studentProgress, string.Empty, Array.Empty<string>());
                values.Add(requirement.Name + ":" + (requirement.IsSatisfied(context) ? "met" : "open"));
            }
            return values.ToArray();
        }

        private static string[] Recommended(CareerCandidate candidate)
        {
            var values = new List<string>();
            for (int i = 0; i < candidate.RecommendedActionKeys.Count; i++) if (!string.IsNullOrEmpty(candidate.RecommendedActionKeys[i])) values.Add(candidate.RecommendedActionKeys[i]);
            for (int i = 0; i < candidate.Routes.Count; i++)
            {
                var route = candidate.Routes[i];
                if (route == null) continue;
                for (int actionIndex = 0; actionIndex < route.RecommendedActionKeys.Count; actionIndex++)
                    if (!string.IsNullOrEmpty(route.RecommendedActionKeys[actionIndex])) values.Add(route.RecommendedActionKeys[actionIndex]);
            }
            return values.ToArray();
        }

        private static string FirstRecommended(CareerCandidate candidate)
        {
            var recommended = Recommended(candidate);
            return recommended.Length == 0 ? "a career candidate" : recommended[0];
        }
    }
}
