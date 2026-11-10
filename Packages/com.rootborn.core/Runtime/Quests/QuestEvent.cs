using System;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Quests
{
    /// <summary>Same names and order as the game's QuestEventKind; the number is what objectives compare.</summary>
    public enum QuestEventKind
    {
        Gather,
        Defeat,
        Collect,
        Harvest,
        Talk,
        TilePlaced,
        LocationActivity,
        LocationVisit,
        PartTimeWork,
        FurniturePlaced,
        DailyPlanSelected,
        CareerInterestSelected,
        DayStarted
    }

    /// <summary>
    /// Something the player did, by ids. <see cref="SubjectId"/> is the id of what it happened to for this kind
    /// (resource, item, NPC, defeat target, tile, activity, location, work, furniture, daily plan route or dream).
    /// </summary>
    public readonly struct QuestEvent
    {
        public readonly QuestEventKind Kind;
        public readonly string EventKey;
        public readonly string SubjectId;
        public readonly int Count;
        public readonly string SurfaceId;
        /// <summary>True when an already committed action is announced again (e.g. furniture already in the room on entry).
        /// Quest chains may use it to catch up; the player's quest log does not count it again.</summary>
        public readonly bool IsReplay;

        public QuestEvent(QuestEventKind kind, string eventKey, string subjectId = null, int count = 1, string surfaceId = null, bool isReplay = false)
        {
            Kind = kind;
            EventKey = eventKey;
            SubjectId = subjectId;
            Count = count;
            SurfaceId = surfaceId ?? string.Empty;
            IsReplay = isReplay;
        }

        public QuestEventFacts ToFacts() => new QuestEventFacts((int)Kind, SubjectId, Count, SurfaceId);
    }

    public interface IQuestEventSink
    {
        void Record(in QuestEvent questEvent);
    }

    /// <summary>Publishes committed player actions. Restore and replay operations do not publish here.</summary>
    public sealed class QuestGameplayEventBridge
    {
        public event Action<QuestEvent, StudentLifeProgress> OnRecorded;

        public static string CreateRequestKey(string action) => action + ":" + Guid.NewGuid().ToString("N");

        public void Record(QuestEvent questEvent, StudentLifeProgress progress)
        {
            if (progress == null || string.IsNullOrEmpty(questEvent.EventKey) || questEvent.Count <= 0) return;
            OnRecorded?.Invoke(questEvent, progress);
        }
    }
}
