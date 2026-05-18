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
    public sealed class CareerCandidateDeepeningLoopE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-career-candidate-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator CAREER_CANDIDATE_E2E_001_010_SaveSlotLocationButtonCandidateUiResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var data = CareerCandidateE2EData.Create();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return null;
            yield return ClickButton(FindButton("SpumCharacterCreatorPanel", "ConfirmButton"));
            yield return WaitForScene("Town", 10f, "CAREER-CANDIDATE-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var runtime = FindRuntime();
            var candidateProgress = runtime.Player.GetComponent<CareerCandidateProgressComponent>();
            if (candidateProgress == null) candidateProgress = runtime.Player.AddComponent<CareerCandidateProgressComponent>();
            candidateProgress.Bind(data.Candidates, data.Hints);
            var careerPanel = CareerCandidatePanel.EnsureInScene(EnsureCanvas());
            var openButton = MakeCareerButton(EnsureCanvas(), () => careerPanel.Show(data.Candidates, candidateProgress.Progress, runtime.Student.Progress));
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "CAREER-CANDIDATE-E2E-002 failed: library location object missing.");
            var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(locationPanel, "CAREER-CANDIDATE-E2E-003 failed: location panel missing.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "CAREER-CANDIDATE-E2E-002 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "CAREER-CANDIDATE-E2E-003 failed: location prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationPanel(2f, "CAREER-CANDIDATE-E2E-003 failed: location UI did not open from actual input.");
            yield return ClickButton(FindButton(locationPanel.transform, "LocationActivityButton_0"));
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.Applied, locationPanel.LastResult.Kind, "CAREER-CANDIDATE-E2E-004 failed: location activity button did not apply.");
            Assert.IsTrue(candidateProgress.LastResult.Applied, "CAREER-CANDIDATE-E2E-004 failed: career candidate hint was not applied from actual location result.");
            Assert.AreEqual("candidate.learning", candidateProgress.LastResult.CandidateId, "CAREER-CANDIDATE-E2E-004 failed: wrong career candidate progressed.");

            yield return ClickButton(openButton);
            Assert.IsTrue(careerPanel.IsOpen, "CAREER-CANDIDATE-E2E-005 failed: career candidate UI did not open from actual UI button.");
            AssertPanelContains(careerPanel.transform, "???", "CAREER-CANDIDATE-E2E-006 failed: career UI omitted locked candidate placeholder.");
            AssertPanelContains(careerPanel.transform, "candidate.learning", "CAREER-CANDIDATE-E2E-006 failed: career UI omitted hinted candidate.");
            AssertPanelContains(careerPanel.transform, "action.visit-library", "CAREER-CANDIDATE-E2E-007 failed: career UI omitted recommended next action.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "CAREER-CANDIDATE-E2E-008 failed: movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "CAREER-CANDIDATE-E2E-008 failed: day result UI did not open from actual day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(resultPanel.transform, "candidate.learning", "CAREER-CANDIDATE-E2E-008 failed: day result UI omitted career candidate progress.");

            string[] files = Directory.GetFiles(Path.Combine(_saveRoot, TestSlotId), "career-candidate-progress*.json");
            Assert.AreEqual(1, files.Length, "CAREER-CANDIDATE-E2E-009 failed: career candidate progress save file missing.");
            string savedJson = File.ReadAllText(files[0]);
            StringAssert.Contains("candidate.learning", savedJson, "CAREER-CANDIDATE-E2E-009 failed: save file omitted candidate id.");
            StringAssert.Contains("hint.learning.library", savedJson, "CAREER-CANDIDATE-E2E-009 failed: save file omitted hint id.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "CAREER-CANDIDATE-E2E-009 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var reloaded = FindRuntime();
            var reloadedCandidates = reloaded.Player.GetComponent<CareerCandidateProgressComponent>();
            if (reloadedCandidates == null) reloadedCandidates = reloaded.Player.AddComponent<CareerCandidateProgressComponent>();
            reloadedCandidates.Bind(data.Candidates, data.Hints);
            Assert.AreEqual(1, reloadedCandidates.Progress.GetHintCount(data.Learning), "CAREER-CANDIDATE-E2E-009 failed: candidate progress did not survive reload.");
            var duplicate = reloadedCandidates.ApplyHintsFromResult(reloaded.Student.Progress, locationPanel.LastResult.RequestId, new[] { "location-activity:duplicate" });
            Assert.IsFalse(duplicate.Applied, "CAREER-CANDIDATE-E2E-010 failed: duplicate hint source applied after reload.");
            Assert.AreEqual(1, reloadedCandidates.Progress.GetHintCount(data.Learning), "CAREER-CANDIDATE-E2E-010 failed: duplicate source changed hint count.");
        }

        private static CareerCandidateE2EData CreateData() => CareerCandidateE2EData.Create();

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    library != null && library.GetComponent<LocationActivityInteractor>() != null && dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && panel != null && EventSystem.current != null)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("CAREER-CANDIDATE-E2E runtime did not expose player, library, day-end point, and UI within timeout.");
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

        private static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null) return canvas;
            var go = new GameObject("CareerCandidateE2ECanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return go.GetComponent<Canvas>();
        }

        private static Button MakeCareerButton(Canvas canvas, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("CareerCandidatesButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(220f, 42f);
            go.GetComponent<Image>().color = Color.white;
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)label.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "Career";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
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
            go.AddComponent<PassiveInputModule>();
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
            DestroyAllNamed("CareerCandidateE2ECanvas");
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

        private sealed class CareerCandidateE2EData
        {
            public CareerCandidateDefinition Learning;
            public CareerCandidateDefinition Technical;
            public CareerCandidateDefinition[] Candidates;
            public CareerHintDefinition[] Hints;

            public static CareerCandidateE2EData Create()
            {
                var libraryRoute = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
                libraryRoute.ConfigureForTests("route.library", "route.library", "desc.route.library", null, null, new[] { "action.visit-library" });
                var workRoute = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
                workRoute.ConfigureForTests("route.workshop", "route.workshop", "desc.route.workshop", null, null, new[] { "action.try-workshop" });
                var learning = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
                learning.ConfigureForTests("candidate.learning", "candidate.learning", "desc.candidate.learning", new[] { libraryRoute }, null, new[] { "action.visit-library" });
                var technical = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
                technical.ConfigureForTests("candidate.technical", "candidate.technical", "desc.candidate.technical", new[] { workRoute }, null, new[] { "action.try-workshop" });
                var source = ScriptableObject.CreateInstance<CareerResultLogHintSource>();
                source.ConfigureForTests("location-activity:");
                var hint = ScriptableObject.CreateInstance<CareerHintDefinition>();
                hint.ConfigureForTests("hint.learning.library", "hint.learning.library", learning, libraryRoute, "insight.learning.library", 1, new CareerHintSourceBase[] { source });
                return new CareerCandidateE2EData { Learning = learning, Technical = technical, Candidates = new[] { learning, technical }, Hints = new[] { hint } };
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
