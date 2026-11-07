using Rootborn.Core.Quests;

namespace Rootborn.Game.Quests
{
    /// <summary>Game-facing wrapper; the progress rules live in Rootborn.Core (QuestTracker) and are tested with dotnet test.</summary>
    public sealed class QuestProgress
    {
        private readonly QuestTracker _tracker;

        public QuestProgress(int objectiveCount)
        {
            _tracker = new QuestTracker(objectiveCount);
        }

        public QuestState State => (QuestState)_tracker.Stage;
        public int ObjectiveCount => _tracker.ObjectiveCount;

        public bool TryAccept() => _tracker.TryAccept();
        public int GetObjectiveCount(int objectiveIndex) => _tracker.GetObjectiveCount(objectiveIndex);

        public bool TryAddObjectiveCount(int objectiveIndex, int delta, int requiredCount, string eventKey)
            => _tracker.TryAddObjectiveCount(objectiveIndex, delta, requiredCount, eventKey);

        public bool TryMarkRewardClaimed() => _tracker.TryMarkRewardClaimed();

        public QuestProgressSaveData ToSaveData(string questId)
        {
            var snapshot = _tracker.ToSnapshot();
            var state = (QuestState)snapshot.Stage;
            return new QuestProgressSaveData
            {
                QuestId = questId,
                State = state,
                StateName = state.ToString(),
                ObjectiveCounts = snapshot.ObjectiveCounts,
                ProcessedEventKeys = snapshot.ProcessedEventKeys,
            };
        }

        public void LoadFromSaveData(QuestProgressSaveData data)
        {
            if (data == null) return;
            _tracker.Restore((QuestStage)data.State, data.StateName, data.ObjectiveCounts, data.ProcessedEventKeys);
        }
    }
}
