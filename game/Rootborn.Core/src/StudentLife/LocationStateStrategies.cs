using System;
using Rootborn.Core.Quests;

namespace Rootborn.Core.StudentLife
{
    /// <summary>시간대 중 하나일 때. 시간대가 비어 있으면 늘 만족한다.</summary>
    public sealed class LocationStateTimeSlotCondition : ILocationStateCondition
    {
        private readonly string[] _timeSlotIds;

        public LocationStateTimeSlotCondition(string[] timeSlotIds) { _timeSlotIds = timeSlotIds ?? Array.Empty<string>(); }

        public bool IsSatisfied(in LocationStateContext context)
        {
            if (_timeSlotIds.Length == 0) return true;
            string currentId = context.ResolveTimeSlotId();
            if (string.IsNullOrEmpty(currentId)) return false;
            for (int i = 0; i < _timeSlotIds.Length; i++) if (!string.IsNullOrEmpty(_timeSlotIds[i]) && _timeSlotIds[i] == currentId) return true;
            return false;
        }
    }

    /// <summary>세계 상태 깃발이 기대한 값일 때. 조회가 없으면 꺼진 것으로 본다.</summary>
    public sealed class LocationStateWorldStateCondition : ILocationStateCondition
    {
        private readonly string _flagId;
        private readonly bool _expectedActive;

        public LocationStateWorldStateCondition(string flagId, bool expectedActive)
        {
            _flagId = flagId ?? string.Empty;
            _expectedActive = expectedActive;
        }

        public bool IsSatisfied(in LocationStateContext context)
        {
            bool active = context.WorldFlags != null && _flagId.Length > 0 && context.WorldFlags.IsActive(_flagId);
            return active == _expectedActive;
        }
    }

    public sealed class LocationStateRelationshipCondition : ILocationStateCondition
    {
        private readonly string _relationshipId;
        private readonly int _minimumValue;

        public LocationStateRelationshipCondition(string relationshipId, int minimumValue)
        {
            _relationshipId = relationshipId ?? string.Empty;
            _minimumValue = minimumValue;
        }

        public bool IsSatisfied(in LocationStateContext context) => context.StudentProgress != null && context.StudentProgress.GetRelationshipValue(_relationshipId) >= Math.Max(0, _minimumValue);
    }

    public sealed class LocationStateStatusCondition : ILocationStateCondition
    {
        private readonly string _statusId;
        private readonly int _minimumValue;

        public LocationStateStatusCondition(string statusId, int minimumValue)
        {
            _statusId = statusId ?? string.Empty;
            _minimumValue = minimumValue;
        }

        public bool IsSatisfied(in LocationStateContext context) => context.StudentProgress != null && context.StudentProgress.GetStatusValue(_statusId) >= Math.Max(0, _minimumValue);
    }

    /// <summary>퀘스트가 정확히 그 단계일 때. 퀘스트 기록이 없으면 만족하지 않는다.</summary>
    public sealed class LocationStateQuestStateCondition : ILocationStateCondition
    {
        private readonly string _questId;
        private readonly QuestStage _requiredStage;

        public LocationStateQuestStateCondition(string questId, QuestStage requiredStage)
        {
            _questId = questId ?? string.Empty;
            _requiredStage = requiredStage;
        }

        public bool IsSatisfied(in LocationStateContext context) => context.QuestLog != null && _questId.Length > 0 && context.QuestLog.GetState(_questId) == _requiredStage;
    }

    /// <summary>활동 기록에 그 활동이 최소 횟수(1 이상) 있을 때.</summary>
    public sealed class LocationStateActivityLogCondition : ILocationStateCondition
    {
        private readonly string _requiredActivityId;
        private readonly int _minimumCount;

        public LocationStateActivityLogCondition(string requiredActivityId, int minimumCount)
        {
            _requiredActivityId = requiredActivityId ?? string.Empty;
            _minimumCount = minimumCount;
        }

        public bool IsSatisfied(in LocationStateContext context)
        {
            if (context.StudentProgress == null || _requiredActivityId.Length == 0) return false;
            string[] ids = context.StudentProgress.GetActivityLogIds();
            int count = 0;
            for (int i = 0; i < ids.Length; i++) if (ids[i] == _requiredActivityId) count++;
            return count >= Math.Max(1, _minimumCount);
        }
    }

    /// <summary>NPC 일정상 그 시간에 NPC가 정한 장소(비면 지금 장소)에 있을 때.</summary>
    public sealed class LocationStateNpcScheduleCondition : ILocationStateCondition
    {
        private readonly string _scheduleId;
        private readonly string _requiredLocationId;

        public LocationStateNpcScheduleCondition(string scheduleId, string requiredLocationId)
        {
            _scheduleId = scheduleId ?? string.Empty;
            _requiredLocationId = requiredLocationId ?? string.Empty;
        }

        public bool IsSatisfied(in LocationStateContext context)
        {
            if (context.NpcSchedules == null || _scheduleId.Length == 0) return false;
            int day = context.StudentProgress != null ? context.StudentProgress.CurrentDay : 1;
            string resolved = context.NpcSchedules.ResolveLocationId(_scheduleId, day, context.ResolveTimeSlotId(), context.StudentProgress);
            string required = _requiredLocationId.Length > 0 ? _requiredLocationId : context.LocationId;
            return !string.IsNullOrEmpty(required) && !string.IsNullOrEmpty(resolved) && resolved == required;
        }
    }

    /// <summary>장소에 활동·NPC·단서·물건·출입구·상점 물건을 보이게 한다(모두 id).</summary>
    public sealed class LocationStateAvailabilityEffect : ILocationStateEffect
    {
        private readonly string[] _activityIds;
        private readonly string[] _npcIds;
        private readonly string[] _clueIds;
        private readonly string[] _interactableObjectIds;
        private readonly string[] _portalIds;
        private readonly string[] _shopItemIds;

        public LocationStateAvailabilityEffect(string[] activityIds, string[] npcIds, string[] clueIds, string[] interactableObjectIds, string[] portalIds, string[] shopItemIds)
        {
            _activityIds = activityIds ?? Array.Empty<string>();
            _npcIds = npcIds ?? Array.Empty<string>();
            _clueIds = clueIds ?? Array.Empty<string>();
            _interactableObjectIds = interactableObjectIds ?? Array.Empty<string>();
            _portalIds = portalIds ?? Array.Empty<string>();
            _shopItemIds = shopItemIds ?? Array.Empty<string>();
        }

        public void AppendTo(LocationStateSummaryBuilder builder)
        {
            if (builder == null) return;
            builder.AddActivities(_activityIds);
            builder.AddNpcs(_npcIds);
            builder.AddClues(_clueIds);
            builder.AddInteractableObjects(_interactableObjectIds);
            builder.AddPortals(_portalIds);
            builder.AddShopItems(_shopItemIds);
        }
    }
}
