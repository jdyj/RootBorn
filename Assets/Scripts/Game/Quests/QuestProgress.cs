using System;

namespace Rootborn.Game.Quests
{
    public sealed class QuestProgress
    {
        private readonly int[] _objectiveCounts;

        public QuestProgress(int objectiveCount)
        {
            if (objectiveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(objectiveCount));
            }

            _objectiveCounts = new int[objectiveCount];
            State = QuestState.NotStarted;
        }

        public QuestState State { get; private set; }
        public int ObjectiveCount => _objectiveCounts.Length;

        public bool TryAccept()
        {
            if (State != QuestState.NotStarted)
            {
                return false;
            }

            State = QuestState.Active;
            return true;
        }
    }
}
