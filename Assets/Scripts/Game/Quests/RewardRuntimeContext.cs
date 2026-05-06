using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;

namespace Rootborn.Game.Quests
{
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly Inventory Inventory;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly object StoryFlags;

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            object storyFlags)
        {
            QuestLog = questLog;
            Inventory = inventory;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
        }
    }
}
