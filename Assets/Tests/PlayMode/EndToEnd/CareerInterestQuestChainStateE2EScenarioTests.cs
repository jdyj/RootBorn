using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
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
    public sealed class CareerInterestQuestChainStateE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string BlockedChainId = "questchain.test.career-interest.blocked-library-access";
        private const string BlockedChainCardName = "QuestChainCard_questchain_test_career_interest_blocked_library_access";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-quest-chain-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator QUEST_CHAIN_E2E_001_014_SaveSlotMovementUiAcceptTrackProgressResultReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "QUEST-CHAIN-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            QuestChainRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForQuestChainRuntime(10f);

            var runtime = FindRuntime();
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "QUEST-CHAIN-E2E-002 failed: library location object missing.");
            var chainPanel = Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
            var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(chainPanel, "QUEST-CHAIN-E2E-004 failed: quest chain log panel missing.");
            Assert.IsNotNull(hud, "QUEST-CHAIN-E2E-008 failed: tracked objective HUD missing.");

            yield return ClickButton(FindButton("QuestChainLogOpenButton"));
            AssertPanelContains(chainPanel.transform, "questchain.test.career-interest.library", "QUEST-CHAIN-E2E-004 failed: chain log did not show registry-authored test chain.");
            AssertPanelContains(chainPanel.transform, "Available", "QUEST-CHAIN-E2E-004 failed: chain did not start as Available in UI.");

            yield return ClickButton(FindButton(chainPanel.transform, "QuestChainAcceptButton"));
            AssertPanelContains(chainPanel.transform, "Active", "QUEST-CHAIN-E2E-005 failed: accept button did not make chain active.");
            yield return ClickButton(FindButton(chainPanel.transform, "QuestChainTrackButton"));
            AssertPanelContains(chainPanel.transform, "Tracked", "QUEST-CHAIN-E2E-005 failed: track button did not make chain tracked.");
            AssertPanelContains(hud.transform, "questchain.test.career-interest.library", "QUEST-CHAIN-E2E-008 failed: tracked chain did not appear in HUD.");
            AssertPanelContains(hud.transform, "0 / 1", "QUEST-CHAIN-E2E-008 failed: HUD did not show current objective progress.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "QUEST-CHAIN-E2E-002 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "QUEST-CHAIN-E2E-003 failed: library prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationPanel(2f, "QUEST-CHAIN-E2E-003 failed: location UI did not open from actual input.");
            var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(locationPanel.transform, "LocationActivityButton_0"));
            yield return null;

            AssertPanelContains(chainPanel.transform, "Completed", "QUEST-CHAIN-E2E-006 failed: actual location button did not complete the tracked chain.");
            AssertPanelContains(hud.transform, "Completed", "QUEST-CHAIN-E2E-006 failed: HUD did not update after location progress.");
            string chainFile = Path.Combine(_saveRoot, TestSlotId, "quest-chain-progress.json");
            Assert.IsTrue(File.Exists(chainFile), "QUEST-CHAIN-E2E-012 failed: quest chain progress save file missing.");
            StringAssert.Contains("questchain.test.career-interest.library", File.ReadAllText(chainFile), "QUEST-CHAIN-E2E-012 failed: save file omitted chain id.");
            StringAssert.Contains("Completed", File.ReadAllText(chainFile), "QUEST-CHAIN-E2E-012 failed: save file omitted completed state.");

            yield return ClickButton(FindButton(chainPanel.transform, "QuestChainClaimRewardButton"));
            AssertPanelContains(chainPanel.transform, "Reward: Claimed", "QUEST-CHAIN-E2E-013 failed: actual claim button did not update reward claimed UI state.");
            string claimedJson = File.ReadAllText(chainFile);
            StringAssert.Contains("RewardClaimedIds", claimedJson, "QUEST-CHAIN-E2E-013 failed: claim did not persist reward claimed flags.");
            StringAssert.Contains("completion", claimedJson, "QUEST-CHAIN-E2E-013 failed: completion reward id was not saved after UI claim.");
            Assert.IsFalse(FindButton(chainPanel.transform, "QuestChainClaimRewardButton").interactable, "QUEST-CHAIN-E2E-013 failed: claimed reward button should not remain user-clickable.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "QUEST-CHAIN-E2E-011 failed: movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "QUEST-CHAIN-E2E-011 failed: day result UI did not open from actual day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(resultPanel.transform, "questchain.test.career-interest.library", "QUEST-CHAIN-E2E-011 failed: day result UI omitted quest chain summary.");
            AssertPanelContains(resultPanel.transform, "Completed", "QUEST-CHAIN-E2E-011 failed: day result UI omitted quest chain completed state.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "QUEST-CHAIN-E2E-012 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            QuestChainRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForQuestChainRuntime(10f);

            var reloadedPanel = Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton("QuestChainLogOpenButton"));
            AssertPanelContains(reloadedPanel.transform, "Completed", "QUEST-CHAIN-E2E-012 failed: completed chain state did not restore after save/load.");
            AssertPanelContains(reloadedPanel.transform, "Reward: Claimed", "QUEST-CHAIN-E2E-013 failed: reward claimed state did not restore after save/load.");
            Assert.IsFalse(FindButton(reloadedPanel.transform, "QuestChainClaimRewardButton").interactable, "QUEST-CHAIN-E2E-013 failed: reward became claimable again after save/load.");
            AssertPanelContains(reloadedPanel.transform, "questchain.test.career-interest.library", "QUEST-CHAIN-E2E-014 failed: SO-only registered test chain did not reload through actual UI path.");
        }

        [UnityTest]
        public IEnumerator QUEST_CHAIN_E2E_009_010_BlockedChainReopensFromActualLocationActivity()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "QUEST-CHAIN-E2E-009 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            QuestChainRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForQuestChainRuntime(10f);

            var runtime = FindRuntime();
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "QUEST-CHAIN-E2E-009 failed: library location object missing.");
            var chainPanel = Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton("QuestChainLogOpenButton"));
            AssertPanelContains(chainPanel.transform, BlockedChainId, "QUEST-CHAIN-E2E-009 failed: blocked test chain was not loaded from SO data.");

            yield return ClickButton(FindButton(chainPanel.transform, BlockedChainCardName));
            yield return ClickButton(FindButton(chainPanel.transform, "QuestChainAcceptButton"));
            AssertPanelContains(chainPanel.transform, "Blocked", "QUEST-CHAIN-E2E-009 failed: missing activity condition did not block the chain.");
            AssertPanelContains(chainPanel.transform, "missing.library.access", "QUEST-CHAIN-E2E-009 failed: blocked UI omitted reason id.");
            AssertPanelContains(chainPanel.transform, "Visit the library desk", "QUEST-CHAIN-E2E-009 failed: blocked UI omitted reopen guidance.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "QUEST-CHAIN-E2E-010 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "QUEST-CHAIN-E2E-010 failed: library prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationPanel(2f, "QUEST-CHAIN-E2E-010 failed: location UI did not open from actual input.");
            var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(locationPanel.transform, "LocationActivityButton_0"));
            yield return null;

            AssertPanelContains(chainPanel.transform, "Completed", "QUEST-CHAIN-E2E-010 failed: actual activity did not reopen and complete the blocked chain.");
            string chainFile = Path.Combine(_saveRoot, TestSlotId, "quest-chain-progress.json");
            Assert.IsTrue(File.Exists(chainFile), "QUEST-CHAIN-E2E-010 failed: quest chain progress save file missing after blocked flow.");
            string json = File.ReadAllText(chainFile);
            StringAssert.Contains(BlockedChainId, json, "QUEST-CHAIN-E2E-010 failed: save file omitted blocked-flow chain id.");
            StringAssert.Contains("Completed", json, "QUEST-CHAIN-E2E-010 failed: save file omitted completed reopened chain state.");
        }

        private static IEnumerator WaitForQuestChainRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                var chainPanel = Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                var openButton = GameObject.Find("QuestChainLogOpenButton");
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && library != null && locationPanel != null && chainPanel != null && hud != null && openButton != null && dayEnd != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("QUEST-CHAIN-E2E runtime did not expose player, library, day-end point, quest chain UI, and open button within timeout.");
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(dayEnd);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), dayEnd.GetComponent<StudentDayEndInteractor>());
        }

        private static GameObject RequireObject(string name, string failure)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, failure);
            return go;
        }

        private static IEnumerator WaitForLocationPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
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

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 480 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
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
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "UI button object missing: " + name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
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
            var button = child.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
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

        private static void AssertPanelContains(Transform panel, string expected, string message)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }
            Assert.Fail(message + " Expected '" + expected + "' in text: " + string.Join(" | ", all));
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
            new GameObject("PassiveEventSystem", typeof(EventSystem), typeof(Rootborn.Game.Common.PassiveInputModule));
        }

        private static void CleanupResidue()
        {
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                var go = objects[i];
                if (go == null) continue;
                string name = go.name;
                if (name.Contains("QuestChain") || name.Contains("SaveSlot") || name == "PassiveEventSystem") Object.Destroy(go);
            }
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly StudentDayEndInteractor DayEnd;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                DayEnd = dayEnd;
            }
        }
    }
}
