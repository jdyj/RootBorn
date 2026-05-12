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
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestPortalPersistenceScenarioTests
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-portal-persist", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            var service = new SaveService(TestSlotId, _saveRoot);
            var metadata = service.CreateUiMetadata(TestSlotId, new CharacterCustomization(), 2026051201, 2026051202);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PORTAL_PERSIST_PM_001_TownWoodInventoryAndQuestProgressSurvivePortalTravelToFarm()
        {
            yield return LoadScene("Town");
            yield return WaitForTownRuntime(15f);
            var town = FindRuntime(requireNpc: true);
            var route = FindGatherWoodRoute(town.Provider);
            var wood = town.Inventory.FindById("Wood");
            Assert.IsNotNull(wood, "Registry wiring missing: Wood item must be findable by the player inventory.");

            yield return OpenGuideDialogue(town);
            yield return ClickButton(FindChoiceButtonFor(town.DialoguePanel, DialogueQuestAction.AcceptQuest, route.Quest));
            Assert.AreEqual(QuestState.Active, town.QuestLogPanel.QuestLog.GetState(route.Quest), "Domain quest state did not become Active after the visible accept choice.");

            int beforeWood = town.Inventory.Inventory.CountOf(wood);
            int beforeObjective = town.QuestLogPanel.QuestLog.GetObjectiveCount(route.Quest, route.ObjectiveIndex);
            yield return GatherTargetResourceUntilInventoryIncreases(town, route.Objective.TargetResource, wood, beforeWood);

            int townWood = town.Inventory.Inventory.CountOf(wood);
            int townObjective = town.QuestLogPanel.QuestLog.GetObjectiveCount(route.Quest, route.ObjectiveIndex);
            Assert.Greater(townWood, beforeWood, "Domain state loss check: Town PlayerInventory did not gain wood from the user gather path.");
            Assert.Greater(townObjective, beforeObjective, "Domain state loss check: Town QuestLog did not progress from the user gather path.");
            AssertInventoryUiContains(town.InventoryPanel, wood.Id, townWood, "Town Inventory UI did not reflect the gathered wood count.");
            AssertPanelContains(town.QuestLogPanel.transform, townObjective + " / " + route.Objective.RequiredCount, "Town QuestLog UI did not reflect gathered wood progress.");

            yield return InteractWithPortal("TownReturnPortal", town.Player, town.Router, new Vector3(0f, 1.35f, 0f), "Farm", "Portal path missing: interacting with the Town portal did not load Farm.");
            yield return WaitForFarmRuntime(15f);
            var farm = FindRuntime(requireNpc: false);
            Assert.AreEqual(townWood, farm.Inventory.Inventory.CountOf(wood), "Domain state loss: Farm PlayerInventory did not preserve Town wood count after portal travel.");
            Assert.AreEqual(townObjective, farm.QuestLogPanel.QuestLog.GetObjectiveCount(route.Quest, route.ObjectiveIndex), "Domain state loss: Farm QuestLog did not preserve Town gather progress after portal travel.");
            AssertInventoryUiContains(farm.InventoryPanel, wood.Id, townWood, "UI rebind missing: Farm Inventory UI did not show the preserved wood count.");
            AssertPanelContains(farm.QuestLogPanel.transform, townObjective + " / " + route.Objective.RequiredCount, "UI rebind missing: Farm QuestLog UI did not show preserved quest progress.");

            yield return InteractWithPortal("FarmPortal", farm.Player, farm.Router, new Vector3(1.35f, 0f, 0f), "Town", "Portal path missing: interacting with the Farm portal did not load Town.");
            yield return WaitForTownRuntime(15f);
            var roundTrip = FindRuntime(requireNpc: true);
            Assert.AreEqual(townWood, roundTrip.Inventory.Inventory.CountOf(wood), "Duplicate/loss check: Farm to Town round trip changed the preserved wood count.");
            Assert.AreEqual(townObjective, roundTrip.QuestLogPanel.QuestLog.GetObjectiveCount(route.Quest, route.ObjectiveIndex), "Duplicate/loss check: Farm to Town round trip changed quest progress.");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator InteractWithPortal(string portalName, GameObject player, PlayerInteractionRouter router, Vector3 approachOffset, string destinationScene, string failure)
        {
            var portal = GameObject.Find(portalName);
            Assert.IsNotNull(portal, failure + " Missing portal '" + portalName + "'.");
            Assert.IsNotNull(portal.GetComponent<WorldPortal>(), failure + " Portal object lacks WorldPortal.");
            player.transform.position = portal.transform.position + approachOffset;
            Physics2D.SyncTransforms();
            yield return null;
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, failure + " Portal prompt was not visible.");
            StringAssert.Contains("Enter", router.PromptText, failure + " Prompt did not target portal.");
            Assert.IsTrue(router.TryInteractWithNearest(), failure + " Portal interaction was not accepted.");
            yield return WaitForScene(destinationScene, 15f, failure);
        }
        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string failure = null)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, failure ?? ("Expected active scene to become " + sceneName + "."));
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            yield return WaitForRuntime(timeoutSeconds, true, "TownReturnPortal", "Town runtime did not expose Player, Inventory UI, QuestLog UI, interaction router, and portal within timeout.");
        }

        private static IEnumerator WaitForFarmRuntime(float timeoutSeconds)
        {
            yield return WaitForRuntime(timeoutSeconds, false, "FarmPortal", "Farm runtime did not expose Player, Inventory UI, QuestLog UI, interaction router, and portal within timeout.");
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds, bool requireNpc, string portalName, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var portal = GameObject.Find(portalName);
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                var inventoryPanel = Object.FindFirstObjectByType<ModernUiInventoryPanel>(FindObjectsInactive.Include);
                var npc = GameObject.Find("GuideNpc");
                bool npcReady = !requireNpc || (npc != null && npc.GetComponent<NpcInteractor>() != null && npc.GetComponent<QuestProvider>() != null);
                if (player != null && player.GetComponent<GatherInteractor>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<PlayerInventory>() != null &&
                    portal != null && portal.GetComponent<WorldPortal>() != null && dialoguePanel != null && questLogPanel != null && questLogPanel.QuestLog != null && inventoryPanel != null && EventSystem.current != null && npcReady)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static RuntimeRefs FindRuntime(bool requireNpc)
        {
            var player = GameObject.Find("Player");
            var npcGo = GameObject.Find("GuideNpc");
            Assert.IsNotNull(player);
            if (requireNpc) Assert.IsNotNull(npcGo);
            return new RuntimeRefs(player, player.GetComponent<GatherInteractor>(), player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<PlayerInventory>(), npcGo != null ? npcGo.GetComponent<NpcInteractor>() : null, npcGo != null ? npcGo.GetComponent<QuestProvider>() : null, Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include), Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include), Object.FindFirstObjectByType<ModernUiInventoryPanel>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenGuideDialogue(RuntimeRefs runtime)
        {
            runtime.Player.transform.position = runtime.Npc.transform.position;
            Physics2D.SyncTransforms();
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                runtime.Router.RefreshPromptNow();
                if (runtime.Router.PromptVisible && runtime.Router.PromptText.Contains("Talk")) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(runtime.Router.PromptVisible, "Guide interaction prompt should be visible before accepting the quest.");
            Assert.IsTrue(runtime.Router.PromptText.Contains("Talk"), "Guide interaction prompt should target the NPC talk action before accepting the quest.");
            runtime.Gather.TriggerInteract();

            elapsed = 0f;
            while (!runtime.DialoguePanel.IsOpen && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(runtime.DialoguePanel.IsOpen, "Guide dialogue should open through the player interaction path.");
        }

        private static IEnumerator GatherTargetResourceUntilInventoryIncreases(RuntimeRefs runtime, ResourceNodeDefinition targetResource, ItemDefinition item, int beforeCount)
        {
            int attempts = 0;
            while (attempts < 80 && runtime.Inventory.Inventory.CountOf(item) <= beforeCount)
            {
                var node = FindTargetResourceNode(targetResource);
                Assert.IsNotNull(node, "Town must contain an active ResourceNode for the gather wood quest target resource.");
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
                if (nodes[i] != null && nodes[i].Definition == targetResource && !nodes[i].IsBroken) return nodes[i];
            }

            return null;
        }

        private static GatherWoodRoute FindGatherWoodRoute(QuestProvider provider)
        {
            Assert.IsNotNull(provider);
            for (int i = 0; i < provider.Quests.Length; i++)
            {
                var quest = provider.Quests[i];
                if (quest == null || quest.Objectives == null) continue;
                for (int j = 0; j < quest.Objectives.Length; j++)
                {
                    if (quest.Objectives[j] is GatherQuestObjective objective && objective.TargetResource != null) return new GatherWoodRoute(quest, objective, j);
                }
            }

            Assert.Fail("No GatherQuestObjective-backed quest found on GuideNpc.");
            return default;
        }

        private static Button FindChoiceButtonFor(DialoguePanel panel, DialogueQuestAction action, QuestDefinition quest)
        {
            var dialogue = GetField<DialogueDefinition>(panel, "_dialogue");
            if (dialogue == null || dialogue.Choices == null) return null;
            for (int i = 0; i < dialogue.Choices.Length; i++)
            {
                var choice = dialogue.Choices[i];
                if (choice != null && choice.QuestAction == action && choice.Quest == quest) return FindButton(panel.transform, "ChoiceButton_" + i);
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
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }

            return null;
        }

        private static void AssertInventoryUiContains(ModernUiInventoryPanel panel, string itemId, int expectedCount, string message)
        {
            Assert.IsNotNull(panel, message);
            panel.Show();
            panel.Refresh();
            var slot = FindChild(panel.transform, "Slot_" + itemId);
            Assert.IsNotNull(slot, message + " Missing slot for " + itemId + ".");
            if (expectedCount > 1)
            {
                var count = FindChild(slot, "Count");
                Assert.IsNotNull(count, message + " Missing count label for " + itemId + ".");
                Assert.AreEqual(expectedCount.ToString(), count.GetComponent<Text>().text, message);
            }
        }

        private static void AssertPanelContains(Transform panel, string expected, string message)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }

            Assert.Fail(message + " Expected visible text to contain '" + expected + "'.");
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
            public readonly ModernUiInventoryPanel InventoryPanel;

            public RuntimeRefs(GameObject player, GatherInteractor gather, PlayerInteractionRouter router, PlayerInventory inventory, NpcInteractor npc, QuestProvider provider, DialoguePanel dialoguePanel, QuestLogPanel questLogPanel, ModernUiInventoryPanel inventoryPanel)
            {
                Player = player;
                Gather = gather;
                Router = router;
                Inventory = inventory;
                Npc = npc;
                Provider = provider;
                DialoguePanel = dialoguePanel;
                QuestLogPanel = questLogPanel;
                InventoryPanel = inventoryPanel;
            }
        }
    }
}
