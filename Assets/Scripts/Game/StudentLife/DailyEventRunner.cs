using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class DailyEventRunner
    {
        public bool TryChoose(DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice, StudentLifeProgress progress, DailyEventProgress eventProgress, string requestId, out LifeActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string eventId = dailyEvent == null ? string.Empty : dailyEvent.Id;
            string choiceId = choice == null ? string.Empty : choice.Id;
            result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, eventId, requestId, choiceId, Array.Empty<string>());
            if (dailyEvent == null || choice == null || progress == null || eventProgress == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId) || eventProgress.HasCompletionRequest(requestId))
            {
                result = new LifeActivityResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, dailyEvent.Id, requestId, choice.Id, Array.Empty<string>());
                return false;
            }

            if (!progress.CanSpend(choice.EnergyCost, choice.FocusCost))
            {
                result = new LifeActivityResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, dailyEvent.Id, requestId, choice.Id, Array.Empty<string>());
                return false;
            }

            progress.Spend(choice.TimeCostMinutes, choice.EnergyCost, choice.FocusCost, choice.StressDelta);
            string[] logs = choice.ApplyOutcomes(progress, dailyEvent);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(dailyEvent.Id, logs);
            eventProgress.GetRecord(dailyEvent.Id).MarkCompleted(choice.Id, requestId, progress.CurrentDay, logs);
            result = new LifeActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, dailyEvent.Id, requestId, choice.Id, Array.Empty<string>());
            return true;
        }

        public bool TryDefer(DailyEventDefinition dailyEvent, DailyEventProgress eventProgress, int currentDay, out DailyEventRecord record)
        {
            record = dailyEvent != null && eventProgress != null ? eventProgress.GetRecord(dailyEvent.Id) : null;
            if (dailyEvent == null || eventProgress == null || dailyEvent.Kind == null || !dailyEvent.Kind.IsDeferrable) return false;
            int safeDay = Mathf.Max(1, currentDay);
            int dueDay = safeDay + Mathf.Max(1, dailyEvent.Kind.DeferDays);
            record.MarkDeferred(safeDay, dueDay);
            return true;
        }

        public bool TryDecline(DailyEventDefinition dailyEvent, DailyEventProgress eventProgress, int currentDay, out DailyEventRecord record)
        {
            record = dailyEvent != null && eventProgress != null ? eventProgress.GetRecord(dailyEvent.Id) : null;
            if (dailyEvent == null || eventProgress == null) return false;
            record.MarkDeclined(currentDay);
            return true;
        }
    }

    public sealed class DailyEventResolver
    {
        private readonly DailyEventDefinition[] _events;
        private readonly Dictionary<string, DailyEventDefinition> _eventsById = new Dictionary<string, DailyEventDefinition>(StringComparer.Ordinal);

        public DailyEventResolver(IReadOnlyList<DailyEventDefinition> events)
        {
            BuildCount = 1;
            _events = Copy(events);
            for (int i = 0; i < _events.Length; i++)
            {
                var dailyEvent = _events[i];
                if (dailyEvent != null && !string.IsNullOrEmpty(dailyEvent.Id)) _eventsById[dailyEvent.Id] = dailyEvent;
            }
        }

        public int BuildCount { get; }

        public bool TryGetEvent(string eventId, out DailyEventDefinition dailyEvent)
        {
            return _eventsById.TryGetValue(string.IsNullOrEmpty(eventId) ? string.Empty : eventId, out dailyEvent);
        }

        public DailyEventDefinition[] GetAvailableEvents(DailyEventContext context)
        {
            var available = new List<DailyEventDefinition>();
            for (int i = 0; i < _events.Length; i++)
            {
                var dailyEvent = _events[i];
                if (dailyEvent == null || context.EventProgress == null) continue;
                var record = context.EventProgress.GetRecord(dailyEvent.Id);
                if (ShouldSkipForState(record, dailyEvent, context)) continue;
                if (IsExpired(record, dailyEvent, context)) continue;
                if (!dailyEvent.HasSatisfiedAvailability(context)) continue;
                record.MarkAvailable(context.Day);
                available.Add(dailyEvent);
            }

            return available.ToArray();
        }

        private static bool ShouldSkipForState(DailyEventRecord record, DailyEventDefinition dailyEvent, DailyEventContext context)
        {
            if (record == null) return true;
            bool repeatable = dailyEvent != null && dailyEvent.Kind != null && dailyEvent.Kind.IsRepeatable;
            if (!repeatable && (record.State == DailyEventStates.Completed || record.State == DailyEventStates.Declined || record.State == DailyEventStates.Expired)) return true;
            if (record.State == DailyEventStates.Cooldown && record.CooldownUntilDay > context.Day) return true;
            if (record.State == DailyEventStates.Deferred && record.DueDay > context.Day) return true;
            return false;
        }

        private static bool IsExpired(DailyEventRecord record, DailyEventDefinition dailyEvent, DailyEventContext context)
        {
            var kind = dailyEvent != null ? dailyEvent.Kind : null;
            if (kind == null || !kind.IsTimed || kind.DueTimeMinutes <= 0) return false;
            if (context.TimeMinutes <= kind.DueTimeMinutes) return false;
            record.MarkExpired(context.Day, kind.DueTimeMinutes);
            return true;
        }

        private static DailyEventDefinition[] Copy(IReadOnlyList<DailyEventDefinition> events)
        {
            if (events == null) return Array.Empty<DailyEventDefinition>();
            var copy = new DailyEventDefinition[events.Count];
            for (int i = 0; i < events.Count; i++) copy[i] = events[i];
            return copy;
        }
    }

    public static class DailyEventLogCodec
    {
        private const string Prefix = "daily-event:";
        private const string UnlockMarker = ":unlock=";

        public static string EncodeDelta(DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice, string targetId, int delta)
        {
            if (dailyEvent == null || choice == null || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + dailyEvent.Id + ":" + choice.Id + ":" + sign + targetId + "=" + delta.ToString();
        }

        public static string EncodeUnlock(DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice, string targetId)
        {
            if (dailyEvent == null || choice == null || string.IsNullOrEmpty(targetId)) return string.Empty;
            return Prefix + dailyEvent.Id + ":" + choice.Id + UnlockMarker + targetId;
        }
    }
}
