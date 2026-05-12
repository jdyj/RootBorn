using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Encyclopedia;
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
    public sealed class EncyclopediaDiscoveryUiE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-encyclopedia-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
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
        public IEnumerator ENCYCLOPEDIA_E2E_001_008_SaveSlotDiscoveryUiReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "ENCYCLOPEDIA-E2E-001 failed: SaveSlot New Game did not enter Town.");
            TownExplorationDiscoveryRuntimeInstaller.EnsureForActiveScene();
            EncyclopediaRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var runtime = FindRuntime();
            Assert.AreEqual(0, runtime.Encyclopedia.Progress.UnlockedCount, "Encyclopedia should start with no discovered entries in a fresh slot.");
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Discovery.transform.position, 0.25f, "ENCYCLOPEDIA-E2E-002 failed: actual keyboard movement could not reach discovery point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "ENCYCLOPEDIA-E2E-002 failed: discovery prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Discovery.LastResult.Kind, "ENCYCLOPEDIA-E2E-002 failed: discovery did not apply through input.");
            Assert.GreaterOrEqual(runtime.Encyclopedia.Progress.UnlockedCount, 1, "ENCYCLOPEDIA-E2E-002 failed: discovery did not unlock an encyclopedia entry.");
            Assert.AreEqual(1, runtime.Encyclopedia.Progress.NewCount, "ENCYCLOPEDIA-E2E-008 failed: first unlock should create one new marker.");

            yield return ClickButton(FindButton("EncyclopediaButton"));
            var panel = Object.FindFirstObjectByType<EncyclopediaPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "ENCYCLOPEDIA-E2E-003 failed: encyclopedia panel was not installed.");
            Assert.IsTrue(panel.IsVisible, "ENCYCLOPEDIA-E2E-003 failed: UI button did not open encyclopedia panel.");
            AssertPanelContains(panel.transform, "???", "ENCYCLOPEDIA-E2E-005 failed: locked entries must be visible as hidden rows.");
            AssertPanelContains(panel.transform, "discovery", "ENCYCLOPEDIA-E2E-006 failed: unlocked detail text did not render.");
            yield return ClickButton(FindButton(panel.transform, "Category_ency.category.npcs"));
            AssertPanelContains(panel.transform, "NPC", "ENCYCLOPEDIA-E2E-004 failed: category tab switch did not render NPC tab content.");
            Assert.AreEqual(0, runtime.Encyclopedia.Progress.NewCount, "ENCYCLOPEDIA-E2E-008 failed: viewing the unlocked entry should clear the new marker.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "ENCYCLOPEDIA-E2E-007 failed: SaveSlot Load did not re-enter Town.");
            TownExplorationDiscoveryRuntimeInstaller.EnsureForActiveScene();
            EncyclopediaRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var reloaded = FindRuntime();
            Assert.GreaterOrEqual(reloaded.Encyclopedia.Progress.UnlockedCount, 1, "ENCYCLOPEDIA-E2E-007 failed: unlock state did not persist after reload.");
            Assert.AreEqual(0, reloaded.Encyclopedia.Progress.NewCount, "ENCYCLOPEDIA-E2E-007 failed: seen new state did not persist after reload.");
            int before = reloaded.Encyclopedia.Progress.UnlockedCount;
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Discovery.transform.position, 0.25f, "ENCYCLOPEDIA-E2E-008 failed: movement could not return to discovery after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, reloaded.Discovery.LastResult.Kind, "ENCYCLOPEDIA-E2E-008 failed: repeated discovery interaction did not dedupe.");
            Assert.AreEqual(before, reloaded.Encyclopedia.Progress.UnlockedCount, "ENCYCLOPEDIA-E2E-008 failed: repeated discovery created duplicate unlocks.");
            Assert.AreEqual(0, reloaded.Encyclopedia.Progress.NewCount, "ENCYCLOPEDIA-E2E-008 failed: repeated discovery recreated a new marker.");
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var discovery = GameObject.Find("TownDiscoveryPoint");
                var button = GameObject.Find("EncyclopediaButton");
                var panel = Object.FindFirstObjectByType<EncyclopediaPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<EncyclopediaProgressComponent>() != null && discovery != null && discovery.GetComponent<DiscoveryPointInteractor>() != null && button != null && panel != null && EventSystem.current != null)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Encyclopedia runtime did not install player progress, discovery point, button, and panel within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            var discovery = GameObject.Find("TownDiscoveryPoint");
            Assert.IsNotNull(player);
            Assert.IsNotNull(discovery);
            return new RuntimeRefs(player, player.GetComponent<PlayerInteractionRouter>(), discovery.GetComponent<DiscoveryPointInteractor>(), player.GetComponent<EncyclopediaProgressComponent>());
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

        private static Button FindButton(string objectName)
        {
            var go = GameObject.Find(objectName);
            Assert.IsNotNull(go, "UI object missing: " + objectName);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, objectName + " does not have Button component.");
            return button;
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
            Assert.Fail(message + " Expected visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
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
            DestroyAllNamed("@Managers");
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
            DestroyAllNamed("EncyclopediaButton");
            DestroyAllNamed("EncyclopediaPanel");
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

        private readonly struct RuntimeRefs
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly DiscoveryPointInteractor Discovery;
            public readonly EncyclopediaProgressComponent Encyclopedia;

            public RuntimeRefs(GameObject player, PlayerInteractionRouter router, DiscoveryPointInteractor discovery, EncyclopediaProgressComponent encyclopedia)
            {
                Player = player;
                Router = router;
                Discovery = discovery;
                Encyclopedia = encyclopedia;
            }
        }
    }
}
