using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.Dialogue
{
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
            RecordId = string.IsNullOrEmpty(recordId) ? string.Empty : recordId;
            EventNoticeKey = string.IsNullOrEmpty(eventNoticeKey) ? string.Empty : eventNoticeKey;
        }
    }

    public sealed class NpcScheduleProgress
    {
        private readonly StudentLifeProgress _progress;

        public NpcScheduleProgress(StudentLifeProgress progress)
        {
            _progress = progress;
        }

        public string[] MeetingRecordIds
        {
            get
            {
                if (_progress == null) return Array.Empty<string>();
                var records = new List<string>();
                string[] ids = _progress.GetActivityLogIds();
                for (int i = 0; i < ids.Length; i++) if (IsMeetingRecord(ids[i])) AddUnique(records, ids[i]);
                return records.ToArray();
            }
        }

        public bool TryRecordMeeting(NpcDefinition npc, LocationDefinition location, string timeSlotId, string eventNoticeKey, out NpcScheduleProgressResult result)
        {
            string recordId = BuildRecordId(npc, location, timeSlotId);
            string notice = string.IsNullOrEmpty(eventNoticeKey) ? string.Empty : eventNoticeKey;
            result = new NpcScheduleProgressResult(NpcScheduleProgressResultKind.InvalidRequest, recordId, notice);
            if (_progress == null || string.IsNullOrEmpty(recordId)) return false;
            if (HasMet(npc, location, timeSlotId))
            {
                result = new NpcScheduleProgressResult(NpcScheduleProgressResultKind.DuplicateMeeting, recordId, notice);
                return false;
            }

            _progress.RecordActivityCompleted(recordId, string.IsNullOrEmpty(notice) ? Array.Empty<string>() : new[] { notice });
            result = new NpcScheduleProgressResult(NpcScheduleProgressResultKind.FirstMeeting, recordId, notice);
            return true;
        }

        public bool HasMet(NpcDefinition npc, LocationDefinition location, string timeSlotId)
        {
            string recordId = BuildRecordId(npc, location, timeSlotId);
            if (string.IsNullOrEmpty(recordId) || _progress == null) return false;
            string[] records = _progress.GetActivityLogIds();
            for (int i = 0; i < records.Length; i++) if (string.Equals(records[i], recordId, StringComparison.Ordinal)) return true;
            return false;
        }

        public string GetLastMeetingLocationId(NpcDefinition npc)
        {
            string npcId = npc != null ? npc.Id : string.Empty;
            if (string.IsNullOrEmpty(npcId) || _progress == null) return string.Empty;
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

        private static string BuildRecordId(NpcDefinition npc, LocationDefinition location, string timeSlotId)
        {
            if (npc == null || location == null || string.IsNullOrEmpty(npc.Id) || string.IsNullOrEmpty(location.Id) || string.IsNullOrEmpty(timeSlotId)) return string.Empty;
            return npc.Id + "@" + location.Id + "@" + timeSlotId;
        }

        private static bool IsMeetingRecord(string recordId)
        {
            if (string.IsNullOrEmpty(recordId)) return false;
            string[] parts = recordId.Split('@');
            return parts.Length == 3 && parts[0].StartsWith("npc.", StringComparison.Ordinal) && parts[1].StartsWith("location.", StringComparison.Ordinal);
        }

        private static void AddUnique(List<string> records, string recordId)
        {
            for (int i = 0; i < records.Count; i++) if (string.Equals(records[i], recordId, StringComparison.Ordinal)) return;
            records.Add(recordId);
        }
    }
}
