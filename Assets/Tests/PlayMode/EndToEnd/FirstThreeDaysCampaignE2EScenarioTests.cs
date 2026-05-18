using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
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
    public sealed class FirstThreeDaysCampaignE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-campaign-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator CAMPAIGN_E2E_001_011_SaveSlotPlaysThreeDaysWithOutsideSchoolRouteReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "CAMPAIGN-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyEventRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForCampaignRuntime(10f);

            var hud = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
            AssertPanelContains(hud.transform, "Town adaptation", "CAMPAIGN-E2E-002 failed: day one HUD omitted today's direction.");
            AssertPanelContains(hud.transform, "Library path", "CAMPAIGN-E2E-002 failed: day one HUD omitted alternate route.");

            var runtime = FindRuntime();
            var square = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "CAMPAIGN-E2E-003 failed: town square object missing.");
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, square.transform.position, 0.25f, "CAMPAIGN-E2E-003 failed: actual keyboard movement could not reach town square.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForHudText("campaign.objective.visit-town", 3f, "CAMPAIGN-E2E-003 failed: actual location interaction did not advance day one campaign objective.");

            yield return EndDayAndClickNext(keyboard, "Find growth", "CAMPAIGN-E2E-004");

            runtime = FindRuntime();
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "CAMPAIGN-E2E-005 failed: library object missing.");
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "CAMPAIGN-E2E-005 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyEventPanel(3f, "CAMPAIGN-E2E-005 failed: school-free library route did not open event UI.");
            var eventPanel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(eventPanel.transform, "DailyEventChoiceButton_0"));
            yield return WaitForHudText("campaign.objective.school-free-growth", 3f, "CAMPAIGN-E2E-005 failed: school-free route did not advance campaign objective.");

            yield return EndDayAndClickNext(keyboard, "Connect future", "CAMPAIGN-E2E-006");

            runtime = FindRuntime();
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "CAMPAIGN-E2E-007 failed: actual keyboard movement could not reach day three event location.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForHudText("campaign.objective.career-hint", 3f, "CAMPAIGN-E2E-007 failed: day three event/career progress did not update campaign.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "CAMPAIGN-E2E-008 failed: actual keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "CAMPAIGN-E2E-008 failed: result UI did not open from actual day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(resultPanel.transform, "campaign.objective.career-hint", "CAMPAIGN-E2E-008 failed: result UI omitted campaign progress change.");
            AssertPanelContains(resultPanel.transform, "Next campaign", "CAMPAIGN-E2E-008 failed: result UI omitted next campaign guide.");

            string campaignFile = Path.Combine(_saveRoot, TestSlotId, "campaign-progress.json");
            Assert.IsTrue(File.Exists(campaignFile), "CAMPAIGN-E2E-009 failed: campaign progress save file missing.");
            string savedJson = File.ReadAllText(campaignFile);
            StringAssert.Contains("campaign.objective.visit-town", savedJson, "CAMPAIGN-E2E-009 failed: save file omitted completed objective.");
            StringAssert.Contains("campaign-result-day-3", savedJson, "CAMPAIGN-E2E-010 failed: save file omitted applied result id for dedupe.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "CAMPAIGN-E2E-009 failed: SaveSlot Load did not re-enter Town.");
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForCampaignRuntime(10f);
            var reloadedHud = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
            AssertPanelContains(reloadedHud.transform, "campaign.objective.visit-town", "CAMPAIGN-E2E-009 failed: campaign objective did not survive reload.");
        }

        private IEnumerator EndDayAndClickNext(Keyboard keyboard, string expectedDayText, string failurePrefix)
        {
            var runtime = FindRuntime();
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, failurePrefix + " failed: actual keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, failurePrefix + " failed: result UI did not open from actual day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(resultPanel.transform, "campaign.objective", failurePrefix + " failed: result UI omitted campaign progress summary.");
            yield return ClickButton(FindButton(resultPanel.transform, "NextDayButton"));
            yield return WaitForHudText(expectedDayText, 3f, failurePrefix + " failed: next day HUD did not refresh after result UI button.");
        }

        private static IEnumerator WaitForCampaignRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var hud = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<CampaignProgressComponent>() != null && hud != null && hud.IsOpen && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("CAMPAIGN-E2E runtime did not expose player, campaign progress, HUD, and UI event system within timeout.");
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(dayEnd);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), dayEnd.GetComponent<StudentDayEndInteractor>());
        }

        private static IEnumerator WaitForHudText(string expected, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var hud = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
                if (hud != null && PanelContains(hud.transform, expected)) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static IEnumerator WaitForDailyEventPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
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

        private static GameObject RequireObject(string name, string failure)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, failure);
            return go;
        }

        private static void AssertPanelContains(Transform panel, string expected, string message)
        {
            if (PanelContains(panel, expected)) return;
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++) all.Add(texts[i].text);
            Assert.Fail(message + " Expected '" + expected + "' in text: " + string.Join(" | ", all));
        }

        private static bool PanelContains(Transform panel, string expected)
        {
            if (panel == null) return false;
            var texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return true;
            return false;
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
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName) Object.DestroyImmediate(transform.gameObject);
            }
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentDayEndInteractor DayEnd;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                DayEnd = dayEnd;
            }
        }
    }
}
