using System.Collections;
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
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class NpcScheduleLifePatternE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-npc-schedule-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator NPCSCHEDULE_E2E_001_011_SaveSlotMovementTimeShiftNpcRelocationInteractionReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickButton(FindButton(GameObject.Find("SpumCharacterCreatorRoot").transform, "ConfirmButton"));
            yield return WaitForScene("Town", 10f, "NPCSCHEDULE-E2E-001 failed: SaveSlot New Game and SPUM confirm did not enter Town.");
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyEventRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForScheduleRuntime(10f);

            var runtime = FindRuntime();
            var teacher = RequireObject(LocationNpcRuntimeInstaller.NpcObjectName("npc.teacher"), "NPCSCHEDULE-E2E-002 failed: teacher NPC object is missing.");
            var school = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.school"), "NPCSCHEDULE-E2E-002 failed: school location object is missing.");
            var square = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "NPCSCHEDULE-E2E-006 failed: town square location object is missing.");
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "NPCSCHEDULE-E2E-005 failed: library location object is missing.");

            Assert.Less(Vector3.Distance(teacher.transform.position, school.transform.position), 3.5f, "NPCSCHEDULE-E2E-002 failed: teacher initial schedule location is not school.");
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, teacher.transform.position, 0.25f, "NPCSCHEDULE-E2E-003 failed: actual keyboard movement could not reach teacher at school.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPCSCHEDULE-E2E-004 failed: teacher prompt was not visible at initial scheduled location.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(3f, "NPCSCHEDULE-E2E-004 failed: teacher dialogue did not open from actual input.");
            var firstDialoguePanel = FindOpenDialoguePanel();
            AssertPanelContains(firstDialoguePanel.transform, "dialogue.teacher.school", "NPCSCHEDULE-E2E-004 failed: teacher did not use school-time schedule dialogue.");
            yield return ClickButton(FindButton(firstDialoguePanel.transform, "CloseButton"));
            yield return WaitForDialogueClosed(2f, "NPCSCHEDULE-E2E-004 failed: teacher dialogue close button did not close the panel.");
            var progress = new NpcScheduleProgress(runtime.Student.Progress);
            Assert.IsTrue(progress.HasMet(teacher.GetComponent<NpcInteractor>().Npc, school.GetComponent<LocationVisitInteractor>().Location, "time.morning"), "NPCSCHEDULE-E2E-009 failed: first scheduled meeting was not recorded.");

            var eventPanel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(eventPanel, "NPCSCHEDULE-E2E-005 failed: daily event panel missing.");
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "NPCSCHEDULE-E2E-005 failed: actual keyboard movement could not reach library to advance time.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyEventPanel(2f, "NPCSCHEDULE-E2E-005 failed: daily event UI did not open from actual location input.");
            int timeBefore = runtime.Student.Progress.TimeMinutes;
            yield return ClickButton(FindButton(eventPanel.transform, "DailyEventChoiceButton_0"));
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, eventPanel.LastResult.Kind, "NPCSCHEDULE-E2E-005 failed: actual event choice did not apply before NPC relocation.");
            Assert.Greater(runtime.Student.Progress.TimeMinutes, timeBefore, "NPCSCHEDULE-E2E-005 failed: actual event choice did not advance student time.");
            yield return WaitForNpcNear(teacher, square.transform.position, 3.5f, 3f, "NPCSCHEDULE-E2E-006 failed: time shift did not move teacher to town square.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, teacher.transform.position, 0.25f, "NPCSCHEDULE-E2E-007 failed: actual keyboard movement could not reach relocated teacher.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPCSCHEDULE-E2E-008 failed: relocated teacher prompt was not visible.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(3f, "NPCSCHEDULE-E2E-008 failed: relocated teacher dialogue did not open from actual input.");
            AssertPanelContains(FindOpenDialoguePanel().transform, "dialogue.teacher.square", "NPCSCHEDULE-E2E-008 failed: teacher did not use changed schedule dialogue.");
            progress = new NpcScheduleProgress(runtime.Student.Progress);
            Assert.IsTrue(progress.HasMet(teacher.GetComponent<NpcInteractor>().Npc, square.GetComponent<LocationVisitInteractor>().Location, "time.afternoon"), "NPCSCHEDULE-E2E-009 failed: relocated scheduled meeting was not recorded.");
            Assert.AreEqual(2, progress.MeetingRecordIds.Length, "NPCSCHEDULE-E2E-010 failed: scheduled meeting records duplicated unexpectedly before reload.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "NPCSCHEDULE-E2E-010 failed: student progress save file missing.");
            string savedJson = File.ReadAllText(studentFile);
            StringAssert.Contains("npc.teacher@location.school@time.morning", savedJson, "NPCSCHEDULE-E2E-010 failed: save file does not record initial teacher meeting.");
            StringAssert.Contains("npc.teacher@location.town-square@time.afternoon", savedJson, "NPCSCHEDULE-E2E-010 failed: save file does not record relocated teacher meeting.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "NPCSCHEDULE-E2E-009 failed: SaveSlot Load did not re-enter Town.");
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyEventRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForScheduleRuntime(10f);

            var reloaded = FindRuntime();
            var reloadedTeacher = RequireObject(LocationNpcRuntimeInstaller.NpcObjectName("npc.teacher"), "NPCSCHEDULE-E2E-009 failed: reloaded teacher object missing.");
            var reloadedSquare = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "NPCSCHEDULE-E2E-009 failed: reloaded square object missing.");
            Assert.Less(Vector3.Distance(reloadedTeacher.transform.position, reloadedSquare.transform.position), 3.5f, "NPCSCHEDULE-E2E-009 failed: reloaded teacher did not restore current time-slot location.");
            var reloadedProgress = new NpcScheduleProgress(reloaded.Student.Progress);
            Assert.AreEqual(2, reloadedProgress.MeetingRecordIds.Length, "NPCSCHEDULE-E2E-010 failed: meeting records changed during reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloadedTeacher.transform.position, 0.25f, "NPCSCHEDULE-E2E-010 failed: movement could not reach reloaded teacher.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return null;
            reloadedProgress = new NpcScheduleProgress(reloaded.Student.Progress);
            Assert.AreEqual(2, reloadedProgress.MeetingRecordIds.Length, "NPCSCHEDULE-E2E-011 failed: repeated scheduled meeting duplicated new markers or rewards.");
        }

        private static IEnumerator WaitForScheduleRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var teacher = GameObject.Find(LocationNpcRuntimeInstaller.NpcObjectName("npc.teacher"));
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var panel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && teacher != null && teacher.GetComponent<NpcInteractor>() != null && library != null && library.GetComponent<LocationActivityInteractor>() != null && panel != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("NPCSCHEDULE-E2E runtime did not expose player, teacher, library event UI, and interaction routing within timeout.");
        }

        private static IEnumerator WaitForNpcNear(GameObject npc, Vector3 target, float maxDistance, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (npc != null && Vector3.Distance(npc.transform.position, target) < maxDistance) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure + " npc=" + (npc != null ? npc.transform.position.ToString() : "null") + " target=" + target);
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>());
        }

        private static GameObject RequireObject(string name, string failure)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, failure);
            return go;
        }

        private static IEnumerator WaitForDialoguePanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (FindOpenDialoguePanel() != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static IEnumerator WaitForDialogueClosed(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (FindOpenDialoguePanel() == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static DialoguePanel FindOpenDialoguePanel()
        {
            var panels = Object.FindObjectsByType<DialoguePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null && panels[i].IsOpen) return panels[i];
            return null;
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

        private static void AssertPanelContains(Transform panel, string expected, string message)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            Assert.Fail(message);
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

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student)
            {
                Player = player;
                Router = router;
                Student = student;
            }
        }
    }
}
