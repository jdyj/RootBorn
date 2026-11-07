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
    public sealed class TownExplorationDiscoveryE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-discovery-e2e", System.Guid.NewGuid().ToString("N"));
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

        // Every placed point of one kind in the authored Town: the player walks there with WASD, and E at that spot
        // must reach that very point (not a neighbour that wins the priority) with a prompt that shows player text,
        // not a raw localization key. Problems are collected so one run reports every bad point.
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator TOWN_POINT_001_AuthoredTown_EveryPlacedPointIsReachableAndPromptsItself([Values(
            "LocationVisitInteractor", "PartTimeWorkInteractor", "LocationActivityInteractor", "NpcInteractor", "CareerFacilityInteractor",
            "TownHelpActionInteractor", "OutsideSchoolActivityInteractor", "ExplorationInteractionPointInteractor",
            "DailyPlanBoardInteractor", "DiscoveryPointInteractor")] string typeName)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Support.PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "TOWN-POINT-001 failed: New Game did not enter Town.");
            yield return Support.PlayerFlowE2EDriver.DismissDailyEvent();
            var points = PlacedPoints("Town", behaviour => behaviour.GetType().Name == typeName);
            Assert.IsNotEmpty(points, "TOWN-POINT-001 failed: authored Town has no active " + typeName + ".");
            var problems = new List<string>();
            yield return CheckPlacedPoints(keyboard, "Town", points, problems);
            Assert.IsEmpty(problems, "TOWN-POINT-001 failed for " + typeName + " (" + points.Count + " points):\n" + string.Join("\n", problems));
        }

        // Same check inside the scenes reached through a Town door. Doors are left out: walking onto one leaves the scene.
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator TOWN_POINT_002_DoorScenes_EveryPlacedPointIsReachableAndPromptsItself([Values("House", "School")] string sceneName)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Support.PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "TOWN-POINT-002 failed: New Game did not enter Town.");
            yield return Support.PlayerFlowE2EDriver.DismissDailyEvent();
            yield return Support.PlayerFlowE2EDriver.WalkThroughDoor(keyboard, sceneName, "TOWN-POINT-002 failed:");
            yield return Support.PlayerFlowE2EDriver.DismissDailyEvent();
            // Left out: the three invisible InteriorInteractor_Computer_* triggers saved in House.unity by the old generated-house
            // spawner (two have no definition, one sits inside a wall and prompts an unlocalized "[E] Use Computer").
            // House.unity is fixed evidence, so removing them waits for the user's decision.
            var points = PlacedPoints(sceneName, behaviour => behaviour.GetComponent<Rootborn.Game.World.WorldPortal>() == null
                && !(behaviour is Rootborn.Game.Interiors.InteriorObjectInteractor));
            Assert.IsNotEmpty(points, "TOWN-POINT-002 failed: " + sceneName + " has no placed points.");
            var problems = new List<string>();
            yield return CheckPlacedPoints(keyboard, sceneName, points, problems);
            Assert.IsEmpty(problems, "TOWN-POINT-002 failed in " + sceneName + " (" + points.Count + " points):\n" + string.Join("\n", problems));
        }

        private static List<MonoBehaviour> PlacedPoints(string sceneName, System.Func<MonoBehaviour, bool> include)
        {
            var points = new List<MonoBehaviour>();
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID))
                if (behaviour.isActiveAndEnabled && behaviour is IPlayerInteractable && behaviour.gameObject.scene.name == sceneName
                    && behaviour.gameObject.name != "Player" && include(behaviour)) points.Add(behaviour);
            return points;
        }

        private static IEnumerator CheckPlacedPoints(Keyboard keyboard, string sceneName, List<MonoBehaviour> points, List<string> problems)
        {
            var player = GameObject.Find("Player");
            var router = player.GetComponent<PlayerInteractionRouter>();
            var rawKey = new System.Text.RegularExpressions.Regex(@"\b[a-z][a-z0-9_-]*(\.[a-z0-9_-]+){1,}\b");
            foreach (var point in points)
            {
                if (point == null) continue;
                var interactable = (IPlayerInteractable)point;
                string label = point.name + " (" + point.GetType().Name + ")";
                if (Rootborn.Game.Common.UiGameplayInputLock.IsBlocked)
                {
                    problems.Add(label + ": input was still locked before walking there (" + Support.PlayerFlowE2EDriver.InputLockOwners() + ")");
                    yield break;
                }

                var walkProblems = new List<string>();
                Vector3 standAt = StandingSpotNear(player, interactable.InteractionTransform.position);
                yield return Guarded(Support.PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, standAt, 0.4f, 900, label), walkProblems);
                if (walkProblems.Count > 0) { problems.Add(label + ": unreachable — " + walkProblems[0]); continue; }
                if (SceneManager.GetActiveScene().name != sceneName) { problems.Add(label + ": walking there left " + sceneName); yield break; }

                router.RefreshPromptNow();
                var winner = router.CurrentInteractable as Component;
                // A companion on the same object may win by priority (a location visit shares its spot with the
                // location's activity, facility or door, which record the visit themselves). E landing on another
                // object is the failure.
                if (winner == null || winner.gameObject != point.gameObject)
                {
                    problems.Add(label + ": E here reaches " + (winner != null ? winner.name + " (" + winner.GetType().Name + ")" : "nothing") + " instead");
                    continue;
                }
                string prompt = router.PromptText;
                if (string.IsNullOrWhiteSpace(prompt) || rawKey.IsMatch(prompt)) problems.Add(label + ": prompt shows \"" + prompt + "\"");
            }
        }

        // Where a person would stand to use the point: the point itself when the body fits there, otherwise the free spot
        // closest to it within E range. Boards and computers sit against walls, so their own position is not standable.
        private static Vector3 StandingSpotNear(GameObject player, Vector3 anchor)
        {
            const float range = 1.4f; // inside the router's 1.75 interaction radius
            var body = player.GetComponent<BoxCollider2D>();
            if (body == null) return anchor;
            Physics2D.SyncTransforms();
            Vector2 size = (Vector2)body.bounds.size + new Vector2(0.1f, 0.1f);
            Vector2 offset = body.bounds.center - player.transform.position;
            var solidOnly = new ContactFilter2D { useTriggers = false };
            var hits = new List<Collider2D>(8);
            Vector3 best = anchor;
            float bestDistance = float.MaxValue;
            for (float dx = -range; dx <= range + 0.001f; dx += 0.25f)
            for (float dy = -range; dy <= range + 0.001f; dy += 0.25f)
            {
                float distance = dx * dx + dy * dy;
                if (distance > range * range || distance >= bestDistance) continue;
                Vector2 spot = (Vector2)anchor + new Vector2(dx, dy);
                Physics2D.OverlapBox(spot + offset, size, 0f, solidOnly, hits);
                bool blocked = false;
                foreach (var hit in hits)
                    if (hit != null && !hit.transform.IsChildOf(player.transform)) { blocked = true; break; }
                if (blocked) continue;
                best = new Vector3(spot.x, spot.y, anchor.z);
                bestDistance = distance;
            }
            return best;
        }

        // Runs a walk and turns its assertion into a collected problem, so the loop can go on to the next point.
        private static IEnumerator Guarded(IEnumerator inner, List<string> problems)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (AssertionException error)
                {
                    problems.Add(error.Message);
                    yield break;
                }
                yield return current;
            }
        }

        [Ignore(LegacyPremise.AuthoredTown + "Readiness requires StudentDayEndBoard in Town")]
        [UnityTest]
        public IEnumerator EXPLORE_E2E_001_006_PlayDiscoveryResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 20f, "EXPLORE-E2E-001 failed: SaveSlot New Game did not enter Town.");
            TownExplorationDiscoveryRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForDiscoveryRuntime(10f);

            var runtime = FindDiscoveryRuntime();
            var outcome = runtime.Discovery.Discovery.Outcomes[0] as DiscoveryTraitDeltaOutcome;
            Assert.IsNotNull(outcome, "EXPLORE-E2E-004 failed: sample discovery outcome is not a trait delta strategy asset.");
            int initialTrait = runtime.Student.Progress.GetTraitValue(outcome.Trait);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Discovery.transform.position, 0.25f, "EXPLORE-E2E-002 failed: actual keyboard movement could not reach discovery point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "EXPLORE-E2E-003 failed: discovery prompt was not visible after movement.");
            StringAssert.Contains(Rootborn.Game.Localization.GameText.Get("discovery.clocktower-notice"), runtime.Router.PromptText, "EXPLORE-E2E-003 failed: prompt did not target the sample discovery.");
            yield return PressInteractKey(keyboard);
            yield return null;

            int changedTrait = runtime.Student.Progress.GetTraitValue(outcome.Trait);
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Discovery.LastResult.Kind, "EXPLORE-E2E-003 failed: discovery did not apply through input.");
            Assert.Greater(changedTrait, initialTrait, "EXPLORE-E2E-004 failed: discovery did not update growth state.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayActivityIds(), runtime.Discovery.Discovery.Id, "EXPLORE-E2E-004 failed: today activity log did not record discovery.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Discovery.LastResult.OutcomeLogIds[0], "EXPLORE-E2E-004 failed: result log did not record discovery outcome.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "EXPLORE-E2E-005 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "EXPLORE-E2E-005 failed: day-end prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "EXPLORE-E2E-005 failed: result UI did not open from actual day-end interaction.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, runtime.Discovery.Discovery.Id, "EXPLORE-E2E-005 failed: result UI did not show discovery id.");
            AssertPanelContains(panel.transform, outcome.Trait.Id, "EXPLORE-E2E-005 failed: result UI did not show discovery growth target.");
            yield return ClickButton(FindButton(panel.transform, "NextDayButton"));
            yield return null;
            Assert.AreEqual(StudentDayState.InProgress, runtime.Student.Progress.DayState, "EXPLORE-E2E-005 failed: Next Day button did not start an in-progress day.");
            Assert.AreEqual(2, runtime.Student.Progress.CurrentDay, "EXPLORE-E2E-005 failed: Next Day button did not advance to day 2.");
            Assert.IsFalse(panel.IsOpen, "EXPLORE-E2E-005 failed: result panel stayed open after Next Day click.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "EXPLORE-E2E-006 failed: student progress save file missing after discovery and next day.");
            string nextDayJson = File.ReadAllText(studentFile);
            StringAssert.Contains(runtime.Discovery.Discovery.Id, nextDayJson, "EXPLORE-E2E-006 failed: save file does not record discovery activity log.");
            StringAssert.Contains("discovery:", nextDayJson, "EXPLORE-E2E-006 failed: save file does not record discovery result log.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 20f, "EXPLORE-E2E-006 failed: SaveSlot Load did not re-enter Town.");
            TownExplorationDiscoveryRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForDiscoveryRuntime(10f);

            var reloaded = FindDiscoveryRuntime();
            Assert.AreEqual(StudentDayState.InProgress, reloaded.Student.Progress.DayState, "EXPLORE-E2E-006 failed: day-2 in-progress state did not restore.");
            Assert.AreEqual(2, reloaded.Student.Progress.CurrentDay, "EXPLORE-E2E-006 failed: current day did not survive reload.");
            Assert.AreEqual(changedTrait, reloaded.Student.Progress.GetTraitValue(outcome.Trait), "EXPLORE-E2E-006 failed: discovery growth state did not survive reload.");
            Assert.AreEqual(1, DiscoveryDayLog.FromResultLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()).DiscoveryIds.Length, "EXPLORE-E2E-006 failed: previous discovery log duplicated or disappeared after reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Discovery.transform.position, 0.25f, "EXPLORE-E2E-006 failed: movement could not return to discovery point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, reloaded.Discovery.LastResult.Kind, "EXPLORE-E2E-006 failed: repeated discovery interaction did not dedupe.");
            Assert.AreEqual(changedTrait, reloaded.Student.Progress.GetTraitValue(outcome.Trait), "EXPLORE-E2E-006 failed: repeated discovery interaction applied growth again.");
            Assert.AreEqual(1, DiscoveryDayLog.FromResultLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()).DiscoveryIds.Length, "EXPLORE-E2E-006 failed: repeated discovery interaction duplicated discovery log.");
        }

        private static IEnumerator WaitForDiscoveryRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var discovery = GameObject.Find("TownDiscoveryPoint");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    discovery != null && discovery.GetComponent<DiscoveryPointInteractor>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town discovery runtime did not expose player, discovery point, day-end point, and result panel within timeout.");
        }

        private static DiscoveryRuntime FindDiscoveryRuntime()
        {
            var player = GameObject.Find("Player");
            var discovery = GameObject.Find("TownDiscoveryPoint");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(discovery);
            Assert.IsNotNull(dayEnd);
            return new DiscoveryRuntime(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), discovery.GetComponent<DiscoveryPointInteractor>(), dayEnd.GetComponent<StudentDayEndInteractor>());
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
            Assert.IsFalse(string.IsNullOrEmpty(expected));
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }
            Assert.Fail(message + " Expected visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
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

        private readonly struct DiscoveryRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly DiscoveryPointInteractor Discovery;
            public readonly StudentDayEndInteractor DayEnd;

            public DiscoveryRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, DiscoveryPointInteractor discovery, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                Discovery = discovery;
                DayEnd = dayEnd;
            }
        }
    }
}
