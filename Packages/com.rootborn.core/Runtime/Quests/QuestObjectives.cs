using System;
using System.Collections.Generic;

namespace Rootborn.Core.Quests
{
    /// <summary>One thing a quest or quest step asks for. What it accepts is a <see cref="QuestObjectiveSpec"/> by id.</summary>
    public abstract class QuestObjective
    {
        private readonly QuestObjectiveSpec _spec;

        protected QuestObjective(string displayKey, int requiredCount, QuestObjectiveSpec spec = null)
        {
            DisplayKey = displayKey ?? string.Empty;
            RequiredCount = Math.Max(1, requiredCount);
            _spec = spec;
        }

        public string DisplayKey { get; }
        public int RequiredCount { get; }

        public virtual bool Matches(in QuestEvent questEvent) => QuestObjectiveMatcher.Matches(_spec, questEvent.ToFacts());

        /// <summary>The event's count when it matches. An event with no positive count credits nothing (the game counted it as 1).</summary>
        public virtual int GetDelta(in QuestEvent questEvent) => questEvent.Count > 0 && Matches(in questEvent) ? questEvent.Count : 0;

        internal static string[] Ids(IReadOnlyList<string> ids) => ids == null ? Array.Empty<string>() : new List<string>(ids).ToArray();
    }

    public sealed class GatherQuestObjective : QuestObjective
    {
        public GatherQuestObjective(string displayKey, int requiredCount, string resourceId)
            : base(displayKey, requiredCount, QuestObjectiveSpec.Listed((int)QuestEventKind.Gather, resourceId)) { TargetResourceId = resourceId; }
        public string TargetResourceId { get; }
    }

    public sealed class DefeatQuestObjective : QuestObjective
    {
        public DefeatQuestObjective(string displayKey, int requiredCount, string targetId)
            : base(displayKey, requiredCount, QuestObjectiveSpec.Listed((int)QuestEventKind.Defeat, targetId)) { TargetId = targetId; }
        public string TargetId { get; }
    }

    public sealed class CollectQuestObjective : QuestObjective
    {
        public CollectQuestObjective(string displayKey, int requiredCount, string itemId)
            : base(displayKey, requiredCount, QuestObjectiveSpec.Listed((int)QuestEventKind.Collect, itemId)) { TargetItemId = itemId; }
        public string TargetItemId { get; }
    }

    public sealed class TalkQuestObjective : QuestObjective
    {
        public TalkQuestObjective(string displayKey, int requiredCount, string npcId)
            : base(displayKey, requiredCount, QuestObjectiveSpec.Listed((int)QuestEventKind.Talk, npcId)) { TargetNpcId = npcId; }
        public string TargetNpcId { get; }
    }

    public sealed class LocationVisitQuestObjective : QuestObjective
    {
        public LocationVisitQuestObjective(string displayKey, int requiredCount, string locationId)
            : base(displayKey, requiredCount, QuestObjectiveSpec.Listed((int)QuestEventKind.LocationVisit, locationId)) { TargetLocationId = locationId; }
        public string TargetLocationId { get; }
    }

    /// <summary>No target (null) accepts any location activity; an empty id accepts nothing.</summary>
    public sealed class LocationActivityQuestObjective : QuestObjective
    {
        public LocationActivityQuestObjective(string displayKey, int requiredCount, string activityId)
            : base(displayKey, requiredCount, activityId == null
                ? QuestObjectiveSpec.AnyEvent((int)QuestEventKind.LocationActivity)
                : QuestObjectiveSpec.Listed((int)QuestEventKind.LocationActivity, activityId)) { TargetActivityId = activityId; }
        public string TargetActivityId { get; }
    }

    /// <summary>No target (null) accepts any placed tile; an empty id accepts nothing.</summary>
    public sealed class TilePlacedQuestObjective : QuestObjective
    {
        public TilePlacedQuestObjective(string displayKey, int requiredCount, string tileId)
            : base(displayKey, requiredCount, tileId == null
                ? QuestObjectiveSpec.AnyEvent((int)QuestEventKind.TilePlaced)
                : QuestObjectiveSpec.Listed((int)QuestEventKind.TilePlaced, tileId)) { TargetTileId = tileId; }
        public string TargetTileId { get; }
    }

    public sealed class PartTimeWorkQuestObjective : QuestObjective
    {
        private readonly string[] _acceptedWorkIds;

        public PartTimeWorkQuestObjective(string displayKey, int requiredCount, IReadOnlyList<string> acceptedWorkIds)
            : this(displayKey, requiredCount, Ids(acceptedWorkIds)) { }

        private PartTimeWorkQuestObjective(string displayKey, int requiredCount, string[] ids)
            : base(displayKey, requiredCount, QuestObjectiveSpec.Listed((int)QuestEventKind.PartTimeWork, ids)) { _acceptedWorkIds = ids; }

        public IReadOnlyList<string> AcceptedWorkIds => _acceptedWorkIds;
    }

    public sealed class FurniturePlacedQuestObjective : QuestObjective
    {
        private readonly string[] _acceptedFurnitureIds;

        public FurniturePlacedQuestObjective(string displayKey, int requiredCount, IReadOnlyList<string> acceptedFurnitureIds, string requiredSurfaceId)
            : this(displayKey, requiredCount, Ids(acceptedFurnitureIds), requiredSurfaceId) { }

        private FurniturePlacedQuestObjective(string displayKey, int requiredCount, string[] ids, string requiredSurfaceId)
            : base(displayKey, requiredCount, QuestObjectiveSpec.Listed((int)QuestEventKind.FurniturePlaced, ids).WithRequiredSurface(requiredSurfaceId))
        {
            _acceptedFurnitureIds = ids;
            RequiredSurfaceId = requiredSurfaceId;
        }

        public IReadOnlyList<string> AcceptedFurnitureIds => _acceptedFurnitureIds;
        public string RequiredSurfaceId { get; }
    }

    /// <summary>Any successful daily plan selection that names a route.</summary>
    public sealed class DailyPlanSelectedQuestObjective : QuestObjective
    {
        public DailyPlanSelectedQuestObjective(string displayKey, int requiredCount)
            : base(displayKey, requiredCount, QuestObjectiveSpec.AnySubject((int)QuestEventKind.DailyPlanSelected)) { }
    }

    /// <summary>Empty accepts any dream, so the player's choice is never limited by this objective.</summary>
    public sealed class CareerInterestSelectedQuestObjective : QuestObjective
    {
        private readonly string[] _acceptedInterestIds;

        public CareerInterestSelectedQuestObjective(string displayKey, int requiredCount, IReadOnlyList<string> acceptedInterestIds)
            : this(displayKey, requiredCount, Ids(acceptedInterestIds)) { }

        private CareerInterestSelectedQuestObjective(string displayKey, int requiredCount, string[] ids)
            : base(displayKey, requiredCount, ids.Length == 0
                ? QuestObjectiveSpec.AnySubject((int)QuestEventKind.CareerInterestSelected)
                : QuestObjectiveSpec.Listed((int)QuestEventKind.CareerInterestSelected, ids)) { _acceptedInterestIds = ids; }

        public IReadOnlyList<string> AcceptedInterestIds => _acceptedInterestIds;
    }

    /// <summary>Matches when any alternative does; one event still counts once.</summary>
    public sealed class AnyOfQuestObjective : QuestObjective
    {
        private readonly QuestObjective[] _alternatives;

        public AnyOfQuestObjective(string displayKey, int requiredCount, IReadOnlyList<QuestObjective> alternatives)
            : base(displayKey, requiredCount)
        {
            _alternatives = alternatives == null ? Array.Empty<QuestObjective>() : new List<QuestObjective>(alternatives).ToArray();
        }

        public IReadOnlyList<QuestObjective> Alternatives => _alternatives;

        // Alternatives are fixed at construction, so an objective cannot contain itself (the game guarded against that cycle).
        public override bool Matches(in QuestEvent questEvent)
        {
            for (int i = 0; i < _alternatives.Length; i++)
                if (_alternatives[i] != null && _alternatives[i].Matches(questEvent)) return true;
            return false;
        }
    }
}
