using System.Collections.Generic;

namespace Rootborn.Game.Quests
{
    public sealed class QuestLog
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
                if (quest == null || _progressByQuest.ContainsKey(quest))
                {
                    continue;
                }

                int objectiveCount = quest.Objectives == null ? 0 : quest.Objectives.Length;
                _progressByQuest.Add(quest, new QuestProgress(objectiveCount));
            }
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
            return TryGetProgress(quest, out var progress) && progress.TryAccept();
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

                    int delta = objective.GetDelta(in questEvent);
                    progress.TryAddObjectiveCount(i, delta, objective.RequiredCount, questEvent.EventKey);
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

            return TryGetProgress(quest, out var progress) && progress.TryMarkRewardClaimed();
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
                        break;
                    }
                }
            }
        }

        private bool TryGetProgress(QuestDefinition quest, out QuestProgress progress)
        {
            if (quest != null)
            {
                return _progressByQuest.TryGetValue(quest, out progress);
            }

            progress = null;
            return false;
        }
    }
}
