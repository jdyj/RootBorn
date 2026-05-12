using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.Quests
{
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly Inventory Inventory;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly StoryFlagSet StoryFlags;
        public readonly StudentLifeProgress StudentLifeProgress;

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags)
            : this(questLog, inventory, knowledgeProgress, storyFlags, null)
        {
        }

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags,
            StudentLifeProgress studentLifeProgress)
        {
            QuestLog = questLog;
            Inventory = inventory;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
            StudentLifeProgress = studentLifeProgress;
        }
    }
}