using System.Collections;
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
using Rootborn.Game.World;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestPortalPlayerStateScenarioTests
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-quest-portal-player-state", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            var service = new SaveService(TestSlotId, _saveRoot);
            var metadata = service.CreateUiMetadata(TestSlotId, new CharacterCustomization(), 2026051111, 2026051112);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);
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
        public IEnumerator QUEST_PLAY_WOOD_006_PortalTravelKeepsPlayerInventoryAndGatherProgressUsesTravellingPlayerQuestLog()
        {
            yield return LoadScene("Farm");
            yield return WaitForFarmRuntime(10f);

            var farmPlayer = GameObject.Find("Player");
            var farmInventory = farmPlayer.GetComponent<PlayerInventory>();
            var wood = farmInventory.FindById("Wood");
            Assert.IsNotNull(wood, "Wood item must be available from the runtime registry.");
            int seededWood = farmInventory.Inventory.CountOf(wood) + 7;
            farmInventory.Inventory.Add(wood, 7);

            var portal = GameObject.Find("FarmPortal").GetComponent<WorldPortal>();
            farmPlayer.transform.position = portal.transform.position + new Vector3(1.2f, 0f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            farmPlayer.GetComponent<GatherInteractor>().TriggerInteract();

            yield return WaitForScene("Town", 10f);
            yield return WaitForTownRuntime(10f);

            Assert.AreEqual(1, CountObjectsNamed("Player"));
            var runtime = FindTownRuntime();
            Assert.AreSame(farmPlayer, runtime.Player, "Portal travel must keep the same player instance for player-owned state.");
            Assert.AreEqual(seededWood, runtime.Inventory.Inventory.CountOf(wood), "Player-owned inventory must survive portal scene changes.");

            var route = FindGatherWoodRoute(runtime.Provider);
            yield return OpenGuideDialogue(runtime);
            yield return ClickButton(FindChoiceButtonFor(runtime.DialoguePanel, DialogueQuestAction.AcceptQuest, route.Quest));
            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));

            int beforeObjective = runtime.QuestLogPanel.QuestLog.GetObjectiveCount(route.Quest, route.ObjectiveIndex);
            int beforeWood = runtime.Inventory.Inventory.CountOf(wood);
            yield return GatherTargetResourceUntilInventoryIncreases(runtime, route.Objective.TargetResource, wood, beforeWood);

            Assert.Greater(runtime.Inventory.Inventory.CountOf(wood), beforeWood, "Travelling player should receive gathered wood.");
            Assert.Greater(runtime.QuestLogPanel.QuestLog.GetObjectiveCount(route.Quest, route.ObjectiveIndex), beforeObjective, "Travelling player's GatherInteractor must be bound to the visible QuestLogPanel QuestLog.");
            AssertPanelContains(runtime.QuestLogPanel.transform, "1 / " + route.Objective.RequiredCount);
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

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForFarmRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var portal = GameObject.Find("FarmPortal");
                if (player != null &&
                    player.GetComponent<PlayerInventory>() != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    portal != null && portal.GetComponent<WorldPortal>() != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Farm runtime did not expose Player, inventory, gather interactor, and portal within timeout.");
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

            Assert.Fail("Town runtime did not expose portal quest flow objects within timeout.");
        }

        private static RuntimeRefs FindTownRuntime()
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
                npcGo.GetComponent<NpcInteractor>(),
                npcGo.GetComponent<QuestProvider>(),
                Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenGuideDialogue(RuntimeRefs runtime)
        {
            runtime.Player.transform.position = runtime.Npc.transform.position;
            Physics2D.SyncTransforms();
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                runtime.Router.RefreshPromptNow();
                if (runtime.Router.PromptVisible && runtime.Router.PromptText.Contains("Talk"))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(runtime.Router.PromptVisible);
            Assert.IsTrue(runtime.Router.PromptText.Contains("Talk"));
            runtime.Gather.TriggerInteract();

            elapsed = 0f;
            while (!runtime.DialoguePanel.IsOpen && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(runtime.DialoguePanel.IsOpen);
        }

        private static IEnumerator GatherTargetResourceUntilInventoryIncreases(RuntimeRefs runtime, ResourceNodeDefinition targetResource, ItemDefinition item, int beforeCount)
        {
            int attempts = 0;
            while (attempts < 80 && runtime.Inventory.Inventory.CountOf(item) <= beforeCount)
            {
                var node = FindTargetResourceNode(targetResource);
                Assert.IsNotNull(node, "Town must contain an active world ResourceNode for the gather wood quest target resource.");
                runtime.Player.transform.position = node.transform.position + new Vector3(0.35f, 0f, 0f);
                Physics2D.SyncTransforms();
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

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;
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

        private static void AssertPanelContains(Transform panel, string expected)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected))
                {
                    return;
                }
            }

            Assert.Fail("Expected QuestLogPanel visible text to contain '" + expected + "'.");
        }

        private static int CountObjectsNamed(string name)
        {
            int count = 0;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name)
                {
                    count++;
                }
            }

            return count;
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
            public readonly NpcInteractor Npc;
            public readonly QuestProvider Provider;
            public readonly DialoguePanel DialoguePanel;
            public readonly QuestLogPanel QuestLogPanel;

            public RuntimeRefs(
                GameObject player,
                GatherInteractor gather,
                PlayerInteractionRouter router,
                PlayerInventory inventory,
                NpcInteractor npc,
                QuestProvider provider,
                DialoguePanel dialoguePanel,
                QuestLogPanel questLogPanel)
            {
                Player = player;
                Gather = gather;
                Router = router;
                Inventory = inventory;
                Npc = npc;
                Provider = provider;
                DialoguePanel = dialoguePanel;
                QuestLogPanel = questLogPanel;
            }
        }
    }
}
