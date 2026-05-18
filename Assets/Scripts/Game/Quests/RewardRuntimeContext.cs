using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;

namespace Rootborn.Game.Quests
{
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly Inventory Inventory;
        public readonly KnowledgeProgress KnowledgeProgress;
        public readonly StoryFlagSet StoryFlags;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly WorldStateProgress WorldStateProgress;

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags)
            : this(questLog, inventory, knowledgeProgress, storyFlags, null, null)
        {
        }

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags,
            StudentLifeProgress studentLifeProgress)
            : this(questLog, inventory, knowledgeProgress, storyFlags, studentLifeProgress, null)
        {
        }

        public RewardRuntimeContext(
            QuestLog questLog,
            Inventory inventory,
            KnowledgeProgress knowledgeProgress,
            StoryFlagSet storyFlags,
            StudentLifeProgress studentLifeProgress,
            WorldStateProgress worldStateProgress)
        {
            QuestLog = questLog;
            Inventory = inventory;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
            StudentLifeProgress = studentLifeProgress;
            WorldStateProgress = worldStateProgress;
        }
    }
}
