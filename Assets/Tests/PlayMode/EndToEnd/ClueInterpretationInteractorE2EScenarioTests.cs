using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.DiscoveryClues;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class ClueInterpretationInteractorE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-clue-interpretation-interactor-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator CLUE_INTERPRET_E2E_012_PlayerMovesToInterpretationInteractorAndCompletesViaPromptAndChoiceButton()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var registry = Rootborn.Game.Managers.Managers.Data.Registry;
            var clue = LoadRegisteredPlayClue(registry);
            var interpretations = GameDataRegistryClueInterpretationExtensions.GetClueInterpretations(registry);
            string playerId = ResolvePlayerId(runtime.Inventory);
            yield return RevealClueThroughSourceButton(runtime, clue);

            var interactorObject = SpawnInterpretationInteractorNearPlayer(runtime.Player, interpretations, runtime.QuestLogPanel.QuestLog);
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, interactorObject.transform.position + new Vector3(0.25f, 0f, 0f), 0.3f, "CLUE-INTERPRET-E2E-012 failed: keyboard movement could not reach interpretation interactor.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "CLUE-INTERPRET-E2E-012 failed: interpretation interactor did not expose a prompt.");
            yield return PressInteractKey(keyboard);
            var panel = Object.FindFirstObjectByType<ClueInterpretationChoicePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "CLUE-INTERPRET-E2E-012 failed: pressing interact did not open interpretation choice UI.");
            Assert.GreaterOrEqual(panel.ButtonCountForTests, 1);
            yield return ClickButton(panel.GetButtonForTests(0));

            var interactor = interactorObject.GetComponent<ClueInterpretationInteractor>();
            Assert.AreEqual(ClueInterpretationResultKind.Completed, interactor.LastResult.Kind, "CLUE-INTERPRET-E2E-012 failed: choice button did not complete interpretation.");
            Assert.IsTrue(ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(interactor.LastResult.InterpretationId).Completed, "CLUE-INTERPRET-E2E-012 failed: completed interpretation was not saved.");
        }

        private IEnumerator RevealClueThroughSourceButton(RuntimeRefs runtime, DiscoveryClueDefinition clue)
        {
            var source = FindSource(clue, DiscoveryClueSourceKind.NpcDialogue);
            string playerId = ResolvePlayerId(runtime.Inventory);
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var context = new DiscoveryClueContext(Rootborn.Game.WorldState.WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId), clueProgress, runtime.StudentLife.EnsureProgress(), runtime.QuestLogPanel.QuestLog, Rootborn.Game.Encyclopedia.EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId), runtime.StudentLife.EnsureProgress().CurrentDay);
            var summaries = DiscoveryClueSummaryBuilder.BuildForSource(new DiscoveryClueLookupCache(new[] { clue }), DiscoveryClueSourceKind.NpcDialogue, source.Id, context, DiscoveryClueSummarySurface.NpcDialogue);
            DiscoveryClueResult revealResult = default;
            var panel = DiscoveryClueSourcePanel.EnsureInScene(Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include));
            panel.Show(summaries, selected =>
            {
                new DiscoveryClueRunner().TryRevealSource(clue, source, context, out revealResult);
                DiscoveryClueProgressPersistence.Save(clueProgress);
            });
            yield return ClickButton(panel.GetButtonForTests(0));
            Assert.AreEqual(DiscoveryClueResultKind.Revealed, revealResult.Kind);
        }

        private static GameObject SpawnInterpretationInteractorNearPlayer(GameObject player, ClueInterpretationDefinition[] interpretations, QuestLog questLog)
        {
            var go = new GameObject("ClueInterpretation_NpcPrompt", typeof(BoxCollider2D));
            go.transform.position = player.transform.position + new Vector3(1.2f, 0f, 0f);
            var collider = go.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 0.8f);
            var interactor = go.AddComponent<ClueInterpretationInteractor>();
            interactor.Bind(interpretations, ClueInterpretationSourceKind.NpcDialogue, "npc.librarian", questLog);
            return go;
        }

        private static DiscoveryClueDefinition LoadRegisteredPlayClue(GameDataRegistry registry)
        {
            var cache = new DiscoveryClueLookupCache(GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClues(registry));
            Assert.IsTrue(cache.TryGetById("clue.discovery.play-loop", out var clue));
            return clue;
        }

        private static DiscoveryClueSourceDefinition FindSource(DiscoveryClueDefinition clue, DiscoveryClueSourceKind kind)
        {
            for (int i = 0; i < clue.Sources.Count; i++) if (clue.Sources[i] != null && clue.Sources[i].Kind == kind) return clue.Sources[i];
            Assert.Fail("Missing clue source: " + kind);
            return null;
        }

        private IEnumerator StartNewTownFromSaveSlotUi()
        {
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 15f, "CLUE-INTERPRET-E2E-012 failed: save slot UI did not enter Town.");
            yield return WaitForTownRuntime(15f);
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && questLogPanel != null && questLogPanel.QuestLog != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Clue interpretation interactor E2E runtime did not expose required Town objects within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            return new RuntimeRefs(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<PlayerInventory>(), player.GetComponent<StudentLifeProgressComponent>(), Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 720 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
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
            Assert.IsNotNull(op);
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
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable);
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
            return child.GetComponent<Button>();
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

        private static string ResolvePlayerId(PlayerInventory inventory)
        {
            var identity = inventory.GetComponent<PlayerIdentity>();
            return identity != null ? identity.PlayerId : "player";
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
            DestroyAllNamed("DiscoveryClueSourcePanel");
            DestroyAllNamed("ClueInterpretationChoicePanel");
            DestroyAllNamed("ClueInterpretation_NpcPrompt");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--) if (transforms[i] != null && transforms[i].name == objectName) Object.DestroyImmediate(transforms[i].gameObject);
        }

        private readonly struct RuntimeRefs
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent StudentLife;
            public readonly QuestLogPanel QuestLogPanel;
            public RuntimeRefs(GameObject player, PlayerInteractionRouter router, PlayerInventory inventory, StudentLifeProgressComponent studentLife, QuestLogPanel questLogPanel)
            {
                Player = player;
                Router = router;
                Inventory = inventory;
                StudentLife = studentLife;
                QuestLogPanel = questLogPanel;
            }
        }
    }
}
