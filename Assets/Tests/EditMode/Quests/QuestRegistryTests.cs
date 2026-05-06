using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestRegistryTests
    {
        [Test]
        public void QUEST_014_GameDataRegistry_ExposesQuestDataArrays()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            Assert.IsNotNull(registry.Quests);
            Assert.IsNotNull(registry.QuestObjectives);
            Assert.IsNotNull(registry.QuestRewards);
            Assert.IsNotNull(registry.QuestCompletionEffects);
            Assert.IsNotNull(registry.QuestConditions);
            Assert.IsNotNull(registry.Npcs);
            Assert.IsNotNull(registry.Dialogues);
            Assert.IsNotNull(registry.StoryFlags);
        }

        [Test]
        public void QUEST_014_DefaultQuestData_IsRegistered()
        {
            var registry = Resources.Load<GameDataRegistry>("GameDataRegistry");

            Assert.IsNotNull(registry);
            Assert.IsNotEmpty(registry.Quests);
            Assert.IsNotEmpty(registry.Npcs);
            Assert.IsNotEmpty(registry.Dialogues);
            Assert.IsNotEmpty(registry.StoryFlags);
        }
    }
}
