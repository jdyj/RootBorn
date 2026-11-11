using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>요약에 필요한 관계 정의의 값(관계 정의는 아직 Core에 없다).</summary>
    public readonly struct RelationshipSummaryInfo
    {
        public RelationshipSummaryInfo(string id, string displayNameKey)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
    }

    /// <summary>요약에 필요한 상태 정의의 값(상태 정의는 아직 Core에 없다).</summary>
    public readonly struct StatusSummaryInfo
    {
        public StatusSummaryInfo(string id, string displayNameKey, bool persistsToNextDay)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            PersistsToNextDay = persistsToNextDay;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public bool PersistsToNextDay { get; }
    }

    public readonly struct RelationshipDeltaEntry
    {
        public readonly string RelationshipId;
        public readonly string DisplayName;
        public readonly int BeforeValue;
        public readonly int AfterValue;

        public RelationshipDeltaEntry(string relationshipId, string displayName, int beforeValue, int afterValue)
        {
            RelationshipId = relationshipId ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? RelationshipId : displayName;
            BeforeValue = Math.Max(0, beforeValue);
            AfterValue = Math.Max(0, afterValue);
        }

        public int Delta => AfterValue - BeforeValue;
    }

    public readonly struct StatusDeltaEntry
    {
        public readonly string StatusId;
        public readonly string DisplayName;
        public readonly int BeforeValue;
        public readonly int AfterValue;
        public readonly bool PersistsToNextDay;

        public StatusDeltaEntry(string statusId, string displayName, int beforeValue, int afterValue, bool persistsToNextDay)
        {
            StatusId = statusId ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? StatusId : displayName;
            BeforeValue = Math.Max(0, beforeValue);
            AfterValue = Math.Max(0, afterValue);
            PersistsToNextDay = persistsToNextDay;
        }

        public int Delta => AfterValue - BeforeValue;
    }

    public readonly struct StudentDayRelationshipConditionSummary
    {
        public readonly RelationshipDeltaEntry[] RelationshipEntries;
        public readonly StatusDeltaEntry[] StatusEntries;
        public readonly string[] NextDayImpacts;

        public StudentDayRelationshipConditionSummary(RelationshipDeltaEntry[] relationshipEntries, StatusDeltaEntry[] statusEntries, string[] nextDayImpacts)
        {
            RelationshipEntries = relationshipEntries ?? Array.Empty<RelationshipDeltaEntry>();
            StatusEntries = statusEntries ?? Array.Empty<StatusDeltaEntry>();
            NextDayImpacts = nextDayImpacts ?? Array.Empty<string>();
        }
    }

    /// <summary>하루 결과의 관계·상태 변화와 내일에 미치는 영향 문장(원본과 같은 영어 문장).</summary>
    public static class StudentDayRelationshipConditionSummaryBuilder
    {
        /// <summary>전·후 진행 상태를 정의 목록 순서로 비교한다.</summary>
        public static StudentDayRelationshipConditionSummary Build(StudentLifeProgress before, StudentLifeProgress after, IReadOnlyList<RelationshipSummaryInfo> relationships, IReadOnlyList<StatusSummaryInfo> statuses)
        {
            var relationshipEntries = new List<RelationshipDeltaEntry>();
            var statusEntries = new List<StatusDeltaEntry>();
            var impacts = new List<string>();
            if (after != null && relationships != null)
            {
                for (int i = 0; i < relationships.Count; i++)
                {
                    var relationship = relationships[i];
                    if (relationship.Id.Length == 0) continue;
                    int beforeValue = before != null ? before.GetRelationshipValue(relationship.Id) : 0;
                    int afterValue = after.GetRelationshipValue(relationship.Id);
                    if (beforeValue != afterValue) AddRelationship(relationshipEntries, impacts, relationship.Id, relationship.DisplayNameKey, beforeValue, afterValue);
                }
            }
            if (after != null && statuses != null)
            {
                for (int i = 0; i < statuses.Count; i++)
                {
                    var status = statuses[i];
                    if (status.Id.Length == 0) continue;
                    int beforeValue = before != null ? before.GetStatusValue(status.Id) : 0;
                    int afterValue = after.GetStatusValue(status.Id);
                    if (beforeValue != afterValue) AddStatus(statusEntries, impacts, status.Id, status.DisplayNameKey, status.PersistsToNextDay, beforeValue, afterValue);
                }
            }
            return new StudentDayRelationshipConditionSummary(relationshipEntries.ToArray(), statusEntries.ToArray(), impacts.ToArray());
        }

        /// <summary>
        /// 결과 기록에서 "relationship.&lt;id&gt;=&lt;변화&gt;", "status.&lt;id&gt;=&lt;변화&gt;"를 찾아 지금 값과 그 전 값을 만든다.
        /// 양수 기록("+relationship.…")과 음수 기록('+' 없음)을 모두 읽는다(원본은 '+'가 붙은 것만 읽어 음수 변화가 빠졌다). 정의 목록에 없는 id는 id를 이름으로, 상태는 내일까지 이어지는 것으로 본다.
        /// </summary>
        public static StudentDayRelationshipConditionSummary BuildFromResultLogs(StudentLifeProgress progress, string[] resultLogIds, IReadOnlyList<RelationshipSummaryInfo> relationships, IReadOnlyList<StatusSummaryInfo> statuses)
        {
            if (progress == null || resultLogIds == null) return new StudentDayRelationshipConditionSummary(null, null, null);
            var relationshipEntries = new List<RelationshipDeltaEntry>();
            var statusEntries = new List<StatusDeltaEntry>();
            var impacts = new List<string>();
            for (int i = 0; i < resultLogIds.Length; i++)
            {
                string log = resultLogIds[i];
                if (string.IsNullOrEmpty(log)) continue;
                if (StudentResultLogDeltas.TryParse(log, "relationship.", out string relationshipId, out int relationshipDelta))
                {
                    int afterValue = progress.GetRelationshipValue(relationshipId);
                    string display = TryFind(relationships, relationshipId, out var relationship) ? relationship.DisplayNameKey : relationshipId;
                    AddRelationship(relationshipEntries, impacts, relationshipId, display, afterValue - relationshipDelta, afterValue);
                }
                else if (StudentResultLogDeltas.TryParse(log, "status.", out string statusId, out int statusDelta))
                {
                    int afterValue = progress.GetStatusValue(statusId);
                    bool found = TryFind(statuses, statusId, out var status);
                    AddStatus(statusEntries, impacts, statusId, found ? status.DisplayNameKey : statusId, !found || status.PersistsToNextDay, afterValue - statusDelta, afterValue);
                }
            }
            return new StudentDayRelationshipConditionSummary(relationshipEntries.ToArray(), statusEntries.ToArray(), impacts.ToArray());
        }

        private static void AddRelationship(List<RelationshipDeltaEntry> entries, List<string> impacts, string id, string display, int beforeValue, int afterValue)
        {
            entries.Add(new RelationshipDeltaEntry(id, display, beforeValue, afterValue));
            impacts.Add(display + " affects tomorrow's NPC response.");
        }

        private static void AddStatus(List<StatusDeltaEntry> entries, List<string> impacts, string id, string display, bool persists, int beforeValue, int afterValue)
        {
            entries.Add(new StatusDeltaEntry(id, display, beforeValue, afterValue, persists));
            impacts.Add(display + (persists ? " carries into tomorrow." : " recovers before tomorrow."));
        }

        private static bool TryFind(IReadOnlyList<RelationshipSummaryInfo> values, string id, out RelationshipSummaryInfo found)
        {
            found = default;
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++) if (values[i].Id.Length > 0 && values[i].Id == id) { found = values[i]; return true; }
            return false;
        }

        private static bool TryFind(IReadOnlyList<StatusSummaryInfo> values, string id, out StatusSummaryInfo found)
        {
            found = default;
            if (values == null) return false;
            for (int i = 0; i < values.Count; i++) if (values[i].Id.Length > 0 && values[i].Id == id) { found = values[i]; return true; }
            return false;
        }
    }

    /// <summary>결과 기록 "…:[+]&lt;종류&gt;.&lt;id&gt;=&lt;변화&gt;" 읽기. 종류 앞은 기록의 처음이거나 ':' 또는 '+'여야 한다.</summary>
    public static class StudentResultLogDeltas
    {
        public static bool Has(string log, string kindPrefix) => TryParse(log, kindPrefix, out _, out _);

        /// <summary>id는 종류를 포함한다("relationship.first-guide.trust").</summary>
        public static bool TryParse(string log, string kindPrefix, out string id, out int delta)
        {
            id = string.Empty;
            delta = 0;
            if (string.IsNullOrEmpty(log) || string.IsNullOrEmpty(kindPrefix)) return false;
            int from = 0;
            while (true)
            {
                int idStart = log.IndexOf(kindPrefix, from, StringComparison.Ordinal);
                if (idStart < 0) return false;
                from = idStart + 1;
                if (idStart > 0 && log[idStart - 1] != ':' && log[idStart - 1] != '+') continue;
                int equals = log.IndexOf('=', idStart);
                if (equals <= idStart) return false;
                if (!int.TryParse(log.Substring(equals + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out delta)) return false;
                id = log.Substring(idStart, equals - idStart);
                return true;
            }
        }
    }
}
