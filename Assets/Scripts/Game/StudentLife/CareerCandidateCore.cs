using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public enum CareerCandidateState
    {
        Locked,
        Hinted,
        Revealed,
        Deepening,
        ReadyForChoice
    }

    [CreateAssetMenu(fileName = "CareerCandidate_New", menuName = "Rootborn/Student Life/Career Candidates/Candidate")]
    public sealed class CareerCandidateDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private string _lockedTeaserKey = "???";
        [SerializeField] private int _revealedHintThreshold = 2;
        [SerializeField] private int _deepeningHintThreshold = 3;
        [SerializeField] private int _readyHintThreshold = 4;
        [SerializeField] private LocationDefinition[] _relatedLocations = Array.Empty<LocationDefinition>();
        [SerializeField] private LifeActivityDefinition[] _relatedActivities = Array.Empty<LifeActivityDefinition>();
        [SerializeField] private CareerCandidateRouteDefinition[] _routes = Array.Empty<CareerCandidateRouteDefinition>();
        [SerializeField] private CareerCandidateRequirementBase[] _requirements = Array.Empty<CareerCandidateRequirementBase>();
        [SerializeField] private string[] _recommendedActionKeys = Array.Empty<string>();
        [SerializeField] private CareerCandidateRewardBase[] _rewards = Array.Empty<CareerCandidateRewardBase>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public string LockedTeaserKey => string.IsNullOrEmpty(_lockedTeaserKey) ? "???" : _lockedTeaserKey;
        public int RevealedHintThreshold => Mathf.Max(1, _revealedHintThreshold);
        public int DeepeningHintThreshold => Mathf.Max(RevealedHintThreshold + 1, _deepeningHintThreshold);
        public int ReadyHintThreshold => Mathf.Max(DeepeningHintThreshold + 1, _readyHintThreshold);
        public IReadOnlyList<LocationDefinition> RelatedLocations => _relatedLocations;
        public IReadOnlyList<LifeActivityDefinition> RelatedActivities => _relatedActivities;
        public IReadOnlyList<CareerCandidateRouteDefinition> Routes => _routes;
        public IReadOnlyList<CareerCandidateRequirementBase> Requirements => _requirements;
        public IReadOnlyList<string> RecommendedActionKeys => _recommendedActionKeys;
        public IReadOnlyList<CareerCandidateRewardBase> Rewards => _rewards;

        public CareerCandidateState GetState(CareerCandidateProgress progress)
        {
            int score = progress != null ? progress.GetUnderstandingScore(this) : 0;
            if (score >= ReadyHintThreshold) return CareerCandidateState.ReadyForChoice;
            if (score >= DeepeningHintThreshold) return CareerCandidateState.Deepening;
            if (score >= RevealedHintThreshold) return CareerCandidateState.Revealed;
            if (score > 0) return CareerCandidateState.Hinted;
            return CareerCandidateState.Locked;
        }

        public bool AreRequirementsSatisfied(CareerCandidateProgress progress, StudentLifeProgress studentProgress)
        {
            var context = new CareerCandidateEvaluationContext(this, progress, studentProgress, string.Empty, Array.Empty<string>());
            for (int i = 0; i < _requirements.Length; i++)
            {
                if (_requirements[i] != null && !_requirements[i].IsSatisfied(context)) return false;
            }

            return true;
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            string descriptionKey,
            CareerCandidateRouteDefinition[] routes,
            CareerCandidateRequirementBase[] requirements,
            string[] recommendedActionKeys,
            LocationDefinition[] relatedLocations = null,
            LifeActivityDefinition[] relatedActivities = null,
            CareerCandidateRewardBase[] rewards = null,
            int revealedHintThreshold = 2,
            int deepeningHintThreshold = 3,
            int readyHintThreshold = 4)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = descriptionKey;
            _routes = routes ?? Array.Empty<CareerCandidateRouteDefinition>();
            _requirements = requirements ?? Array.Empty<CareerCandidateRequirementBase>();
            _recommendedActionKeys = recommendedActionKeys ?? Array.Empty<string>();
            _relatedLocations = relatedLocations ?? Array.Empty<LocationDefinition>();
            _relatedActivities = relatedActivities ?? Array.Empty<LifeActivityDefinition>();
            _rewards = rewards ?? Array.Empty<CareerCandidateRewardBase>();
            _revealedHintThreshold = revealedHintThreshold;
            _deepeningHintThreshold = deepeningHintThreshold;
            _readyHintThreshold = readyHintThreshold;
        }
    }

    public readonly struct CareerCandidateEvaluationContext
    {
        public readonly CareerCandidateDefinition Candidate;
        public readonly CareerCandidateProgress CandidateProgress;
        public readonly StudentLifeProgress StudentProgress;
        public readonly string SourceRequestId;
        public readonly string[] ResultLogIds;

        public CareerCandidateEvaluationContext(CareerCandidateDefinition candidate, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, string sourceRequestId, string[] resultLogIds)
        {
            Candidate = candidate;
            CandidateProgress = candidateProgress;
            StudentProgress = studentProgress;
            SourceRequestId = string.IsNullOrEmpty(sourceRequestId) ? string.Empty : sourceRequestId;
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }
    }

    public abstract class CareerCandidateRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(CareerCandidateEvaluationContext context);
    }

    public abstract class CareerHintSourceBase : ScriptableObject
    {
        public abstract bool Matches(CareerCandidateEvaluationContext context);
    }

    public abstract class CareerCandidateRewardBase : ScriptableObject
    {
        public abstract string RewardId { get; }
        public abstract void Apply(CareerCandidateEvaluationContext context);
    }

    [CreateAssetMenu(fileName = "CareerCandidateReq_HintCount", menuName = "Rootborn/Student Life/Career Candidates/Requirements/Hint Count")]
    public sealed class CareerCandidateHintCountRequirement : CareerCandidateRequirementBase
    {
        [SerializeField] private int _minimumHintCount = 1;

        public override bool IsSatisfied(CareerCandidateEvaluationContext context)
        {
            return context.CandidateProgress != null && context.CandidateProgress.GetHintCount(context.Candidate) >= Mathf.Max(1, _minimumHintCount);
        }

        public void ConfigureForTests(int minimumHintCount) => _minimumHintCount = minimumHintCount;
    }

    [CreateAssetMenu(fileName = "CareerCandidateReq_Skill", menuName = "Rootborn/Student Life/Career Candidates/Requirements/Skill Threshold")]
    public sealed class CareerCandidateSkillRequirement : CareerCandidateRequirementBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _minimumValue = 1;

        public override bool IsSatisfied(CareerCandidateEvaluationContext context)
        {
            return context.StudentProgress != null && _skill != null && context.StudentProgress.GetSkillValue(_skill) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(SkillDefinition skill, int minimumValue)
        {
            _skill = skill;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "CareerCandidateReq_ActivityLog", menuName = "Rootborn/Student Life/Career Candidates/Requirements/Activity Log")]
    public sealed class CareerCandidateActivityLogRequirement : CareerCandidateRequirementBase
    {
        [SerializeField] private LifeActivityDefinition _activity;

        public override bool IsSatisfied(CareerCandidateEvaluationContext context)
        {
            if (context.StudentProgress == null || _activity == null) return false;
            return Contains(context.StudentProgress.GetActivityLogIds(), _activity.Id);
        }

        public void ConfigureForTests(LifeActivityDefinition activity) => _activity = activity;
        private static bool Contains(string[] values, string target)
        {
            if (string.IsNullOrEmpty(target) || values == null) return false;
            for (int i = 0; i < values.Length; i++) if (values[i] == target) return true;
            return false;
        }
    }

    [CreateAssetMenu(fileName = "CareerHintSource_ActivityLog", menuName = "Rootborn/Student Life/Career Candidates/Hint Sources/Activity Log")]
    public sealed class CareerActivityLogHintSource : CareerHintSourceBase
    {
        [SerializeField] private LifeActivityDefinition _activity;

        public override bool Matches(CareerCandidateEvaluationContext context)
        {
            if (context.StudentProgress == null || _activity == null) return false;
            return Contains(context.StudentProgress.GetActivityLogIds(), _activity.Id) || Contains(context.StudentProgress.GetTodayActivityIds(), _activity.Id);
        }

        public void ConfigureForTests(LifeActivityDefinition activity) => _activity = activity;
        private static bool Contains(string[] values, string target)
        {
            if (string.IsNullOrEmpty(target) || values == null) return false;
            for (int i = 0; i < values.Length; i++) if (values[i] == target) return true;
            return false;
        }
    }

    [CreateAssetMenu(fileName = "CareerCandidateReward_CareerHint", menuName = "Rootborn/Student Life/Career Candidates/Rewards/Career Hint")]
    public sealed class CareerCandidateCareerHintReward : CareerCandidateRewardBase
    {
        [SerializeField] private string _rewardId;
        [SerializeField] private CareerDefinition _career;

        public override string RewardId => string.IsNullOrEmpty(_rewardId) ? name : _rewardId;

        public override void Apply(CareerCandidateEvaluationContext context)
        {
            if (context.StudentProgress != null && _career != null) context.StudentProgress.UnlockCareerHint(_career);
        }

        public void ConfigureForTests(string rewardId, CareerDefinition career)
        {
            _rewardId = rewardId;
            _career = career;
        }
    }

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
            UnderstandingScore = Mathf.Max(0, understandingScore);
        }
    }

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

        public CareerCandidateHintApplyResult ApplyHint(CareerHintDefinition hint, StudentLifeProgress studentProgress, string sourceRequestId, string[] resultLogIds)
        {
            if (hint == null || hint.Candidate == null) return new CareerCandidateHintApplyResult(false, string.Empty, string.Empty, CareerCandidateState.Locked, 0);
            string requestId = string.IsNullOrEmpty(sourceRequestId) ? hint.Id : sourceRequestId;
            if (string.IsNullOrEmpty(requestId) || !_appliedSourceRequestIds.Add(requestId))
            {
                return new CareerCandidateHintApplyResult(false, hint.Candidate.Id, hint.Id, hint.Candidate.GetState(this), GetUnderstandingScore(hint.Candidate));
            }

            var context = new CareerCandidateEvaluationContext(hint.Candidate, this, studentProgress, requestId, resultLogIds);
            if (!hint.Matches(context))
            {
                _appliedSourceRequestIds.Remove(requestId);
                return new CareerCandidateHintApplyResult(false, hint.Candidate.Id, hint.Id, hint.Candidate.GetState(this), GetUnderstandingScore(hint.Candidate));
            }

            CandidateStateData state = GetOrCreate(hint.Candidate.Id);
            AddUnique(state.HintIds, hint.Id);
            state.UnderstandingScore = Mathf.Max(0, state.UnderstandingScore + hint.UnderstandingDelta);
            state.LastHintSourceId = requestId;
            var newState = hint.Candidate.GetState(this);
            string line = hint.Candidate.Id + ":" + hint.Id + ":" + newState;
            AddUnique(_todayHintResultLines, line);
            return new CareerCandidateHintApplyResult(true, hint.Candidate.Id, hint.Id, newState, state.UnderstandingScore);
        }

        public CareerCandidateHintApplyResult ApplyFirstMatchingHint(IReadOnlyList<CareerHintDefinition> hints, StudentLifeProgress studentProgress, string sourceRequestId, string[] resultLogIds)
        {
            if (hints == null) return new CareerCandidateHintApplyResult(false, string.Empty, string.Empty, CareerCandidateState.Locked, 0);
            for (int i = 0; i < hints.Count; i++)
            {
                var result = ApplyHint(hints[i], studentProgress, sourceRequestId, resultLogIds);
                if (result.Applied) return result;
            }

            return new CareerCandidateHintApplyResult(false, string.Empty, string.Empty, CareerCandidateState.Locked, 0);
        }

        public bool TryClaimRewards(CareerCandidateDefinition candidate, StudentLifeProgress studentProgress)
        {
            if (candidate == null || candidate.Rewards == null || candidate.GetState(this) < CareerCandidateState.Deepening) return false;
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
        public bool HasClaimedReward(CareerCandidateDefinition candidate, CareerCandidateRewardBase reward) => candidate != null && reward != null && _claimedRewardIds.Contains(candidate.Id + ":" + reward.RewardId);
        public int GetUnderstandingScore(CareerCandidateDefinition candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.UnderstandingScore : 0;
        public int GetHintCount(CareerCandidateDefinition candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.HintIds.Count : 0;
        public string[] GetHintIds(CareerCandidateDefinition candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.HintIds.ToArray() : Array.Empty<string>();
        public string GetLastHintSourceId(CareerCandidateDefinition candidate) => candidate != null && _candidates.TryGetValue(candidate.Id, out var state) ? state.LastHintSourceId : string.Empty;
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
                AppliedSourceRequestIds = ToArray(_appliedSourceRequestIds),
                ClaimedRewardIds = ToArray(_claimedRewardIds),
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
                    state.UnderstandingScore = Mathf.Max(0, entry.UnderstandingScore);
                    state.LastHintSourceId = string.IsNullOrEmpty(entry.LastHintSourceId) ? string.Empty : entry.LastHintSourceId;
                    if (entry.HintIds != null)
                    {
                        for (int hintIndex = 0; hintIndex < entry.HintIds.Length; hintIndex++) AddUnique(state.HintIds, entry.HintIds[hintIndex]);
                    }
                }
            }

            RestoreStrings(progress._appliedSourceRequestIds, saveData.AppliedSourceRequestIds);
            RestoreStrings(progress._claimedRewardIds, saveData.ClaimedRewardIds);
            RestoreOrderedStrings(progress._todayHintResultLines, saveData.TodayHintResultLines);
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

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value);
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static void RestoreStrings(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static void RestoreOrderedStrings(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) AddUnique(target, values[i]);
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

        public CareerCandidateSummary(
            string candidateId,
            string displayName,
            string description,
            CareerCandidateState state,
            int hintCount,
            int understandingScore,
            string[] hintIds,
            string[] relatedLocationKeys,
            string[] relatedActivityKeys,
            string[] requirementLines,
            string[] recommendedActionKeys)
        {
            CandidateId = string.IsNullOrEmpty(candidateId) ? string.Empty : candidateId;
            DisplayName = string.IsNullOrEmpty(displayName) ? "???" : displayName;
            Description = string.IsNullOrEmpty(description) ? string.Empty : description;
            State = state;
            HintCount = Mathf.Max(0, hintCount);
            UnderstandingScore = Mathf.Max(0, understandingScore);
            HintIds = hintIds ?? Array.Empty<string>();
            RelatedLocationKeys = relatedLocationKeys ?? Array.Empty<string>();
            RelatedActivityKeys = relatedActivityKeys ?? Array.Empty<string>();
            RequirementLines = requirementLines ?? Array.Empty<string>();
            RecommendedActionKeys = recommendedActionKeys ?? Array.Empty<string>();
        }
    }

    public static class CareerCandidateSummaryBuilder
    {
        public static CareerCandidateSummary Build(CareerCandidateDefinition candidate, CareerCandidateProgress progress, StudentLifeProgress studentProgress)
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
                LocationKeys(candidate),
                ActivityKeys(candidate),
                RequirementLines(candidate, progress, studentProgress),
                Recommended(candidate));
        }

        public static CareerCandidateSummary[] BuildAll(IReadOnlyList<CareerCandidateDefinition> candidates, CareerCandidateProgress progress, StudentLifeProgress studentProgress)
        {
            if (candidates == null) return Array.Empty<CareerCandidateSummary>();
            var summaries = new List<CareerCandidateSummary>();
            for (int i = 0; i < candidates.Count; i++) if (candidates[i] != null) summaries.Add(Build(candidates[i], progress, studentProgress));
            return summaries.ToArray();
        }

        private static string[] LocationKeys(CareerCandidateDefinition candidate)
        {
            var values = new List<string>();
            for (int i = 0; i < candidate.RelatedLocations.Count; i++) if (candidate.RelatedLocations[i] != null) values.Add(candidate.RelatedLocations[i].DisplayNameKey);
            return values.ToArray();
        }

        private static string[] ActivityKeys(CareerCandidateDefinition candidate)
        {
            var values = new List<string>();
            for (int i = 0; i < candidate.RelatedActivities.Count; i++) if (candidate.RelatedActivities[i] != null) values.Add(candidate.RelatedActivities[i].DisplayNameKey);
            return values.ToArray();
        }

        private static string[] RequirementLines(CareerCandidateDefinition candidate, CareerCandidateProgress progress, StudentLifeProgress studentProgress)
        {
            var values = new List<string>();
            for (int i = 0; i < candidate.Requirements.Count; i++)
            {
                var requirement = candidate.Requirements[i];
                if (requirement == null) continue;
                var context = new CareerCandidateEvaluationContext(candidate, progress, studentProgress, string.Empty, Array.Empty<string>());
                values.Add(requirement.name + ":" + (requirement.IsSatisfied(context) ? "met" : "open"));
            }

            return values.ToArray();
        }

        private static string[] Recommended(CareerCandidateDefinition candidate)
        {
            var values = new List<string>();
            for (int i = 0; i < candidate.RecommendedActionKeys.Count; i++) if (!string.IsNullOrEmpty(candidate.RecommendedActionKeys[i])) values.Add(candidate.RecommendedActionKeys[i]);
            for (int i = 0; i < candidate.Routes.Count; i++)
            {
                var route = candidate.Routes[i];
                if (route == null) continue;
                for (int actionIndex = 0; actionIndex < route.RecommendedActionKeys.Count; actionIndex++) if (!string.IsNullOrEmpty(route.RecommendedActionKeys[actionIndex])) values.Add(route.RecommendedActionKeys[actionIndex]);
            }

            return values.ToArray();
        }

        private static string FirstRecommended(CareerCandidateDefinition candidate)
        {
            var recommended = Recommended(candidate);
            return recommended.Length == 0 ? "a career candidate" : recommended[0];
        }
    }
}
