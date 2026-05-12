using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestPlayUserFlowScenarioTests
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-quest-play-user-flow", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QUEST_PLAY_WOOD_001_GuideDialogueAcceptsGatherWoodQuestThroughButtonAndQuestLogShowsZeroProgress()
        {
            yield return LoadTownWithSlot(2026051001, 2026051002);
            var runtime = FindRuntime();
            var route = FindGatherWoodRoute(runtime.Provider);

            yield return OpenGuideDialogue(runtime);
            Assert.IsTrue(runtime.DialoguePanel.IsOpen);

            var acceptButton = FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.AcceptQuest, route.Quest);
            Assert.IsNotNull(acceptButton, "Gather wood quest acceptance must be exposed as a real DialoguePanel Button.");
            yield return ClickButton(acceptButton);

            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
            AssertPanelContains(runtime.QuestLogPanel.transform, route.Quest.DisplayNameKey);
            AssertPanelContains(runtime.QuestLogPanel.transform, route.Objective.DisplayKey);
            AssertPanelContains(runtime.QuestLogPanel.transform, "0 / " + route.Objective.RequiredCount);
        }

        [UnityTest]
        public IEnumerator QUEST_PLAY_WOOD_002_WorldTreeInteractionGrantsWoodAndRefreshesQuestProgressGui()
        {
            yield return LoadTownWithSlot(2026051003, 2026051004);
            var runtime = FindRuntime();
            var route = FindGatherWoodRoute(runtime.Provider);
            var woodItem = ResolveFirstDropItem(runtime.Inventory, route.Objective.TargetResource);

            yield return AcceptGatherWoodQuest(runtime, route);
            int beforeWood = runtime.Inventory.Inventory.CountOf(woodItem);

            yield return GatherTargetResourceUntilInventoryIncreases(runtime, route.Objective.TargetResource, woodItem, beforeWood);

            Assert.Greater(runtime.Inventory.Inventory.CountOf(woodItem), beforeWood, "Actual world gathering must add the target wood resource to PlayerInventory.");
            Assert.GreaterOrEqual(runtime.QuestLogPanel.QuestLog.GetObjectiveCount(route.Quest, route.ObjectiveIndex), 1);
            AssertPanelContains(runtime.QuestLogPanel.transform, "1 / " + route.Objective.RequiredCount);
        }

        [UnityTest]
        public IEnumerator QUEST_PLAY_WOOD_003_RequiredWoodCompletesQuestAndClaimChoiceAppearsOnlyAfterCompletion()
        {
            yield return LoadTownWithSlot(2026051005, 2026051006);
            var runtime = FindRuntime();
            var route = FindGatherWoodRoute(runtime.Provider);
            var woodItem = ResolveFirstDropItem(runtime.Inventory, route.Objective.TargetResource);

            yield return AcceptGatherWoodQuest(runtime, route);
            yield return OpenGuideDialogue(runtime);
            Assert.IsNull(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest), "ClaimReward choice must not be visible before the gather objective is complete.");

            yield return GatherUntilQuestCompleted(runtime, route, woodItem);

            Assert.AreEqual(QuestState.Completed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
            AssertPanelContains(runtime.QuestLogPanel.transform, QuestState.Completed.ToString());

            yield return OpenGuideDialogue(runtime);
            Assert.IsNotNull(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest), "ClaimReward choice must appear after the gather objective is complete.");
        }

        [UnityTest]
        public IEnumerator QUEST_PLAY_WOOD_004_NpcClaimButtonPaysRewardOnceAndQuestGuiShowsVisibleReward()
        {
            yield return LoadTownWithSlot(2026051007, 2026051008);
            var runtime = FindRuntime();
            var route = FindGatherWoodRoute(runtime.Provider);
            var woodItem = ResolveFirstDropItem(runtime.Inventory, route.Objective.TargetResource);
            var reward = FindFirstItemReward(route.Quest);
            int beforeReward = runtime.Inventory.Inventory.CountOf(reward.Item);

            yield return AcceptGatherWoodQuest(runtime, route);
            yield return GatherUntilQuestCompleted(runtime, route, woodItem);
            yield return OpenGuideDialogue(runtime);
            yield return ClickButton(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest));

            Assert.AreEqual(QuestState.RewardClaimed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
            Assert.AreEqual(beforeReward + reward.Count, runtime.Inventory.Inventory.CountOf(reward.Item));
            AssertPanelContains(runtime.QuestLogPanel.transform, QuestState.RewardClaimed.ToString());
            AssertPanelContains(runtime.QuestLogPanel.transform, reward.Item.DisplayKey);
        }

        [UnityTest]
        public IEnumerator QUEST_PLAY_WOOD_005_RewardClaimedPersistsAfterTownReloadAndCannotBeClaimedTwice()
        {
            yield return LoadTownWithSlot(2026051009, 2026051010);
            var runtime = FindRuntime();
            var route = FindGatherWoodRoute(runtime.Provider);
            var woodItem = ResolveFirstDropItem(runtime.Inventory, route.Objective.TargetResource);
            var reward = FindFirstItemReward(route.Quest);

            yield return AcceptGatherWoodQuest(runtime, route);
            yield return GatherUntilQuestCompleted(runtime, route, woodItem);
            yield return OpenGuideDialogue(runtime);
            yield return ClickButton(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest));
            int afterClaimRewardCount = runtime.Inventory.Inventory.CountOf(reward.Item);
            Assert.AreEqual(QuestState.RewardClaimed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
            Assert.IsTrue(File.Exists(Path.Combine(_saveRoot, TestSlotId, "quest-log.json")), "QuestLog save file must be written after reward claim.");

            yield return OpenGuideDialogue(runtime);
            Assert.IsNull(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest), "ClaimReward choice must disappear after reward is claimed.");
            Assert.AreEqual(afterClaimRewardCount, runtime.Inventory.Inventory.CountOf(reward.Item));

            yield return LoadScene("Town");
            yield return WaitForTownRuntime(10f);
            runtime = FindRuntime();
            route = FindGatherWoodRoute(runtime.Provider);
            reward = FindFirstItemReward(route.Quest);

            Assert.AreEqual(QuestState.RewardClaimed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
            yield return OpenGuideDialogue(runtime);
            Assert.IsNull(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest), "Saved RewardClaimed state must prevent duplicate reward selection after reload.");
            AssertPanelContains(runtime.QuestLogPanel.transform, QuestState.RewardClaimed.ToString());
            AssertPanelContains(runtime.QuestLogPanel.transform, reward.Item.DisplayKey);
        }

        private IEnumerator LoadTownWithSlot(int worldSeed, int tileSeed)
        {
            var service = new SaveService(TestSlotId, _saveRoot);
            var metadata = service.CreateUiMetadata(TestSlotId, new CharacterCustomization(), worldSeed, tileSeed);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);
            yield return LoadScene("Town");
            yield return WaitForTownRuntime(10f);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var npc = GameObject.Find("GuideNpc");
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (player != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    player.GetComponent<PlayerInventory>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    npc != null &&
                    npc.GetComponent<NpcInteractor>() != null &&
                    npc.GetComponent<QuestProvider>() != null &&
                    dialoguePanel != null &&
                    questLogPanel != null &&
                    questLogPanel.QuestLog != null &&
                    EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town runtime did not expose user quest flow objects within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            var npcGo = GameObject.Find("GuideNpc");
            Assert.IsNotNull(player);
            Assert.IsNotNull(npcGo);
            return new RuntimeRefs(
                player,
                player.GetComponent<GatherInteractor>(),
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<PlayerInventory>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                npcGo.GetComponent<NpcInteractor>(),
                npcGo.GetComponent<QuestProvider>(),
                Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenGuideDialogue(RuntimeRefs runtime)
        {
            runtime.Player.transform.position = runtime.Npc.transform.position + new Vector3(0.75f, 0f, 0f);
            yield return null;
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible);
            runtime.Gather.TriggerInteract();
            yield return null;
            Assert.IsTrue(runtime.DialoguePanel.IsOpen);
        }

        private static IEnumerator AcceptGatherWoodQuest(RuntimeRefs runtime, GatherWoodRoute route)
        {
            yield return OpenGuideDialogue(runtime);
            yield return ClickButton(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.AcceptQuest, route.Quest));
            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
        }

        private static IEnumerator GatherUntilQuestCompleted(RuntimeRefs runtime, GatherWoodRoute route, ItemDefinition woodItem)
        {
            int guard = 0;
            while (runtime.QuestLogPanel.QuestLog.GetState(route.Quest) != QuestState.Completed && guard < 80)
            {
                int before = runtime.Inventory.Inventory.CountOf(woodItem);
                yield return GatherTargetResourceUntilInventoryIncreases(runtime, route.Objective.TargetResource, woodItem, before);
                guard++;
            }

            Assert.AreEqual(QuestState.Completed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
        }

        private static IEnumerator GatherTargetResourceUntilInventoryIncreases(RuntimeRefs runtime, ResourceNodeDefinition targetResource, ItemDefinition item, int beforeCount)
        {
            int attempts = 0;
            while (attempts < 80 && runtime.Inventory.Inventory.CountOf(item) <= beforeCount)
            {
                var node = FindTargetResourceNode(targetResource);
                Assert.IsNotNull(node, "Town must contain an active world ResourceNode for the gather wood quest target resource.");
                runtime.Player.transform.position = node.transform.position + new Vector3(0.35f, 0f, 0f);
                yield return null;
                runtime.Gather.TriggerInteract();
                yield return null;
                attempts++;
            }

            Assert.Greater(runtime.Inventory.Inventory.CountOf(item), beforeCount, "World interaction should break a target resource node and add its drop to inventory.");
        }

        private static ResourceNode FindTargetResourceNode(ResourceNodeDefinition targetResource)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].Definition == targetResource && !nodes[i].IsBroken)
                {
                    return nodes[i];
                }
            }

            return null;
        }

        private static Button FindChoiceButtonFor(DialoguePanel panel, DialogueQuestAction action, QuestDefinition quest)
        {
            var dialogue = GetField<DialogueDefinition>(panel, "_dialogue");
            if (dialogue == null || dialogue.Choices == null)
            {
                return null;
            }

            for (int i = 0; i < dialogue.Choices.Length; i++)
            {
                var choice = dialogue.Choices[i];
                if (choice != null && choice.QuestAction == action && choice.Quest == quest)
                {
                    return FindButton(panel.transform, "ChoiceButton_" + i);
                }
            }

            return null;
        }

        private static Button FindButton(Transform root, string name)
        {
            var child = FindChild(root, name);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static GatherWoodRoute FindGatherWoodRoute(QuestProvider provider)
        {
            Assert.IsNotNull(provider);
            for (int i = 0; i < provider.Quests.Length; i++)
            {
                var quest = provider.Quests[i];
                if (quest == null || quest.Objectives == null)
                {
                    continue;
                }

                for (int j = 0; j < quest.Objectives.Length; j++)
                {
                    if (quest.Objectives[j] is GatherQuestObjective objective && objective.TargetResource != null)
                    {
                        return new GatherWoodRoute(quest, objective, j);
                    }
                }
            }

            Assert.Fail("No GatherQuestObjective-backed wood quest found on GuideNpc.");
            return default;
        }

        private static ItemDefinition ResolveFirstDropItem(PlayerInventory inventory, ResourceNodeDefinition resource)
        {
            Assert.IsNotNull(inventory);
            Assert.IsNotNull(resource);
            Assert.IsNotNull(resource.Drops);
            Assert.Greater(resource.Drops.Length, 0, "Gather target resource must define a dropped inventory item.");
            var item = inventory.FindById(resource.Drops[0].ResourceId);
            Assert.IsNotNull(item, "Target resource drop must resolve to an ItemDefinition in the runtime registry.");
            return item;
        }

        private static Rootborn.Game.Quests.Rewards.ItemQuestReward FindFirstItemReward(QuestDefinition quest)
        {
            Assert.IsNotNull(quest);
            Assert.IsNotNull(quest.Rewards);
            for (int i = 0; i < quest.Rewards.Length; i++)
            {
                if (quest.Rewards[i] is Rootborn.Game.Quests.Rewards.ItemQuestReward reward && reward.Item != null)
                {
                    return reward;
                }
            }

            Assert.Fail("Gather wood quest must expose a visible item reward.");
            return null;
        }

        private static void AssertPanelContains(Transform panel, string expected)
        {
            Assert.IsFalse(string.IsNullOrEmpty(expected));
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected))
                {
                    return;
                }
            }

            Assert.Fail("Expected QuestLogPanel visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
        }

        private static T GetField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return field.GetValue(target) as T;
        }

        private readonly struct GatherWoodRoute
        {
            public readonly QuestDefinition Quest;
            public readonly GatherQuestObjective Objective;
            public readonly int ObjectiveIndex;

            public GatherWoodRoute(QuestDefinition quest, GatherQuestObjective objective, int objectiveIndex)
            {
                Quest = quest;
                Objective = objective;
                ObjectiveIndex = objectiveIndex;
            }
        }

        private readonly struct RuntimeRefs
        {
            public readonly GameObject Player;
            public readonly GatherInteractor Gather;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent StudentLife;
            public readonly NpcInteractor Npc;
            public readonly QuestProvider Provider;
            public readonly DialoguePanel DialoguePanel;
            public readonly QuestLogPanel QuestLogPanel;

            public RuntimeRefs(
                GameObject player,
                GatherInteractor gather,
                PlayerInteractionRouter router,
                PlayerInventory inventory,
                StudentLifeProgressComponent studentLife,
                NpcInteractor npc,
                QuestProvider provider,
                DialoguePanel dialoguePanel,
                QuestLogPanel questLogPanel)
            {
                Player = player;
                Gather = gather;
                Router = router;
                Inventory = inventory;
                StudentLife = studentLife;
                Npc = npc;
                Provider = provider;
                DialoguePanel = dialoguePanel;
                QuestLogPanel = questLogPanel;
            }
        }
    }
}
