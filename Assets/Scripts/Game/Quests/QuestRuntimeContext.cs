using Rootborn.Game.Knowledge;
using Rootborn.Game.Story;

namespace Rootborn.Game.Quests
{
    public readonly struct QuestRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly QuestDefinition Quest;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly StoryFlagSet StoryFlags;

        public QuestRuntimeContext(
            QuestLog questLog,
            QuestDefinition quest,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags)
        {
            QuestLog = questLog;
            Quest = quest;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
        }
    }
}
