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

        // Mom's house decorating quest keeps the old quest id (saves may hold it) but counts furniture placed at home.
        [Test]
        public void QUEST_HOUSE_DECORATE_001_MomOffersHouseDecoratingQuestCountingThreeHouseFurniture()
        {
            var quest = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Quests.QuestDefinition>("Assets/Data/Quests/CareerRoutes/Quest_TilePlacementInterior.asset");
            var myRoom = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective>("Assets/Data/StudentLife/MainStory/Objectives/Objective_MyRoom.asset");
            var mom = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Dialogue.NpcDefinition>("Assets/Data/NPCs/Npc_FirstGuide.asset");
            var day2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Dialogue.DialogueDefinition>("Assets/Data/Dialogue/Dialogue_FirstNpc_Day2.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Localization.GameTextCatalog>("Assets/Localization/GameTextCatalog.asset");
            Assert.AreEqual("quest.tile-placement.interior", quest.Id);
            Assert.AreEqual(1, quest.Objectives.Length);
            var objective = quest.Objectives[0] as Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective;
            Assert.IsNotNull(objective, "Decorating counts placed furniture.");
            Assert.AreEqual("house", objective.RequiredSurfaceId);
            Assert.AreEqual(3, objective.RequiredCount);
            CollectionAssert.AreEquivalent(myRoom.AcceptedFurniture, objective.AcceptedFurniture, "The furniture a player can place at home.");
            Assert.IsTrue(System.Array.Exists(quest.CompletionEffects, e => e is TraitDeltaCompletionEffect t && t.Delta == 2 && t.CareerHint != null), "Creativity +2 and the interior hint stay.");
            CollectionAssert.Contains(mom.Quests, quest);
            Assert.IsTrue(System.Array.Exists(day2.Choices, c => c.Quest == quest && c.QuestAction == Rootborn.Game.Dialogue.DialogueQuestAction.AcceptQuest));
            Assert.IsTrue(System.Array.Exists(day2.Choices, c => c.Quest == quest && c.QuestAction == Rootborn.Game.Dialogue.DialogueQuestAction.ClaimReward));
            CollectionAssert.Contains(UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset").QuestObjectives, objective);
            foreach (var key in new[] { quest.DisplayNameKey, quest.DescriptionKey, objective.DisplayKey, "dialogue.choice.acceptDecorateHouse", "dialogue.choice.claimDecorateHouse", "ui.journal.quest.claim", "ui.journal.quest.claimed", "ui.journal.quest.claim-failed" })
            {
                Assert.AreNotEqual(key, catalog.Get(key, "ko"), key + " ko");
                Assert.AreNotEqual(key, catalog.Get(key, "en"), key + " en");
            }
        }

        // Mom's Gather Wood quest can be finished in the authored Town: wood trees stand on open ground near home.
        [Test]
        public void QUEST_GATHER_WOOD_001_AuthoredTownHasModernWoodTreesForTheGatherObjective()
        {
            var quest = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Quests.QuestDefinition>("Assets/Data/Quests/Quest_GatherWood.asset");
            var gather = quest.Objectives[0] as Rootborn.Game.Quests.Objectives.GatherQuestObjective;
            Assert.IsNotNull(gather);
            // A Town already open in the editor plus this copy logs "more than one global light"; only objects are read here.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                GameObject root = null;
                foreach (var candidate in scene.GetRootGameObjects()) if (candidate.name == "TownResourcePoints") root = candidate;
                Assert.IsNotNull(root, "Town needs an authored root for resource points.");
                var nodes = root.GetComponentsInChildren<Rootborn.Game.Resources.ResourceNode>(true);
                Assert.GreaterOrEqual(nodes.Length, 3);
                foreach (var node in nodes)
                {
                    Assert.AreSame(gather.TargetResource, node.Definition, node.name);
                    var sprite = node.GetComponentInChildren<SpriteRenderer>().sprite;
                    Assert.IsNotNull(sprite, node.name);
                    StringAssert.StartsWith("Assets/Modern_Exteriors_32x32/", UnityEditor.AssetDatabase.GetAssetPath(sprite), node.name + " uses Modern art.");
                    Assert.Less(Vector2.Distance(node.transform.position, new Vector2(18.5f, -15.5f)), 12f, node.name + " is near the home door.");
                    var collider = node.GetComponent<Collider2D>();
                    Assert.IsTrue(collider != null && collider.isTrigger, node.name + " must not block walking.");
                }
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}