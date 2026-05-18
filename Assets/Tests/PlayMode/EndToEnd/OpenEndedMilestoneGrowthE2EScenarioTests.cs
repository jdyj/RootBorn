using System.Collections;
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
    public sealed class OpenEndedMilestoneGrowthE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-milestone-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator MILESTONE_E2E_001_010_PlayOutsideSchoolMilestoneResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "MILESTONE-E2E-001 failed: SaveSlot New Game did not enter Town.");
            yield return WaitForMilestoneRuntime(10f);
            LogAssert.ignoreFailingMessages = true;

            var runtime = FindMilestoneRuntime();
            AssertPanelContains(runtime.Hud.transform, "milestone.village-adaptation", "MILESTONE-E2E-002 failed: milestone HUD missing active milestone.");
            AssertPanelContains(runtime.Hud.transform, "outside.help-neighbor", "MILESTONE-E2E-002 failed: milestone HUD missing next recommended outside-school action.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Outside.transform.position, 0.25f, "MILESTONE-E2E-003 failed: actual keyboard movement could not reach outside-school point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "MILESTONE-E2E-003 failed: outside-school prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Outside.LastResult.Kind, "MILESTONE-E2E-004 failed: outside-school action did not apply through input.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "MILESTONE-E2E-006 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "MILESTONE-E2E-006 failed: day-end prompt was not visible.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "MILESTONE-E2E-006 failed: result UI did not open from actual day-end input.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, "Milestones", "MILESTONE-E2E-006 failed: result UI missing milestone summary section.");
            AssertPanelContains(panel.transform, "milestone.village-adaptation", "MILESTONE-E2E-006 failed: result UI missing completed milestone id.");
            AssertPanelContains(panel.transform, "outside.help-neighbor", "MILESTONE-E2E-006 failed: result UI missing next recommended action.");

            string milestoneFile = Path.Combine(_saveRoot, TestSlotId, "milestone-progress-" + runtime.Student.Progress.PlayerId + ".json");
            if (!File.Exists(milestoneFile)) milestoneFile = Path.Combine(_saveRoot, TestSlotId, "milestone-progress.json");
            Assert.IsTrue(File.Exists(milestoneFile), "MILESTONE-E2E-007 failed: milestone save file missing after day end.");
            string savedJson = File.ReadAllText(milestoneFile);
            StringAssert.Contains("milestone.village-adaptation", savedJson, "MILESTONE-E2E-007 failed: saved milestone id missing.");
            StringAssert.Contains("RewardClaimed", savedJson, "MILESTONE-E2E-007 failed: saved reward-claim state field missing.");
            StringAssert.Contains("\"Completed\": true", savedJson, "MILESTONE-E2E-007 failed: saved milestone completion state missing.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "MILESTONE-E2E-007 failed: SaveSlot Load did not re-enter Town.");
            yield return WaitForMilestoneRuntime(10f);

            var reloaded = FindMilestoneRuntime();
            AssertPanelContains(reloaded.Hud.transform, "milestone.village-adaptation", "MILESTONE-E2E-007 failed: reloaded HUD missing milestone.");
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "MILESTONE-E2E-008 failed: movement could not return to day-end point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "MILESTONE-E2E-008 failed: result UI did not reopen after reload.");
            string savedAgainJson = File.ReadAllText(milestoneFile);
            Assert.AreEqual(savedJson, savedAgainJson, "MILESTONE-E2E-008 failed: repeated result interaction changed milestone save data.");
        }

        private static IEnumerator WaitForMilestoneRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var outside = GameObject.Find("OutsideSchoolActivityPoint");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var result = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<MilestoneHudPanel>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " controller=" + (player != null && player.GetComponent<PlayerController>() != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " outside=" + (outside != null)
                    + " outsideInteractor=" + (outside != null && outside.GetComponent<OutsideSchoolActivityInteractor>() != null)
                    + " dayEnd=" + (dayEnd != null)
                    + " dayEndInteractor=" + (dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null)
                    + " result=" + (result != null)
                    + " hud=" + (hud != null)
                    + " hudText=" + (hud != null ? hud.VisibleText : "<none>")
                    + " eventSystem=" + (EventSystem.current != null);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    outside != null && outside.GetComponent<OutsideSchoolActivityInteractor>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    result != null && hud != null && hud.VisibleText.Contains("milestone.village-adaptation") && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("MILESTONE-E2E setup failed: milestone runtime did not expose player, outside-school point, day-end point, HUD, result panel, and event system. Last state: " + lastState);
        }

        private static MilestoneRuntime FindMilestoneRuntime()
        {
            var player = GameObject.Find("Player");
            var outside = GameObject.Find("OutsideSchoolActivityPoint");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            var hud = Object.FindFirstObjectByType<MilestoneHudPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(player);
            Assert.IsNotNull(outside);
            Assert.IsNotNull(dayEnd);
            Assert.IsNotNull(hud);
            return new MilestoneRuntime(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), outside.GetComponent<OutsideSchoolActivityInteractor>(), dayEnd.GetComponent<StudentDayEndInteractor>(), hud);
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
            for (int i = 0; i < 360 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
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

        private static Button FindButton(Transform root, string buttonName)
        {
            if (root.name == buttonName && root.TryGetComponent<Button>(out var button)) return button;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindButton(root.GetChild(i), buttonName);
                if (found != null) return found;
            }
            return null;
        }

        private static void AssertPanelContains(Transform root, string expected, string failure)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }
            Assert.Fail(failure + " Expected text: " + expected);
        }

        private static ButtonControl ControlFor(Keyboard keyboard, Key key)
        {
            return (ButtonControl)keyboard[key];
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }

        private static void CleanupResidue()
        {
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && (objects[i].name.StartsWith("SaveSlotSelectPanel") || objects[i].name == "EventSystem"))
                {
                    Object.Destroy(objects[i]);
                }
            }
        }

        private readonly struct MilestoneRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly OutsideSchoolActivityInteractor Outside;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly MilestoneHudPanel Hud;

            public MilestoneRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, OutsideSchoolActivityInteractor outside, StudentDayEndInteractor dayEnd, MilestoneHudPanel hud)
            {
                Player = player;
                Router = router;
                Student = student;
                Outside = outside;
                DayEnd = dayEnd;
                Hud = hud;
            }
        }
    }
}
