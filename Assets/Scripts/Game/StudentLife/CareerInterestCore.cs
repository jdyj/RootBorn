using System;
using System.Collections.Generic;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public enum CareerInterestSelectionState { None, Selected, ChangePending, ChangedToday, LockedByCondition }

    [CreateAssetMenu(fileName = "CareerInterest_New", menuName = "Rootborn/Student Life/Career Interests/Interest")]
    public sealed class CareerInterestDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private CareerCandidateDefinition _candidate;
        [SerializeField] private LocationDefinition[] _relatedLocations = Array.Empty<LocationDefinition>();
        [SerializeField] private ScriptableObject[] _relatedActivities = Array.Empty<ScriptableObject>();
        [SerializeField] private CareerInterestUnlockRequirementBase[] _unlockRequirements = Array.Empty<CareerInterestUnlockRequirementBase>();
        [SerializeField] private CareerInterestSelectionRuleBase[] _selectionRules = Array.Empty<CareerInterestSelectionRuleBase>();
        [SerializeField] private CareerInterestRecommendationBase[] _recommendations = Array.Empty<CareerInterestRecommendationBase>();
        [SerializeField] private CareerInterestRewardBase[] _rewards = Array.Empty<CareerInterestRewardBase>();
        [SerializeField] private QuestDefinition[] _linkedQuests = Array.Empty<QuestDefinition>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public CareerCandidateDefinition Candidate => _candidate;
        public IReadOnlyList<LocationDefinition> RelatedLocations => _relatedLocations;
        public IReadOnlyList<ScriptableObject> RelatedActivities => _relatedActivities;
        public IReadOnlyList<CareerInterestUnlockRequirementBase> UnlockRequirements => _unlockRequirements;
        public IReadOnlyList<CareerInterestSelectionRuleBase> SelectionRules => _selectionRules;
        public IReadOnlyList<CareerInterestRecommendationBase> Recommendations => _recommendations;
        public IReadOnlyList<CareerInterestRewardBase> Rewards => _rewards;
        public IReadOnlyList<QuestDefinition> LinkedQuests => _linkedQuests;

        public CareerInterestSelectionEvaluation Evaluate(CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            var context = new CareerInterestEvaluationContext(this, interestProgress, candidateProgress, studentProgress, Mathf.Max(0, day));
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
            var context = new CareerInterestEvaluationContext(this, interestProgress, candidateProgress, studentProgress, Mathf.Max(0, day));
            for (int i = 0; i < _recommendations.Length; i++)
            {
                var recommendation = _recommendations[i];
                if (recommendation == null) continue;
                AddUnique(values, recommendation.GetRecommendationKeys(context));
            }

            if (_candidate != null)
            {
                for (int i = 0; i < _candidate.RecommendedActionKeys.Count; i++) AddUnique(values, _candidate.RecommendedActionKeys[i]);
            }

            return values.ToArray();
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, CareerCandidateDefinition candidate, LocationDefinition[] relatedLocations, ScriptableObject[] relatedActivities, CareerInterestUnlockRequirementBase[] unlockRequirements, CareerInterestSelectionRuleBase[] selectionRules, CareerInterestRecommendationBase[] recommendations, CareerInterestRewardBase[] rewards, QuestDefinition[] linkedQuests = null)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = descriptionKey;
            _candidate = candidate;
            _relatedLocations = relatedLocations ?? Array.Empty<LocationDefinition>();
            _relatedActivities = relatedActivities ?? Array.Empty<ScriptableObject>();
            _unlockRequirements = unlockRequirements ?? Array.Empty<CareerInterestUnlockRequirementBase>();
            _selectionRules = selectionRules ?? Array.Empty<CareerInterestSelectionRuleBase>();
            _recommendations = recommendations ?? Array.Empty<CareerInterestRecommendationBase>();
            _rewards = rewards ?? Array.Empty<CareerInterestRewardBase>();
            _linkedQuests = linkedQuests ?? Array.Empty<QuestDefinition>();
        }

        private static void AddUnique(List<string> target, string value)
        {
            if (!string.IsNullOrEmpty(value) && !target.Contains(value)) target.Add(value);
        }

        private static void AddUnique(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) AddUnique(target, values[i]);
        }
    }

    public readonly struct CareerInterestEvaluationContext
    {
        public readonly CareerInterestDefinition Interest;
        public readonly CareerInterestProgress InterestProgress;
        public readonly CareerCandidateProgress CandidateProgress;
        public readonly StudentLifeProgress StudentProgress;
        public readonly int Day;

        public CareerInterestEvaluationContext(CareerInterestDefinition interest, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            Interest = interest;
            InterestProgress = interestProgress;
            CandidateProgress = candidateProgress;
            StudentProgress = studentProgress;
            Day = Mathf.Max(0, day);
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

    public abstract class CareerInterestUnlockRequirementBase : ScriptableObject { public abstract CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context); }
    public abstract class CareerInterestSelectionRuleBase : ScriptableObject { public abstract CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context); }
    public abstract class CareerInterestRecommendationBase : ScriptableObject { public abstract string[] GetRecommendationKeys(CareerInterestEvaluationContext context); }
    public abstract class CareerInterestRewardBase : ScriptableObject { public abstract string RewardId { get; } public abstract void Apply(CareerInterestEvaluationContext context); }

    [Serializable]
    public sealed class CareerInterestProgressSaveData
    {
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
    }

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
        public string[] SelectionHistoryIds => _selectionHistoryIds.ToArray();
        public string[] TodayInterestActionSummary => _todayInterestActionSummary.ToArray();

        public CareerInterestSelectionResult TrySelect(CareerInterestDefinition interest, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day, string requestId)
        {
            if (interest == null) return new CareerInterestSelectionResult(false, string.Empty, CareerInterestSelectionState.LockedByCondition, "missing interest");
            var evaluation = interest.Evaluate(this, candidateProgress, studentProgress, day);
            if (!evaluation.Allowed) return new CareerInterestSelectionResult(false, interest.Id, evaluation.State, evaluation.Reason);
            bool changed = CurrentInterestId != interest.Id;
            PreviousInterestId = changed ? CurrentInterestId : PreviousInterestId;
            CurrentInterestId = interest.Id;
            LastRecommendationSourceVersion = interest.Id + ":" + Mathf.Max(0, day);
            if (changed)
            {
                ChangeCountForCurrentDay = LastChangedDay == day ? ChangeCountForCurrentDay + 1 : 1;
                LastChangedDay = Mathf.Max(0, day);
                AddUnique(_selectionHistoryIds, interest.Id);
                AddUnique(_todayInterestActionSummary, interest.Id + ":selected");
            }

            ApplyRewardsOnce(interest, candidateProgress, studentProgress, Mathf.Max(0, day));
            return new CareerInterestSelectionResult(changed, interest.Id, CareerInterestSelectionState.Selected, changed ? string.Empty : "interest already selected");
        }

        public bool HasClaimedReward(string interestId, string rewardId) => !string.IsNullOrEmpty(interestId) && !string.IsNullOrEmpty(rewardId) && _claimedRewardIds.Contains(interestId + ":" + rewardId);

        public CareerInterestProgressSaveData ToSaveData()
        {
            return new CareerInterestProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, CurrentInterestId = CurrentInterestId, PreviousInterestId = PreviousInterestId, LastChangedDay = LastChangedDay, ChangeCountForCurrentDay = ChangeCountForCurrentDay, SelectionHistoryIds = _selectionHistoryIds.ToArray(), ClaimedRewardIds = ToArray(_claimedRewardIds), LastRecommendationSourceVersion = LastRecommendationSourceVersion, TodayInterestActionSummary = _todayInterestActionSummary.ToArray() };
        }

        public static CareerInterestProgress FromSaveData(CareerInterestProgressSaveData saveData)
        {
            if (saveData == null) return new CareerInterestProgress("default", "player");
            var progress = new CareerInterestProgress(saveData.SaveSlot, saveData.PlayerId);
            progress.CurrentInterestId = string.IsNullOrEmpty(saveData.CurrentInterestId) ? string.Empty : saveData.CurrentInterestId;
            progress.PreviousInterestId = string.IsNullOrEmpty(saveData.PreviousInterestId) ? string.Empty : saveData.PreviousInterestId;
            progress.LastChangedDay = Mathf.Max(0, saveData.LastChangedDay);
            progress.ChangeCountForCurrentDay = Mathf.Max(0, saveData.ChangeCountForCurrentDay);
            progress.LastRecommendationSourceVersion = string.IsNullOrEmpty(saveData.LastRecommendationSourceVersion) ? string.Empty : saveData.LastRecommendationSourceVersion;
            RestoreOrderedStrings(progress._selectionHistoryIds, saveData.SelectionHistoryIds);
            RestoreStrings(progress._claimedRewardIds, saveData.ClaimedRewardIds);
            RestoreOrderedStrings(progress._todayInterestActionSummary, saveData.TodayInterestActionSummary);
            return progress;
        }

        private void ApplyRewardsOnce(CareerInterestDefinition interest, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            var context = new CareerInterestEvaluationContext(interest, this, candidateProgress, studentProgress, day);
            for (int i = 0; i < interest.Rewards.Count; i++)
            {
                var reward = interest.Rewards[i];
                if (reward == null || string.IsNullOrEmpty(reward.RewardId)) continue;
                string key = interest.Id + ":" + reward.RewardId;
                if (!_claimedRewardIds.Add(key)) continue;
                reward.Apply(context);
            }
        }

        private static void AddUnique(List<string> values, string value) { if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value); }
        private static string[] ToArray(HashSet<string> values) { var result = new string[values.Count]; values.CopyTo(result); return result; }
        private static void RestoreStrings(HashSet<string> target, string[] values) { if (values == null) return; for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]); }
        private static void RestoreOrderedStrings(List<string> target, string[] values) { if (values == null) return; for (int i = 0; i < values.Length; i++) AddUnique(target, values[i]); }
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
        public static CareerInterestSummary Build(CareerInterestDefinition interest, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            if (interest == null) return default;
            var evaluation = interest.Evaluate(interestProgress, candidateProgress, studentProgress, day);
            var state = interestProgress != null && interestProgress.CurrentInterestId == interest.Id ? CareerInterestSelectionState.Selected : evaluation.State;
            return new CareerInterestSummary(interest.Id, interest.DisplayNameKey, interest.DescriptionKey, state, evaluation.Reason, LocationKeys(interest), ActivityKeys(interest), interest.BuildRecommendations(interestProgress, candidateProgress, studentProgress, day));
        }

        public static CareerInterestSummary[] BuildAll(IReadOnlyList<CareerInterestDefinition> interests, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            if (interests == null) return Array.Empty<CareerInterestSummary>();
            var values = new List<CareerInterestSummary>();
            for (int i = 0; i < interests.Count; i++) if (interests[i] != null) values.Add(Build(interests[i], interestProgress, candidateProgress, studentProgress, day));
            return values.ToArray();
        }

        private static string[] LocationKeys(CareerInterestDefinition interest) { var values = new List<string>(); for (int i = 0; i < interest.RelatedLocations.Count; i++) if (interest.RelatedLocations[i] != null) values.Add(interest.RelatedLocations[i].DisplayNameKey); return values.ToArray(); }
        private static string[] ActivityKeys(CareerInterestDefinition interest) { var values = new List<string>(); for (int i = 0; i < interest.RelatedActivities.Count; i++) { var activity = interest.RelatedActivities[i]; if (activity is StudentLifeDefinitionBase studentLifeDefinition) values.Add(studentLifeDefinition.DisplayNameKey); else if (activity is LifeActivityDefinition lifeActivity) values.Add(lifeActivity.DisplayNameKey); else if (activity != null) values.Add(activity.name); } return values.ToArray(); }
    }
}
