using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using Rootborn.UI.WorldState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class QuestDrivenWorldStateE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-world-state-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            CleanupResidue();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WORLD_STATE_E2E_001_009_SaveSlotQuestClaimShowsWorldChangeReloadsAndDedupes()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var route = FindWorldStateGatherRoute(runtime.Provider);
            var flag = route.WorldReward.Flags[0];
            var itemReward = FindFirstItemReward(route.Quest);
            var woodItem = ResolveFirstDropItem(runtime.Inventory, route.Objective.TargetResource);

            yield return OpenGuideDialogueByKeyboard(runtime, keyboard);
            yield return ClickButton(FindFirstChoiceButton(runtime.DialoguePanel));
            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(route.Quest), "WORLD-STATE-E2E-003 failed: first visible dialogue button did not accept the world-state quest.");

            yield return GatherUntilQuestCompletedByKeyboard(runtime, keyboard, route, woodItem);
            yield return OpenGuideDialogueByKeyboard(runtime, keyboard);
            yield return ClickButton(FindFirstChoiceButton(runtime.DialoguePanel));
            Assert.AreEqual(QuestState.RewardClaimed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest), "WORLD-STATE-E2E-004 failed: claim path did not mark the quest reward claimed.");

            string playerId = ResolvePlayerId(runtime.Inventory);
            var progress = WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            Assert.IsTrue(progress.IsActive(flag), "WORLD-STATE-E2E-004 failed: quest reward did not activate the world-state flag.");
            Assert.AreEqual(1, progress.ActiveFlagCount, "WORLD-STATE-E2E-008 failed: first claim should activate exactly one world-state flag.");
            string worldStateFile = Path.Combine(_saveRoot, TestSlotId, WorldStateProgressPersistence.FileNameFor(playerId));
            Assert.IsTrue(File.Exists(worldStateFile), "WORLD-STATE-E2E-007 failed: world-state save file was not written after reward claim.");
            StringAssert.Contains(flag.Id, File.ReadAllText(worldStateFile), "WORLD-STATE-E2E-007 failed: world-state save file does not contain the activated flag.");

            yield return OpenDayResultByKeyboard(runtime, keyboard);
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            Assert.AreEqual(1, resultPanel.GetWorldStateCardCountForTests(), "WORLD-STATE-E2E-004 failed: day-result UI did not render the world-state change as a common card.");
            StringAssert.Contains(flag.DisplayNameKey, resultPanel.GetWorldStateTextForTests(), "WORLD-STATE-E2E-004 failed: day-result world-state card did not use flag display data.");

            yield return ClickButton(FindButton("WorldStateLogButton"));
            var worldLog = Object.FindFirstObjectByType<WorldStateLogPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(worldLog, "WORLD-STATE-E2E-006 failed: world-state log panel was not installed.");
            Assert.AreEqual(1, worldLog.CardCountForTests, "WORLD-STATE-E2E-006 failed: world-state log did not render the same change as a common card.");
            StringAssert.Contains(flag.DisplayNameKey, worldLog.TextForTests, "WORLD-STATE-E2E-006 failed: world-state log card did not use flag display data.");

            Assert.IsNotNull(GameObject.Find("WorldStateChange_" + SanitizeObjectName(flag.Id)), "WORLD-STATE-E2E-005 failed: activated world-state flag was not reflected in a playable scene object.");

            int afterClaimItemCount = runtime.Inventory.Inventory.CountOf(itemReward.Item);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "WORLD-STATE-E2E-007 failed: save slot UI did not reload Town.");
            yield return WaitForTownRuntime(15f);

            var reloaded = FindRuntime();
            route = FindWorldStateGatherRoute(reloaded.Provider);
            flag = route.WorldReward.Flags[0];
            playerId = ResolvePlayerId(reloaded.Inventory);
            progress = WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            Assert.IsTrue(progress.IsActive(flag), "WORLD-STATE-E2E-007 failed: activated world-state flag did not survive reload.");
            Assert.AreEqual(QuestState.RewardClaimed, reloaded.QuestLogPanel.QuestLog.GetState(route.Quest), "WORLD-STATE-E2E-008 failed: reward-claimed quest state did not survive reload.");
            Assert.AreEqual(afterClaimItemCount, reloaded.Inventory.Inventory.CountOf(itemReward.Item), "WORLD-STATE-E2E-008 failed: reload changed the item reward count.");

            yield return ClickButton(FindButton("WorldStateLogButton"));
            worldLog = Object.FindFirstObjectByType<WorldStateLogPanel>(FindObjectsInactive.Include);
            Assert.AreEqual(1, worldLog.CardCountForTests, "WORLD-STATE-E2E-007 failed: world-state log duplicated or lost the card after reload.");
            Assert.AreEqual(1, progress.ActiveFlagCount, "WORLD-STATE-E2E-008 failed: duplicate reward path created another world-state flag record.");
        }

        private static IEnumerator StartNewTownFromSaveSlotUi()
        {
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 15f, "WORLD-STATE-E2E-001 failed: save slot UI did not enter Town.");
            yield return WaitForTownRuntime(15f);
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var npc = GameObject.Find("GuideNpc");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<GatherInteractor>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && npc != null && npc.GetComponent<NpcInteractor>() != null && npc.GetComponent<QuestProvider>() != null && dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && dialoguePanel != null && questLogPanel != null && questLogPanel.QuestLog != null && resultPanel != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("World-state E2E runtime did not expose player, guide NPC, quest UI, and day-end UI within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            var npcGo = GameObject.Find("GuideNpc");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(npcGo);
            Assert.IsNotNull(dayEnd);
            return new RuntimeRefs(player, player.GetComponent<GatherInteractor>(), player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<PlayerInventory>(), player.GetComponent<StudentLifeProgressComponent>(), npcGo.GetComponent<NpcInteractor>(), npcGo.GetComponent<QuestProvider>(), Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include), Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include), dayEnd.GetComponent<StudentDayEndInteractor>());
        }

        private IEnumerator OpenGuideDialogueByKeyboard(RuntimeRefs runtime, Keyboard keyboard)
        {
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.3f, "WORLD-STATE-E2E-002 failed: actual keyboard movement could not reach guide NPC.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "WORLD-STATE-E2E-003 failed: guide NPC prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(runtime.DialoguePanel, 3f, "WORLD-STATE-E2E-003 failed: dialogue panel did not open through actual input.");
            yield return null;
        }

        private IEnumerator OpenDayResultByKeyboard(RuntimeRefs runtime, Keyboard keyboard)
        {
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "WORLD-STATE-E2E-004 failed: actual keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "WORLD-STATE-E2E-004 failed: day-end prompt was not visible.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "WORLD-STATE-E2E-004 failed: day-result UI did not open after actual day-end interaction.");
        }

        private IEnumerator GatherUntilQuestCompletedByKeyboard(RuntimeRefs runtime, Keyboard keyboard, WorldStateGatherRoute route, ItemDefinition item)
        {
            int questGuard = 0;
            while (runtime.QuestLogPanel.QuestLog.GetState(route.Quest) != QuestState.Completed && questGuard < 80)
            {
                int before = runtime.Inventory.Inventory.CountOf(item);
                int interactionGuard = 0;
                while (runtime.Inventory.Inventory.CountOf(item) <= before && interactionGuard < 80)
                {
                    var node = FindTargetResourceNode(route.Objective.TargetResource);
                    Assert.IsNotNull(node, "WORLD-STATE-E2E-003 failed: Town has no active resource node for the quest target.");
                    yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, node.transform.position + new Vector3(0.35f, 0f, 0f), 0.3f, "WORLD-STATE-E2E-003 failed: keyboard movement could not reach target resource node.");
                    runtime.Router.RefreshPromptNow();
                    yield return PressInteractKey(keyboard);
                    yield return null;
                    interactionGuard++;
                }
                Assert.Greater(runtime.Inventory.Inventory.CountOf(item), before, "WORLD-STATE-E2E-003 failed: resource interaction did not add the target item.");
                questGuard++;
            }
            Assert.AreEqual(QuestState.Completed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
        }

        private static ResourceNode FindTargetResourceNode(ResourceNodeDefinition targetResource)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++) if (nodes[i] != null && nodes[i].Definition == targetResource && !nodes[i].IsBroken) return nodes[i];
            return null;
        }

        private static IEnumerator WaitForDialoguePanel(DialoguePanel panel, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static IEnumerator WaitForDayResultPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static WorldStateGatherRoute FindWorldStateGatherRoute(QuestProvider provider)
        {
            Assert.IsNotNull(provider);
            for (int i = 0; i < provider.Quests.Length; i++)
            {
                var quest = provider.Quests[i];
                if (quest == null || quest.Objectives == null || quest.Rewards == null) continue;
                WorldStateChangeQuestReward worldReward = null;
                ItemQuestReward itemReward = null;
                for (int r = 0; r < quest.Rewards.Length; r++)
                {
                    if (quest.Rewards[r] is WorldStateChangeQuestReward candidate && candidate.Flags.Length > 0 && candidate.Flags[0] != null) worldReward = candidate;
                    if (quest.Rewards[r] is ItemQuestReward itemCandidate && itemCandidate.Item != null) itemReward = itemCandidate;
                }
                if (worldReward == null || itemReward == null) continue;
                for (int j = 0; j < quest.Objectives.Length; j++) if (quest.Objectives[j] is GatherQuestObjective objective && objective.TargetResource != null) return new WorldStateGatherRoute(quest, objective, j, worldReward);
            }
            Assert.Fail("WORLD-STATE-E2E-009 failed: no registry-backed gather quest exposes both item and WorldStateChangeQuestReward data.");
            return default;
        }

        private static Button FindFirstChoiceButton(DialoguePanel panel)
        {
            var root = FindChild(panel.transform, "ChoiceButtons");
            Assert.IsNotNull(root, "WORLD-STATE-E2E-003 failed: dialogue choice root missing.");
            var buttons = root.GetComponentsInChildren<Button>(true);
            Assert.Greater(buttons.Length, 0, "WORLD-STATE-E2E-003 failed: no visible dialogue choice buttons were available.");
            return buttons[0];
        }

        private static ItemDefinition ResolveFirstDropItem(PlayerInventory inventory, ResourceNodeDefinition resource)
        {
            var item = inventory.FindById(resource.Drops[0].ResourceId);
            Assert.IsNotNull(item);
            return item;
        }

        private static ItemQuestReward FindFirstItemReward(QuestDefinition quest)
        {
            for (int i = 0; i < quest.Rewards.Length; i++) if (quest.Rewards[i] is ItemQuestReward reward && reward.Item != null) return reward;
            Assert.Fail("WORLD-STATE-E2E expected an item reward on the quest.");
            return null;
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 720 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure);
        }

        private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            Press(keyboard.eKey);
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Release(keyboard.eKey);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, failure);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(string objectName)
        {
            var go = GameObject.Find(objectName);
            Assert.IsNotNull(go, "UI button missing: " + objectName);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, objectName + " does not have Button component.");
            return button;
        }

        private static Button FindButton(string cardName, string buttonName)
        {
            var card = GameObject.Find(cardName);
            Assert.IsNotNull(card, "Save slot UI card missing: " + cardName);
            return FindButton(card.transform, buttonName);
        }

        private static Button FindButton(Transform root, string name)
        {
            var child = FindChild(root, name);
            Assert.IsNotNull(child, "UI button missing: " + name);
            return child.GetComponent<Button>();
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

        private static string ResolvePlayerId(PlayerInventory inventory)
        {
            var identity = inventory.GetComponent<PlayerIdentity>();
            return identity != null ? identity.PlayerId : "player";
        }

        private static string SanitizeObjectName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
            DestroyAllNamed("WorldStateLogPanel");
            DestroyAllNamed("WorldStateLogButton");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--) if (transforms[i] != null && transforms[i].name == objectName) Object.DestroyImmediate(transforms[i].gameObject);
        }

        private readonly struct WorldStateGatherRoute
        {
            public readonly QuestDefinition Quest;
            public readonly GatherQuestObjective Objective;
            public readonly WorldStateChangeQuestReward WorldReward;
            public WorldStateGatherRoute(QuestDefinition quest, GatherQuestObjective objective, int objectiveIndex, WorldStateChangeQuestReward worldReward)
            {
                Quest = quest;
                Objective = objective;
                WorldReward = worldReward;
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
            public readonly StudentDayEndInteractor DayEnd;
            public RuntimeRefs(GameObject player, GatherInteractor gather, PlayerInteractionRouter router, PlayerInventory inventory, StudentLifeProgressComponent studentLife, NpcInteractor npc, QuestProvider provider, DialoguePanel dialoguePanel, QuestLogPanel questLogPanel, StudentDayEndInteractor dayEnd)
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
                DayEnd = dayEnd;
            }
        }
    }
}
