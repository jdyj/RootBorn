using System;
using System.Collections.Generic;
using Rootborn.Game.Dialogue;

namespace Rootborn.Game.StudentLife
{
    public enum LocationVisitResultKind
    {
        FirstVisit,
        DuplicateVisit,
        FirstMeeting,
        DuplicateMeeting,
        InvalidRequest
    }

    public readonly struct LocationVisitResult
    {
        public readonly LocationVisitResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string EntityId;
        public readonly string RequestId;

        public LocationVisitResult(LocationVisitResultKind kind, StudentLifeProgress progress, string entityId, string requestId)
        {
            Kind = kind;
            SaveSlot = progress == null ? "default" : progress.SaveSlot;
            PlayerId = progress == null ? "player" : progress.PlayerId;
            EntityId = string.IsNullOrEmpty(entityId) ? string.Empty : entityId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
        }
    }

    public sealed class LocationVisitProgress
    {
        private const string LocationRequestPrefix = "location-visit:";
        private const string NpcRequestPrefix = "npc-meet:";
        private readonly StudentLifeProgress _progress;

        public LocationVisitProgress(StudentLifeProgress progress)
        {
            _progress = progress;
        }

        public string[] VisitedLocationIds => FilterActivityIds("location.");
        public string[] MetNpcIds => FilterActivityIds("npc.");

        public bool HasVisited(LocationDefinition location)
        {
            return _progress != null && location != null && _progress.HasAppliedRequest(LocationRequestPrefix + location.Id);
        }

        public bool HasMet(NpcDefinition npc)
        {
            return _progress != null && npc != null && _progress.HasAppliedRequest(NpcRequestPrefix + npc.Id);
        }

        public bool TryRecordLocationVisit(LocationDefinition location, out LocationVisitResult result)
        {
            string id = location == null ? string.Empty : location.Id;
            string requestId = LocationRequestPrefix + id;
            result = new LocationVisitResult(LocationVisitResultKind.InvalidRequest, _progress, id, requestId);
            if (_progress == null || location == null || string.IsNullOrEmpty(id)) return false;
            if (!_progress.MarkRequestApplied(requestId))
            {
                result = new LocationVisitResult(LocationVisitResultKind.DuplicateVisit, _progress, id, requestId);
                return false;
            }

            _progress.RecordActivityCompleted(id, new[] { "location:" + id + ":visited" });
            result = new LocationVisitResult(LocationVisitResultKind.FirstVisit, _progress, id, requestId);
            return true;
        }

        public bool TryRecordNpcMeeting(NpcDefinition npc, out LocationVisitResult result)
        {
            string id = npc == null ? string.Empty : npc.Id;
            string requestId = NpcRequestPrefix + id;
            result = new LocationVisitResult(LocationVisitResultKind.InvalidRequest, _progress, id, requestId);
            if (_progress == null || npc == null || string.IsNullOrEmpty(id)) return false;
            if (!_progress.MarkRequestApplied(requestId))
            {
                result = new LocationVisitResult(LocationVisitResultKind.DuplicateMeeting, _progress, id, requestId);
                return false;
            }

            _progress.RecordActivityCompleted(id, new[] { "npc:" + id + ":met" });
            result = new LocationVisitResult(LocationVisitResultKind.FirstMeeting, _progress, id, requestId);
            return true;
        }

        private string[] FilterActivityIds(string prefix)
        {
            if (_progress == null) return Array.Empty<string>();
            string[] ids = _progress.GetActivityLogIds();
            var result = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (!string.IsNullOrEmpty(id) && id.StartsWith(prefix, StringComparison.Ordinal) && id.IndexOf('@') < 0 && !result.Contains(id))
                {
                    result.Add(id);
                }
            }

            return result.ToArray();
        }
    }
}
