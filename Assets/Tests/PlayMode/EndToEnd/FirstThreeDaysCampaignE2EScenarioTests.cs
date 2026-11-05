using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class FirstThreeDaysCampaignE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ScreenshotPath = "Builds/Logs/campaign-objective-journal-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 8f;
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
            Time.timeScale = 1f;
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
        [Timeout(600000)]
        public IEnumerator CAMPAIGN_E2E_001_011_SaveSlotPlaysThreeDaysWithOutsideSchoolRouteReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return null;
            var creatorConfirm = FindOptionalButton("SpumCharacterCreatorPanel", "ConfirmButton");
            if (creatorConfirm != null) yield return ClickButton(creatorConfirm);
            yield return WaitForScene("Town", 20f, "CAMPAIGN-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyEventRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForCampaignRuntime(10f);
            AssertNoLegacyCampaignHudObjects("CAMPAIGN-E2E-011 failed: legacy Campaign HUD objects should not exist at runtime.");

            var runtime = FindRuntime();
            yield return PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "CAMPAIGN-E2E-002 failed: actual Tab input did not open Objective Journal.");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Campaign"));
            StringAssert.Contains("Campaign", runtime.Journal.VisibleText, "CAMPAIGN-E2E-002 failed: Campaign category did not display campaign entries.");
            StringAssert.Contains("Town adaptation", runtime.Journal.VisibleText, "CAMPAIGN-E2E-002 failed: Campaign Journal omitted today's direction.");
            StringAssert.Contains("Library path", runtime.Journal.VisibleText, "CAMPAIGN-E2E-002 failed: Campaign Journal omitted alternate route.");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "CAMPAIGN-E2E-002 failed: Track click did not update TrackedObjectiveHud.");
            StringAssert.Contains("Campaign", runtime.TrackedHud.VisibleText, "CAMPAIGN-E2E-002 failed: tracked HUD did not show campaign text.");
            yield return PressTab(keyboard);
            Assert.IsFalse(runtime.Journal.IsVisible, "CAMPAIGN-E2E-002 failed: second Tab should close Objective Journal before movement.");

            var square = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "CAMPAIGN-E2E-003 failed: town square object missing.");
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, ApproachTownSquare(square.transform.position), 0.25f, "CAMPAIGN-E2E-003 failed: actual keyboard movement could not reach town square approach point.");
            runtime.Router.RefreshPromptNow();
            StringAssert.Contains("Explore", runtime.Router.PromptText, "CAMPAIGN-E2E-003 failed: town square approach must target the location visit prompt, not a nearby activity prompt.");
            yield return PressInteractKey(keyboard);
            yield return WaitForCampaignJournalText(keyboard, "campaign.objective.visit-town", 3f, "CAMPAIGN-E2E-003 failed: actual location interaction did not advance day one campaign objective.");

            yield return EndDayAndClickNext(keyboard, "Find growth", "CAMPAIGN-E2E-004");

            runtime = FindRuntime();
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "CAMPAIGN-E2E-005 failed: library object missing.");
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "CAMPAIGN-E2E-005 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyEventPanel(3f, "CAMPAIGN-E2E-005 failed: school-free library route did not open event UI.");
            var eventPanel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(eventPanel.transform, "DailyEventChoiceButton_0"));
            yield return WaitForCampaignJournalText(keyboard, "campaign.objective.school-free-growth", 3f, "CAMPAIGN-E2E-005 failed: school-free route did not advance campaign objective.");

            yield return EndDayAndClickNext(keyboard, "Connect future", "CAMPAIGN-E2E-006");

            runtime = FindRuntime();
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "CAMPAIGN-E2E-007 failed: actual keyboard movement could not reach day three event location.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForCampaignJournalText(keyboard, "campaign.objective.career-hint", 3f, "CAMPAIGN-E2E-007 failed: day three event/career progress did not update campaign.");

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
            yield return WaitForScene("Town", 20f, "CAMPAIGN-E2E-009 failed: SaveSlot Load did not re-enter Town.");
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForCampaignRuntime(10f);
            AssertNoLegacyCampaignHudObjects("CAMPAIGN-E2E-011 failed: legacy Campaign HUD objects should not exist after reload.");
            var reloaded = FindRuntime();
            yield return PressTab(keyboard);
            yield return ClickButton(FindButton(reloaded.Journal.transform, "ObjectiveCategory_Campaign"));
            yield return ClickButton(FindButton(reloaded.Journal.transform, "ObjectiveRow_0"));
            StringAssert.Contains("campaign.objective.visit-town", reloaded.Journal.VisibleText, "CAMPAIGN-E2E-009 failed: campaign objective did not survive reload in Objective Journal.");
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "CAMPAIGN-E2E-009 failed: tracked campaign objective was not restored after reload and Journal refresh.");
            StringAssert.Contains("Campaign", reloaded.TrackedHud.VisibleText, "CAMPAIGN-E2E-009 failed: restored tracked HUD did not show campaign text.");
            CaptureScreenshot();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(File.Exists(ScreenshotPath), "CAMPAIGN-E2E-011 failed: Game View screenshot evidence was not written.");
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
            yield return WaitForCampaignJournalText(keyboard, expectedDayText, 3f, failurePrefix + " failed: next day Campaign Journal did not refresh after result UI button.");
        }

        private static IEnumerator WaitForCampaignRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var trackedHud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " controller=" + (player != null && player.GetComponent<PlayerController>() != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " campaign=" + (player != null && player.GetComponent<CampaignProgressComponent>() != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " trackedHud=" + (trackedHud != null)
                    + " eventSystem=" + (EventSystem.current != null);
                if (player != null
                    && player.GetComponent<PlayerController>() != null
                    && player.GetComponent<PlayerInteractionRouter>() != null
                    && player.GetComponent<StudentLifeProgressComponent>() != null
                    && player.GetComponent<CampaignProgressComponent>() != null
                    && journal != null
                    && provider != null
                    && trackedHud != null
                    && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("CAMPAIGN-E2E runtime did not expose player, campaign progress, Objective Journal, tracked HUD, and UI event system within timeout. Last state: " + lastState);
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            var trackedHud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(player);
            Assert.IsNotNull(dayEnd);
            Assert.IsNotNull(journal);
            Assert.IsNotNull(trackedHud);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), dayEnd.GetComponent<StudentDayEndInteractor>(), journal, trackedHud);
        }

        private IEnumerator WaitForCampaignJournalText(Keyboard keyboard, string expected, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var runtime = FindRuntime();
                if (!runtime.Journal.IsVisible)
                {
                    yield return PressTab(keyboard);
                }

                yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Campaign"));
                yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
                if (runtime.Journal.VisibleText.Contains(expected) || runtime.TrackedHud.VisibleText.Contains(expected))
                {
                    yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
                    if (runtime.Journal.IsVisible)
                    {
                        yield return PressTab(keyboard);
                    }
                    yield break;
                }

                if (runtime.Journal.IsVisible)
                {
                    yield return PressTab(keyboard);
                }

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

        private IEnumerator PressTab(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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

        private static Button FindOptionalButton(string rootName, string buttonName)
        {
            var root = GameObject.Find(rootName);
            return root != null ? FindButton(root.transform, buttonName) : null;
        }

        private static Button FindButton(Transform root, string name)
        {
            if (root.name == name && root.TryGetComponent<Button>(out var button)) return button;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindButton(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
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

        private static Vector3 ApproachTownSquare(Vector3 position)
        {
            return position + new Vector3(-1.55f, -0.6f, 0f);
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

        private static void AssertNoLegacyCampaignHudObjects(string message)
        {
            Assert.IsNull(GameObject.Find("CampaignHudPanel"), message + " CampaignHudPanel exists.");
            Assert.IsNull(GameObject.Find("CampaignHudRoot"), message + " CampaignHudRoot exists.");
            Assert.IsNull(GameObject.Find("[CampaignCanvas]"), message + " [CampaignCanvas] exists.");
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

        private static void CaptureScreenshot()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
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
            DestroyAllNamed("SpumCharacterCreatorPanel");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
            DestroyAllNamed("CampaignHudPanel");
            DestroyAllNamed("CampaignHudRoot");
            DestroyAllNamed("[CampaignCanvas]");
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
            public readonly ObjectiveJournalPanel Journal;
            public readonly TrackedObjectiveHud TrackedHud;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentDayEndInteractor dayEnd, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                DayEnd = dayEnd;
                Journal = journal;
                TrackedHud = trackedHud;
            }
        }
    }
}