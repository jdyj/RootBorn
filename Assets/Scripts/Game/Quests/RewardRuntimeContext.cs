using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Story;

namespace Rootborn.Game.Quests
{
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly Inventory Inventory;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly StoryFlagSet StoryFlags;

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags)
        {
            QuestLog = questLog;
            Inventory = inventory;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
        }
    }
}
