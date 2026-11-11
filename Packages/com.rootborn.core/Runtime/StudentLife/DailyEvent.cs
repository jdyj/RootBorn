using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    public static class DailyEventStates
    {
        public const string Unseen = "unseen";
        public const string Available = "available";
        public const string Deferred = "deferred";
        public const string Completed = "completed";
        public const string Declined = "declined";
        public const string Expired = "expired";
        public const string Cooldown = "cooldown";
    }

    /// <summary>하루 이벤트의 종류. 미룰 수 있는지, 시간 제한이 있는지, 반복되는지.</summary>
    public sealed class DailyEventKind
    {
        public DailyEventKind(string id, bool isOptional, bool isDeferrable, bool isTimed, bool isRequired, bool isRepeatable, bool isConditional, int deferDays, int dueTimeMinutes)
        {
            Id = id ?? string.Empty;
            IsOptional = isOptional;
            IsDeferrable = isDeferrable;
            IsTimed = isTimed;
            IsRequired = isRequired;
            IsRepeatable = isRepeatable;
            IsConditional = isConditional;
            DeferDays = Math.Max(0, deferDays);
            DueTimeMinutes = Math.Max(0, dueTimeMinutes);
        }

        public string Id { get; }
        public bool IsOptional { get; }
        public bool IsDeferrable { get; }
        public bool IsTimed { get; }
        public bool IsRequired { get; }
        public bool IsRepeatable { get; }
        public bool IsConditional { get; }
        public int DeferDays { get; }
        public int DueTimeMinutes { get; }
    }

    public interface IDailyEventAvailabilityRule
    {
        bool IsSatisfied(DailyEventContext context, DailyEvent dailyEvent);
    }

    /// <summary>결과를 적용하고 기록 한 줄을 돌려준다. 바뀐 것이 없으면 빈 문자열.</summary>
    public interface IDailyEventOutcome
    {
        string Apply(StudentLifeProgress progress, DailyEvent dailyEvent, DailyEventChoice choice);
    }

    /// <summary>장소·NPC에 붙는 하루 이벤트 하나. 장소와 NPC는 id로 가리킨다.</summary>
    public sealed class DailyEvent
    {
        private readonly IDailyEventAvailabilityRule[] _availabilityRules;
        private readonly DailyEventChoice[] _choices;

        public DailyEvent(string id, string descriptionKey, string locationId, string npcId, DailyEventKind kind, IDailyEventAvailabilityRule[] availabilityRules, DailyEventChoice[] choices)
        {
            Id = id ?? string.Empty;
            DescriptionKey = string.IsNullOrEmpty(descriptionKey) ? Id : descriptionKey;
            LocationId = locationId ?? string.Empty;
            NpcId = npcId ?? string.Empty;
            Kind = kind;
            _availabilityRules = availabilityRules ?? Array.Empty<IDailyEventAvailabilityRule>();
            _choices = choices ?? Array.Empty<DailyEventChoice>();
        }

        public string Id { get; }
        public string DescriptionKey { get; }
        public string LocationId { get; }
        public string NpcId { get; }
        public DailyEventKind Kind { get; }
        public IReadOnlyList<IDailyEventAvailabilityRule> AvailabilityRules => _availabilityRules;
        public IReadOnlyList<DailyEventChoice> Choices => _choices;

        public bool HasSatisfiedAvailability(DailyEventContext context)
        {
            for (int i = 0; i < _availabilityRules.Length; i++)
            {
                IDailyEventAvailabilityRule rule = _availabilityRules[i];
                if (rule != null && !rule.IsSatisfied(context, this)) return false;
            }
            return true;
        }
    }

    public sealed class DailyEventChoice
    {
        private readonly IDailyEventAvailabilityRule[] _availabilityRules;
        private readonly IDailyEventOutcome[] _outcomes;

        public DailyEventChoice(string id, string resultSummaryKey, ActivityTimePolicy timePolicy, int timeCostMinutes, int energyCost, int focusCost, int stressDelta,
            bool isDeferChoice, bool isDeclineChoice, IDailyEventAvailabilityRule[] availabilityRules, IDailyEventOutcome[] outcomes)
        {
            Id = id ?? string.Empty;
            ResultSummaryKey = string.IsNullOrEmpty(resultSummaryKey) ? Id : resultSummaryKey;
            TimePolicy = timePolicy;
            TimeCostMinutes = Math.Max(0, timeCostMinutes);
            EnergyCost = Math.Max(0, energyCost);
            FocusCost = Math.Max(0, focusCost);
            StressDelta = stressDelta;
            IsDeferChoice = isDeferChoice;
            IsDeclineChoice = isDeclineChoice;
            _availabilityRules = availabilityRules ?? Array.Empty<IDailyEventAvailabilityRule>();
            _outcomes = outcomes ?? Array.Empty<IDailyEventOutcome>();
        }

        public string Id { get; }
        public string ResultSummaryKey { get; }
        public ActivityTimePolicy TimePolicy { get; }
        public int TimeCostMinutes { get; }
        public int EnergyCost { get; }
        public int FocusCost { get; }
        public int StressDelta { get; }
        public bool IsDeferChoice { get; }
        public bool IsDeclineChoice { get; }
        public IReadOnlyList<IDailyEventAvailabilityRule> AvailabilityRules => _availabilityRules;
        public IReadOnlyList<IDailyEventOutcome> Outcomes => _outcomes;

        public bool HasSatisfiedAvailability(DailyEventContext context, DailyEvent dailyEvent)
        {
            for (int i = 0; i < _availabilityRules.Length; i++)
            {
                IDailyEventAvailabilityRule rule = _availabilityRules[i];
                if (rule != null && !rule.IsSatisfied(context, dailyEvent)) return false;
            }
            return true;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress, DailyEvent dailyEvent)
        {
            var logs = new List<string>();
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, dailyEvent, this) : string.Empty;
                if (!string.IsNullOrEmpty(log)) logs.Add(log);
            }
            return logs.ToArray();
        }
    }

    /// <summary>이벤트를 고를 때의 상황. 장소는 id로 받는다.</summary>
    public readonly struct DailyEventContext
    {
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly DailyEventProgress EventProgress;
        public readonly string LocationId;
        public readonly int Day;
        public readonly int TimeMinutes;

        public DailyEventContext(StudentLifeProgress studentLifeProgress, DailyEventProgress eventProgress, string locationId, int day, int timeMinutes)
        {
            StudentLifeProgress = studentLifeProgress;
            EventProgress = eventProgress;
            LocationId = locationId ?? string.Empty;
            Day = Math.Max(1, day);
            TimeMinutes = Math.Max(0, timeMinutes);
        }
    }

    public readonly struct DailyEventResultSummary
    {
        public readonly string EventId;
        public readonly string State;
        public readonly string SelectedChoiceId;
        public readonly string CompletionRequestId;
        public readonly string[] ResultSummaryLogIds;

        public DailyEventResultSummary(string eventId, string state, string selectedChoiceId, string completionRequestId, string[] resultSummaryLogIds)
        {
            EventId = string.IsNullOrEmpty(eventId) ? string.Empty : eventId;
            State = string.IsNullOrEmpty(state) ? DailyEventStates.Unseen : state;
            SelectedChoiceId = string.IsNullOrEmpty(selectedChoiceId) ? string.Empty : selectedChoiceId;
            CompletionRequestId = string.IsNullOrEmpty(completionRequestId) ? string.Empty : completionRequestId;
            ResultSummaryLogIds = resultSummaryLogIds ?? Array.Empty<string>();
        }

        public static DailyEventResultSummary FromRecord(DailyEventRecord record)
        {
            if (record == null) return new DailyEventResultSummary(string.Empty, DailyEventStates.Unseen, string.Empty, string.Empty, Array.Empty<string>());
            return new DailyEventResultSummary(record.EventId, record.State, record.SelectedChoiceId, record.CompletionRequestId, record.ResultSummaryLogIds);
        }
    }
}
