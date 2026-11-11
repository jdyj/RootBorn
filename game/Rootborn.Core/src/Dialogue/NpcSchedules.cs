using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Dialogue
{
    /// <summary>
    /// 일정 규칙을 볼 때 주어지는 것. 시간대는 id로 받는다. 시각을 시간대로 바꿀 때는 <see cref="LocationStateTimeSlots"/>를 쓴다
    /// (원본 NpcScheduleResolver.ResolveTimeSlotId는 옮기지 않았다. 2026-11-11 사용자 결정).
    /// </summary>
    public readonly struct NpcScheduleContext
    {
        public readonly int Day;
        public readonly int WeekdayIndex;
        public readonly string TimeSlotId;
        public readonly StudentLifeProgress Progress;

        public NpcScheduleContext(int day, int weekdayIndex, string timeSlotId, StudentLifeProgress progress)
        {
            Day = Math.Max(1, day);
            WeekdayIndex = Math.Min(6, Math.Max(0, weekdayIndex));
            TimeSlotId = timeSlotId ?? string.Empty;
            Progress = progress;
        }
    }

    public interface INpcScheduleRule
    {
        bool IsSatisfied(in NpcScheduleContext context);
    }

    /// <summary>시간대 중 하나와 맞으면 참. 시간대를 하나도 정하지 않으면 언제나 참.</summary>
    public sealed class TimeSlotScheduleRule : INpcScheduleRule
    {
        private readonly string[] _timeSlotIds;

        public TimeSlotScheduleRule(string[] timeSlotIds) { _timeSlotIds = timeSlotIds ?? Array.Empty<string>(); }

        public IReadOnlyList<string> TimeSlotIds => _timeSlotIds;

        public bool IsSatisfied(in NpcScheduleContext context)
        {
            if (_timeSlotIds.Length == 0) return true;
            for (int i = 0; i < _timeSlotIds.Length; i++)
                if (!string.IsNullOrEmpty(_timeSlotIds[i]) && string.Equals(_timeSlotIds[i], context.TimeSlotId, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    /// <summary>일정 한 줄. 규칙이 모두 맞으면 그 장소에 있다. 장소는 id로 가진다.</summary>
    public sealed class NpcScheduleEntry
    {
        private readonly INpcScheduleRule[] _rules;

        public NpcScheduleEntry(string locationId, DialogueDefinition dialogue, string dialogueKey, string interactionHintKey, string eventNoticeKey, int priority, INpcScheduleRule[] rules)
        {
            LocationId = locationId ?? string.Empty;
            Dialogue = dialogue;
            DialogueKey = dialogueKey ?? string.Empty;
            InteractionHintKey = interactionHintKey ?? string.Empty;
            EventNoticeKey = eventNoticeKey ?? string.Empty;
            Priority = priority;
            _rules = rules ?? Array.Empty<INpcScheduleRule>();
        }

        public string LocationId { get; }
        public DialogueDefinition Dialogue { get; }
        public string DialogueKey { get; }
        public string InteractionHintKey { get; }
        public string EventNoticeKey { get; }
        public int Priority { get; }
        public IReadOnlyList<INpcScheduleRule> Rules => _rules;

        public bool IsMatch(in NpcScheduleContext context)
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (rule != null && !rule.IsSatisfied(in context)) return false;
            }

            return true;
        }
    }

    /// <summary>NPC 하나의 일정. id는 데이터 내보내기의 자산 이름이다. 대체 장소가 없으면 NPC의 집이다.</summary>
    public sealed class NpcSchedule
    {
        private readonly NpcScheduleEntry[] _entries;
        private readonly string _fallbackLocationId;

        public NpcSchedule(string id, NpcDefinition npc, string fallbackLocationId, NpcScheduleEntry[] entries)
        {
            Id = id ?? string.Empty;
            Npc = npc;
            _fallbackLocationId = fallbackLocationId ?? string.Empty;
            _entries = entries ?? Array.Empty<NpcScheduleEntry>();
        }

        public string Id { get; }
        public NpcDefinition Npc { get; }
        public string FallbackLocationId => _fallbackLocationId.Length > 0 ? _fallbackLocationId : Npc != null ? Npc.HomeLocationId : string.Empty;
        public IReadOnlyList<NpcScheduleEntry> Entries => _entries;
    }

    public readonly struct NpcScheduleResult
    {
        public readonly NpcDefinition Npc;
        public readonly string LocationId;
        public readonly DialogueDefinition Dialogue;
        public readonly string DialogueKey;
        public readonly string InteractionHintKey;
        public readonly string EventNoticeKey;
        public readonly NpcScheduleEntry Entry;

        public NpcScheduleResult(NpcDefinition npc, string locationId, DialogueDefinition dialogue, string dialogueKey, string interactionHintKey, string eventNoticeKey, NpcScheduleEntry entry)
        {
            Npc = npc;
            LocationId = locationId ?? string.Empty;
            Dialogue = dialogue;
            DialogueKey = dialogueKey ?? string.Empty;
            InteractionHintKey = interactionHintKey ?? string.Empty;
            EventNoticeKey = eventNoticeKey ?? string.Empty;
            Entry = entry;
        }
    }

    /// <summary>맞는 일정 줄 중 우선순위가 가장 높은 것(같으면 앞의 것). 없으면 대체 장소와 NPC 기본 대화.</summary>
    public sealed class NpcScheduleResolver
    {
        public NpcScheduleResult Resolve(NpcSchedule schedule, in NpcScheduleContext context)
        {
            if (schedule == null) return default;
            NpcScheduleEntry best = null;
            for (int i = 0; i < schedule.Entries.Count; i++)
            {
                var entry = schedule.Entries[i];
                if (entry == null || !entry.IsMatch(in context)) continue;
                if (best == null || entry.Priority > best.Priority) best = entry;
            }

            string locationId = best != null && best.LocationId.Length > 0 ? best.LocationId : schedule.FallbackLocationId;
            var dialogue = best != null ? best.Dialogue : schedule.Npc?.DefaultDialogue;
            return new NpcScheduleResult(schedule.Npc, locationId, dialogue, best?.DialogueKey, best?.InteractionHintKey, best?.EventNoticeKey, best);
        }
    }

    public enum NpcScheduleProgressResultKind
    {
        FirstMeeting,
        DuplicateMeeting,
        InvalidRequest
    }

    public readonly struct NpcScheduleProgressResult
    {
        public readonly NpcScheduleProgressResultKind Kind;
        public readonly string RecordId;
        public readonly string EventNoticeKey;

        public NpcScheduleProgressResult(NpcScheduleProgressResultKind kind, string recordId, string eventNoticeKey)
        {
            Kind = kind;
            RecordId = recordId ?? string.Empty;
            EventNoticeKey = eventNoticeKey ?? string.Empty;
        }
    }

    /// <summary>
    /// NPC를 어느 장소·시간대에 만났는지. 따로 세이브하지 않고 학생 진행의 활동 기록에 "npc@장소@시간대"로 남긴다(원본과 같다).
    /// 처음 만날 때 알림 키가 있으면 오늘 결과 기록에 한 번 남는다.
    /// </summary>
    public sealed class NpcScheduleProgress
    {
        private readonly StudentLifeProgress _progress;

        public NpcScheduleProgress(StudentLifeProgress progress) { _progress = progress; }

        /// <summary>"npc."로 시작하는 NPC와 "location."으로 시작하는 장소로 된 세 토막 기록만 만남으로 본다(원본과 같다).</summary>
        public string[] MeetingRecordIds
        {
            get
            {
                if (_progress == null) return Array.Empty<string>();
                var records = new List<string>();
                string[] ids = _progress.GetActivityLogIds();
                for (int i = 0; i < ids.Length; i++) if (IsMeetingRecord(ids[i]) && !records.Contains(ids[i])) records.Add(ids[i]);
                return records.ToArray();
            }
        }

        public bool TryRecordMeeting(NpcDefinition npc, string locationId, string timeSlotId, string eventNoticeKey, out NpcScheduleProgressResult result)
        {
            string recordId = BuildRecordId(npc, locationId, timeSlotId);
            string notice = eventNoticeKey ?? string.Empty;
            result = new NpcScheduleProgressResult(NpcScheduleProgressResultKind.InvalidRequest, recordId, notice);
            if (_progress == null || recordId.Length == 0) return false;
            if (HasMet(npc, locationId, timeSlotId))
            {
                result = new NpcScheduleProgressResult(NpcScheduleProgressResultKind.DuplicateMeeting, recordId, notice);
                return false;
            }

            _progress.RecordActivityCompleted(recordId, notice.Length == 0 ? Array.Empty<string>() : new[] { notice });
            result = new NpcScheduleProgressResult(NpcScheduleProgressResultKind.FirstMeeting, recordId, notice);
            return true;
        }

        public bool HasMet(NpcDefinition npc, string locationId, string timeSlotId)
        {
            string recordId = BuildRecordId(npc, locationId, timeSlotId);
            if (recordId.Length == 0 || _progress == null) return false;
            return Array.IndexOf(_progress.GetActivityLogIds(), recordId) >= 0;
        }

        /// <summary>그 NPC 만남 기록 중 가장 나중 것의 장소 id.</summary>
        public string GetLastMeetingLocationId(NpcDefinition npc)
        {
            string npcId = npc != null ? npc.Id : string.Empty;
            if (npcId.Length == 0 || _progress == null) return string.Empty;
            string prefix = npcId + "@";
            string last = string.Empty;
            string[] records = _progress.GetActivityLogIds();
            for (int i = 0; i < records.Length; i++)
            {
                string record = records[i];
                if (string.IsNullOrEmpty(record) || !record.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string[] parts = record.Split('@');
                if (parts.Length >= 2) last = parts[1];
            }

            return last;
        }

        private static string BuildRecordId(NpcDefinition npc, string locationId, string timeSlotId)
        {
            if (npc == null || string.IsNullOrEmpty(npc.Id) || string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(timeSlotId)) return string.Empty;
            return npc.Id + "@" + locationId + "@" + timeSlotId;
        }

        private static bool IsMeetingRecord(string recordId)
        {
            if (string.IsNullOrEmpty(recordId)) return false;
            string[] parts = recordId.Split('@');
            return parts.Length == 3 && parts[0].StartsWith("npc.", StringComparison.Ordinal) && parts[1].StartsWith("location.", StringComparison.Ordinal);
        }
    }

    /// <summary>일정 id·NPC id → 일정 조회. 같은 NPC 일정이 여럿이면 뒤의 것이 NPC 조회를 이긴다(원본 GameDataLookupCache).</summary>
    public sealed class NpcScheduleLookupCache
    {
        private readonly IReadOnlyList<NpcSchedule> _schedules;
        private Dictionary<string, NpcSchedule> _byId;
        private Dictionary<string, NpcSchedule> _byNpcId;

        public NpcScheduleLookupCache(IReadOnlyList<NpcSchedule> schedules)
        {
            _schedules = schedules ?? Array.Empty<NpcSchedule>();
        }

        public int BuildCount { get; private set; }

        public bool TryGetById(string scheduleId, out NpcSchedule schedule)
        {
            EnsureBuilt();
            schedule = null;
            return !string.IsNullOrEmpty(scheduleId) && _byId.TryGetValue(scheduleId, out schedule);
        }

        public bool TryGetByNpcId(string npcId, out NpcSchedule schedule)
        {
            EnsureBuilt();
            schedule = null;
            return !string.IsNullOrEmpty(npcId) && _byNpcId.TryGetValue(npcId, out schedule);
        }

        private void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, NpcSchedule>(StringComparer.Ordinal);
            _byNpcId = new Dictionary<string, NpcSchedule>(StringComparer.Ordinal);
            for (int i = 0; i < _schedules.Count; i++)
            {
                var schedule = _schedules[i];
                if (schedule == null) continue;
                if (schedule.Id.Length > 0) _byId[schedule.Id] = schedule;
                if (schedule.Npc != null && schedule.Npc.Id.Length > 0) _byNpcId[schedule.Npc.Id] = schedule;
            }

            BuildCount++;
        }
    }

    /// <summary>
    /// 장소 상태가 일정 id로 NPC 위치를 묻는 창구(main의 ILocationStateNpcSchedules). 요일은 원본 LocationStateNpcScheduleCondition처럼 0이다.
    /// 없는 일정이면 빈 문자열.
    /// </summary>
    public sealed class NpcScheduleAccess : ILocationStateNpcSchedules
    {
        private readonly NpcScheduleLookupCache _schedules;
        private readonly NpcScheduleResolver _resolver = new NpcScheduleResolver();

        public NpcScheduleAccess(NpcScheduleLookupCache schedules)
        {
            _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        }

        public string ResolveLocationId(string scheduleId, int day, string timeSlotId, StudentLifeProgress progress)
        {
            if (!_schedules.TryGetById(scheduleId, out var schedule)) return string.Empty;
            return _resolver.Resolve(schedule, new NpcScheduleContext(day, 0, timeSlotId, progress)).LocationId;
        }
    }
}
