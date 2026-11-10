using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Quests
{
    // Other areas (knowledge, story flags, world state, furniture unlocks) are not in Rootborn.Core yet.
    // Quests reach them only through these interfaces, by id; the area's own progress class implements them later.

    /// <summary>What quests read and write of the knowledge area.</summary>
    public interface IQuestKnowledge
    {
        bool IsUnlocked(string knowledgeId);
        void Seed(string knowledgeId);
    }

    /// <summary>What quests read and write of the story flags.</summary>
    public interface IQuestStoryFlags
    {
        bool IsSet(string flagId);
        bool Set(string flagId);
    }

    /// <summary>What quests read and write of the world state. The world state resolves a flag's scope from its id.</summary>
    public interface IQuestWorldState
    {
        bool IsActive(string flagId);
        bool TryActivate(string flagId, QuestWorldStateSource source);
        bool MarkEffectVersionApplied(string flagId, int version);
    }

    /// <summary>What quests read and write of the furniture unlocks.</summary>
    public interface IQuestFurnitureUnlocks
    {
        bool IsUnlocked(string unlockToken);
        bool TryUnlock(string unlockToken);
    }

    /// <summary>Which quest change turned a world state flag on. Same fields as the game's WorldStateActivationSource.</summary>
    public readonly struct QuestWorldStateSource
    {
        public readonly string QuestChainId;
        public readonly string QuestStepId;
        public readonly string EventId;
        public readonly int Day;

        public QuestWorldStateSource(string questChainId, string questStepId, string eventId, int day)
        {
            QuestChainId = questChainId ?? string.Empty;
            QuestStepId = questStepId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Day = day;
        }
    }

    /// <summary>What a condition may look at.</summary>
    public readonly struct QuestRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly QuestDefinition Quest;
        public readonly IQuestKnowledge KnowledgeProgress;
        public readonly IQuestStoryFlags StoryFlags;
        public readonly StudentLifeProgress StudentLifeProgress;

        public QuestRuntimeContext(QuestLog questLog, QuestDefinition quest, IQuestKnowledge knowledgeProgress, IQuestStoryFlags storyFlags,
            StudentLifeProgress studentLifeProgress = null)
        {
            QuestLog = questLog;
            Quest = quest;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
            StudentLifeProgress = studentLifeProgress;
        }
    }

    /// <summary>What a reward or completion effect may change.</summary>
    public readonly struct RewardRuntimeContext
    {
        public readonly QuestLog QuestLog;
        public readonly ItemInventory Inventory;
        public readonly IQuestKnowledge KnowledgeProgress;
        public readonly IQuestStoryFlags StoryFlags;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly IQuestWorldState WorldStateProgress;
        public readonly IQuestFurnitureUnlocks FurnitureUnlockProgress;

        public RewardRuntimeContext(QuestLog questLog, ItemInventory inventory, IQuestKnowledge knowledgeProgress, IQuestStoryFlags storyFlags,
            StudentLifeProgress studentLifeProgress = null, IQuestWorldState worldStateProgress = null, IQuestFurnitureUnlocks furnitureUnlockProgress = null)
        {
            QuestLog = questLog;
            Inventory = inventory;
            KnowledgeProgress = knowledgeProgress;
            StoryFlags = storyFlags;
            StudentLifeProgress = studentLifeProgress;
            WorldStateProgress = worldStateProgress;
            FurnitureUnlockProgress = furnitureUnlockProgress;
        }
    }
}
