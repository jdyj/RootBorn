using System;
using System.Collections.Generic;

namespace Rootborn.Core.Quests
{
    /// <summary>Same order and names as the game's QuestState, so saved numbers and names map one to one.</summary>
    public enum QuestStage
    {
        NotStarted,
        Active,
        Completed,
        RewardClaimed
    }

    /// <summary>A copy of one quest's progress for saving.</summary>
    public sealed class QuestSnapshot
    {
        public QuestSnapshot(QuestStage stage, int[] objectiveCounts, string[] processedEventKeys)
        {
            Stage = stage;
            ObjectiveCounts = objectiveCounts ?? Array.Empty<int>();
            ProcessedEventKeys = processedEventKeys ?? Array.Empty<string>();
        }

        public QuestStage Stage { get; }
        public int[] ObjectiveCounts { get; }
        public string[] ProcessedEventKeys { get; }
    }

    /// <summary>
    /// One quest's progress: accept, count objective events once per event key, complete when every objective is done,
    /// and claim the reward once. Knows nothing about what an objective matches; callers pass the delta and requirement.
    /// </summary>
    public sealed class QuestTracker
    {
        private readonly int[] _objectiveCounts;
        private readonly bool[] _objectiveComplete;
        private readonly HashSet<string> _processedEventKeys = new HashSet<string>(StringComparer.Ordinal);

        public QuestTracker(int objectiveCount)
        {
            if (objectiveCount < 0) throw new ArgumentOutOfRangeException(nameof(objectiveCount));
            _objectiveCounts = new int[objectiveCount];
            _objectiveComplete = new bool[objectiveCount];
            Stage = QuestStage.NotStarted;
        }

        public QuestStage Stage { get; private set; }
        public int ObjectiveCount => _objectiveCounts.Length;

        public bool TryAccept()
        {
            if (Stage != QuestStage.NotStarted) return false;
            Stage = QuestStage.Active;
            return true;
        }

        public int GetObjectiveCount(int objectiveIndex)
        {
            ValidateObjectiveIndex(objectiveIndex);
            return _objectiveCounts[objectiveIndex];
        }

        /// <summary>
        /// Adds progress to one objective. A non-empty <paramref name="eventKey"/> is counted once per quest, even when the
        /// objective it reached was already done. Returns true only when the count changed.
        /// </summary>
        public bool TryAddObjectiveCount(int objectiveIndex, int delta, int requiredCount, string eventKey)
        {
            ValidateObjectiveIndex(objectiveIndex);
            if (Stage != QuestStage.Active || delta <= 0 || requiredCount <= 0) return false;
            if (!string.IsNullOrEmpty(eventKey) && !_processedEventKeys.Add(eventKey)) return false;
            if (_objectiveComplete[objectiveIndex]) return false;

            int next = _objectiveCounts[objectiveIndex] + delta;
            _objectiveCounts[objectiveIndex] = next > requiredCount ? requiredCount : next;
            _objectiveComplete[objectiveIndex] = _objectiveCounts[objectiveIndex] >= requiredCount;
            if (AllObjectivesComplete()) Stage = QuestStage.Completed;
            return true;
        }

        public bool TryMarkRewardClaimed()
        {
            if (Stage != QuestStage.Completed) return false;
            Stage = QuestStage.RewardClaimed;
            return true;
        }

        public QuestSnapshot ToSnapshot()
        {
            return new QuestSnapshot(Stage, (int[])_objectiveCounts.Clone(), new List<string>(_processedEventKeys).ToArray());
        }

        /// <summary>
        /// Replaces progress with saved values. A recognised <paramref name="stageName"/> wins over <paramref name="stage"/>.
        /// Completed or claimed quests keep every objective done; negative or missing counts load as zero.
        /// </summary>
        public void Restore(QuestStage stage, string stageName, int[] objectiveCounts, IEnumerable<string> processedEventKeys)
        {
            Stage = stage;
            if (!string.IsNullOrEmpty(stageName) && Enum.TryParse(stageName, out QuestStage named) && Enum.IsDefined(typeof(QuestStage), named))
                Stage = named;

            bool finished = Stage == QuestStage.Completed || Stage == QuestStage.RewardClaimed;
            for (int i = 0; i < _objectiveCounts.Length; i++)
            {
                int saved = objectiveCounts != null && i < objectiveCounts.Length ? objectiveCounts[i] : 0;
                _objectiveCounts[i] = saved < 0 ? 0 : saved;
                _objectiveComplete[i] = finished;
            }

            _processedEventKeys.Clear();
            if (processedEventKeys == null) return;
            foreach (string key in processedEventKeys)
                if (!string.IsNullOrEmpty(key)) _processedEventKeys.Add(key);
        }

        private bool AllObjectivesComplete()
        {
            if (_objectiveComplete.Length == 0) return false;
            foreach (bool done in _objectiveComplete)
                if (!done) return false;
            return true;
        }

        private void ValidateObjectiveIndex(int objectiveIndex)
        {
            if (objectiveIndex < 0 || objectiveIndex >= _objectiveCounts.Length) throw new ArgumentOutOfRangeException(nameof(objectiveIndex));
        }
    }
}
