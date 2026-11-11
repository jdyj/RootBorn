using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.StudentLife
{
    public enum CareerInterestSelectionState { None, Selected, ChangePending, ChangedToday, LockedByCondition }

    public readonly struct CareerInterestEvaluationContext
    {
        public readonly CareerInterest Interest;
        public readonly CareerInterestProgress InterestProgress;
        public readonly CareerCandidateProgress CandidateProgress;
        public readonly StudentLifeProgress StudentProgress;
        public readonly int Day;

        public CareerInterestEvaluationContext(CareerInterest interest, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            Interest = interest;
            InterestProgress = interestProgress;
            CandidateProgress = candidateProgress;
            StudentProgress = studentProgress;
            Day = Math.Max(0, day);
        }
    }

    public readonly struct CareerInterestSelectionEvaluation
    {
        public readonly bool Allowed;
        public readonly CareerInterestSelectionState State;
        public readonly string Reason;

        public CareerInterestSelectionEvaluation(bool allowed, CareerInterestSelectionState state, string reason)
        {
            Allowed = allowed;
            State = state;
            Reason = string.IsNullOrEmpty(reason) ? string.Empty : reason;
        }

        public static CareerInterestSelectionEvaluation Permit(CareerInterestSelectionState state, string reason) => new CareerInterestSelectionEvaluation(true, state, reason);
        public static CareerInterestSelectionEvaluation Blocked(CareerInterestSelectionState state, string reason) => new CareerInterestSelectionEvaluation(false, state, reason);
    }

    public readonly struct CareerInterestSelectionResult
    {
        public readonly bool Applied;
        public readonly string InterestId;
        public readonly CareerInterestSelectionState State;
        public readonly string Reason;

        public CareerInterestSelectionResult(bool applied, string interestId, CareerInterestSelectionState state, string reason)
        {
            Applied = applied;
            InterestId = string.IsNullOrEmpty(interestId) ? string.Empty : interestId;
            State = state;
            Reason = string.IsNullOrEmpty(reason) ? string.Empty : reason;
        }
    }

    public interface ICareerInterestUnlockRequirement { CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context); }
    public interface ICareerInterestSelectionRule { CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context); }
    public interface ICareerInterestRecommendation { string[] GetRecommendationKeys(CareerInterestEvaluationContext context); }
    public interface ICareerInterestReward { string RewardId { get; } void Apply(CareerInterestEvaluationContext context); }

    /// <summary>
    /// 장래희망(진로 관심) 하나. 고를 수 있는지는 해금 조건 → 선택 규칙 순서로 본다.
    /// 연결 퀘스트와 뒤따르는 NPC는 id로 가리킨다.
    /// </summary>
    public sealed class CareerInterest
    {
        private readonly DefinitionRef[] _relatedLocations;
        private readonly DefinitionRef[] _relatedActivities;
        private readonly ICareerInterestUnlockRequirement[] _unlockRequirements;
        private readonly ICareerInterestSelectionRule[] _selectionRules;
        private readonly ICareerInterestRecommendation[] _recommendations;
        private readonly ICareerInterestReward[] _rewards;
        private readonly string[] _linkedQuestIds;

        public CareerInterest(string id, string displayNameKey, string descriptionKey, CareerCandidate candidate, DefinitionRef[] relatedLocations, DefinitionRef[] relatedActivities,
            ICareerInterestUnlockRequirement[] unlockRequirements, ICareerInterestSelectionRule[] selectionRules, ICareerInterestRecommendation[] recommendations, ICareerInterestReward[] rewards,
            string[] linkedQuestIds = null, string followUpNpcId = null, string followUpLineKey = null, string changedLineKey = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            Candidate = candidate;
            _relatedLocations = relatedLocations ?? Array.Empty<DefinitionRef>();
            _relatedActivities = relatedActivities ?? Array.Empty<DefinitionRef>();
            _unlockRequirements = unlockRequirements ?? Array.Empty<ICareerInterestUnlockRequirement>();
            _selectionRules = selectionRules ?? Array.Empty<ICareerInterestSelectionRule>();
            _recommendations = recommendations ?? Array.Empty<ICareerInterestRecommendation>();
            _rewards = rewards ?? Array.Empty<ICareerInterestReward>();
            _linkedQuestIds = linkedQuestIds ?? Array.Empty<string>();
            FollowUpNpcId = followUpNpcId ?? string.Empty;
            FollowUpLineKey = followUpLineKey ?? string.Empty;
            ChangedLineKey = changedLineKey ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public CareerCandidate Candidate { get; }
        public IReadOnlyList<DefinitionRef> RelatedLocations => _relatedLocations;
        public IReadOnlyList<DefinitionRef> RelatedActivities => _relatedActivities;
        public IReadOnlyList<ICareerInterestUnlockRequirement> UnlockRequirements => _unlockRequirements;
        public IReadOnlyList<ICareerInterestSelectionRule> SelectionRules => _selectionRules;
        public IReadOnlyList<ICareerInterestRecommendation> Recommendations => _recommendations;
        public IReadOnlyList<ICareerInterestReward> Rewards => _rewards;
        public IReadOnlyList<string> LinkedQuestIds => _linkedQuestIds;

        /// <summary>이 장래희망을 고른 뒤 한 번 반응하는 NPC.</summary>
        public string FollowUpNpcId { get; }
        public string FollowUpLineKey { get; }

        /// <summary>다른 장래희망에서 바꿨을 때 반응 대사 앞에 붙는 대사.</summary>
        public string ChangedLineKey { get; }

        public CareerInterestSelectionEvaluation Evaluate(CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            var context = new CareerInterestEvaluationContext(this, interestProgress, candidateProgress, studentProgress, Math.Max(0, day));
            for (int i = 0; i < _unlockRequirements.Length; i++)
            {
                var requirement = _unlockRequirements[i];
                if (requirement == null) continue;
                var result = requirement.Evaluate(context);
                if (!result.Allowed) return result;
            }
            for (int i = 0; i < _selectionRules.Length; i++)
            {
                var rule = _selectionRules[i];
                if (rule == null) continue;
                var result = rule.Evaluate(context);
                if (!result.Allowed) return result;
            }
            return CareerInterestSelectionEvaluation.Permit(CareerInterestSelectionState.Selected, string.Empty);
        }

        public string[] BuildRecommendations(CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            var values = new List<string>();
            var context = new CareerInterestEvaluationContext(this, interestProgress, candidateProgress, studentProgress, Math.Max(0, day));
            for (int i = 0; i < _recommendations.Length; i++)
            {
                var recommendation = _recommendations[i];
                if (recommendation == null) continue;
                var keys = recommendation.GetRecommendationKeys(context);
                if (keys != null) for (int k = 0; k < keys.Length; k++) CareerText.AddUnique(values, keys[k]);
            }
            if (Candidate != null)
                for (int i = 0; i < Candidate.RecommendedActionKeys.Count; i++) CareerText.AddUnique(values, Candidate.RecommendedActionKeys[i]);
            return values.ToArray();
        }
    }

    /// <summary>후보가 정한 상태 이상이어야 고를 수 있다.</summary>
    public sealed class CareerInterestCandidateStateRequirement : ICareerInterestUnlockRequirement
    {
        public CareerInterestCandidateStateRequirement(CareerCandidateState minimumState = CareerCandidateState.Revealed) { MinimumState = minimumState; }
        public CareerCandidateState MinimumState { get; }

        public CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context)
        {
            var candidate = context.Interest != null ? context.Interest.Candidate : null;
            var state = candidate != null ? candidate.GetState(context.CandidateProgress) : CareerCandidateState.Locked;
            return state < MinimumState
                ? CareerInterestSelectionEvaluation.Blocked(CareerInterestSelectionState.LockedByCondition, "candidate state " + state + " below " + MinimumState)
                : CareerInterestSelectionEvaluation.Permit(CareerInterestSelectionState.Selected, string.Empty);
        }
    }

    /// <summary>하루에 바꿀 수 있는 횟수. 같은 것을 다시 고르면 막을 수 있다.</summary>
    public sealed class CareerInterestDailyChangeLimitRule : ICareerInterestSelectionRule
    {
        public CareerInterestDailyChangeLimitRule(int maxChangesPerDay = 1, bool allowSameInterestNoOp = false)
        {
            MaxChangesPerDay = maxChangesPerDay;
            AllowSameInterestNoOp = allowSameInterestNoOp;
        }

        public int MaxChangesPerDay { get; }
        public bool AllowSameInterestNoOp { get; }

        public CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context)
        {
            if (context.InterestProgress == null) return CareerInterestSelectionEvaluation.Permit(CareerInterestSelectionState.Selected, string.Empty);
            string nextId = context.Interest != null ? context.Interest.Id : string.Empty;
            if (AllowSameInterestNoOp && context.InterestProgress.CurrentInterestId == nextId)
                return CareerInterestSelectionEvaluation.Blocked(CareerInterestSelectionState.Selected, "interest already selected");
            if (!string.IsNullOrEmpty(context.InterestProgress.CurrentInterestId) && context.InterestProgress.LastChangedDay == context.Day
                && context.InterestProgress.ChangeCountForCurrentDay >= Math.Max(1, MaxChangesPerDay))
                return CareerInterestSelectionEvaluation.Blocked(CareerInterestSelectionState.ChangedToday, "daily change limit reached");
            return CareerInterestSelectionEvaluation.Permit(CareerInterestSelectionState.Selected, string.Empty);
        }
    }

    public sealed class CareerInterestStaticRecommendation : ICareerInterestRecommendation
    {
        private readonly string[] _keys;
        public CareerInterestStaticRecommendation(string[] recommendationKeys) { _keys = recommendationKeys ?? Array.Empty<string>(); }
        public string[] GetRecommendationKeys(CareerInterestEvaluationContext context) => _keys;
    }

    /// <summary>집중을 지금 값에서 늘린다. 하루 집중을 넘기지 않는다(Unity 판은 상한이 없었다).</summary>
    public sealed class CareerInterestFocusReward : ICareerInterestReward
    {
        public CareerInterestFocusReward(string rewardId, int focusDelta = 1) { RewardId = rewardId ?? string.Empty; FocusDelta = focusDelta; }
        public string RewardId { get; }
        public int FocusDelta { get; }

        public void Apply(CareerInterestEvaluationContext context)
        {
            context.StudentProgress?.AddFocusUpToDailyFocus(FocusDelta);
        }
    }

    /// <summary>세이브 파일에 쓰는 모양. 필드 이름은 기존 세이브 파일과 같아야 한다.</summary>
    [Serializable]
    public sealed class CareerInterestProgressSaveData : IVersionedSave
    {
        /// <summary>이 프로그램이 아는 가장 높은 형식 버전. 0은 Unity 판이 쓴 파일(Version 필드 없음)이다.</summary>
        public const int KnownVersion = 1;

        public int Version;
        public string SaveSlot;
        public string PlayerId;
        public string CurrentInterestId;
        public string PreviousInterestId;
        public int LastChangedDay;
        public int ChangeCountForCurrentDay;
        public string[] SelectionHistoryIds = Array.Empty<string>();
        public string[] ClaimedRewardIds = Array.Empty<string>();
        public string LastRecommendationSourceVersion;
        public string[] TodayInterestActionSummary = Array.Empty<string>();
        public string LastFollowUpInterestId;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>한 플레이어가 고른 장래희망. 보상은 장래희망·보상마다 한 번만 준다.</summary>
    public sealed class CareerInterestProgress
    {
        private readonly List<string> _selectionHistoryIds = new List<string>();
        private readonly HashSet<string> _claimedRewardIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _todayInterestActionSummary = new List<string>();

        public CareerInterestProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public string CurrentInterestId { get; private set; } = string.Empty;
        public string PreviousInterestId { get; private set; } = string.Empty;
        public int LastChangedDay { get; private set; }
        public int ChangeCountForCurrentDay { get; private set; }
        public string LastRecommendationSourceVersion { get; private set; } = string.Empty;
        public string LastFollowUpInterestId { get; private set; } = string.Empty;
        public string[] SelectionHistoryIds => _selectionHistoryIds.ToArray();
        public string[] TodayInterestActionSummary => _todayInterestActionSummary.ToArray();

        /// <summary>새로 고른 장래희망마다 한 번 true. NPC가 같은 반응을 되풀이하지 않게 한다.</summary>
        public bool TryBeginFollowUp()
        {
            if (string.IsNullOrEmpty(CurrentInterestId) || LastFollowUpInterestId == CurrentInterestId) return false;
            LastFollowUpInterestId = CurrentInterestId;
            return true;
        }

        public CareerInterestSelectionResult TrySelect(CareerInterest interest, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day, string requestId)
        {
            if (interest == null) return new CareerInterestSelectionResult(false, string.Empty, CareerInterestSelectionState.LockedByCondition, "missing interest");
            var evaluation = interest.Evaluate(this, candidateProgress, studentProgress, day);
            if (!evaluation.Allowed) return new CareerInterestSelectionResult(false, interest.Id, evaluation.State, evaluation.Reason);
            bool changed = CurrentInterestId != interest.Id;
            PreviousInterestId = changed ? CurrentInterestId : PreviousInterestId;
            CurrentInterestId = interest.Id;
            LastRecommendationSourceVersion = interest.Id + ":" + Math.Max(0, day);
            if (changed)
            {
                ChangeCountForCurrentDay = LastChangedDay == day ? ChangeCountForCurrentDay + 1 : 1;
                LastChangedDay = Math.Max(0, day);
                CareerText.AddUnique(_selectionHistoryIds, interest.Id);
                CareerText.AddUnique(_todayInterestActionSummary, interest.Id + ":selected");
                LastFollowUpInterestId = string.Empty;
            }
            ApplyRewardsOnce(interest, candidateProgress, studentProgress, Math.Max(0, day));
            return new CareerInterestSelectionResult(changed, interest.Id, CareerInterestSelectionState.Selected, changed ? string.Empty : "interest already selected");
        }

        public bool HasClaimedReward(string interestId, string rewardId) => !string.IsNullOrEmpty(interestId) && !string.IsNullOrEmpty(rewardId) && _claimedRewardIds.Contains(interestId + ":" + rewardId);

        public CareerInterestProgressSaveData ToSaveData()
        {
            return new CareerInterestProgressSaveData
            {
                SaveSlot = SaveSlot, PlayerId = PlayerId, CurrentInterestId = CurrentInterestId, PreviousInterestId = PreviousInterestId,
                LastChangedDay = LastChangedDay, ChangeCountForCurrentDay = ChangeCountForCurrentDay, SelectionHistoryIds = _selectionHistoryIds.ToArray(),
                ClaimedRewardIds = CareerText.ToArray(_claimedRewardIds), LastRecommendationSourceVersion = LastRecommendationSourceVersion,
                TodayInterestActionSummary = _todayInterestActionSummary.ToArray(), LastFollowUpInterestId = LastFollowUpInterestId,
            };
        }

        public static CareerInterestProgress FromSaveData(CareerInterestProgressSaveData saveData)
        {
            if (saveData == null) return new CareerInterestProgress("default", "player");
            var progress = new CareerInterestProgress(saveData.SaveSlot, saveData.PlayerId);
            progress.CurrentInterestId = string.IsNullOrEmpty(saveData.CurrentInterestId) ? string.Empty : saveData.CurrentInterestId;
            progress.PreviousInterestId = string.IsNullOrEmpty(saveData.PreviousInterestId) ? string.Empty : saveData.PreviousInterestId;
            progress.LastChangedDay = Math.Max(0, saveData.LastChangedDay);
            progress.ChangeCountForCurrentDay = Math.Max(0, saveData.ChangeCountForCurrentDay);
            progress.LastRecommendationSourceVersion = string.IsNullOrEmpty(saveData.LastRecommendationSourceVersion) ? string.Empty : saveData.LastRecommendationSourceVersion;
            progress.LastFollowUpInterestId = string.IsNullOrEmpty(saveData.LastFollowUpInterestId) ? string.Empty : saveData.LastFollowUpInterestId;
            CareerText.RestoreOrdered(progress._selectionHistoryIds, saveData.SelectionHistoryIds);
            CareerText.RestoreSet(progress._claimedRewardIds, saveData.ClaimedRewardIds);
            CareerText.RestoreOrdered(progress._todayInterestActionSummary, saveData.TodayInterestActionSummary);
            return progress;
        }

        private void ApplyRewardsOnce(CareerInterest interest, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            var context = new CareerInterestEvaluationContext(interest, this, candidateProgress, studentProgress, day);
            for (int i = 0; i < interest.Rewards.Count; i++)
            {
                var reward = interest.Rewards[i];
                if (reward == null || string.IsNullOrEmpty(reward.RewardId)) continue;
                if (!_claimedRewardIds.Add(interest.Id + ":" + reward.RewardId)) continue;
                reward.Apply(context);
            }
        }
    }

    public readonly struct CareerInterestSummary
    {
        public readonly string InterestId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly CareerInterestSelectionState State;
        public readonly string Reason;
        public readonly string[] RelatedLocationKeys;
        public readonly string[] RelatedActivityKeys;
        public readonly string[] RecommendedActionKeys;

        public CareerInterestSummary(string interestId, string displayName, string description, CareerInterestSelectionState state, string reason, string[] relatedLocationKeys, string[] relatedActivityKeys, string[] recommendedActionKeys)
        {
            InterestId = string.IsNullOrEmpty(interestId) ? string.Empty : interestId;
            DisplayName = string.IsNullOrEmpty(displayName) ? "???" : displayName;
            Description = string.IsNullOrEmpty(description) ? string.Empty : description;
            State = state;
            Reason = string.IsNullOrEmpty(reason) ? string.Empty : reason;
            RelatedLocationKeys = relatedLocationKeys ?? Array.Empty<string>();
            RelatedActivityKeys = relatedActivityKeys ?? Array.Empty<string>();
            RecommendedActionKeys = recommendedActionKeys ?? Array.Empty<string>();
        }
    }

    public static class CareerInterestSummaryBuilder
    {
        public static CareerInterestSummary Build(CareerInterest interest, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            if (interest == null) return default;
            var evaluation = interest.Evaluate(interestProgress, candidateProgress, studentProgress, day);
            var state = interestProgress != null && interestProgress.CurrentInterestId == interest.Id ? CareerInterestSelectionState.Selected : evaluation.State;
            return new CareerInterestSummary(interest.Id, interest.DisplayNameKey, interest.DescriptionKey, state, evaluation.Reason,
                CareerCandidateSummaryBuilder.DisplayKeys(interest.RelatedLocations), CareerCandidateSummaryBuilder.DisplayKeys(interest.RelatedActivities),
                interest.BuildRecommendations(interestProgress, candidateProgress, studentProgress, day));
        }

        public static CareerInterestSummary[] BuildAll(IReadOnlyList<CareerInterest> interests, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            if (interests == null) return Array.Empty<CareerInterestSummary>();
            var values = new List<CareerInterestSummary>();
            for (int i = 0; i < interests.Count; i++) if (interests[i] != null) values.Add(Build(interests[i], interestProgress, candidateProgress, studentProgress, day));
            return values.ToArray();
        }
    }
}
