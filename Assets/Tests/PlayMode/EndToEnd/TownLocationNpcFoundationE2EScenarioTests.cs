using System.Collections;
using System.IO;
using NUnit.Framework;
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
    public sealed class TownLocationNpcFoundationE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-location-npc-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator LOCNPC_E2E_001_009_SaveSlotMovementLocationNpcInteractionReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickButton(FindButton(GameObject.Find("SpumCharacterCreatorRoot").transform, "ConfirmButton"));
            yield return WaitForScene("Town", 10f, "LOCNPC-E2E-001 failed: SaveSlot New Game and SPUM confirm did not enter Town.");
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForLocationNpcRuntime(10f);

            var runtime = FindRuntime();
            var square = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "LOCNPC-E2E-004 failed: town square location object is missing.");
            var school = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.school"), "LOCNPC-E2E-004 failed: school location object is missing.");
            var guide = RequireObject(LocationNpcRuntimeInstaller.NpcObjectName("npc.first-guide"), "LOCNPC-E2E-002 failed: guide NPC object is missing.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, guide.transform.position, 0.25f, "LOCNPC-E2E-002 failed: actual keyboard movement could not reach guide NPC.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "LOCNPC-E2E-003 failed: guide NPC prompt was not visible after movement.");
            StringAssert.Contains("Talk", runtime.Router.PromptText, "LOCNPC-E2E-003 failed: prompt did not expose NPC interaction.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(3f, "LOCNPC-E2E-003 failed: NPC dialogue UI did not open from actual input.");

            var dialoguePanel = FindOpenDialoguePanel();
            AssertPanelContains(dialoguePanel.transform, "dialogue.first", "LOCNPC-E2E-003 failed: NPC dialogue UI did not render default dialogue text.");
            var visits = new LocationVisitProgress(runtime.Student.Progress);
            Assert.IsTrue(visits.HasMet(guide.GetComponent<NpcInteractor>().Npc), "LOCNPC-E2E-007 failed: NPC meeting was not reflected in domain state.");
            Assert.IsNotNull(guide.GetComponent<NpcInteractor>().Session.Current, "LOCNPC-E2E-003 failed: NPC dialogue did not open from actual input.");

            yield return VisitLocation(runtime, keyboard, square, "Town Square", "LOCNPC-E2E-005 failed: town square prompt was not visible after movement.");
            yield return VisitLocation(runtime, keyboard, school, "School", "LOCNPC-E2E-005 failed: school prompt was not visible after movement.");

            visits = new LocationVisitProgress(runtime.Student.Progress);
            Assert.IsTrue(visits.HasVisited(square.GetComponent<LocationVisitInteractor>().Location), "LOCNPC-E2E-007 failed: town square visit was not reflected in domain state.");
            Assert.IsTrue(visits.HasVisited(school.GetComponent<LocationVisitInteractor>().Location), "LOCNPC-E2E-007 failed: school visit was not reflected in domain state.");
            CollectionAssert.Contains(visits.VisitedLocationIds, "location.town-square", "LOCNPC-E2E-005 failed: location visit feedback state did not include town square.");
            CollectionAssert.Contains(visits.VisitedLocationIds, "location.school", "LOCNPC-E2E-005 failed: location visit feedback state did not include school.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "LOCNPC-E2E-008 failed: student progress save file missing after location/NPC interactions.");
            string savedJson = File.ReadAllText(studentFile);
            StringAssert.Contains("location.town-square", savedJson, "LOCNPC-E2E-008 failed: save file does not record town square visit.");
            StringAssert.Contains("location.school", savedJson, "LOCNPC-E2E-008 failed: save file does not record school visit.");
            StringAssert.Contains("npc.first-guide", savedJson, "LOCNPC-E2E-008 failed: save file does not record guide meeting.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "LOCNPC-E2E-008 failed: SaveSlot Load did not re-enter Town.");
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForLocationNpcRuntime(10f);

            var reloaded = FindRuntime();
            var reloadedSquare = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "LOCNPC-E2E-008 failed: reloaded town square object missing.");
            var reloadedGuide = RequireObject(LocationNpcRuntimeInstaller.NpcObjectName("npc.first-guide"), "LOCNPC-E2E-008 failed: reloaded guide object missing.");
            var reloadedVisits = new LocationVisitProgress(reloaded.Student.Progress);
            Assert.IsTrue(reloadedVisits.HasVisited(reloadedSquare.GetComponent<LocationVisitInteractor>().Location), "LOCNPC-E2E-008 failed: town square visit did not survive reload.");
            Assert.IsTrue(reloadedVisits.HasMet(reloadedGuide.GetComponent<NpcInteractor>().Npc), "LOCNPC-E2E-008 failed: guide meeting did not survive reload.");
            Assert.AreEqual(2, reloadedVisits.VisitedLocationIds.Length, "LOCNPC-E2E-009 failed: visit state duplicated before repeated interaction.");
            Assert.AreEqual(1, reloadedVisits.MetNpcIds.Length, "LOCNPC-E2E-009 failed: NPC state duplicated before repeated interaction.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, LocationApproachPosition(reloadedSquare, "Town Square"), 0.05f, "LOCNPC-E2E-009 failed: movement could not return to town square after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return null;
            reloadedVisits = new LocationVisitProgress(reloaded.Student.Progress);
            Assert.AreEqual(2, reloadedVisits.VisitedLocationIds.Length, "LOCNPC-E2E-009 failed: repeated location visit duplicated discovery state.");
        }

        private IEnumerator VisitLocation(RuntimeState runtime, Keyboard keyboard, GameObject location, string expectedPrompt, string visibilityFailure)
        {
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, LocationApproachPosition(location, expectedPrompt), 0.05f, "LOCNPC-E2E-004 failed: actual keyboard movement could not reach " + expectedPrompt + ".");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, visibilityFailure);
            StringAssert.Contains(expectedPrompt, runtime.Router.PromptText, "LOCNPC-E2E-005 failed: location prompt did not show " + expectedPrompt + ".");
            yield return PressInteractKey(keyboard);
            yield return null;
        }

        private static Vector3 LocationApproachPosition(GameObject location, string expectedPrompt)
        {
            if (expectedPrompt == "Town Square") return location.transform.position + new Vector3(-1.45f, 0f, 0f);
            return location.transform.position;
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

        private static DialoguePanel FindOpenDialoguePanel()
        {
            var panels = Object.FindObjectsByType<DialoguePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < panels.Length; i++) if (panels[i] != null && panels[i].IsOpen) return panels[i];
            return null;
        }

        private static void AssertPanelContains(Transform panel, string expected, string message)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            Assert.Fail(message);
        }

        private static IEnumerator WaitForLocationNpcRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var square = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"));
                var school = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.school"));
                var guide = GameObject.Find(LocationNpcRuntimeInstaller.NpcObjectName("npc.first-guide"));
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    square != null && square.GetComponent<LocationVisitInteractor>() != null &&
                    school != null && school.GetComponent<LocationVisitInteractor>() != null &&
                    guide != null && guide.GetComponent<NpcInteractor>() != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("LOCNPC-E2E runtime did not expose player, two locations, and guide NPC within timeout.");
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

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student)
            {
                Player = player;
                Router = router;
                Student = student;
            }
        }
    }
}
