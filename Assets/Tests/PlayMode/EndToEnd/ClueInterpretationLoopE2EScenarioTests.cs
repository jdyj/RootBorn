using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.DiscoveryClues;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class ClueInterpretationLoopE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-clue-interpretation-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator CLUE_INTERPRET_E2E_001_011_SaveSlotUiChoicePanelCompletesPersistsAndDedupesSoOnlyInterpretationPath()
        {
            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            Assert.IsNotNull(registry, "CLUE-INTERPRET-E2E-001 failed: Town runtime did not expose GameDataRegistry.");
            var clue = LoadRegisteredPlayClue(registry);
            var interpretations = GameDataRegistryClueInterpretationExtensions.GetClueInterpretations(registry);
            var interpretationCache = new ClueInterpretationLookupCache(interpretations);
            Assert.GreaterOrEqual(interpretationCache.GetByClue(clue.Id).Length, 3, "CLUE-INTERPRET-E2E-011 failed: SO-only interpretation data did not expose 3 paths.");
            string playerId = ResolvePlayerId(runtime.Inventory);

            yield return RevealClueThroughSourceButton(runtime, clue, DiscoveryClueSourceKind.NpcDialogue, DiscoveryClueSummarySurface.NpcDialogue);
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            Assert.IsTrue(clueProgress.GetRecord(clue.Id).Seen, "CLUE-INTERPRET-E2E-002 failed: clue was not discovered through an actual UI button path.");

            var context = MakeContext(runtime, clueProgress, ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId), playerId);
            var npcSummaries = ClueInterpretationSummaryBuilder.BuildForSource(interpretationCache, ClueInterpretationSourceKind.NpcDialogue, "npc.librarian", context, ClueInterpretationSummarySurface.NpcDialogue);
            var objectSummaries = ClueInterpretationSummaryBuilder.BuildForSource(interpretationCache, ClueInterpretationSourceKind.ObjectInteraction, "object.workbench", context, ClueInterpretationSummarySurface.ObjectPanel);
            Assert.GreaterOrEqual(npcSummaries.Length, 1, "CLUE-INTERPRET-E2E-004 failed: NPC interpretation path was not visible from SO source data.");
            Assert.GreaterOrEqual(objectSummaries.Length, 1, "CLUE-INTERPRET-E2E-005 failed: object interaction interpretation path was not visible from SO source data.");

            ClueInterpretationResult selectedResult = default;
            var choicePanel = ClueInterpretationChoicePanel.EnsureInScene(Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include));
            choicePanel.Show(npcSummaries, selected =>
            {
                var runner = new ClueInterpretationRunner();
                var currentProgress = ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId);
                var currentContext = MakeContext(runtime, DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId), currentProgress, playerId);
                var selectedDefinition = FindInterpretation(interpretations, selected.InterpretationId);
                runner.TryComplete(selectedDefinition, currentContext, out selectedResult);
                ClueInterpretationProgressPersistence.Save(currentProgress);
                EncyclopediaProgressPersistence.Save(currentContext.EncyclopediaProgress);
                WorldStateProgressPersistence.Save(currentContext.WorldStateProgress);
            });
            yield return ClickButton(choicePanel.GetButtonForTests(0));
            Assert.AreEqual(ClueInterpretationResultKind.Completed, selectedResult.Kind, "CLUE-INTERPRET-E2E-006 failed: actual interpretation UI button did not complete a path.");

            var savedInterpretation = ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var record = savedInterpretation.GetRecord(selectedResult.InterpretationId);
            Assert.IsTrue(record.Selected && record.Completed && record.RewardClaimed, "CLUE-INTERPRET-E2E-009 failed: selected/completed/reward state was not saved.");
            Assert.GreaterOrEqual(record.EncyclopediaExpansionIds.Length + record.CareerHintGrantHistory.Length + record.FollowUpQuestIds.Length + record.RelatedWorldStateChanges.Length + record.RelationshipChangeIds.Length + record.StatusChangeIds.Length, 2, "CLUE-INTERPRET-E2E-007 failed: interpretation did not apply at least two result categories.");
            Assert.GreaterOrEqual(EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId).UnlockedCount, 1, "CLUE-INTERPRET-E2E-007 failed: encyclopedia expansion did not persist.");
            Assert.GreaterOrEqual(runtime.StudentLife.EnsureProgress().GetCareerHintIds().Length, 1, "CLUE-INTERPRET-E2E-007 failed: career hint was not granted.");

            var duplicateProgress = ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var duplicateContext = MakeContext(runtime, DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId), duplicateProgress, playerId);
            var duplicateDefinition = FindInterpretation(interpretations, selectedResult.InterpretationId);
            Assert.IsFalse(new ClueInterpretationRunner().TryComplete(duplicateDefinition, duplicateContext, out var duplicateResult));
            Assert.AreEqual(ClueInterpretationResultKind.AlreadyCompleted, duplicateResult.Kind, "CLUE-INTERPRET-E2E-010 failed: duplicate interpretation did not report idempotent completion.");
            Assert.AreEqual(1, duplicateProgress.GetRecord(selectedResult.InterpretationId).CompletedCount, "CLUE-INTERPRET-E2E-010 failed: duplicate check changed completion count.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "CLUE-INTERPRET-E2E-009 failed: save slot UI did not reload Town.");
            yield return WaitForTownRuntime(15f);
            var reloaded = FindRuntime();
            playerId = ResolvePlayerId(reloaded.Inventory);
            var restored = ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(selectedResult.InterpretationId);
            Assert.IsTrue(restored.Selected && restored.Completed && restored.RewardClaimed, "CLUE-INTERPRET-E2E-009 failed: interpretation state did not survive save-slot reload.");
        }

        private IEnumerator RevealClueThroughSourceButton(RuntimeRefs runtime, DiscoveryClueDefinition clue, DiscoveryClueSourceKind kind, DiscoveryClueSummarySurface surface)
        {
            var source = FindSource(clue, kind);
            string playerId = ResolvePlayerId(runtime.Inventory);
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var context = new DiscoveryClueContext(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId), clueProgress, runtime.StudentLife.EnsureProgress(), runtime.QuestLogPanel.QuestLog, EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId), runtime.StudentLife.EnsureProgress().CurrentDay);
            var summaries = DiscoveryClueSummaryBuilder.BuildForSource(new DiscoveryClueLookupCache(new[] { clue }), kind, source.Id, context, surface);
            Assert.AreEqual(1, summaries.Length);
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

        private static ClueInterpretationContext MakeContext(RuntimeRefs runtime, DiscoveryClueProgress clueProgress, ClueInterpretationProgress interpretationProgress, string playerId)
        {
            return new ClueInterpretationContext(
                WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId),
                clueProgress,
                interpretationProgress,
                runtime.StudentLife.EnsureProgress(),
                runtime.QuestLogPanel.QuestLog,
                EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId),
                runtime.StudentLife.EnsureProgress().CurrentDay);
        }

        private static DiscoveryClueDefinition LoadRegisteredPlayClue(GameDataRegistry registry)
        {
            var clues = GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClues(registry);
            Assert.AreSame(registry.DiscoveryClues, clues, "CLUE-INTERPRET-E2E-011 failed: clue must load from registered GameDataRegistry arrays.");
            var cache = new DiscoveryClueLookupCache(clues);
            Assert.IsTrue(cache.TryGetById("clue.discovery.play-loop", out var clue));
            return clue;
        }

        private static ClueInterpretationDefinition FindInterpretation(ClueInterpretationDefinition[] interpretations, string interpretationId)
        {
            for (int i = 0; i < interpretations.Length; i++) if (interpretations[i] != null && interpretations[i].Id == interpretationId) return interpretations[i];
            Assert.Fail("Missing interpretation: " + interpretationId);
            return null;
        }

        private static DiscoveryClueSourceDefinition FindSource(DiscoveryClueDefinition clue, DiscoveryClueSourceKind kind)
        {
            for (int i = 0; i < clue.Sources.Count; i++) if (clue.Sources[i] != null && clue.Sources[i].Kind == kind) return clue.Sources[i];
            Assert.Fail("Missing clue source: " + kind);
            return null;
        }

        private static IEnumerator StartNewTownFromSaveSlotUi()
        {
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 15f, "CLUE-INTERPRET-E2E-001 failed: save slot UI did not enter Town.");
            yield return WaitForTownRuntime(15f);
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && questLogPanel != null && questLogPanel.QuestLog != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Clue interpretation E2E runtime did not expose required Town objects within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            return new RuntimeRefs(player.GetComponent<PlayerInventory>(), player.GetComponent<StudentLifeProgressComponent>(), Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
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
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--) if (transforms[i] != null && transforms[i].name == objectName) Object.DestroyImmediate(transforms[i].gameObject);
        }

        private readonly struct RuntimeRefs
        {
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent StudentLife;
            public readonly QuestLogPanel QuestLogPanel;
            public RuntimeRefs(PlayerInventory inventory, StudentLifeProgressComponent studentLife, QuestLogPanel questLogPanel)
            {
                Inventory = inventory;
                StudentLife = studentLife;
                QuestLogPanel = questLogPanel;
            }
        }
    }
}
