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
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class AAStudentFullProgressionE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-student-full-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
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
            CleanupResidue();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator STUDENT_E2E_001_012_FullPlayableStudentQuestPortalSaveRewardLoop()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            var saveSlots = SaveSlotSelectPanel.EnsureInScene();
            saveSlots.Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 15f, "STUDENT-E2E-001 failed: SaveSlot New Game did not enter Town.");
            yield return WaitForTownRuntime(15f);
            LogAssert.ignoreFailingMessages = false;

            var town = FindTownRuntime();
            var study = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            int initialTrait = town.Student.Progress.GetTraitValue(study.PrimaryTrait);
            int initialSkill = town.Student.Progress.GetSkillValue(study.PrimarySkill);

            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, study.transform.position, 0.25f, "STUDENT-E2E-002 failed: keyboard movement could not reach the study activity.");
            town.Router.RefreshPromptNow();
            Assert.IsTrue(town.Router.PromptVisible, "STUDENT-E2E-002 failed: study prompt was not visible after keyboard movement.");
            StringAssert.Contains("Study Basics", town.Router.PromptText, "STUDENT-E2E-002 failed: prompt did not target the study activity.");
            yield return PressInteractKey(keyboard);
            yield return null;

            int studiedTrait = town.Student.Progress.GetTraitValue(study.PrimaryTrait);
            int studiedSkill = town.Student.Progress.GetSkillValue(study.PrimarySkill);
            Assert.AreEqual(LifeActivityResultKind.Applied, study.LastResult.Kind, "STUDENT-E2E-003 failed: study interaction did not apply through input.");
            Assert.Greater(studiedTrait, initialTrait, "STUDENT-E2E-004 failed: study did not increase trait domain state.");
            Assert.Greater(studiedSkill, initialSkill, "STUDENT-E2E-004 failed: study did not increase skill domain state.");
            Assert.IsTrue(town.Student.Progress.IsCareerHintUnlocked(study.PrimaryCareer), "STUDENT-E2E-004 failed: study did not unlock career hint domain state.");

            var route = FindGatherWoodRoute(town.Provider);
            var woodItem = ResolveFirstDropItem(town.Inventory, route.Objective.TargetResource);
            var reward = FindFirstItemReward(route.Quest);
            int rewardBeforeClaim = town.Inventory.Inventory.CountOf(reward.Item);

            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, town.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.2f, "STUDENT-E2E-005 failed: keyboard movement could not reach GuideNpc.");
            yield return OpenGuideDialogueThroughInput(town, keyboard, "STUDENT-E2E-005 failed: GuideNpc dialogue did not open through prompt/input.");
            yield return ClickButton(FindChoiceButtonFor(town.DialoguePanel, DialogueQuestAction.AcceptQuest, route.Quest));
            Assert.AreEqual(QuestState.Active, town.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-E2E-005 failed: dialogue button did not accept quest in domain state.");
            AssertPanelContains(town.QuestLogPanel.transform, "0 / " + route.Objective.RequiredCount, "STUDENT-E2E-007 failed: QuestLog UI did not show accepted quest progress.");

            yield return GatherUntilQuestCompletedThroughMovement(town, keyboard, route, woodItem);
            Assert.AreEqual(QuestState.Completed, town.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-E2E-006 failed: world resource interactions did not complete quest domain state.");
            AssertPanelContains(town.QuestLogPanel.transform, "1 / " + route.Objective.RequiredCount, "STUDENT-E2E-007 failed: QuestLog UI did not show gathered quest progress.");

            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, town.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.2f, "STUDENT-E2E-012 failed: keyboard movement could not return to GuideNpc for reward claim.");
            yield return OpenGuideDialogueThroughInput(town, keyboard, "STUDENT-E2E-012 failed: GuideNpc dialogue did not reopen for reward claim.");
            yield return ClickButton(FindChoiceButtonFor(town.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest));
            int rewardAfterClaim = town.Inventory.Inventory.CountOf(reward.Item);
            Assert.AreEqual(QuestState.RewardClaimed, town.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-E2E-012 failed: reward claim did not set RewardClaimed domain state.");
            Assert.AreEqual(rewardBeforeClaim + reward.Count, rewardAfterClaim, "STUDENT-E2E-012 failed: reward item was not paid exactly once.");

            var townPortal = GameObject.Find("TownReturnPortal");
            Vector3 portalPromptPosition = townPortal.transform.position + new Vector3(0f, 1.35f, 0f);
            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, new Vector3(town.Player.transform.position.x, portalPromptPosition.y, 0f), 0.3f, "STUDENT-E2E-008 failed: keyboard movement could not reach the safe portal approach lane.");
            yield return WalkPlayerWithKeyboardTo(town.Player, keyboard, portalPromptPosition, 0.3f, "STUDENT-E2E-008 failed: keyboard movement could not reach Town portal prompt range.");
            Assert.AreEqual("Town", SceneManager.GetActiveScene().name, "STUDENT-E2E-008 failed: movement entered the portal trigger before interaction input.");
            town.Router.RefreshPromptNow();
            Assert.IsTrue(town.Router.PromptVisible, "STUDENT-E2E-008 failed: portal prompt was not visible before input.");
            StringAssert.Contains("Enter", town.Router.PromptText, "STUDENT-E2E-008 failed: prompt did not target portal interaction.");
            yield return PressInteractKey(keyboard);
            yield return WaitForScene("Farm", 15f, "STUDENT-E2E-008 failed: portal interaction input did not load Farm.");
            yield return WaitForFarmRuntime(15f);

            var farm = FindFarmRuntime();
            Assert.AreEqual(rewardAfterClaim, farm.Inventory.Inventory.CountOf(reward.Item), "STUDENT-E2E-009 failed: reward inventory did not survive portal transition.");
            Assert.AreEqual(QuestState.RewardClaimed, farm.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-E2E-009 failed: quest state did not survive portal transition.");
            Assert.AreEqual(studiedTrait, farm.Student.Progress.GetTraitValue(study.PrimaryTrait), "STUDENT-E2E-009 failed: student trait did not survive portal transition.");
            AssertPanelContains(farm.QuestLogPanel.transform, QuestState.RewardClaimed.ToString(), "STUDENT-E2E-009 failed: Farm QuestLog UI did not show preserved reward state.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            string questFile = Path.Combine(_saveRoot, TestSlotId, "quest-log.json");
            string worldFile = Path.Combine(_saveRoot, TestSlotId, "world-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "STUDENT-E2E-010 failed: student progress save file missing after playable loop.");
            Assert.IsTrue(File.Exists(questFile), "STUDENT-E2E-010 failed: quest save file missing after playable loop.");
            Assert.IsTrue(File.Exists(worldFile), "STUDENT-E2E-010 failed: world progress save file missing after portal interaction.");
            string studentJson = File.ReadAllText(studentFile);
            StringAssert.Contains("LastActivityId", studentJson, "STUDENT-E2E-010 failed: student save does not record last activity field.");
            StringAssert.Contains("TutorialStageId", studentJson, "STUDENT-E2E-010 failed: student save does not record tutorial/story stage field.");
            StringAssert.Contains("tutorial.day1", studentJson, "STUDENT-E2E-010 failed: student save does not preserve first tutorial/story stage without day-end settlement.");
            StringAssert.Contains("ActivityLogIds", studentJson, "STUDENT-E2E-010 failed: student save does not record activity log field.");
            StringAssert.Contains("activity.study-basics", studentJson, "STUDENT-E2E-010 failed: student save does not record completed study activity.");
            StringAssert.Contains("RewardClaimed", File.ReadAllText(questFile), "STUDENT-E2E-010 failed: quest save does not record claimed reward state.");
            string worldJson = File.ReadAllText(worldFile);
            StringAssert.Contains("Farm", worldJson, "STUDENT-E2E-010 failed: world save does not record current scene after portal travel.");
            StringAssert.Contains("farm-entry", worldJson, "STUDENT-E2E-010 failed: world save does not record destination entry point after portal travel.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            LogAssert.ignoreFailingMessages = true;
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            var loadPanel = SaveSlotSelectPanel.EnsureInScene();
            loadPanel.Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "STUDENT-E2E-010 failed: SaveSlot Load did not re-enter Town.");
            yield return WaitForTownRuntime(15f);
            LogAssert.ignoreFailingMessages = false;

            var reloaded = FindTownRuntime();
            var reloadedStudy = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            route = FindGatherWoodRoute(reloaded.Provider);
            reward = FindFirstItemReward(route.Quest);
            Assert.AreEqual(studiedTrait, reloaded.Student.Progress.GetTraitValue(reloadedStudy.PrimaryTrait), "STUDENT-E2E-011 failed: trait did not restore after SaveSlot reload.");
            Assert.AreEqual(studiedSkill, reloaded.Student.Progress.GetSkillValue(reloadedStudy.PrimarySkill), "STUDENT-E2E-011 failed: skill did not restore after SaveSlot reload.");
            Assert.IsTrue(reloaded.Student.Progress.IsCareerHintUnlocked(reloadedStudy.PrimaryCareer), "STUDENT-E2E-011 failed: career hint did not restore after SaveSlot reload.");
            Assert.AreEqual(QuestState.RewardClaimed, reloaded.QuestLogPanel.QuestLog.GetState(route.Quest), "STUDENT-E2E-011 failed: quest reward state did not restore after SaveSlot reload.");
            Assert.AreEqual(rewardAfterClaim, reloaded.Inventory.Inventory.CountOf(reward.Item), "STUDENT-E2E-011 failed: reward inventory count did not restore after SaveSlot reload.");
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.2f, "STUDENT-E2E-012 failed: keyboard movement could not reach GuideNpc after reload.");
            yield return OpenGuideDialogueThroughInput(reloaded, keyboard, "STUDENT-E2E-012 failed: GuideNpc dialogue did not open after reload.");
            Assert.IsNull(FindChoiceButtonFor(reloaded.DialoguePanel, DialogueQuestAction.ClaimReward, route.Quest), "STUDENT-E2E-012 failed: claimed reward was offered again after SaveSlot reload.");
            Assert.AreEqual(rewardAfterClaim, reloaded.Inventory.Inventory.CountOf(reward.Item), "STUDENT-E2E-012 failed: reward count changed after duplicate-claim check.");
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            yield return WaitForRuntime(timeoutSeconds, requireNpc: true, portalName: "TownReturnPortal", failure: "Town runtime did not expose full student quest loop objects within timeout.");
        }

        private static IEnumerator WaitForFarmRuntime(float timeoutSeconds)
        {
            yield return WaitForRuntime(timeoutSeconds, requireNpc: false, portalName: "FarmPortal", failure: "Farm runtime did not expose persisted student quest loop objects within timeout.");
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds, bool requireNpc, string portalName, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var npc = GameObject.Find("GuideNpc");
                var portal = GameObject.Find(portalName);
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                bool npcReady = !requireNpc || (npc != null && npc.GetComponent<NpcInteractor>() != null && npc.GetComponent<QuestProvider>() != null);
                if (player != null &&
                    player.GetComponent<PlayerController>() != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    player.GetComponent<PlayerInventory>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    portal != null && portal.GetComponent<WorldPortal>() != null &&
                    dialoguePanel != null && questLogPanel != null && questLogPanel.QuestLog != null &&
                    EventSystem.current != null && npcReady)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
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

        private static FarmRuntime FindFarmRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            return new FarmRuntime(
                player,
                player.GetComponent<PlayerInventory>(),
                player.GetComponent<StudentLifeProgressComponent>(),
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
                Assert.IsNotNull(node, "STUDENT-E2E-006 failed: no active target resource node remained for quest progress.");
                yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, node.transform.position + new Vector3(0.35f, 0f, 0f), 0.25f, "STUDENT-E2E-006 failed: keyboard movement could not reach target resource node.");
                int hitGuard = 0;
                while (runtime.Inventory.Inventory.CountOf(woodItem) <= before && hitGuard < 60)
                {
                    runtime.Router.RefreshPromptNow();
                    yield return PressInteractKey(keyboard);
                    yield return null;
                    hitGuard++;
                }

                Assert.Greater(runtime.Inventory.Inventory.CountOf(woodItem), before, "STUDENT-E2E-006 failed: repeated resource interaction input did not add wood inventory.");
                guard++;
            }
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 900 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                    ? (delta.x >= 0f ? Key.D : Key.A)
                    : (delta.y >= 0f ? Key.W : Key.S);

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
            var button = FindChild(card.transform, buttonName)?.GetComponent<Button>();
            Assert.IsNotNull(button, "Save slot UI button missing: " + buttonName);
            return button;
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
                if (transform == null || transform.name != objectName)
                {
                    continue;
                }

                Object.DestroyImmediate(transform.gameObject);
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

        private readonly struct FarmRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent Student;
            public readonly QuestLogPanel QuestLogPanel;

            public FarmRuntime(GameObject player, PlayerInventory inventory, StudentLifeProgressComponent student, QuestLogPanel questLogPanel)
            {
                Player = player;
                Inventory = inventory;
                Student = student;
                QuestLogPanel = questLogPanel;
            }
        }
    }
}
