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
    }
}
