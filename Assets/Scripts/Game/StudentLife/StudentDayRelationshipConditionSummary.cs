using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [Serializable]
    public readonly struct RelationshipDeltaEntry
    {
        public readonly string RelationshipId;
        public readonly string DisplayName;
        public readonly int BeforeValue;
        public readonly int AfterValue;
        public int Delta => AfterValue - BeforeValue;

        public RelationshipDeltaEntry(string relationshipId, string displayName, int beforeValue, int afterValue)
        {
            RelationshipId = string.IsNullOrEmpty(relationshipId) ? string.Empty : relationshipId;
            DisplayName = string.IsNullOrEmpty(displayName) ? RelationshipId : displayName;
            BeforeValue = Mathf.Max(0, beforeValue);
            AfterValue = Mathf.Max(0, afterValue);
        }
    }

    [Serializable]
    public readonly struct StatusDeltaEntry
    {
        public readonly string StatusId;
        public readonly string DisplayName;
        public readonly int BeforeValue;
        public readonly int AfterValue;
        public readonly bool PersistsToNextDay;
        public int Delta => AfterValue - BeforeValue;

        public StatusDeltaEntry(string statusId, string displayName, int beforeValue, int afterValue, bool persistsToNextDay)
        {
            StatusId = string.IsNullOrEmpty(statusId) ? string.Empty : statusId;
            DisplayName = string.IsNullOrEmpty(displayName) ? StatusId : displayName;
            BeforeValue = Mathf.Max(0, beforeValue);
            AfterValue = Mathf.Max(0, afterValue);
            PersistsToNextDay = persistsToNextDay;
        }
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

    public static class StudentDayRelationshipConditionSummaryBuilder
    {
        public static StudentDayRelationshipConditionSummary Build(StudentLifeProgress before, StudentLifeProgress after, IReadOnlyList<RelationshipDefinition> relationships, IReadOnlyList<StatusDefinition> statuses)
        {
            var relationshipEntries = new List<RelationshipDeltaEntry>();
            var statusEntries = new List<StatusDeltaEntry>();
            var impacts = new List<string>();

            if (after != null && relationships != null)
            {
                for (int i = 0; i < relationships.Count; i++)
                {
                    var relationship = relationships[i];
                    if (relationship == null) continue;
                    int beforeValue = before != null ? before.GetRelationshipValue(relationship) : 0;
                    int afterValue = after.GetRelationshipValue(relationship);
                    if (beforeValue != afterValue) AddRelationshipEntry(relationshipEntries, impacts, relationship, beforeValue, afterValue);
                }
            }

            if (after != null && statuses != null)
            {
                for (int i = 0; i < statuses.Count; i++)
                {
                    var status = statuses[i];
                    if (status == null) continue;
                    int beforeValue = before != null ? before.GetStatusValue(status) : 0;
                    int afterValue = after.GetStatusValue(status);
                    if (beforeValue != afterValue) AddStatusEntry(statusEntries, impacts, status, beforeValue, afterValue);
                }
            }

            return new StudentDayRelationshipConditionSummary(relationshipEntries.ToArray(), statusEntries.ToArray(), impacts.ToArray());
        }

        public static StudentDayRelationshipConditionSummary BuildFromResultLogs(StudentLifeProgress progress, string[] resultLogIds, IReadOnlyList<RelationshipDefinition> relationships, IReadOnlyList<StatusDefinition> statuses)
        {
            var relationshipEntries = new List<RelationshipDeltaEntry>();
            var statusEntries = new List<StatusDeltaEntry>();
            var impacts = new List<string>();
            if (progress == null || resultLogIds == null) return new StudentDayRelationshipConditionSummary(null, null, null);

            for (int i = 0; i < resultLogIds.Length; i++)
            {
                string log = resultLogIds[i];
                if (string.IsNullOrEmpty(log)) continue;
                if (TryParseDelta(log, "+relationship.", out string relationshipId, out int relationshipDelta))
                {
                    var relationship = FindRelationship(relationships, relationshipId);
                    int afterValue = relationship != null ? progress.GetRelationshipValue(relationship) : progress.GetRelationshipValueById(relationshipId);
                    AddRelationshipEntry(relationshipEntries, impacts, relationship, relationshipId, afterValue - relationshipDelta, afterValue);
                }
                else if (TryParseDelta(log, "+status.", out string statusId, out int statusDelta))
                {
                    var status = FindStatus(statuses, statusId);
                    int afterValue = status != null ? progress.GetStatusValue(status) : progress.GetStatusValueById(statusId);
                    AddStatusEntry(statusEntries, impacts, status, statusId, afterValue - statusDelta, afterValue);
                }
            }

            return new StudentDayRelationshipConditionSummary(relationshipEntries.ToArray(), statusEntries.ToArray(), impacts.ToArray());
        }

        private static void AddRelationshipEntry(List<RelationshipDeltaEntry> entries, List<string> impacts, RelationshipDefinition relationship, int beforeValue, int afterValue)
        {
            AddRelationshipEntry(entries, impacts, relationship, relationship != null ? relationship.Id : string.Empty, beforeValue, afterValue);
        }

        private static void AddRelationshipEntry(List<RelationshipDeltaEntry> entries, List<string> impacts, RelationshipDefinition relationship, string relationshipId, int beforeValue, int afterValue)
        {
            string display = relationship != null ? relationship.DisplayNameKey : relationshipId;
            entries.Add(new RelationshipDeltaEntry(relationshipId, display, beforeValue, afterValue));
            impacts.Add(display + " affects tomorrow's NPC response.");
        }

        private static void AddStatusEntry(List<StatusDeltaEntry> entries, List<string> impacts, StatusDefinition status, int beforeValue, int afterValue)
        {
            AddStatusEntry(entries, impacts, status, status != null ? status.Id : string.Empty, beforeValue, afterValue);
        }

        private static void AddStatusEntry(List<StatusDeltaEntry> entries, List<string> impacts, StatusDefinition status, string statusId, int beforeValue, int afterValue)
        {
            string display = status != null ? status.DisplayNameKey : statusId;
            bool persists = status == null || status.PersistsToNextDay;
            entries.Add(new StatusDeltaEntry(statusId, display, beforeValue, afterValue, persists));
            impacts.Add(display + (persists ? " carries into tomorrow." : " recovers before tomorrow."));
        }

        private static RelationshipDefinition FindRelationship(IReadOnlyList<RelationshipDefinition> definitions, string id)
        {
            if (definitions == null) return null;
            for (int i = 0; i < definitions.Count; i++) if (definitions[i] != null && definitions[i].Id == id) return definitions[i];
            return null;
        }

        private static StatusDefinition FindStatus(IReadOnlyList<StatusDefinition> definitions, string id)
        {
            if (definitions == null) return null;
            for (int i = 0; i < definitions.Count; i++) if (definitions[i] != null && definitions[i].Id == id) return definitions[i];
            return null;
        }

        private static bool TryParseDelta(string log, string marker, out string id, out int delta)
        {
            id = string.Empty;
            delta = 0;
            int markerIndex = log.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0) return false;
            int idStart = markerIndex + 1;
            int equals = log.IndexOf('=', idStart);
            if (equals <= idStart) return false;
            id = log.Substring(idStart, equals - idStart);
            return int.TryParse(log.Substring(equals + 1), out delta);
        }
    }
}
