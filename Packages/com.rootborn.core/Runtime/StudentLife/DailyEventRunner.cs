using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 하루 이벤트의 선택지 고르기·미루기·거절하기. 순서: 요청 확인 → 중복 요청 → 선택지 가용 규칙 → 시간·체력·집중 → 적용.
    /// 거절하면 아무것도 바꾸지 않는다. 선택지 가용 규칙 검사는 원본에 없던 것이다.
    /// </summary>
    public sealed class DailyEventRunner
    {
        /// <summary>지금 장소를 이벤트의 장소로 보고 고른다.</summary>
        public bool TryChoose(DailyEvent dailyEvent, DailyEventChoice choice, StudentLifeProgress progress, DailyEventProgress eventProgress, string requestId, out LifeActivityResult result)
            => TryChoose(dailyEvent, choice, progress, eventProgress, requestId, null, out result);

        /// <summary>지금 장소(비면 이벤트의 장소)에서 고른다. 선택지 가용 규칙은 이 장소와 진행 상태의 날·시각으로 본다.</summary>
        public bool TryChoose(DailyEvent dailyEvent, DailyEventChoice choice, StudentLifeProgress progress, DailyEventProgress eventProgress, string requestId, string currentLocationId, out LifeActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string eventId = dailyEvent == null ? string.Empty : dailyEvent.Id;
            string choiceId = choice == null ? string.Empty : choice.Id;
            LifeActivityResult Result(LifeActivityResultKind kind) => new LifeActivityResult(kind, saveSlot, playerId, eventId, requestId, choiceId, Array.Empty<string>());

            result = Result(LifeActivityResultKind.InvalidRequest);
            if (dailyEvent == null || choice == null || progress == null || eventProgress == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId) || eventProgress.HasCompletionRequest(requestId))
            {
                result = Result(LifeActivityResultKind.DuplicateRequest);
                return false;
            }
            var context = new DailyEventContext(progress, eventProgress, string.IsNullOrEmpty(currentLocationId) ? dailyEvent.LocationId : currentLocationId, progress.CurrentDay, progress.TimeMinutes);
            if (!choice.HasSatisfiedAvailability(context, dailyEvent))
            {
                result = Result(LifeActivityResultKind.RequirementFailed);
                return false;
            }
            if (!progress.CanUseTimePolicy(choice.TimePolicy) || !progress.CanSpend(choice.EnergyCost, choice.FocusCost))
            {
                result = Result(LifeActivityResultKind.InsufficientResources);
                return false;
            }

            progress.TryUseTimePolicy(choice.TimePolicy);
            progress.Spend(choice.TimeCostMinutes, choice.EnergyCost, choice.FocusCost, choice.StressDelta);
            string[] logs = choice.ApplyOutcomes(progress, dailyEvent);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(dailyEvent.Id, logs);
            eventProgress.GetRecord(dailyEvent.Id).MarkCompleted(choice.Id, requestId, progress.CurrentDay, logs);
            result = Result(LifeActivityResultKind.Applied);
            return true;
        }

        /// <summary>미룰 수 있는 종류만. 다시 나오는 날은 오늘 + 미루는 날 수(최소 1).</summary>
        public bool TryDefer(DailyEvent dailyEvent, DailyEventProgress eventProgress, int currentDay, out DailyEventRecord record)
        {
            record = dailyEvent != null && eventProgress != null ? eventProgress.GetRecord(dailyEvent.Id) : null;
            if (dailyEvent == null || eventProgress == null || dailyEvent.Kind == null || !dailyEvent.Kind.IsDeferrable) return false;
            int safeDay = Math.Max(1, currentDay);
            record.MarkDeferred(safeDay, safeDay + Math.Max(1, dailyEvent.Kind.DeferDays));
            return true;
        }

        public bool TryDecline(DailyEvent dailyEvent, DailyEventProgress eventProgress, int currentDay, out DailyEventRecord record)
        {
            record = dailyEvent != null && eventProgress != null ? eventProgress.GetRecord(dailyEvent.Id) : null;
            if (dailyEvent == null || eventProgress == null) return false;
            record.MarkDeclined(currentDay);
            return true;
        }
    }

    /// <summary>지금 상황에서 보여 줄 이벤트를 고른다. 고른 이벤트는 "볼 수 있음"으로, 시간이 지난 이벤트는 "만료"로 기록한다.</summary>
    public sealed class DailyEventResolver
    {
        private readonly DailyEvent[] _events;
        private readonly Dictionary<string, DailyEvent> _eventsById = new Dictionary<string, DailyEvent>(StringComparer.Ordinal);

        public DailyEventResolver(IReadOnlyList<DailyEvent> events)
        {
            BuildCount = 1;
            _events = Copy(events);
            for (int i = 0; i < _events.Length; i++)
            {
                DailyEvent dailyEvent = _events[i];
                if (dailyEvent != null && !string.IsNullOrEmpty(dailyEvent.Id)) _eventsById[dailyEvent.Id] = dailyEvent;
            }
        }

        public int BuildCount { get; }

        public bool TryGetEvent(string eventId, out DailyEvent dailyEvent) => _eventsById.TryGetValue(string.IsNullOrEmpty(eventId) ? string.Empty : eventId, out dailyEvent);

        public DailyEvent[] GetAvailableEvents(DailyEventContext context)
        {
            var available = new List<DailyEvent>();
            for (int i = 0; i < _events.Length; i++)
            {
                DailyEvent dailyEvent = _events[i];
                if (dailyEvent == null || context.EventProgress == null) continue;
                DailyEventRecord record = context.EventProgress.GetRecord(dailyEvent.Id);
                if (ShouldSkipForState(record, dailyEvent, context)) continue;
                if (IsExpired(record, dailyEvent, context)) continue;
                if (!dailyEvent.HasSatisfiedAvailability(context)) continue;
                record.MarkAvailable(context.Day);
                available.Add(dailyEvent);
            }
            return available.ToArray();
        }

        private static bool ShouldSkipForState(DailyEventRecord record, DailyEvent dailyEvent, DailyEventContext context)
        {
            if (record == null) return true;
            bool repeatable = dailyEvent.Kind != null && dailyEvent.Kind.IsRepeatable;
            if (!repeatable && (record.State == DailyEventStates.Completed || record.State == DailyEventStates.Declined || record.State == DailyEventStates.Expired)) return true;
            if (record.State == DailyEventStates.Cooldown && record.CooldownUntilDay > context.Day) return true;
            if (record.State == DailyEventStates.Deferred && record.DueDay > context.Day) return true;
            return false;
        }

        private static bool IsExpired(DailyEventRecord record, DailyEvent dailyEvent, DailyEventContext context)
        {
            DailyEventKind kind = dailyEvent.Kind;
            if (kind == null || !kind.IsTimed || kind.DueTimeMinutes <= 0) return false;
            if (context.TimeMinutes <= kind.DueTimeMinutes) return false;
            record.MarkExpired(context.Day, kind.DueTimeMinutes);
            return true;
        }

        private static DailyEvent[] Copy(IReadOnlyList<DailyEvent> events)
        {
            if (events == null) return Array.Empty<DailyEvent>();
            var copy = new DailyEvent[events.Count];
            for (int i = 0; i < events.Count; i++) copy[i] = events[i];
            return copy;
        }
    }
}
