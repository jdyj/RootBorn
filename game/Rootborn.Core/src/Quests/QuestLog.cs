using System;
using System.Collections.Generic;
using Rootborn.Core.Rewards;

namespace Rootborn.Core.Quests
{
    /// <summary>One quest: what it asks for, what it gives, and the conditions around it.</summary>
    public sealed class QuestDefinition
    {
        private readonly QuestObjective[] _objectives;
        private readonly QuestReward[] _rewards;
        private readonly QuestCompletionEffect[] _completionEffects;
        private readonly QuestCondition[] _providerConditions;
        private readonly QuestCondition[] _prerequisites;

        public QuestDefinition(string id, string displayNameKey, string descriptionKey, IReadOnlyList<QuestObjective> objectives,
            IReadOnlyList<QuestReward> rewards = null, IReadOnlyList<QuestCompletionEffect> completionEffects = null,
            IReadOnlyList<QuestCondition> providerConditions = null, IReadOnlyList<QuestCondition> prerequisites = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            _objectives = Copy(objectives);
            _rewards = Copy(rewards);
            _completionEffects = Copy(completionEffects);
            _providerConditions = Copy(providerConditions);
            _prerequisites = Copy(prerequisites);
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public IReadOnlyList<QuestObjective> Objectives => _objectives;
        public IReadOnlyList<QuestReward> Rewards => _rewards;
        public IReadOnlyList<QuestCompletionEffect> CompletionEffects => _completionEffects;
        public IReadOnlyList<QuestCondition> ProviderConditions => _providerConditions;
        public IReadOnlyList<QuestCondition> Prerequisites => _prerequisites;

        internal static T[] Copy<T>(IReadOnlyList<T> values) => values == null ? Array.Empty<T>() : new List<T>(values).ToArray();
    }

    /// <summary>Same field names and types as the game's save file (written by JsonUtility; the enum is saved as its number).</summary>
    [Serializable]
    public sealed class QuestLogSaveData : Save.IVersionedSave
    {
        /// <summary>The highest format version this program reads. Version 0 is a file written by the Unity build.</summary>
        public const int KnownVersion = 1;

        public int Version;
        public QuestProgressSaveData[] Quests = Array.Empty<QuestProgressSaveData>();

        int Save.IVersionedSave.Version { get => Version; set => Version = value; }
        int Save.IVersionedSave.KnownVersion => KnownVersion;
    }

    [Serializable]
    public sealed class QuestProgressSaveData
    {
        public string QuestId;
        public QuestStage State;
        public string StateName;
        public int[] ObjectiveCounts = Array.Empty<int>();
        public string[] ProcessedEventKeys = Array.Empty<string>();
    }

    /// <summary>One quest's progress, saved as <see cref="QuestProgressSaveData"/>. The rules are <see cref="QuestTracker"/>.</summary>
    public sealed class QuestProgress
    {
        private readonly QuestTracker _tracker;

        public QuestProgress(int objectiveCount)
        {
            _tracker = new QuestTracker(objectiveCount);
        }

        public QuestStage State => _tracker.Stage;
        public int ObjectiveCount => _tracker.ObjectiveCount;

        public bool TryAccept() => _tracker.TryAccept();
        public int GetObjectiveCount(int objectiveIndex) => _tracker.GetObjectiveCount(objectiveIndex);

        public bool TryAddObjectiveCount(int objectiveIndex, int delta, int requiredCount, string eventKey)
            => _tracker.TryAddObjectiveCount(objectiveIndex, delta, requiredCount, eventKey);

        public bool TryMarkRewardClaimed() => _tracker.TryMarkRewardClaimed();

        public QuestProgressSaveData ToSaveData(string questId)
        {
            var snapshot = _tracker.ToSnapshot();
            return new QuestProgressSaveData
            {
                QuestId = questId,
                State = snapshot.Stage,
                StateName = snapshot.Stage.ToString(),
                ObjectiveCounts = snapshot.ObjectiveCounts,
                ProcessedEventKeys = snapshot.ProcessedEventKeys,
            };
        }

        public void LoadFromSaveData(QuestProgressSaveData data)
        {
            if (data == null) return;
            _tracker.Restore(data.State, data.StateName, data.ObjectiveCounts, data.ProcessedEventKeys);
        }
    }

    public enum QuestStateChangeKind
    {
        Accepted,
        ObjectiveProgressed,
        Completed,
        RewardClaimed
    }

    public readonly struct QuestStateChange
    {
        public readonly QuestDefinition Quest;
        public readonly QuestStage State;
        public readonly QuestStateChangeKind Kind;
        public readonly int ObjectiveIndex;
        public readonly int ObjectiveCount;
        public readonly string EventKey;

        public QuestStateChange(QuestDefinition quest, QuestStage state, QuestStateChangeKind kind, int objectiveIndex = -1, int objectiveCount = 0, string eventKey = null)
        {
            Quest = quest;
            State = state;
            Kind = kind;
            ObjectiveIndex = objectiveIndex;
            ObjectiveCount = objectiveCount;
            EventKey = eventKey;
        }
    }

    /// <summary>
    /// The player's quests: accept, count matching events once per event key, and claim each reward once with items,
    /// rewards, completion effects and the claimed state committed together. A definition with the same id as a
    /// known quest finds that quest's progress.
    /// </summary>
    public sealed class QuestLog : IQuestEventSink
    {
        private readonly Dictionary<QuestDefinition, QuestProgress> _progressByQuest = new Dictionary<QuestDefinition, QuestProgress>();
        // One claim at a time: a reward or effect that starts another claim while one runs gets false.
        private readonly RewardClaimGate _claimGate = new RewardClaimGate();

        public QuestLog(IEnumerable<QuestDefinition> quests)
        {
            if (quests == null) return;
            foreach (var quest in quests) AddQuest(quest);
        }

        public event Action<QuestStateChange> OnStateChanged;

        /// <summary>Adds a quest with fresh progress. An earlier quest with the same id is dropped with its progress.</summary>
        public bool AddQuest(QuestDefinition quest)
        {
            if (quest == null) return false;
            if (TryFindQuestById(quest.Id, out var existing)) _progressByQuest.Remove(existing);
            else if (_progressByQuest.ContainsKey(quest)) return false;

            _progressByQuest.Add(quest, new QuestProgress(quest.Objectives.Count));
            return true;
        }

        public bool ReplaceQuestById(QuestDefinition quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id)) return false;
            var matches = new List<QuestDefinition>();
            foreach (var pair in _progressByQuest)
                if (pair.Key.Id == quest.Id) matches.Add(pair.Key);
            for (int i = 0; i < matches.Count; i++) _progressByQuest.Remove(matches[i]);
            AddQuest(quest);
            return true;
        }

        public QuestStage GetState(QuestDefinition quest) => TryGetProgress(quest, out var progress) ? progress.State : QuestStage.NotStarted;

        public QuestStage GetState(string questId)
            => TryFindQuestById(questId, out var quest) && _progressByQuest.TryGetValue(quest, out var progress) ? progress.State : QuestStage.NotStarted;

        public int GetObjectiveCount(QuestDefinition quest, int objectiveIndex)
            => TryGetProgress(quest, out var progress) ? progress.GetObjectiveCount(objectiveIndex) : 0;

        public bool Accept(QuestDefinition quest) => Accept(quest, default);

        /// <summary>제공 조건과 선행 조건이 모두 맞을 때만 받는다(Unity 판은 확인하지 않았다). 조건이 있는 퀘스트는 문맥 없이 받을 수 없다.</summary>
        public bool Accept(QuestDefinition quest, in QuestRuntimeContext context)
        {
            if (!TryGetProgress(quest, out var progress) || !ConditionsHold(quest.ProviderConditions, in context) || !ConditionsHold(quest.Prerequisites, in context) || !progress.TryAccept()) return false;
            RaiseStateChanged(new QuestStateChange(quest, progress.State, QuestStateChangeKind.Accepted));
            return true;
        }

        /// <summary>지금 받을 수 있는지(시작 전이고 제공 조건·선행 조건이 맞는지). Accept와 같은 판정이고 아무것도 바꾸지 않는다.</summary>
        public bool CanAccept(QuestDefinition quest, in QuestRuntimeContext context)
            => TryGetProgress(quest, out var progress) && progress.State == QuestStage.NotStarted
               && ConditionsHold(quest.ProviderConditions, in context) && ConditionsHold(quest.Prerequisites, in context);

        private static bool ConditionsHold(IReadOnlyList<QuestCondition> conditions, in QuestRuntimeContext context)
        {
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null && !conditions[i].IsSatisfied(in context)) return false;
            return true;
        }

        public void Record(in QuestEvent questEvent) => RecordEvent(in questEvent);

        /// <summary>Counts matching objectives. A replayed event (<see cref="QuestEvent.IsReplay"/>) is never counted again here.</summary>
        public void RecordEvent(in QuestEvent questEvent)
        {
            if (questEvent.IsReplay) return;
            foreach (var pair in _progressByQuest)
            {
                var quest = pair.Key;
                var progress = pair.Value;
                for (int i = 0; i < quest.Objectives.Count; i++)
                {
                    var objective = quest.Objectives[i];
                    if (objective == null || !objective.Matches(in questEvent)) continue;

                    QuestStage beforeState = progress.State;
                    int delta = objective.GetDelta(in questEvent);
                    if (!progress.TryAddObjectiveCount(i, delta, objective.RequiredCount, questEvent.EventKey)) continue;

                    RaiseStateChanged(new QuestStateChange(quest, progress.State, QuestStateChangeKind.ObjectiveProgressed, i, progress.GetObjectiveCount(i), questEvent.EventKey));
                    if (beforeState != QuestStage.Completed && progress.State == QuestStage.Completed)
                        RaiseStateChanged(new QuestStateChange(quest, progress.State, QuestStateChangeKind.Completed, i, progress.GetObjectiveCount(i), questEvent.EventKey));
                }
            }
        }

        public bool CanClaimReward(QuestDefinition quest, in RewardRuntimeContext context)
        {
            if (_claimGate.IsClaiming || !TryGetProgress(quest, out var progress) || progress.State != QuestStage.Completed) return false;
            // Checks all item grants, rewards and completion effects together: each fitting alone does not mean they all fit.
            return RewardTransaction.CanApply(QuestRewardBatch.Bundle(quest.Rewards, quest.CompletionEffects, in context), context.Inventory);
        }

        public bool ClaimReward(QuestDefinition quest, in RewardRuntimeContext context)
        {
            var capturedContext = context;
            QuestProgress progress = null;
            // Items, other rewards, completion effects and the claim commit together, so inventory observers see the claim recorded.
            // The notification stays inside the gate, so a listener cannot start another claim while this one finishes.
            return _claimGate.TryClaim(
                () => TryGetProgress(quest, out progress) && progress.State == QuestStage.Completed,
                () =>
                {
                    if (!RewardTransaction.TryApply(QuestRewardBatch.Bundle(quest.Rewards, quest.CompletionEffects, in capturedContext),
                            capturedContext.Inventory, () => progress.TryMarkRewardClaimed()))
                        return false;

                    RaiseStateChanged(new QuestStateChange(quest, progress.State, QuestStateChangeKind.RewardClaimed));
                    return true;
                });
        }

        public QuestLogSaveData ToSaveData()
        {
            var list = new List<QuestProgressSaveData>(_progressByQuest.Count);
            foreach (var pair in _progressByQuest)
            {
                if (string.IsNullOrEmpty(pair.Key.Id)) continue;
                list.Add(pair.Value.ToSaveData(pair.Key.Id));
            }

            for (int i = 0; i < _unknownRecords.Count; i++) list.Add(_unknownRecords[i]);
            return new QuestLogSaveData { Quests = list.ToArray() };
        }

        // 세이브에 있지만 지금 데이터에 없는 퀘스트의 기록. 규칙에는 보이지 않고 그대로 다시 쓴다.
        // 버리면 다음 저장에 사라지고, 그 퀘스트가 데이터에 돌아왔을 때 받은 보상을 다시 받을 수 있다.
        private readonly List<QuestProgressSaveData> _unknownRecords = new List<QuestProgressSaveData>();

        private void KeepUnknown(QuestProgressSaveData saved)
        {
            for (int i = 0; i < _unknownRecords.Count; i++)
            {
                if (_unknownRecords[i].QuestId != saved.QuestId) continue;
                _unknownRecords[i] = saved;
                return;
            }

            _unknownRecords.Add(saved);
        }

        public void LoadFromSaveData(QuestLogSaveData saveData)
        {
            if (saveData == null || saveData.Quests == null) return;
            for (int i = 0; i < saveData.Quests.Length; i++)
            {
                var saved = saveData.Quests[i];
                if (saved == null || string.IsNullOrEmpty(saved.QuestId)) continue;
                bool known = false;
                foreach (var pair in _progressByQuest)
                {
                    if (pair.Key.Id != saved.QuestId) continue;
                    pair.Value.LoadFromSaveData(saved);
                    known = true;
                }

                if (!known) KeepUnknown(saved);
            }
        }

        private bool TryGetProgress(QuestDefinition quest, out QuestProgress progress)
        {
            if (quest != null)
            {
                if (_progressByQuest.TryGetValue(quest, out progress)) return true;
                if (TryFindQuestById(quest.Id, out var matchingQuest)) return _progressByQuest.TryGetValue(matchingQuest, out progress);
            }

            progress = null;
            return false;
        }

        private bool TryFindQuestById(string questId, out QuestDefinition quest)
        {
            if (!string.IsNullOrEmpty(questId))
            {
                foreach (var pair in _progressByQuest)
                {
                    if (pair.Key.Id != questId) continue;
                    quest = pair.Key;
                    return true;
                }
            }

            quest = null;
            return false;
        }

        private void RaiseStateChanged(QuestStateChange change) => OnStateChanged?.Invoke(change);
    }
}
