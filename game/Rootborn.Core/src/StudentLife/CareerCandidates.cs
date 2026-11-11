using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    public enum CareerCandidateState
    {
        Locked,
        Hinted,
        Revealed,
        Deepening,
        ReadyForChoice
    }

    public readonly struct CareerCandidateEvaluationContext
    {
        public readonly CareerCandidate Candidate;
        public readonly CareerCandidateProgress CandidateProgress;
        public readonly StudentLifeProgress StudentProgress;
        public readonly string SourceRequestId;
        public readonly string[] ResultLogIds;

        public CareerCandidateEvaluationContext(CareerCandidate candidate, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, string sourceRequestId, string[] resultLogIds)
        {
            Candidate = candidate;
            CandidateProgress = candidateProgress;
            StudentProgress = studentProgress;
            SourceRequestId = string.IsNullOrEmpty(sourceRequestId) ? string.Empty : sourceRequestId;
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }
    }

    public interface ICareerCandidateRequirement
    {
        /// <summary>요약 화면에 "이름:met/open"으로 보이는 이름.</summary>
        string Name { get; }
        bool IsSatisfied(CareerCandidateEvaluationContext context);
    }

    public interface ICareerHintSource
    {
        bool Matches(CareerCandidateEvaluationContext context);
    }

    public interface ICareerCandidateReward
    {
        string RewardId { get; }
        void Apply(CareerCandidateEvaluationContext context);
    }

    /// <summary>진로 후보로 가는 길 하나(장소·활동·추천 행동).</summary>
    public sealed class CareerCandidateRoute
    {
        private readonly DefinitionRef[] _locations;
        private readonly DefinitionRef[] _activities;
        private readonly string[] _recommendedActionKeys;

        public CareerCandidateRoute(string id, string displayNameKey, string descriptionKey, DefinitionRef[] locations, DefinitionRef[] activities, string[] recommendedActionKeys)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            _locations = locations ?? Array.Empty<DefinitionRef>();
            _activities = activities ?? Array.Empty<DefinitionRef>();
            _recommendedActionKeys = recommendedActionKeys ?? Array.Empty<string>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public IReadOnlyList<DefinitionRef> Locations => _locations;
        public IReadOnlyList<DefinitionRef> Activities => _activities;
        public IReadOnlyList<string> RecommendedActionKeys => _recommendedActionKeys;
    }

    /// <summary>진로 후보. 이해 점수가 문턱을 넘을 때마다 상태가 오른다(문턱은 늘 앞 단계보다 크다).</summary>
    public sealed class CareerCandidate
    {
        private readonly DefinitionRef[] _relatedLocations;
        private readonly DefinitionRef[] _relatedActivities;
        private readonly ICareerCandidateRequirement[] _requirements;
        private readonly CareerCandidateRoute[] _routes;
        private readonly string[] _recommendedActionKeys;
        private readonly ICareerCandidateReward[] _rewards;
        private readonly int _revealedHintThreshold, _deepeningHintThreshold, _readyHintThreshold;
        private readonly string _lockedTeaserKey;

        public CareerCandidate(string id, string displayNameKey, string descriptionKey, string lockedTeaserKey,
            DefinitionRef[] relatedLocations, DefinitionRef[] relatedActivities, ICareerCandidateRequirement[] requirements,
            CareerCandidateRoute[] routes, string[] recommendedActionKeys, ICareerCandidateReward[] rewards,
            int revealedHintThreshold = 2, int deepeningHintThreshold = 3, int readyHintThreshold = 4)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            _lockedTeaserKey = lockedTeaserKey;
            _relatedLocations = relatedLocations ?? Array.Empty<DefinitionRef>();
            _relatedActivities = relatedActivities ?? Array.Empty<DefinitionRef>();
            _requirements = requirements ?? Array.Empty<ICareerCandidateRequirement>();
            _routes = routes ?? Array.Empty<CareerCandidateRoute>();
            _recommendedActionKeys = recommendedActionKeys ?? Array.Empty<string>();
            _rewards = rewards ?? Array.Empty<ICareerCandidateReward>();
            _revealedHintThreshold = revealedHintThreshold;
            _deepeningHintThreshold = deepeningHintThreshold;
            _readyHintThreshold = readyHintThreshold;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string LockedTeaserKey => string.IsNullOrEmpty(_lockedTeaserKey) ? "???" : _lockedTeaserKey;
        public int RevealedHintThreshold => Math.Max(1, _revealedHintThreshold);
        public int DeepeningHintThreshold => Math.Max(RevealedHintThreshold + 1, _deepeningHintThreshold);
        public int ReadyHintThreshold => Math.Max(DeepeningHintThreshold + 1, _readyHintThreshold);
        public IReadOnlyList<DefinitionRef> RelatedLocations => _relatedLocations;
        public IReadOnlyList<DefinitionRef> RelatedActivities => _relatedActivities;
        public IReadOnlyList<CareerCandidateRoute> Routes => _routes;
        public IReadOnlyList<ICareerCandidateRequirement> Requirements => _requirements;
        public IReadOnlyList<string> RecommendedActionKeys => _recommendedActionKeys;
        public IReadOnlyList<ICareerCandidateReward> Rewards => _rewards;

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
                if (_requirements[i] != null && !_requirements[i].IsSatisfied(context)) return false;
            return true;
        }
    }

    /// <summary>진로 힌트. 출처 중 하나라도 맞으면 후보의 이해 점수를 올린다.</summary>
    public sealed class CareerHint
    {
        private readonly ICareerHintSource[] _sources;
        private readonly string _insightKey;
        private readonly int _understandingDelta;

        public CareerHint(string id, string displayNameKey, CareerCandidate candidate, CareerCandidateRoute route, string insightKey, int understandingDelta, bool appliesOnce, ICareerHintSource[] sources)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            Candidate = candidate;
            Route = route;
            _insightKey = insightKey;
            _understandingDelta = understandingDelta;
            AppliesOnce = appliesOnce;
            _sources = sources ?? Array.Empty<ICareerHintSource>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public CareerCandidate Candidate { get; }
        public CareerCandidateRoute Route { get; }
        public string InsightKey => string.IsNullOrEmpty(_insightKey) ? DisplayNameKey : _insightKey;
        public int UnderstandingDelta => Math.Max(1, _understandingDelta);

        /// <summary>출처가 몇 번 다시 맞아도 후보에 한 번만 센다.</summary>
        public bool AppliesOnce { get; }
        public IReadOnlyList<ICareerHintSource> Sources => _sources;

        public bool Matches(CareerCandidateEvaluationContext context)
        {
            if (Candidate == null || _sources.Length == 0) return false;
            for (int i = 0; i < _sources.Length; i++)
                if (_sources[i] != null && _sources[i].Matches(context)) return true;
            return false;
        }
    }

    public sealed class CareerCandidateHintCountRequirement : ICareerCandidateRequirement
    {
        public CareerCandidateHintCountRequirement(string name, int minimumHintCount = 1) { Name = name ?? string.Empty; MinimumHintCount = minimumHintCount; }
        public string Name { get; }
        public int MinimumHintCount { get; }
        public bool IsSatisfied(CareerCandidateEvaluationContext context)
            => context.CandidateProgress != null && context.CandidateProgress.GetHintCount(context.Candidate) >= Math.Max(1, MinimumHintCount);
    }

    public sealed class CareerCandidateSkillRequirement : ICareerCandidateRequirement
    {
        public CareerCandidateSkillRequirement(string name, string skillId, int minimumValue = 1) { Name = name ?? string.Empty; SkillId = skillId; MinimumValue = minimumValue; }
        public string Name { get; }
        public string SkillId { get; }
        public int MinimumValue { get; }
        public bool IsSatisfied(CareerCandidateEvaluationContext context)
            => context.StudentProgress != null && !string.IsNullOrEmpty(SkillId) && context.StudentProgress.GetSkillValue(SkillId) >= Math.Max(0, MinimumValue);
    }

    public sealed class CareerCandidateActivityLogRequirement : ICareerCandidateRequirement
    {
        public CareerCandidateActivityLogRequirement(string name, string activityId) { Name = name ?? string.Empty; ActivityId = activityId; }
        public string Name { get; }
        public string ActivityId { get; }
        public bool IsSatisfied(CareerCandidateEvaluationContext context)
        {
            if (context.StudentProgress == null || string.IsNullOrEmpty(ActivityId)) return false;
            return CareerText.Contains(context.StudentProgress.GetActivityLogIds(), ActivityId);
        }
    }

    /// <summary>
    /// 활동 기록 출처. 결과 기록이 주어지면 그 기록만 본다("활동id" 또는 "활동id:"로 시작).
    /// 없으면 전체·오늘 활동 기록을 본다.
    /// </summary>
    public sealed class CareerActivityLogHintSource : ICareerHintSource
    {
        public CareerActivityLogHintSource(string activityId) { ActivityId = activityId; }
        public string ActivityId { get; }

        public bool Matches(CareerCandidateEvaluationContext context)
        {
            if (context.StudentProgress == null || string.IsNullOrEmpty(ActivityId)) return false;
            if (context.ResultLogIds != null && context.ResultLogIds.Length > 0)
            {
                for (int i = 0; i < context.ResultLogIds.Length; i++)
                {
                    string value = context.ResultLogIds[i];
                    if (value == ActivityId || (!string.IsNullOrEmpty(value) && value.StartsWith(ActivityId + ":", StringComparison.Ordinal))) return true;
                }
                return false;
            }
            return CareerText.Contains(context.StudentProgress.GetActivityLogIds(), ActivityId) || CareerText.Contains(context.StudentProgress.GetTodayActivityIds(), ActivityId);
        }
    }

    /// <summary>결과 기록 중 하나가 정한 글자를 포함하면 맞는다. 글자가 비면 맞지 않는다.</summary>
    public sealed class CareerResultLogHintSource : ICareerHintSource
    {
        public CareerResultLogHintSource(string requiredText) { RequiredText = requiredText; }
        public string RequiredText { get; }

        public bool Matches(CareerCandidateEvaluationContext context)
        {
            if (string.IsNullOrEmpty(RequiredText)) return false;
            for (int i = 0; i < context.ResultLogIds.Length; i++)
            {
                string log = context.ResultLogIds[i];
                if (!string.IsNullOrEmpty(log) && log.Contains(RequiredText)) return true;
            }
            return false;
        }
    }

    public sealed class CareerCandidateCareerHintReward : ICareerCandidateReward
    {
        public CareerCandidateCareerHintReward(string rewardId, string careerId) { RewardId = rewardId ?? string.Empty; CareerId = careerId; }
        public string RewardId { get; }
        public string CareerId { get; }

        public void Apply(CareerCandidateEvaluationContext context)
        {
            if (context.StudentProgress != null && !string.IsNullOrEmpty(CareerId)) context.StudentProgress.UnlockCareerHint(CareerId);
        }
    }

    internal static class CareerText
    {
        public static bool Contains(string[] values, string target)
        {
            if (string.IsNullOrEmpty(target) || values == null) return false;
            for (int i = 0; i < values.Length; i++) if (values[i] == target) return true;
            return false;
        }

        public static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value);
        }

        public static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        public static void RestoreSet(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        public static void RestoreOrdered(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) AddUnique(target, values[i]);
        }
    }
}
