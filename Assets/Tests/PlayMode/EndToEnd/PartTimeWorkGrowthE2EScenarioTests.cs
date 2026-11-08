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
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class PartTimeWorkGrowthE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-work-e2e", System.Guid.NewGuid().ToString("N"));
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

        // JOB-001 in the authored Town: walk to the corner store, press E, play its minigame on screen, then reload.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator JOB_001_AuthoredTown_CornerStoreShiftThroughMinigameSurvivesReloadOnce()
        {
            const string WorkId = "work.shift.corner-store";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterTownFromSaveSlot("NewGameButton", "JOB-001 failed: New Game did not enter Town.");

            var player = GameObject.Find("Player");
            var student = player.GetComponent<StudentLifeProgressComponent>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var work = FindAuthoredWork(WorkId);
            var traitOutcome = work.Work.Outcomes.Count > 0 ? work.Work.Outcomes[0] as WorkTraitDeltaOutcome : null;
            Assert.IsNotNull(traitOutcome, "JOB-001 failed: corner store work has no trait outcome to observe.");
            int traitBefore = student.Progress.GetTraitValue(traitOutcome.Trait);
            int spentBefore = student.Progress.TimePointsSpentToday;

            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, work.transform.position + new Vector3(0f, -0.5f, 0f), 0.45f, 900, "JOB-001 failed: keyboard movement could not reach the corner store work point.");
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, "JOB-001 failed: no interaction prompt next to the corner store work point.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);

            Rootborn.UI.Minigames.MinigamePanel panel = null;
            for (int i = 0; i < 120 && (panel == null || !panel.IsOpen); i++)
            {
                panel = Object.FindFirstObjectByType<Rootborn.UI.Minigames.MinigamePanel>(FindObjectsInactive.Include);
                yield return null;
            }
            Assert.IsTrue(panel != null && panel.IsOpen, "JOB-001 failed: pressing E at the work point did not open its minigame.");
            yield return PlayerFlowE2EDriver.PlayMinigameToEnd(panel, 90f, "JOB-001 failed:");
            Assert.IsTrue(panel.Session.Succeeded, "JOB-001 failed: the minigame played from the screen did not succeed (score " + panel.Session.Score + ").");

            int traitAfter = student.Progress.GetTraitValue(traitOutcome.Trait);
            Assert.AreEqual(traitBefore + traitOutcome.Delta, traitAfter, "JOB-001 failed: work growth was not applied exactly once.");
            Assert.Greater(student.Progress.TimePointsSpentToday, spentBefore, "JOB-001 failed: work did not spend time.");
            Assert.AreEqual(1, System.Array.FindAll(student.Progress.GetTodayActivityIds(), id => id == WorkId).Length, "JOB-001 failed: today's log must record the work once.");
            Assert.IsFalse(student.Progress.HasPendingMinigame, "JOB-001 failed: the finished minigame is still pending.");
            if (panel.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "Close"));

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return EnterTownFromSaveSlot("LoadButton", "JOB-001 failed: Load did not re-enter Town.");
            var reloaded = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(traitAfter, reloaded.Progress.GetTraitValue(traitOutcome.Trait), "JOB-001 failed: work growth did not survive reload.");
            Assert.AreEqual(1, System.Array.FindAll(reloaded.Progress.GetTodayActivityIds(), id => id == WorkId).Length, "JOB-001 failed: reload duplicated or lost the work log.");
        }

        // JOB-002: the board-placement and target-and-dodge jobs play to a pass from the screen and grow the trait once.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator JOB_002_AuthoredTown_EveryJobPassesFromTheScreen([Values(
            "work.shift.corner-store", "work.workshop-helper", "work.restaurant-helper", "work.studio-helper", "work.supply-helper",
            "work.training-helper", "work.clinic-helper", "work.fitness-helper", "work.errand-helper")] string workId)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return EnterTownFromSaveSlot("NewGameButton", "JOB-002 failed: New Game did not enter Town.");
            var player = GameObject.Find("Player");
            var student = player.GetComponent<StudentLifeProgressComponent>();
            var work = FindAuthoredWork(workId);
            var traitOutcome = work.Work.Outcomes.Count > 0 ? work.Work.Outcomes[0] as WorkTraitDeltaOutcome : null;
            Assert.IsNotNull(traitOutcome, "JOB-002 failed: " + workId + " has no trait outcome to observe.");
            int traitBefore = student.Progress.GetTraitValue(traitOutcome.Trait);

            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, work.transform.position, "JOB-002 failed: " + workId);
            Rootborn.UI.Minigames.MinigamePanel panel = null;
            for (int i = 0; i < 120 && (panel == null || !panel.IsOpen); i++)
            {
                panel = Object.FindFirstObjectByType<Rootborn.UI.Minigames.MinigamePanel>(FindObjectsInactive.Include);
                yield return null;
            }
            Assert.IsTrue(panel != null && panel.IsOpen, "JOB-002 failed: E at " + workId + " did not open its minigame.");
            yield return PlayerFlowE2EDriver.PlayMinigameToEnd(panel, 120f, "JOB-002 failed (" + workId + "):");
            Assert.IsTrue(panel.Session.Succeeded, "JOB-002 failed: " + workId + " played from the screen did not pass (score " + panel.Session.Score + ").");
            Assert.AreEqual(traitBefore + traitOutcome.Delta, student.Progress.GetTraitValue(traitOutcome.Trait), "JOB-002 failed: " + workId + " growth was not applied exactly once.");
            Assert.IsFalse(student.Progress.HasPendingMinigame, "JOB-002 failed: the finished minigame is still pending.");
        }

        private IEnumerator EnterTownFromSaveSlot(string buttonName, string failure)
        {
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return Rootborn.Tests.PlayMode.PlayModeScenarioStart.WaitForGameData();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, buttonName));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, failure);
            float deadline = Time.unscaledTime + 15f;
            while (Time.unscaledTime < deadline)
            {
                var player = GameObject.Find("Player");
                if (player != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<PlayerInventory>() != null
                    && player.GetComponent<PlayerInteractionRouter>() != null && EventSystem.current != null) yield break;
                yield return null;
            }
            Assert.Fail(failure + " Player runtime was not ready in Town.");
        }

        private static PartTimeWorkInteractor FindAuthoredWork(string workId)
        {
            foreach (var candidate in Object.FindObjectsByType<PartTimeWorkInteractor>(FindObjectsSortMode.None))
                if (candidate.Work != null && candidate.Work.Id == workId) return candidate;
            Assert.Fail("JOB-001 failed: authored Town has no work point for " + workId);
            return null;
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
            // Mouse device through the scene UI module, never a direct handler call.
            yield return Rootborn.Tests.PlayMode.EndToEnd.Support.PlayerFlowE2EDriver.ClickButton(button);
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

        private readonly struct WorkRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent Student;
            public readonly PartTimeWorkInteractor Work;
            public readonly StudentDayEndInteractor DayEnd;

            public WorkRuntime(GameObject player, PlayerInteractionRouter router, PlayerInventory inventory, StudentLifeProgressComponent student, PartTimeWorkInteractor work, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Inventory = inventory;
                Student = student;
                Work = work;
                DayEnd = dayEnd;
            }
        }
    }
}
