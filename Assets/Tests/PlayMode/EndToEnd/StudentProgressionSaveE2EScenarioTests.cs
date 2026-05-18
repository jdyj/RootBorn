using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
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
    public sealed class AStudentProgressionSaveE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            CleanupSaveSlotUiResidue();
            Time.timeScale = 1f;
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-student-save-e2e", System.Guid.NewGuid().ToString("N"));
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
            CleanupSaveSlotUiResidue();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator STUDENT_E2E_010_011_SaveSlotUiReloadRestoresStudentProgressAfterRealStudyInteraction()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            EnsurePassiveEventSystem();
            var panel = SaveSlotSelectPanel.EnsureInScene();
            panel.Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return null;
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 15f, "STUDENT-E2E-001 failed: New Game button did not enter Town through the save slot UI.");
            yield return WaitForTownStudentRuntime(15f);
            LogAssert.ignoreFailingMessages = false;

            var player = GameObject.Find("Player");
            yield return RebindPlayerInputActions(player);
            var progress = player.GetComponent<StudentLifeProgressComponent>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var studyDesk = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            int initialTrait = progress.Progress.GetTraitValue(studyDesk.PrimaryTrait);
            int initialSkill = progress.Progress.GetSkillValue(studyDesk.PrimarySkill);

            yield return WalkPlayerWithKeyboardTo(player, keyboard, studyDesk.transform.position);
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, "STUDENT-E2E-002 failed: study prompt was not visible after walking to the study object.");
            StringAssert.Contains("Study Basics", router.PromptText, "STUDENT-E2E-002 failed: prompt did not target the study activity.");

            yield return PressInteractKey(keyboard);
            yield return null;

            int studiedTrait = progress.Progress.GetTraitValue(studyDesk.PrimaryTrait);
            int studiedSkill = progress.Progress.GetSkillValue(studyDesk.PrimarySkill);
            Assert.AreEqual(LifeActivityResultKind.Applied, studyDesk.LastResult.Kind, "STUDENT-E2E-003 failed: E interaction did not apply the study activity.");
            Assert.Greater(studiedTrait, initialTrait, "STUDENT-E2E-004 failed: domain trait state did not increase after study.");
            Assert.Greater(studiedSkill, initialSkill, "STUDENT-E2E-004 failed: domain skill state did not increase after study.");
            Assert.IsTrue(progress.Progress.IsCareerHintUnlocked(studyDesk.PrimaryCareer), "STUDENT-E2E-004 failed: career hint was not unlocked after study.");

            string saveFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(saveFile), "STUDENT-E2E-010 failed: real study interaction did not write student-life-progress.json for the active save slot.");
            string savedStudentProgress = File.ReadAllText(saveFile);
            StringAssert.Contains("activity.study-basics", savedStudentProgress, "STUDENT-E2E-010 failed: saved progress does not show which activity was completed.");
            StringAssert.Contains("LastActivityId", savedStudentProgress, "STUDENT-E2E-010 failed: saved progress does not include the last completed activity field.");
            StringAssert.Contains("ActivityLogIds", savedStudentProgress, "STUDENT-E2E-010 failed: saved progress does not include the activity progress log.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            LogAssert.ignoreFailingMessages = true;
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            var loadPanel = SaveSlotSelectPanel.EnsureInScene();
            loadPanel.Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return null;
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "STUDENT-E2E-010 failed: Load button did not re-enter Town through the save slot UI.");
            yield return WaitForTownStudentRuntime(15f);
            LogAssert.ignoreFailingMessages = false;

            var reloadedPlayer = GameObject.Find("Player");
            var reloadedProgress = reloadedPlayer.GetComponent<StudentLifeProgressComponent>();
            var reloadedStudyDesk = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            Assert.AreEqual(studiedTrait, reloadedProgress.Progress.GetTraitValue(reloadedStudyDesk.PrimaryTrait), "STUDENT-E2E-011 failed: saved trait progress was not restored after save UI reload.");
            Assert.AreEqual(studiedSkill, reloadedProgress.Progress.GetSkillValue(reloadedStudyDesk.PrimarySkill), "STUDENT-E2E-011 failed: saved skill progress was not restored after save UI reload.");
            Assert.IsTrue(reloadedProgress.Progress.IsCareerHintUnlocked(reloadedStudyDesk.PrimaryCareer), "STUDENT-E2E-011 failed: saved career hint unlock was not restored after save UI reload.");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone)
            {
                yield return null;
            }
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

        private static IEnumerator WaitForTownStudentRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var studyDesk = GameObject.Find("StudyBasicsActivity");
                if (player != null &&
                    player.GetComponent<PlayerController>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    studyDesk != null && studyDesk.GetComponent<StudentLifeActivityInteractor>() != null &&
                    EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town student runtime did not expose Player, interaction router, and study object within timeout.");
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition)
        {
            var before = player.transform.position;
            yield return HoldKey(keyboard, Key.W, 24);
            yield return ReleaseKeyboard(keyboard, Key.W);
            Assert.Greater(Vector3.Distance(player.transform.position, before), 0.05f, "Player must move through keyboard input before interaction is attempted.");

            Key activeKey = Key.None;
            for (int i = 0; i < 300 && Vector3.Distance(player.transform.position, targetPosition) > 0.2f; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                    ? (delta.x >= 0f ? Key.D : Key.A)
                    : (delta.y >= 0f ? Key.W : Key.S);

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

            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), 0.25f, "Player should be able to walk to the study object using keyboard input.");
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

        private static IEnumerator RebindPlayerInputActions(GameObject player)
        {
            var controller = player.GetComponent<PlayerController>();
            var gather = player.GetComponent<GatherInteractor>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(gather);
            controller.enabled = false;
            gather.enabled = false;
            yield return null;
            controller.enabled = true;
            gather.enabled = true;
            yield return null;
        }

        private IEnumerator HoldKey(Keyboard keyboard, Key key, int frameCount)
        {
            Press(ControlFor(keyboard, key));
            for (int i = 0; i < frameCount; i++)
            {
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
        }

        private IEnumerator ReleaseKeyboard(Keyboard keyboard, Key key)
        {
            Release(ControlFor(keyboard, key));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            if (key == Key.E) return keyboard.eKey;
            Assert.Fail("Unsupported keyboard key for student progression E2E test: " + key);
            return keyboard.eKey;
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
            var button = FindChild(card.transform, buttonName)?.GetComponent<Button>();
            Assert.IsNotNull(button, "Save slot UI button missing: " + buttonName);
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

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
        }

        private static void CleanupSaveSlotUiResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform == null || transform.name != objectName)
                {
                    continue;
                }

                Object.DestroyImmediate(transform.gameObject);
            }
        }
    }
}