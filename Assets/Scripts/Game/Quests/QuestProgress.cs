using System;
using System.Collections.Generic;

namespace Rootborn.Game.Quests
{
    public sealed class QuestProgress
    {
        private readonly int[] _objectiveCounts;
        private readonly bool[] _objectiveComplete;
        private readonly HashSet<string> _processedEventKeys = new HashSet<string>();

        public QuestProgress(int objectiveCount)
        {
            if (objectiveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(objectiveCount));
            }

            _objectiveCounts = new int[objectiveCount];
            _objectiveComplete = new bool[objectiveCount];
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

        public int GetObjectiveCount(int objectiveIndex)
        {
            ValidateObjectiveIndex(objectiveIndex);
            return _objectiveCounts[objectiveIndex];
        }

        public bool TryAddObjectiveCount(int objectiveIndex, int delta, int requiredCount, string eventKey)
        {
            ValidateObjectiveIndex(objectiveIndex);
            if (State != QuestState.Active || delta <= 0 || requiredCount <= 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(eventKey) && !_processedEventKeys.Add(eventKey))
            {
                return false;
            }

            if (_objectiveComplete[objectiveIndex])
            {
                return false;
            }

            int next = _objectiveCounts[objectiveIndex] + delta;
            _objectiveCounts[objectiveIndex] = next > requiredCount ? requiredCount : next;
            _objectiveComplete[objectiveIndex] = _objectiveCounts[objectiveIndex] >= requiredCount;

            if (AllObjectivesComplete())
            {
                State = QuestState.Completed;
            }

            return true;
        }

        private bool AllObjectivesComplete()
        {
            if (_objectiveComplete.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < _objectiveComplete.Length; i++)
            {
                if (!_objectiveComplete[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void ValidateObjectiveIndex(int objectiveIndex)
        {
            if (objectiveIndex < 0 || objectiveIndex >= _objectiveCounts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(objectiveIndex));
            }
        }
    }
}
