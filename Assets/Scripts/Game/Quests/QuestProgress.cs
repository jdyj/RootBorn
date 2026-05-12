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

        public bool TryMarkRewardClaimed()
        {
            if (State != QuestState.Completed)
            {
                return false;
            }

            State = QuestState.RewardClaimed;
            return true;
        }

        public QuestProgressSaveData ToSaveData(string questId)
        {
            return new QuestProgressSaveData
            {
                QuestId = questId,
                State = State,
                StateName = State.ToString(),
                ObjectiveCounts = (int[])_objectiveCounts.Clone(),
                ProcessedEventKeys = new List<string>(_processedEventKeys).ToArray(),
            };
        }

        public void LoadFromSaveData(QuestProgressSaveData data)
        {
            if (data == null)
            {
                return;
            }

            State = data.State;
            if (!string.IsNullOrEmpty(data.StateName) && Enum.TryParse(data.StateName, out QuestState namedState))
            {
                State = namedState;
            }

            bool savedAsFinished = State == QuestState.Completed || State == QuestState.RewardClaimed;
            for (int i = 0; i < _objectiveCounts.Length; i++)
            {
                _objectiveCounts[i] = 0;
                _objectiveComplete[i] = savedAsFinished;
            }

            if (data.ObjectiveCounts != null)
            {
                for (int i = 0; i < _objectiveCounts.Length && i < data.ObjectiveCounts.Length; i++)
                {
                    _objectiveCounts[i] = data.ObjectiveCounts[i] < 0 ? 0 : data.ObjectiveCounts[i];
                }
            }

            _processedEventKeys.Clear();
            if (data.ProcessedEventKeys == null)
            {
                return;
            }

            for (int i = 0; i < data.ProcessedEventKeys.Length; i++)
            {
                string key = data.ProcessedEventKeys[i];
                if (!string.IsNullOrEmpty(key))
                {
                    _processedEventKeys.Add(key);
                }
            }
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