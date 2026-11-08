using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
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
    public sealed class StudentDayResultLoopE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-student-day-e2e", System.Guid.NewGuid().ToString("N"));
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

        // End of day in the authored world: walk from Town through the home door, end the day at the board in House,
        // reload, see the same result without settling twice, then start the next day from the result panel.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator DAY_END_001_AuthoredHouse_EndDayReloadShowsSameResultThenNextDay()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "DAY-END-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", "DAY-END-001 failed: could not go home.");

            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int day = student.Progress.CurrentDay;
            Assert.AreEqual(StudentDayState.InProgress, student.Progress.DayState, "DAY-END-001 failed: a new day should be in progress.");
            yield return WalkToDayEndBoardAndInteract(keyboard, "DAY-END-001 failed:");
            yield return WaitForDayResultPanel(3f, "DAY-END-001 failed: ending the day at the board did not open the result panel.");
            Assert.AreEqual(StudentDayState.ResultReady, student.Progress.DayState, "DAY-END-001 failed: the day did not end.");
            Assert.AreEqual(day, student.Progress.CurrentDay, "DAY-END-001 failed: ending the day must not skip to the next day by itself.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "House", "DAY-END-002 failed: Load did not return to House.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var reloaded = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(StudentDayState.ResultReady, reloaded.Progress.DayState, "DAY-END-002 failed: the ended day did not survive reload.");
            Assert.AreEqual(day, reloaded.Progress.CurrentDay, "DAY-END-002 failed: reload changed the day.");
            yield return WalkToDayEndBoardAndInteract(keyboard, "DAY-END-002 failed:");
            yield return WaitForDayResultPanel(3f, "DAY-END-002 failed: the board did not reopen the saved result.");
            Assert.AreEqual(day, reloaded.Progress.CurrentDay, "DAY-END-002 failed: reopening the result settled the day again.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "NextDayButton"));
            yield return null;
            Assert.AreEqual(day + 1, reloaded.Progress.CurrentDay, "DAY-END-003 failed: Next Day did not start the next day.");
            Assert.AreEqual(StudentDayState.InProgress, reloaded.Progress.DayState, "DAY-END-003 failed: the next day is not in progress.");
        }

        private static IEnumerator WalkToDayEndBoardAndInteract(Keyboard keyboard, string failure)
        {
            var board = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(board, failure + " House has no StudentDayEndBoard.");
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, board.transform.position, 0.9f, 900, failure + " could not walk to the day-end board.");
            // After a reload the board is bound to the result panel a moment after the player appears.
            var interactor = board.GetComponent<StudentDayEndInteractor>();
            for (int i = 0; i < 300 && !interactor.CanInteract(player); i++) yield return null;
            Assert.IsTrue(interactor.CanInteract(player), failure + " the day-end board never became usable.");
            var router = player.GetComponent<PlayerInteractionRouter>();
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, failure + " no prompt next to the day-end board.");
            Assert.That(router.PromptText, Does.Not.Contain(Rootborn.Game.Localization.GameText.Localized("ui.prompt.enter", "Enter")), failure + " the prompt points at the door, not the board: " + router.PromptText);
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
        }

        private static IEnumerator WaitForDayResultPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure + " [" + PlayerFlowE2EDriver.InputLockOwners() + " daily event open:" + (GameObject.Find("DailyEventCloseButton") != null) + "]");
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
                if (transform != null && transform.name == objectName)
                {
                    Object.DestroyImmediate(transform.gameObject);
                }
            }
        }

        private readonly struct DayRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly StudentLifeActivityInteractor Study;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly NpcInteractor Npc;
            public readonly DialoguePanel DialoguePanel;

            public DayRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, StudentLifeActivityInteractor study, StudentDayEndInteractor dayEnd, NpcInteractor npc, DialoguePanel dialoguePanel)
            {
                Player = player;
                Router = router;
                Student = student;
                Study = study;
                DayEnd = dayEnd;
                Npc = npc;
                DialoguePanel = dialoguePanel;
            }
        }
    }
}
