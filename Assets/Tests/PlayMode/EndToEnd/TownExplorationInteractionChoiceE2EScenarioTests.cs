using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.StudentLife;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class TownExplorationInteractionChoiceE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-exploration-choice-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator EXPLORATION_CHOICE_E2E_001_011_SaveSlotTownPlacedPointChoiceReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "EXPLORATION-CHOICE-E2E-001 failed: SaveSlot New Game did not enter Town.");
            TownExplorationInteractionRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForExplorationRuntime(10f);

            var runtime = FindExplorationRuntime();
            var choice = FindChoiceWithOutcome<ExplorationItemGrantOutcome>(runtime.Interaction.Interaction);
            var itemOutcome = FindOutcome<ExplorationItemGrantOutcome>(choice);
            Assert.IsNotNull(itemOutcome, "EXPLORATION-CHOICE-E2E-011 failed: SO-only test choice must grant an item.");
            var item = ReadObjectField<Rootborn.Game.Common.ItemDefinition>(itemOutcome, "_item");
            Assert.IsNotNull(item, "EXPLORATION-CHOICE-E2E-008 failed: item grant outcome has no item data.");
            int beforeItemCount = runtime.Inventory.Inventory.CountOf(item);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Interaction.transform.position, 0.25f, "EXPLORATION-CHOICE-E2E-003 failed: keyboard movement could not reach placed exploration point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "EXPLORATION-CHOICE-E2E-004 failed: exploration prompt was not visible after movement.");
            StringAssert.Contains("Exploration Test Alley Crate", runtime.Router.PromptText, "EXPLORATION-CHOICE-E2E-004 failed: prompt did not target SO exploration point.");
            yield return PressInteractKey(keyboard);
            yield return null;

            var panel = Object.FindFirstObjectByType<ExplorationChoicePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "EXPLORATION-CHOICE-E2E-005 failed: choice panel missing after actual prompt/input path.");
            Assert.IsTrue(panel.IsOpen, "EXPLORATION-CHOICE-E2E-005 failed: choice panel not open.");
            Assert.GreaterOrEqual(panel.Model.Choices.Length, 2, "EXPLORATION-CHOICE-E2E-006 failed: expected 2+ choices from SO interaction.");
            Assert.IsFalse(panel.Model.Choices[1].Available, "EXPLORATION-CHOICE-E2E-006 failed: locked SO choice was not shown as locked.");

            yield return ClickButton(FindButton(panel.transform, "ChoiceButton_" + choice.Id));
            Assert.AreEqual(ExplorationChoiceResultKind.Applied, runtime.Interaction.LastResult.Kind, "EXPLORATION-CHOICE-E2E-007 failed: actual UI button did not apply choice. Result: " + panel.ResultText);
            Assert.AreEqual(beforeItemCount + 1, runtime.Inventory.Inventory.CountOf(item), "EXPLORATION-CHOICE-E2E-008 failed: item reward not reflected.");
            Assert.IsTrue(panel.ResultText.Contains(item.Id), "EXPLORATION-CHOICE-E2E-008 failed: result UI did not show item grant.");
            Assert.IsTrue(panel.Progress.GetRecord(runtime.Interaction.Interaction.Id).Completed, "EXPLORATION-CHOICE-E2E-009 failed: exploration progress did not mark completion.");

            string explorationSave = ResolveExplorationSavePath(runtime.Student.Progress.PlayerId);
            Assert.IsTrue(File.Exists(explorationSave), "EXPLORATION-CHOICE-E2E-009 failed: exploration progress save file missing.");
            string json = File.ReadAllText(explorationSave);
            StringAssert.Contains(runtime.Interaction.Interaction.Id, json, "EXPLORATION-CHOICE-E2E-009 failed: saved progress lacks interaction id.");
            StringAssert.Contains(choice.Id, json, "EXPLORATION-CHOICE-E2E-009 failed: saved progress lacks selected choice id.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "EXPLORATION-CHOICE-E2E-009 failed: SaveSlot Load did not re-enter Town.");
            TownExplorationInteractionRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForExplorationRuntime(10f);

            var reloaded = FindExplorationRuntime();
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Interaction.transform.position, 0.25f, "EXPLORATION-CHOICE-E2E-009 failed: movement could not return to exploration point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return null;
            var reloadedPanel = Object.FindFirstObjectByType<ExplorationChoicePanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(reloadedPanel.transform, "ChoiceButton_" + choice.Id));
            Assert.AreEqual(ExplorationChoiceResultKind.DuplicateRequest, reloaded.Interaction.LastResult.Kind, "EXPLORATION-CHOICE-E2E-010 failed: repeated loaded result did not dedupe.");
        }

        private static IEnumerator WaitForExplorationRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var point = GameObject.Find("TownExplorationInteractionPoint");
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<PlayerInventory>() != null &&
                    point != null && point.GetComponent<ExplorationInteractionPointInteractor>() != null &&
                    EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town exploration interaction runtime did not expose player and placed interaction point within timeout.");
        }

        private static ExplorationRuntime FindExplorationRuntime()
        {
            var player = GameObject.Find("Player");
            var point = GameObject.Find("TownExplorationInteractionPoint");
            Assert.IsNotNull(player);
            Assert.IsNotNull(point);
            return new ExplorationRuntime(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), player.GetComponent<PlayerInventory>(), point.GetComponent<ExplorationInteractionPointInteractor>());
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

        private static T FindOutcome<T>(ExplorationChoiceDefinition choice) where T : ExplorationOutcomeBase
        {
            for (int i = 0; choice != null && i < choice.Outcomes.Count; i++)
            {
                if (choice.Outcomes[i] is T typed) return typed;
            }

            return null;
        }

        private static ExplorationChoiceDefinition FindChoiceWithOutcome<T>(ExplorationInteractionDefinition interaction) where T : ExplorationOutcomeBase
        {
            for (int i = 0; interaction != null && i < interaction.Choices.Count; i++)
            {
                var choice = interaction.Choices[i];
                if (FindOutcome<T>(choice) != null) return choice;
            }

            return null;
        }

        private static T ReadObjectField<T>(Object target, string fieldName) where T : Object
        {
            var serialized = new SerializedObject(target);
            return serialized.FindProperty(fieldName).objectReferenceValue as T;
        }

        private string ResolveExplorationSavePath(string playerId)
        {
            string fileName = string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId ? "exploration-progress.json" : "exploration-progress-" + playerId + ".json";
            return Path.Combine(_saveRoot, TestSlotId, fileName);
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
            DestroyAllNamed("ExplorationChoicePanel");
            DestroyAllNamed("ExplorationChoiceCanvas");
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

        private readonly struct ExplorationRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly PlayerInventory Inventory;
            public readonly ExplorationInteractionPointInteractor Interaction;

            public ExplorationRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, PlayerInventory inventory, ExplorationInteractionPointInteractor interaction)
            {
                Player = player;
                Router = router;
                Student = student;
                Inventory = inventory;
                Interaction = interaction;
            }
        }
    }
}
