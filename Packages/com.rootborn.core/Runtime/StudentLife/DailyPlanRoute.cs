using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 일과 길 하나. 장소 활동은 명시한 활동이거나, 연습·알바를 명시하지 않았을 때 같은 장소·같은 성장 길이면 맞는다.
    /// 연습과 알바는 명시한 바로 그 정의(같은 객체)만 맞는다.
    /// </summary>
    public sealed class DailyPlanRoute
    {
        private readonly IDailyPlanLocationActivity[] _matchingActivities;
        private readonly CareerPractice[] _matchingPractices;
        private readonly IMinigamePartTimeWork[] _matchingWorks;
        private readonly IDailyPlanRouteRequirement[] _requirements;
        private readonly IDailyPlanRouteOutcome[] _outcomes;
        private readonly string _objectiveTitleKey, _objectiveDetailKey;

        public DailyPlanRoute(string id, string displayNameKey, string targetLocationId, string growthRoute,
            IDailyPlanLocationActivity[] matchingActivities, CareerPractice[] matchingPractices, IMinigamePartTimeWork[] matchingWorks,
            IDailyPlanRouteRequirement[] requirements, IDailyPlanRouteOutcome[] outcomes, string objectiveTitleKey, string objectiveDetailKey, string nextHintKey)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            TargetLocationId = targetLocationId ?? string.Empty;
            GrowthRoute = growthRoute ?? string.Empty;
            _matchingActivities = matchingActivities ?? Array.Empty<IDailyPlanLocationActivity>();
            _matchingPractices = matchingPractices ?? Array.Empty<CareerPractice>();
            _matchingWorks = matchingWorks ?? Array.Empty<IMinigamePartTimeWork>();
            _requirements = requirements ?? Array.Empty<IDailyPlanRouteRequirement>();
            _outcomes = outcomes ?? Array.Empty<IDailyPlanRouteOutcome>();
            _objectiveTitleKey = objectiveTitleKey;
            _objectiveDetailKey = objectiveDetailKey;
            NextHintKey = nextHintKey ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }

        /// <summary>비어 있으면 장소로 맞추지 않는다.</summary>
        public string TargetLocationId { get; }
        public string GrowthRoute { get; }
        public IReadOnlyList<IDailyPlanLocationActivity> MatchingActivities => _matchingActivities;
        public IReadOnlyList<CareerPractice> MatchingPractices => _matchingPractices;
        public IReadOnlyList<IMinigamePartTimeWork> MatchingWorks => _matchingWorks;
        public IReadOnlyList<IDailyPlanRouteRequirement> Requirements => _requirements;
        public IReadOnlyList<IDailyPlanRouteOutcome> Outcomes => _outcomes;
        public string ObjectiveTitleKey => string.IsNullOrEmpty(_objectiveTitleKey) ? DisplayNameKey : _objectiveTitleKey;
        public string ObjectiveDetailKey => string.IsNullOrEmpty(_objectiveDetailKey) ? DisplayNameKey : _objectiveDetailKey;
        public string NextHintKey { get; }

        /// <summary>하루에 이 길을 넣을 수 있는 계획 칸 수. 길이 속한 묶음이 정한다(여러 묶음이면 가장 작은 값). 묶음에 없으면 제한이 없다.</summary>
        public int MaxSelectionsPerDay { get; private set; } = int.MaxValue;

        internal void LimitSelectionsPerDay(int max) => MaxSelectionsPerDay = Math.Min(MaxSelectionsPerDay, Math.Max(1, max));

        public bool IsAvailable(StudentLifeProgress progress) => IsAvailable(new DailyPlanRouteContext(progress));

        public bool IsAvailable(DailyPlanRouteContext context)
        {
            if (context.StudentProgress == null) return false;
            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(context, this)) return false;
            }
            return true;
        }

        public bool Matches(IDailyPlanLocationActivity activity)
        {
            if (activity == null) return false;
            for (int i = 0; i < _matchingActivities.Length; i++)
                if (ReferenceEquals(_matchingActivities[i], activity)) return true;
            // 연습·알바를 명시한 길은 실제 연습·알바 정산으로만 채운다. 위의 명시 장소 활동은 그대로 맞는다.
            if (_matchingPractices.Length > 0 || _matchingWorks.Length > 0) return false;
            return TargetLocationId.Length > 0 && activity.LocationId == TargetLocationId && activity.GrowthRoute == GrowthRoute;
        }

        public bool MatchesPractice(CareerPractice practice)
        {
            if (practice == null) return false;
            foreach (var candidate in _matchingPractices) if (ReferenceEquals(candidate, practice)) return true;
            return false;
        }

        public bool MatchesWork(IMinigamePartTimeWork work)
        {
            if (work == null) return false;
            foreach (var candidate in _matchingWorks) if (ReferenceEquals(candidate, work)) return true;
            return false;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
            if (progress == null) return Array.Empty<string>();
            var logs = new List<string>();
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, this) : string.Empty;
                if (!string.IsNullOrEmpty(log)) logs.Add(log);
            }
            return logs.ToArray();
        }
    }

    public sealed class DailyPlanRouteSet
    {
        private readonly DailyPlanRoute[] _routes;
        private readonly int _maxSelectionsPerDay;

        public DailyPlanRouteSet(string id, string displayNameKey, DailyPlanRoute[] routes, int maxSelectionsPerDay = 1)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            _routes = routes ?? Array.Empty<DailyPlanRoute>();
            _maxSelectionsPerDay = maxSelectionsPerDay;
            for (int i = 0; i < _routes.Length; i++) _routes[i]?.LimitSelectionsPerDay(MaxSelectionsPerDay);
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public IReadOnlyList<DailyPlanRoute> Routes => _routes;
        /// <summary>이 묶음의 길 하나를 하루에 몇 칸까지 넣을 수 있는지(데이터 값, 1 이상). Unity 판에서는 쓰이지 않던 값이다.</summary>
        public int MaxSelectionsPerDay => Math.Max(1, _maxSelectionsPerDay);

        public DailyPlanRoute[] GetAvailableRoutes(StudentLifeProgress progress) => GetAvailableRoutes(new DailyPlanRouteContext(progress));

        public DailyPlanRoute[] GetAvailableRoutes(DailyPlanRouteContext context)
        {
            if (context.StudentProgress == null) return Array.Empty<DailyPlanRoute>();
            var routes = new List<DailyPlanRoute>();
            for (int i = 0; i < _routes.Length; i++)
            {
                var route = _routes[i];
                if (route != null && route.IsAvailable(context)) routes.Add(route);
            }
            return routes.ToArray();
        }
    }

    /// <summary>진로 힌트가 열렸는지(또는 아직 안 열렸는지).</summary>
    public sealed class DailyPlanCareerHintRequirement : IDailyPlanRouteRequirement
    {
        public DailyPlanCareerHintRequirement(string careerId, bool expectedUnlocked = true) { CareerId = careerId; ExpectedUnlocked = expectedUnlocked; }
        public string CareerId { get; }
        public bool ExpectedUnlocked { get; }

        public bool IsSatisfied(DailyPlanRouteContext context, DailyPlanRoute route)
        {
            var progress = context.StudentProgress;
            bool unlocked = progress != null && !string.IsNullOrEmpty(CareerId) && progress.IsCareerHintUnlocked(CareerId);
            return unlocked == ExpectedUnlocked;
        }
    }

    /// <summary>알바를 했는지. 오늘 활동, 오늘 결과 기록, 어제 결과 기록에서 찾는다.</summary>
    public sealed class DailyPlanPartTimeWorkRequirement : IDailyPlanRouteRequirement
    {
        private readonly IPartTimeWorkLogReader _logReader;

        public DailyPlanPartTimeWorkRequirement(string workId, bool expectedCompleted, IPartTimeWorkLogReader logReader)
        {
            WorkId = workId;
            ExpectedCompleted = expectedCompleted;
            _logReader = logReader;
        }

        public string WorkId { get; }
        public bool ExpectedCompleted { get; }

        public bool IsSatisfied(DailyPlanRouteContext context, DailyPlanRoute route) => HasCompletedWork(context.StudentProgress) == ExpectedCompleted;

        private bool HasCompletedWork(StudentLifeProgress progress)
        {
            if (progress == null || string.IsNullOrEmpty(WorkId)) return false;
            if (CareerText.Contains(progress.GetTodayActivityIds(), WorkId)) return true;
            if (_logReader == null) return false;
            if (CareerText.Contains(_logReader.ReadWorkIds(progress.GetTodayResultLogIds()), WorkId)) return true;
            return CareerText.Contains(_logReader.ReadWorkIds(progress.GetPreviousDayResultLogIds()), WorkId);
        }
    }

    /// <summary>세계 상태 깃발이 켜졌는지(또는 꺼졌는지). 세계 상태가 없으면 언제나 만족하지 않는다.</summary>
    public sealed class DailyPlanWorldStateRequirement : IDailyPlanRouteRequirement
    {
        public DailyPlanWorldStateRequirement(string flagId, bool expectedActive = true) { FlagId = flagId; ExpectedActive = expectedActive; }
        public string FlagId { get; }
        public bool ExpectedActive { get; }

        public bool IsSatisfied(DailyPlanRouteContext context, DailyPlanRoute route)
        {
            if (string.IsNullOrEmpty(FlagId) || context.WorldState == null) return false;
            return context.WorldState.IsActive(FlagId) == ExpectedActive;
        }
    }
}
