using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>캠페인 목표를 평가할 때의 상황. 방문한 장소는 id 목록으로 받는다.</summary>
    public readonly struct CampaignEvaluationContext
    {
        public readonly StudentLifeProgress StudentProgress;
        public readonly CampaignProgress CampaignProgress;
        public readonly IReadOnlyList<string> VisitedLocationIds;
        public readonly IReadOnlyList<string> EventIds;

        public CampaignEvaluationContext(StudentLifeProgress studentProgress, CampaignProgress campaignProgress, IReadOnlyList<string> visitedLocationIds, IReadOnlyList<string> eventIds)
        {
            StudentProgress = studentProgress;
            CampaignProgress = campaignProgress;
            VisitedLocationIds = visitedLocationIds ?? Array.Empty<string>();
            EventIds = eventIds ?? Array.Empty<string>();
        }
    }

    public readonly struct CampaignObjectiveEvaluation
    {
        public readonly int Current;
        public readonly int Required;
        public bool IsComplete => Current >= Required;

        public CampaignObjectiveEvaluation(int current, int required)
        {
            Required = Math.Max(1, required);
            Current = Math.Max(0, current);
        }
    }

    public abstract class CampaignObjective
    {
        protected CampaignObjective(string id, bool requiresSchoolClass)
        {
            Id = id ?? string.Empty;
            RequiresSchoolClass = requiresSchoolClass;
        }

        public string Id { get; }
        public bool RequiresSchoolClass { get; }
        public abstract CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context);
    }

    public abstract class CampaignReward
    {
        protected CampaignReward(string id) { Id = id ?? string.Empty; }
        public string Id { get; }
        public virtual bool CanApply(StudentLifeProgress progress) => true;
        public abstract void Apply(StudentLifeProgress progress);
    }

    public sealed class CampaignRoute
    {
        private readonly CampaignObjective[] _objectives;

        public CampaignRoute(string id, string displayNameKey, string descriptionKey, CampaignObjective[] objectives)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? DisplayNameKey : descriptionKey;
            _objectives = objectives ?? Array.Empty<CampaignObjective>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public IReadOnlyList<CampaignObjective> Objectives => _objectives;

        /// <summary>목표가 하나 이상 있고 그중 수업이 필요한 것이 없으면 true.</summary>
        public bool CanCompleteWithoutSchoolClass
        {
            get
            {
                if (_objectives.Length == 0) return false;
                for (int i = 0; i < _objectives.Length; i++) if (_objectives[i] != null && _objectives[i].RequiresSchoolClass) return false;
                return true;
            }
        }
    }

    public sealed class CampaignDay
    {
        private readonly CampaignRoute[] _routes;

        public CampaignDay(string id, int dayNumber, string themeKey, string directionText, string nextGuideText, CampaignRoute[] routes)
        {
            Id = id ?? string.Empty;
            DayNumber = Math.Max(1, dayNumber);
            ThemeKey = string.IsNullOrEmpty(themeKey) ? Id : themeKey;
            DirectionText = string.IsNullOrEmpty(directionText) ? ThemeKey : directionText;
            NextGuideText = string.IsNullOrEmpty(nextGuideText) ? string.Empty : nextGuideText;
            _routes = routes ?? Array.Empty<CampaignRoute>();
        }

        public string Id { get; }
        public int DayNumber { get; }
        public string ThemeKey { get; }
        public string DirectionText { get; }
        public string NextGuideText { get; }
        public IReadOnlyList<CampaignRoute> Routes => _routes;

        public bool HasSchoolFreeCompletionRoute()
        {
            for (int i = 0; i < _routes.Length; i++) if (_routes[i] != null && _routes[i].CanCompleteWithoutSchoolClass) return true;
            return false;
        }
    }

    public sealed class Campaign
    {
        private readonly CampaignDay[] _days;
        private readonly CampaignReward[] _rewards;

        public Campaign(string id, string descriptionKey, CampaignDay[] days, CampaignReward[] rewards)
        {
            Id = id ?? string.Empty;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? Id : descriptionKey;
            _days = days ?? Array.Empty<CampaignDay>();
            _rewards = rewards ?? Array.Empty<CampaignReward>();
        }

        public string Id { get; }
        public string DescriptionKey { get; }
        public IReadOnlyList<CampaignDay> Days => _days;
        public IReadOnlyList<CampaignReward> Rewards => _rewards;

        /// <summary>날 번호가 같은 날. 없으면 (번호 - 1)번째 날, 넘치면 마지막 날.</summary>
        public CampaignDay GetDayByNumber(int dayNumber)
        {
            int safeDay = Math.Max(1, dayNumber);
            for (int i = 0; i < _days.Length; i++) if (_days[i] != null && _days[i].DayNumber == safeDay) return _days[i];
            return _days.Length > 0 ? _days[Math.Min(_days.Length - 1, safeDay - 1)] : null;
        }
    }

    public readonly struct CampaignApplyResult
    {
        public readonly string[] CompletedObjectiveIds;
        public CampaignApplyResult(string[] completedObjectiveIds) { CompletedObjectiveIds = completedObjectiveIds ?? Array.Empty<string>(); }
    }

    /// <summary>
    /// 모든 날·경로의 목표를 평가해 새로 끝난 것을 기록한다. 같은 resultId는 한 번만 반영한다.
    /// 새로 끝난 목표가 없으면 resultId를 남기지 않는다(원본은 이때도 resultId를 소모했다).
    /// </summary>
    public sealed class CampaignRunner
    {
        public bool TryApplyProgress(Campaign campaign, CampaignEvaluationContext context, string resultId, out CampaignApplyResult result)
        {
            result = new CampaignApplyResult(Array.Empty<string>());
            if (campaign == null || context.CampaignProgress == null || string.IsNullOrEmpty(resultId)) return false;
            if (context.CampaignProgress.IsResultApplied(resultId)) return false;
            var completed = new List<string>();
            foreach (CampaignDay day in campaign.Days)
            {
                if (day == null) continue;
                foreach (CampaignRoute route in day.Routes)
                {
                    if (route == null) continue;
                    foreach (CampaignObjective objective in route.Objectives)
                    {
                        if (objective == null || context.CampaignProgress.IsObjectiveCompleted(objective.Id)) continue;
                        if (!objective.Evaluate(in context).IsComplete) continue;
                        if (context.CampaignProgress.CompleteObjective(objective.Id)) completed.Add(objective.Id);
                    }
                }
            }
            if (completed.Count == 0) return false;
            context.CampaignProgress.MarkResultApplied(resultId);
            result = new CampaignApplyResult(completed.ToArray());
            return true;
        }
    }

    public readonly struct CampaignHudSummary
    {
        public readonly string TodayDirection;
        public readonly string AvailableRoutesText;
        public readonly string CurrentProgressText;
        public readonly string NextGuideText;

        public CampaignHudSummary(string todayDirection, string availableRoutesText, string currentProgressText, string nextGuideText)
        {
            TodayDirection = todayDirection ?? string.Empty;
            AvailableRoutesText = availableRoutesText ?? string.Empty;
            CurrentProgressText = currentProgressText ?? string.Empty;
            NextGuideText = nextGuideText ?? string.Empty;
        }
    }

    public readonly struct CampaignDayResultSummary
    {
        public readonly string SelectedRouteText;
        public readonly string ProgressText;
        public readonly string NextGuideText;

        public CampaignDayResultSummary(string selectedRouteText, string progressText, string nextGuideText)
        {
            SelectedRouteText = selectedRouteText ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            NextGuideText = nextGuideText ?? string.Empty;
        }
    }

    /// <summary>화면에 보일 캠페인 요약 문자열을 만든다. 오늘 방향에는 날의 주제 키를 쓴다(원본과 같음).</summary>
    public static class CampaignSummaryBuilder
    {
        public static CampaignHudSummary BuildHudSummary(Campaign campaign, CampaignProgress progress)
        {
            CampaignDay day = campaign != null ? campaign.GetDayByNumber(progress != null ? progress.CurrentDayNumber : 1) : null;
            return new CampaignHudSummary(day != null ? day.ThemeKey : string.Empty, FormatRoutes(day), FormatProgress(progress), day != null ? day.NextGuideText : string.Empty);
        }

        public static CampaignDayResultSummary BuildDayResultSummary(Campaign campaign, CampaignProgress progress, CampaignApplyResult result)
        {
            CampaignDay day = campaign != null ? campaign.GetDayByNumber(progress != null ? progress.CurrentDayNumber : 1) : null;
            string selected = day != null && progress != null ? progress.GetSelectedRouteId(day.Id) : string.Empty;
            string progressText = result.CompletedObjectiveIds != null && result.CompletedObjectiveIds.Length > 0 ? string.Join("\n", result.CompletedObjectiveIds) : FormatProgress(progress);
            return new CampaignDayResultSummary(selected, progressText, day != null ? day.NextGuideText : string.Empty);
        }

        private static string FormatRoutes(CampaignDay day)
        {
            if (day == null) return string.Empty;
            var names = new List<string>();
            foreach (CampaignRoute route in day.Routes) if (route != null) names.Add(route.DisplayNameKey);
            return string.Join("\n", names.ToArray());
        }

        private static string FormatProgress(CampaignProgress progress) => progress == null ? string.Empty : string.Join("\n", progress.LastProgressLogIds);
    }
}
