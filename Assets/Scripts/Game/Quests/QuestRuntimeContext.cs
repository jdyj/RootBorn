using Rootborn.Game.Knowledge;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.Quests
{
    public readonly struct QuestRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly QuestDefinition Quest;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly StoryFlagSet StoryFlags;
        public readonly StudentLifeProgress StudentLifeProgress;

        public QuestRuntimeContext(
            QuestLog questLog,
            QuestDefinition quest,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags)
            : this(questLog, quest, knowledgeProgress, storyFlags, null)
        {
        }

        public QuestRuntimeContext(
            QuestLog questLog,
            QuestDefinition quest,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags,
            StudentLifeProgress studentLifeProgress)
        {
            QuestLog = questLog;
            Quest = quest;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
            StudentLifeProgress = studentLifeProgress;
        }
    }
}
