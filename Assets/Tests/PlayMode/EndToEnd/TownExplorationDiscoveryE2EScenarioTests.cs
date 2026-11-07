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
            var points = PlacedPoints(sceneName, behaviour => behaviour.GetComponent<Rootborn.Game.World.WorldPortal>() == null);
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
                    && behaviour.gameObject.name != "Player" && behaviour.GetComponent<Collider2D>() != null && include(behaviour)) points.Add(behaviour);
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
