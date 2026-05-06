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
