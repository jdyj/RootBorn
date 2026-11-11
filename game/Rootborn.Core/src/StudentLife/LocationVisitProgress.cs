using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
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
            EntityId = entityId ?? string.Empty;
            RequestId = requestId ?? string.Empty;
        }
    }

    /// <summary>
    /// 장소 첫 방문과 NPC 첫 만남. 진행 상태의 적용 요청("location-visit:&lt;id&gt;", "npc-meet:&lt;id&gt;")으로 한 번만 기록하고,
    /// 활동 기록에 id를 남겨 도감·해금이 읽게 한다. 따로 저장할 것은 없다(진행 상태에 들어간다).
    /// </summary>
    public sealed class LocationVisitProgress
    {
        private const string LocationRequestPrefix = "location-visit:";
        private const string NpcRequestPrefix = "npc-meet:";
        private readonly StudentLifeProgress _progress;

        public LocationVisitProgress(StudentLifeProgress progress) { _progress = progress; }

        /// <summary>활동 기록 중 "location."으로 시작하고 '@'가 없는 id(중복 없이).</summary>
        public string[] VisitedLocationIds => FilterActivityIds("location.");

        /// <summary>활동 기록 중 "npc."로 시작하고 '@'가 없는 id(중복 없이).</summary>
        public string[] MetNpcIds => FilterActivityIds("npc.");

        public bool HasVisited(string locationId) => _progress != null && !string.IsNullOrEmpty(locationId) && _progress.HasAppliedRequest(LocationRequestPrefix + locationId);
        public bool HasMet(string npcId) => _progress != null && !string.IsNullOrEmpty(npcId) && _progress.HasAppliedRequest(NpcRequestPrefix + npcId);

        public bool TryRecordLocationVisit(string locationId, out LocationVisitResult result)
            => TryRecord(locationId, LocationRequestPrefix, "location:", ":visited", LocationVisitResultKind.FirstVisit, LocationVisitResultKind.DuplicateVisit, out result);

        public bool TryRecordNpcMeeting(string npcId, out LocationVisitResult result)
            => TryRecord(npcId, NpcRequestPrefix, "npc:", ":met", LocationVisitResultKind.FirstMeeting, LocationVisitResultKind.DuplicateMeeting, out result);

        private bool TryRecord(string id, string requestPrefix, string logPrefix, string logSuffix, LocationVisitResultKind first, LocationVisitResultKind duplicate, out LocationVisitResult result)
        {
            id = id ?? string.Empty;
            string requestId = requestPrefix + id;
            result = new LocationVisitResult(LocationVisitResultKind.InvalidRequest, _progress, id, requestId);
            if (_progress == null || id.Length == 0) return false;
            if (!_progress.MarkRequestApplied(requestId))
            {
                result = new LocationVisitResult(duplicate, _progress, id, requestId);
                return false;
            }
            _progress.RecordActivityCompleted(id, new[] { logPrefix + id + logSuffix });
            result = new LocationVisitResult(first, _progress, id, requestId);
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
                if (!string.IsNullOrEmpty(id) && id.StartsWith(prefix, StringComparison.Ordinal) && id.IndexOf('@') < 0 && !result.Contains(id)) result.Add(id);
            }
            return result.ToArray();
        }
    }
}
