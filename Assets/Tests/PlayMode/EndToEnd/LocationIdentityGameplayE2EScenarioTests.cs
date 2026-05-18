using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
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
    public sealed class LocationIdentityGameplayE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-location-identity-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator LOCIDENT_E2E_001_012_SaveSlotMovementTwoLocationActivitiesResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "LOCIDENT-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForLocationIdentityRuntime(10f);

            var runtime = FindRuntime();
            var square = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "LOCIDENT-E2E-002 failed: town square object missing.");
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "LOCIDENT-E2E-005 failed: library object missing.");
            var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "LOCIDENT-E2E-003 failed: location identity panel missing.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, square.transform.position, 0.25f, "LOCIDENT-E2E-002 failed: actual keyboard movement could not reach town square.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "LOCIDENT-E2E-003 failed: town square prompt was not visible after movement.");
            StringAssert.Contains("Explore", runtime.Router.PromptText, "LOCIDENT-E2E-003 failed: prompt did not expose location identity interaction.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationIdentityPanel(2f, "LOCIDENT-E2E-003 failed: location identity UI did not open from actual input.");
            AssertPanelContains(panel.transform, "square", "LOCIDENT-E2E-003 failed: first location UI did not show town square identity text.");
            AssertPanelContains(panel.transform, "SocialHelp", "LOCIDENT-E2E-003 failed: first location UI did not show social/help route.");

            int relationshipBefore = FirstRelationshipValue(runtime.Student.Progress);
            yield return ClickButton(FindButton(panel.transform, "LocationActivityButton_0"));
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, panel.LastResult.Kind, "LOCIDENT-E2E-004 failed: first location activity did not apply through UI button.");
            Assert.AreEqual("location.town-square", panel.LastResult.LocationId, "LOCIDENT-E2E-004 failed: first location result used wrong location.");
            Assert.AreEqual(LocationGrowthRoute.SocialHelp, panel.LastResult.GrowthRoute, "LOCIDENT-E2E-004 failed: first location route was not social/help.");
            int relationshipAfter = FirstRelationshipValue(runtime.Student.Progress);
            Assert.Greater(relationshipAfter, relationshipBefore, "LOCIDENT-E2E-008 failed: town square result did not change relationship/community state.");
            string squareActivityId = panel.LastResult.ActivityId;

            yield return ClickButton(FindButton(panel.transform, "LocationActivityButton_0"));
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, panel.LastResult.Kind, "LOCIDENT-E2E-011 failed: repeated same location activity did not report duplicate.");
            Assert.AreEqual(relationshipAfter, FirstRelationshipValue(runtime.Student.Progress), "LOCIDENT-E2E-011 failed: repeated same location activity changed relationship again.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "LOCIDENT-E2E-005 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "LOCIDENT-E2E-006 failed: library prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationIdentityPanel(2f, "LOCIDENT-E2E-006 failed: second location identity UI did not open from actual input.");
            AssertPanelContains(panel.transform, "library", "LOCIDENT-E2E-006 failed: second location UI did not show library identity text.");
            AssertPanelContains(panel.transform, "SelfStudy", "LOCIDENT-E2E-006 failed: second location UI did not show a route different from the square.");

            int skillBefore = FirstSkillValue(runtime.Student.Progress);
            yield return ClickButton(FindButton(panel.transform, "LocationActivityButton_0"));
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, panel.LastResult.Kind, "LOCIDENT-E2E-007 failed: second location activity did not apply through UI button.");
            Assert.AreEqual("location.library", panel.LastResult.LocationId, "LOCIDENT-E2E-007 failed: second location result used wrong location.");
            Assert.AreEqual(LocationGrowthRoute.SelfStudy, panel.LastResult.GrowthRoute, "LOCIDENT-E2E-008 failed: second location route was not distinct from square route.");
            Assert.Greater(FirstSkillValue(runtime.Student.Progress), skillBefore, "LOCIDENT-E2E-008 failed: library result did not change skill/study state.");
            string libraryActivityId = panel.LastResult.ActivityId;

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "LOCIDENT-E2E-009 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "LOCIDENT-E2E-009 failed: result UI did not open from actual day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(resultPanel.transform, squareActivityId, "LOCIDENT-E2E-009 failed: result UI omitted first location activity.");
            AssertPanelContains(resultPanel.transform, libraryActivityId, "LOCIDENT-E2E-009 failed: result UI omitted second location activity.");
            AssertPanelContains(resultPanel.transform, "location-activity:", "LOCIDENT-E2E-010 failed: result UI omitted location activity result details.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "LOCIDENT-E2E-010 failed: student progress save file missing after day end.");
            string savedJson = File.ReadAllText(studentFile);
            StringAssert.Contains(squareActivityId, savedJson, "LOCIDENT-E2E-010 failed: save file does not record first location activity.");
            StringAssert.Contains(libraryActivityId, savedJson, "LOCIDENT-E2E-010 failed: save file does not record second location activity.");
            StringAssert.Contains("location-activity:", savedJson, "LOCIDENT-E2E-010 failed: save file does not record location result logs.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "LOCIDENT-E2E-010 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForLocationIdentityRuntime(10f);

            var reloaded = FindRuntime();
            Assert.AreEqual(StudentDayState.ResultReady, reloaded.Student.Progress.DayState, "LOCIDENT-E2E-010 failed: result-ready state did not restore.");
            CollectionAssert.Contains(reloaded.Student.Progress.GetPreviousDayActivityIds(), squareActivityId, "LOCIDENT-E2E-010 failed: first location result did not survive reload.");
            CollectionAssert.Contains(reloaded.Student.Progress.GetPreviousDayActivityIds(), libraryActivityId, "LOCIDENT-E2E-010 failed: second location result did not survive reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "LOCIDENT-E2E-011 failed: movement could not return to day-end point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "LOCIDENT-E2E-011 failed: result UI did not reopen from saved result state.");
            Assert.AreEqual(2, CountLocationActivityLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()), "LOCIDENT-E2E-011 failed: re-opening saved result duplicated location activity rewards/logs.");
        }

        private static IEnumerator WaitForLocationIdentityRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var square = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"));
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    square != null && square.GetComponent<LocationActivityInteractor>() != null &&
                    library != null && library.GetComponent<LocationActivityInteractor>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("LOCIDENT-E2E runtime did not expose player, two location identity points, day-end point, and UI within timeout.");
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

        private static int FirstRelationshipValue(StudentLifeProgress progress)
        {
            string[] ids = progress.GetRelationshipIds();
            return ids.Length == 0 ? 0 : progress.GetRelationshipValueById(ids[0]);
        }

        private static int FirstSkillValue(StudentLifeProgress progress)
        {
            string[] ids = progress.GetSkillIds();
            return ids.Length == 0 ? 0 : progress.GetSkillValueById(ids[0]);
        }

        private static int CountLocationActivityLogs(string[] logs)
        {
            int count = 0;
            if (logs == null) return 0;
            for (int i = 0; i < logs.Length; i++) if (!string.IsNullOrEmpty(logs[i]) && logs[i].StartsWith("location-activity:")) count++;
            return count;
        }

        private static IEnumerator WaitForLocationIdentityPanel(float timeoutSeconds, string failure)
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
