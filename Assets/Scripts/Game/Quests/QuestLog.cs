using System;
using System.Collections.Generic;

namespace Rootborn.Game.Quests
{
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
        public readonly QuestState State;
        public readonly QuestStateChangeKind Kind;
        public readonly int ObjectiveIndex;
        public readonly int ObjectiveCount;
        public readonly string EventKey;

        public QuestStateChange(
            QuestDefinition quest,
            QuestState state,
            QuestStateChangeKind kind,
            int objectiveIndex = -1,
            int objectiveCount = 0,
            string eventKey = null)
        {
            Quest = quest;
            State = state;
            Kind = kind;
            ObjectiveIndex = objectiveIndex;
            ObjectiveCount = objectiveCount;
            EventKey = eventKey;
        }
    }

    public sealed class QuestLog : IQuestEventSink
    {
        private readonly Dictionary<QuestDefinition, QuestProgress> _progressByQuest;

        public QuestLog(IEnumerable<QuestDefinition> quests)
        {
            _progressByQuest = new Dictionary<QuestDefinition, QuestProgress>();
            if (quests == null)
            {
                return;
            }

            foreach (var quest in quests)
            {
                AddQuest(quest);
            }
        }

        public event Action<QuestStateChange> OnStateChanged;

        public bool AddQuest(QuestDefinition quest)
        {
            if (quest == null)
            {
                return false;
            }

            if (TryFindQuestById(quest.Id, out var existing))
            {
                _progressByQuest.Remove(existing);
            }
            else if (_progressByQuest.ContainsKey(quest))
            {
                return false;
            }

            int objectiveCount = quest.Objectives == null ? 0 : quest.Objectives.Length;
            _progressByQuest.Add(quest, new QuestProgress(objectiveCount));
            return true;
        }

        public bool ReplaceQuestById(QuestDefinition quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Id))
            {
                return false;
            }

            var matches = new List<QuestDefinition>();
            foreach (var pair in _progressByQuest)
            {
                if (pair.Key != null && pair.Key.Id == quest.Id)
                {
                    matches.Add(pair.Key);
                }
            }

            for (int i = 0; i < matches.Count; i++)
            {
                _progressByQuest.Remove(matches[i]);
            }

            AddQuest(quest);
            return true;
        }

        public QuestState GetState(QuestDefinition quest)
        {
            return TryGetProgress(quest, out var progress) ? progress.State : QuestState.NotStarted;
        }

        public int GetObjectiveCount(QuestDefinition quest, int objectiveIndex)
        {
            return TryGetProgress(quest, out var progress) ? progress.GetObjectiveCount(objectiveIndex) : 0;
        }

        public bool Accept(QuestDefinition quest)
        {
            if (!TryGetProgress(quest, out var progress) || !progress.TryAccept())
            {
                return false;
            }

            RaiseStateChanged(new QuestStateChange(quest, progress.State, QuestStateChangeKind.Accepted));
            return true;
        }

        public void Record(in QuestEvent questEvent)
        {
            RecordEvent(in questEvent);
        }

        public void RecordEvent(in QuestEvent questEvent)
        {
            foreach (var pair in _progressByQuest)
            {
                var quest = pair.Key;
                var progress = pair.Value;
                if (quest.Objectives == null)
                {
                    continue;
                }

                for (int i = 0; i < quest.Objectives.Length; i++)
                {
                    var objective = quest.Objectives[i];
                    if (objective == null || !objective.Matches(in questEvent))
                    {
                        continue;
                    }

                    QuestState beforeState = progress.State;
                    int delta = objective.GetDelta(in questEvent);
                    if (!progress.TryAddObjectiveCount(i, delta, objective.RequiredCount, questEvent.EventKey))
                    {
                        continue;
                    }

                    RaiseStateChanged(new QuestStateChange(
                        quest,
                        progress.State,
                        QuestStateChangeKind.ObjectiveProgressed,
                        i,
                        progress.GetObjectiveCount(i),
                        questEvent.EventKey));

                    if (beforeState != QuestState.Completed && progress.State == QuestState.Completed)
                    {
                        RaiseStateChanged(new QuestStateChange(
                            quest,
                            progress.State,
                            QuestStateChangeKind.Completed,
                            i,
                            progress.GetObjectiveCount(i),
                            questEvent.EventKey));
                    }
                }
            }
        }

        public bool CanClaimReward(QuestDefinition quest, in RewardRuntimeContext context)
        {
            if (!TryGetProgress(quest, out var progress) || progress.State != QuestState.Completed)
            {
                return false;
            }

            if (quest.Rewards != null)
            {
                for (int i = 0; i < quest.Rewards.Length; i++)
                {
                    var reward = quest.Rewards[i];
                    if (reward != null && !reward.CanApply(in context))
                    {
                        return false;
                    }
                }
            }

            if (quest.CompletionEffects != null)
            {
                for (int i = 0; i < quest.CompletionEffects.Length; i++)
                {
                    var effect = quest.CompletionEffects[i];
                    if (effect != null && !effect.CanApply(in context))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool ClaimReward(QuestDefinition quest, in RewardRuntimeContext context)
        {
            if (!CanClaimReward(quest, in context))
            {
                return false;
            }

            if (quest.Rewards != null)
            {
                for (int i = 0; i < quest.Rewards.Length; i++)
                {
                    quest.Rewards[i]?.Apply(in context);
                }
            }

            if (quest.CompletionEffects != null)
            {
                for (int i = 0; i < quest.CompletionEffects.Length; i++)
                {
                    quest.CompletionEffects[i]?.Apply(in context);
                }
            }

            if (!TryGetProgress(quest, out var progress) || !progress.TryMarkRewardClaimed())
            {
                return false;
            }

            RaiseStateChanged(new QuestStateChange(quest, progress.State, QuestStateChangeKind.RewardClaimed));
            return true;
        }

        public QuestLogSaveData ToSaveData()
        {
            var list = new List<QuestProgressSaveData>(_progressByQuest.Count);
            foreach (var pair in _progressByQuest)
            {
                if (pair.Key == null || string.IsNullOrEmpty(pair.Key.Id))
                {
                    continue;
                }

                list.Add(pair.Value.ToSaveData(pair.Key.Id));
            }

            return new QuestLogSaveData { Quests = list.ToArray() };
        }

        public void LoadFromSaveData(QuestLogSaveData saveData)
        {
            if (saveData == null || saveData.Quests == null)
            {
                return;
            }

            for (int i = 0; i < saveData.Quests.Length; i++)
            {
                var saved = saveData.Quests[i];
                if (saved == null || string.IsNullOrEmpty(saved.QuestId))
                {
                    continue;
                }

                foreach (var pair in _progressByQuest)
                {
                    if (pair.Key != null && pair.Key.Id == saved.QuestId)
                    {
                        pair.Value.LoadFromSaveData(saved);
                    }
                }
            }
        }

        private bool TryGetProgress(QuestDefinition quest, out QuestProgress progress)
        {
            if (quest != null)
            {
                if (_progressByQuest.TryGetValue(quest, out progress))
                {
                    return true;
                }

                if (TryFindQuestById(quest.Id, out var matchingQuest))
                {
                    return _progressByQuest.TryGetValue(matchingQuest, out progress);
                }
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
                    if (pair.Key != null && pair.Key.Id == questId)
                    {
                        quest = pair.Key;
                        return true;
                    }
                }
            }

            quest = null;
            return false;
        }

        private void RaiseStateChanged(QuestStateChange change)
        {
            OnStateChanged?.Invoke(change);
        }
    }
}
