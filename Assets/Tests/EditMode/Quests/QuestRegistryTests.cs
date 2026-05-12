using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests.Effects;
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

        [Test]
        public void QUEST_STUDENT_002_RegistryContainsFourCareerQuestTraitGrowthRoutes()
        {
            var registry = Resources.Load<GameDataRegistry>("GameDataRegistry");
            Assert.IsNotNull(registry);

            var expectedCareerIds = new HashSet<string>
            {
                "career.chef",
                "career.interior",
                "career.soldier",
                "career.emergency-care",
            };
            var coveredCareerIds = new HashSet<string>();

            for (int i = 0; i < registry.Quests.Length; i++)
            {
                var quest = registry.Quests[i];
                if (quest == null || quest.CompletionEffects == null)
                {
                    continue;
                }

                for (int j = 0; j < quest.CompletionEffects.Length; j++)
                {
                    if (quest.CompletionEffects[j] is TraitDeltaCompletionEffect effect && effect.CareerHint != null)
                    {
                        Assert.IsNotNull(effect.Trait, quest.Id);
                        Assert.Greater(effect.Delta, 0, quest.Id);
                        coveredCareerIds.Add(effect.CareerHint.Id);
                    }
                }
            }

            foreach (var expectedCareerId in expectedCareerIds)
            {
                CollectionAssert.Contains(coveredCareerIds, expectedCareerId);
            }
        }
    }
}