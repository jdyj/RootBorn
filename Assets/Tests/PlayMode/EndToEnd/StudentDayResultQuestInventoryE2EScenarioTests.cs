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
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class StudentDayResultQuestInventoryE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-student-day-quest-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator STUDENT_DAY_E2E_012_QuestRewardInventorySummaryAppearsAfterActualPlayAndDoesNotDuplicateAfterReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 15f, "STUDENT-DAY-E2E-012 failed: SaveSlot New Game did not enter Town.");
            yield return WaitForTownRuntime(15f);
            LogAssert.ignoreFailingMessages = false;

            var town = FindTownRuntime();
            var study = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, study.transform.position, 0.25f, "STUDENT-DAY-E2E-012 failed: keyboard movement could not reach study activity.");
            town.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, study.LastResult.Kind, "STUDENT-DAY-E2E-012 failed: activity did not apply through input before day summary.");

            var route = FindGatherWoodRoute(town.Provider);
            var woodItem = ResolveFirstDropItem(town.Inventory, route.Objective.TargetResource);
            var reward = FindFirstItemReward(route.Quest);
            int rewardBeforeClaim = town.Inventory.Inventory.CountOf(reward.Item);

            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, town.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.2f, "STUDENT-DAY-E2E-012 failed: keyboard movement could not reach GuideNpc.");
            yield return OpenGuideDialogueThroughInput(town, keyboard, "STUDENT-DAY-E2E-012 failed: GuideNpc dialogue did not open through prompt/input.");
            yield return ClickButton(FindChoiceButtonFor(town.DialoguePanel, DialogueQuestAction.AcceptQuest, route.Quest));
            Assert.AreEqual(QuestState.Active, town.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-DAY-E2E-012 failed: visible accept choice did not activate quest.");

            yield return GatherUntilQuestCompletedThroughMovement(town, keyboard, route, woodItem);
            Assert.AreEqual(QuestState.Completed, town.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-DAY-E2E-012 failed: actual resource interaction did not complete quest.");

            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, town.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.2f, "STUDENT-DAY-E2E-012 failed: keyboard movement could not return to GuideNpc for reward.");
            yield return OpenGuideDialogueThroughInput(town, keyboard, "STUDENT-DAY-E2E-012 failed: GuideNpc dialogue did not reopen for reward.");
            yield return ClickButton(FindChoiceButtonFor(town.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest));
            int rewardAfterClaim = town.Inventory.Inventory.CountOf(reward.Item);
            Assert.AreEqual(rewardBeforeClaim + reward.Count, rewardAfterClaim, "STUDENT-DAY-E2E-012 failed: reward was not paid exactly once before day end.");
            Assert.AreEqual(QuestState.RewardClaimed, town.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-DAY-E2E-012 failed: reward claim state did not update before day end.");

            var dayEnd = GameObject.Find("StudentDayEndBoard").GetComponent<StudentDayEndInteractor>();
            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, dayEnd.transform.position, 0.25f, "STUDENT-DAY-E2E-012 failed: keyboard movement could not reach day-end board.");
            town.Router.RefreshPromptNow();
            Assert.IsTrue(town.Router.PromptVisible, "STUDENT-DAY-E2E-012 failed: day-end prompt was not visible.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "STUDENT-DAY-E2E-012 failed: result panel did not open from actual day-end interaction.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, study.Activity.Id, "STUDENT-DAY-E2E-012 failed: result UI did not include played activity.");
            AssertPanelContains(panel.transform, route.Quest.DisplayNameKey, "STUDENT-DAY-E2E-012 failed: result UI did not include changed quest.");
            AssertPanelContains(panel.transform, QuestState.RewardClaimed.ToString(), "STUDENT-DAY-E2E-012 failed: result UI did not include reward claimed quest state.");
            AssertPanelContains(panel.transform, reward.Item.DisplayKey, "STUDENT-DAY-E2E-012 failed: result UI did not include reward item.");
            AssertPanelContains(panel.transform, "+" + reward.Count, "STUDENT-DAY-E2E-012 failed: result UI did not include reward inventory delta.");
            AssertPanelContains(panel.transform, woodItem.DisplayKey, "STUDENT-DAY-E2E-012 failed: result UI did not include gathered inventory delta.");
            AssertPanelContains(panel.transform, "Next Day", "STUDENT-DAY-E2E-012 failed: result UI did not include next-day guidance.");

            yield return ClickButton(FindButton(panel.transform, "NextDayButton"));
            Assert.AreEqual(StudentDayState.InProgress, town.Student.Progress.DayState, "STUDENT-DAY-E2E-012 failed: next-day button did not resume in-progress state.");
            Assert.AreEqual(rewardAfterClaim, town.Inventory.Inventory.CountOf(reward.Item), "STUDENT-DAY-E2E-012 failed: next-day button changed reward inventory count.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            LogAssert.ignoreFailingMessages = true;
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "STUDENT-DAY-E2E-012 failed: SaveSlot Load did not re-enter Town.");
            yield return WaitForTownRuntime(15f);
            LogAssert.ignoreFailingMessages = false;

            var reloaded = FindTownRuntime();
            route = FindGatherWoodRoute(reloaded.Provider);
            reward = FindFirstItemReward(route.Quest);
            Assert.AreEqual(QuestState.RewardClaimed, reloaded.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-DAY-E2E-012 failed: quest reward state did not survive slot reload.");
            Assert.AreEqual(rewardAfterClaim, reloaded.Inventory.Inventory.CountOf(reward.Item), "STUDENT-DAY-E2E-012 failed: reward inventory count changed after slot reload.");
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.2f, "STUDENT-DAY-E2E-012 failed: keyboard movement could not reach GuideNpc after reload.");
            yield return OpenGuideDialogueThroughInput(reloaded, keyboard, "STUDENT-DAY-E2E-012 failed: GuideNpc dialogue did not open after reload.");
            Assert.IsNull(FindChoiceButtonFor(reloaded.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest), "STUDENT-DAY-E2E-012 failed: claimed reward was offered again after reload.");
            Assert.AreEqual(rewardAfterClaim, reloaded.Inventory.Inventory.CountOf(reward.Item), "STUDENT-DAY-E2E-012 failed: duplicate reward check changed inventory count.");
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
                if (player != null &&
                    player.GetComponent<PlayerController>() != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    player.GetComponent<PlayerInventory>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    npc != null && npc.GetComponent<NpcInteractor>() != null && npc.GetComponent<QuestProvider>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    dialoguePanel != null && questLogPanel != null && questLogPanel.QuestLog != null && resultPanel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town runtime did not expose full student quest day-result loop objects within timeout.");
        }

        private static TownRuntime FindTownRuntime()
        {
            var player = GameObject.Find("Player");
            var npc = GameObject.Find("GuideNpc");
            Assert.IsNotNull(player);
            Assert.IsNotNull(npc);
            return new TownRuntime(
                player,
                player.GetComponent<GatherInteractor>(),
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<PlayerInventory>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                npc.GetComponent<NpcInteractor>(),
                npc.GetComponent<QuestProvider>(),
                Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
        }

        private IEnumerator OpenGuideDialogueThroughInput(TownRuntime runtime, Keyboard keyboard, string failure)
        {
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, failure + " Prompt was not visible.");
            StringAssert.Contains("Talk", runtime.Router.PromptText, failure + " Prompt did not target Talk.");
            yield return PressInteractKey(keyboard);
            float elapsed = 0f;
            while (!runtime.DialoguePanel.IsOpen && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(runtime.DialoguePanel.IsOpen, failure);
        }

        private IEnumerator GatherUntilQuestCompletedThroughMovement(TownRuntime runtime, Keyboard keyboard, GatherWoodRoute route, ItemDefinition woodItem)
        {
            int guard = 0;
            while (runtime.QuestLogPanel.QuestLog.GetState(route.Quest) != QuestState.Completed && guard < 80)
            {
                int before = runtime.Inventory.Inventory.CountOf(woodItem);
                var node = FindTargetResourceNode(route.Objective.TargetResource);
                Assert.IsNotNull(node, "STUDENT-DAY-E2E-012 failed: no active target resource node remained for quest progress.");
                yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, node.transform.position + new Vector3(0.35f, 0f, 0f), 0.25f, "STUDENT-DAY-E2E-012 failed: keyboard movement could not reach target resource node.");
                int hitGuard = 0;
                while (runtime.Inventory.Inventory.CountOf(woodItem) <= before && hitGuard < 60)
                {
                    runtime.Router.RefreshPromptNow();
                    yield return PressInteractKey(keyboard);
                    yield return null;
                    hitGuard++;
                }

                Assert.Greater(runtime.Inventory.Inventory.CountOf(woodItem), before, "STUDENT-DAY-E2E-012 failed: repeated resource interaction input did not add target inventory.");
                guard++;
            }
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 900 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    if (activeKey != Key.None)
                    {
                        Release(ControlFor(keyboard, activeKey));
                    }
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }

                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            if (activeKey != Key.None)
            {
                Release(ControlFor(keyboard, activeKey));
            }

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

        private static IEnumerator WaitForDayResultPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
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

            Assert.Fail("No GatherQuestObjective-backed quest found on GuideNpc.");
            return default;
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

        private static ItemQuestReward FindFirstItemReward(QuestDefinition quest)
        {
            Assert.IsNotNull(quest);
            Assert.IsNotNull(quest.Rewards);
            for (int i = 0; i < quest.Rewards.Length; i++)
            {
                if (quest.Rewards[i] is ItemQuestReward reward && reward.Item != null)
                {
                    return reward;
                }
            }

            Assert.Fail("Gather wood quest must expose a visible item reward.");
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

        private static Button FindButton(string cardName, string buttonName)
        {
            var card = GameObject.Find(cardName);
            Assert.IsNotNull(card, "Save slot UI card missing: " + cardName);
            return FindButton(card.transform, buttonName);
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

        private static void AssertPanelContains(Transform panel, string expected, string message)
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

            Assert.Fail(message + " Expected visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
        }

        private static T GetField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return field.GetValue(target) as T;
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            if (key == Key.E) return keyboard.eKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

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
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName)
                {
                    Object.DestroyImmediate(transform.gameObject);
                }
            }
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

        private readonly struct TownRuntime
        {
            public readonly GameObject Player;
            public readonly GatherInteractor Gather;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent Student;
            public readonly NpcInteractor Npc;
            public readonly QuestProvider Provider;
            public readonly DialoguePanel DialoguePanel;
            public readonly QuestLogPanel QuestLogPanel;

            public TownRuntime(GameObject player, GatherInteractor gather, PlayerInteractionRouter router, PlayerInventory inventory, StudentLifeProgressComponent student, NpcInteractor npc, QuestProvider provider, DialoguePanel dialoguePanel, QuestLogPanel questLogPanel)
            {
                Player = player;
                Gather = gather;
                Router = router;
                Inventory = inventory;
                Student = student;
                Npc = npc;
                Provider = provider;
                DialoguePanel = dialoguePanel;
                QuestLogPanel = questLogPanel;
            }
        }
    }
}
