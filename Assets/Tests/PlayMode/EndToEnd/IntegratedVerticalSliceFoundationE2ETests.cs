using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.Game.VerticalSlice;
using Rootborn.Tests.PlayMode.Scenarios;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using Rootborn.UI.WorldState;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class IntegratedVerticalSliceFoundationE2ETests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-integrated-vertical-slice-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator TOWN_FLOW_001_003_NewGameTownObjectiveJournalDayResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.AreEqual("TOWN-FLOW-001", ScenarioId.TOWN_FLOW_001);
            Assert.AreEqual("TOWN-FLOW-002", ScenarioId.TOWN_FLOW_002);
            Assert.AreEqual("TOWN-FLOW-003", ScenarioId.TOWN_FLOW_003);
            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var usage = LoadUsageDefinition();
            var route = FindWorldStateGatherRoute(runtime.Provider);
            var flag = usage.SourceFlag;
            var itemReward = FindFirstItemReward(route.Quest);
            var woodItem = ResolveFirstDropItem(runtime.Inventory, route.Objective.TargetResource);

            yield return OpenGuideDialogueByKeyboard(runtime, keyboard);
            yield return ClickButton(FindFirstChoiceButton(runtime.DialoguePanel));
            yield return GatherUntilQuestCompletedByKeyboard(runtime, keyboard, route, woodItem);
            yield return OpenGuideDialogueByKeyboard(runtime, keyboard);
            yield return ClickButton(FindFirstChoiceButton(runtime.DialoguePanel));

            string playerId = ResolvePlayerId(runtime.Inventory);
            var world = WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            Assert.IsTrue(world.IsActive(flag), "WORLD-USAGE-E2E-002 failed: actual quest reward did not create the Shared world-state change.");

            var usageObject = SpawnUsageObjectNearPlayer(runtime.Player, usage, runtime.QuestLogPanel.QuestLog);
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, usageObject.transform.position + new Vector3(0.35f, 0f, 0f), 0.3f, "WORLD-USAGE-E2E-003 failed: keyboard movement could not reach the opened usage object.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "WORLD-USAGE-E2E-004 failed: opened usage object did not expose an interaction prompt.");
            StringAssert.Contains("Inspect", runtime.Router.PromptText, "WORLD-USAGE-E2E-004 failed: prompt did not come from the usage SO data.");
            yield return PressInteractKey(keyboard);
            yield return null;

            var usageInteractor = usageObject.GetComponent<WorldStateUsageInteractor>();
            Assert.AreEqual(WorldStateUsageResultKind.Applied, usageInteractor.LastResult.Kind, "WORLD-USAGE-E2E-005 failed: actual input path did not execute the usage loop.");
            var usageProgress = WorldStateUsageProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var record = usageProgress.GetRecord(usage.Id);
            Assert.AreEqual(1, record.UsedCount, "WORLD-USAGE-E2E-008 failed: personal usage count was not saved.");
            Assert.GreaterOrEqual(record.DiscoveredEncyclopediaEntryIds.Length + record.CareerHintGrantHistory.Length + record.UnlockedFollowUpQuestIds.Length, 2, "WORLD-USAGE-E2E-006 failed: usage did not produce at least two follow-up result categories.");
            var encyclopedia = EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            Assert.GreaterOrEqual(encyclopedia.UnlockedCount, 1, "WORLD-USAGE-E2E-006 failed: usage did not persist an encyclopedia discovery.");
            Assert.GreaterOrEqual(runtime.StudentLife.EnsureProgress().GetCareerHintIds().Length, 1, "WORLD-USAGE-E2E-006 failed: usage did not grant a personal career hint.");
            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(FindFirstFollowUpQuest(usage)), "WORLD-USAGE-E2E-006 failed: usage did not activate its follow-up quest in the playable quest log.");

            var summary = VerticalSliceSummaryBuilder.Build(runtime.StudentLife.EnsureProgress(), questChanged: true, clueChanged: false, worldStateChanged: true, houseMotivation: "House: prepare a study room");
            Assert.GreaterOrEqual(summary.ChangedDomainIds.Length, 2, "VERTICAL-E2E-004 failed: actual player flow did not produce at least two changed domains for the integrated summary.");
            var journal = EnsureObjectiveJournalPanel();
            journal.SetVerticalSliceSummary(summary);
            journal.Show();
            StringAssert.Contains("Choose the next town objective", journal.VisibleText, "VERTICAL-E2E-005 failed: Objective Journal did not show the next action after player flow.");
            StringAssert.Contains("Open Objective Journal", journal.VisibleText, "VERTICAL-E2E-005 failed: Objective Journal omitted follow-up route guidance.");
            yield return CaptureGameViewEvidence("town-core-flow-objective-journal.png");

            var context = new WorldStateUsageContext(world, usageProgress, runtime.StudentLife.EnsureProgress(), runtime.QuestLogPanel.QuestLog, encyclopedia, null, runtime.StudentLife.EnsureProgress().CurrentDay);
            var summaries = WorldStateUsageSummaryBuilder.BuildForSurface(new WorldStateUsageLookupCache(new[] { usage }), context, WorldStateUsageSummarySurface.DayResult);
            yield return OpenDayResultByKeyboard(runtime, keyboard);
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            resultPanel.ShowWorldStateUsages(summaries);
            resultPanel.AppendVerticalSliceGuide(summary);
            Assert.AreEqual(1, resultPanel.GetWorldStateUsageCardCountForTests(), "WORLD-USAGE-E2E-007 failed: day-result UI did not render usage summary card.");
            StringAssert.Contains(usage.DisplayNameKey, resultPanel.GetWorldStateUsageTextForTests());
            StringAssert.Contains("House", resultPanel.NextGuideTextForTests, "VERTICAL-E2E-006/007 failed: day-result UI omitted House or next-life motivation.");
            yield return CaptureGameViewEvidence("town-core-flow-day-result.png");

            var usageLog = WorldStateUsageLogPanel.EnsureInScene(Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include));
            usageLog.Show(summaries);
            Assert.AreEqual(1, usageLog.CardCountForTests, "WORLD-USAGE-E2E-007 failed: world usage log did not render usage summary card.");
            StringAssert.Contains(record.DiscoveredEncyclopediaEntryIds[0], usageLog.TextForTests);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, usageObject.transform.position + new Vector3(0.35f, 0f, 0f), 0.3f, "WORLD-USAGE-E2E-009 failed: keyboard movement could not return to the opened usage object.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "WORLD-USAGE-E2E-009 failed: repeated use did not go through the actual usage prompt.");
            yield return PressInteractKey(keyboard);
            yield return null;
            var afterDuplicate = WorldStateUsageProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(usage.Id);
            Assert.AreEqual(1, afterDuplicate.UsedCount, "WORLD-USAGE-E2E-009 failed: repeated use duplicated a one-shot usage reward.");

            int afterClaimItemCount = runtime.Inventory.Inventory.CountOf(itemReward.Item);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "WORLD-USAGE-E2E-008 failed: save slot UI did not reload Town.");
            yield return WaitForTownRuntime(15f);

            var reloaded = FindRuntime();
            playerId = ResolvePlayerId(reloaded.Inventory);
            usageProgress = WorldStateUsageProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            Assert.AreEqual(1, usageProgress.GetRecord(usage.Id).UsedCount, "WORLD-USAGE-E2E-008 failed: usage state did not survive save-slot reload.");
            var reloadedSummary = VerticalSliceSummaryBuilder.Build(reloaded.StudentLife.EnsureProgress(), questChanged: true, clueChanged: false, worldStateChanged: usageProgress.GetRecord(usage.Id).UsedCount > 0, houseMotivation: "House: prepare a study room");
            var reloadedJournal = EnsureObjectiveJournalPanel();
            reloadedJournal.SetVerticalSliceSummary(reloadedSummary);
            reloadedJournal.Show();
            StringAssert.Contains("Choose the next town objective", reloadedJournal.VisibleText, "VERTICAL-E2E-008 failed: objective UI did not rebuild from reloaded state.");
            Assert.AreEqual(afterClaimItemCount, reloaded.Inventory.Inventory.CountOf(itemReward.Item), "WORLD-USAGE-E2E-009 failed: reload changed unrelated quest reward count.");
        }

        private static WorldStateUsageDefinition LoadUsageDefinition()
        {
            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            var usages = GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsages(registry);
            var cache = new WorldStateUsageLookupCache(usages);
            Assert.IsTrue(cache.TryGetById("usage.library.archive-table", out var usage), "WORLD-USAGE-E2E-010 failed: SO-only usage loop was not loaded from data path.");
            Assert.GreaterOrEqual(usage.Outcomes.Count, 2, "WORLD-USAGE-E2E-010 failed: SO usage loop must have multiple outcome strategies wired.");
            return usage;
        }

        private static GameObject SpawnUsageObjectNearPlayer(GameObject player, WorldStateUsageDefinition usage, QuestLog questLog)
        {
            var go = new GameObject("WorldStateUsage_" + SanitizeObjectName(usage.Id), typeof(BoxCollider2D));
            go.transform.position = player.transform.position + new Vector3(1.2f, 0f, 0f);
            var collider = go.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 0.8f);
            var interactor = go.AddComponent<WorldStateUsageInteractor>();
            interactor.Bind(usage, questLog);
            return go;
        }

        private static IEnumerator CaptureGameViewEvidence(string fileName)
        {
            string directory = Path.Combine(Application.dataPath, "..", "production", "qa", "evidence");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float elapsed = 0f;
            while (!File.Exists(path) && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(File.Exists(path), "VERTICAL-E2E-010 failed: Game View screenshot was not captured: " + path);
        }

        private static ObjectiveJournalPanel EnsureObjectiveJournalPanel()
        {
            var existing = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            Assert.IsNotNull(canvas, "VERTICAL-E2E-005 failed: Canvas missing for Objective Journal.");
            var go = new GameObject("IntegratedVerticalSliceObjectiveJournal", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<ObjectiveJournalPanel>();
        }
        private static IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickButton(FindButton("ConfirmButton"));
            yield return WaitForScene("Town", 15f, "VERTICAL-E2E-001 failed: save slot UI and SPUM confirm did not enter Town.");
            yield return WaitForTownRuntime(15f);
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var npc = GameObject.Find("GuideNpc");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<GatherInteractor>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && npc != null && npc.GetComponent<NpcInteractor>() != null && npc.GetComponent<QuestProvider>() != null && dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && dialoguePanel != null && questLogPanel != null && questLogPanel.QuestLog != null && resultPanel != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("World usage E2E runtime did not expose required Town objects within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            var npcGo = GameObject.Find("GuideNpc");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(npcGo);
            Assert.IsNotNull(dayEnd);
            return new RuntimeRefs(player, player.GetComponent<GatherInteractor>(), player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<PlayerInventory>(), player.GetComponent<StudentLifeProgressComponent>(), npcGo.GetComponent<NpcInteractor>(), npcGo.GetComponent<QuestProvider>(), Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include), Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include), dayEnd.GetComponent<StudentDayEndInteractor>());
        }

        private IEnumerator OpenGuideDialogueByKeyboard(RuntimeRefs runtime, Keyboard keyboard)
        {
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Npc.transform.position + new Vector3(0.75f, 0f, 0f), 0.3f, "WORLD-USAGE-E2E-003 failed: keyboard movement could not reach guide NPC.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible);
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(runtime.DialoguePanel, 3f, "WORLD-USAGE-E2E-005 failed: dialogue panel did not open through actual input.");
            yield return null;
        }

        private IEnumerator OpenDayResultByKeyboard(RuntimeRefs runtime, Keyboard keyboard)
        {
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "WORLD-USAGE-E2E-003 failed: keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible);
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "WORLD-USAGE-E2E-007 failed: day-result UI did not open after actual day-end interaction.");
        }

        private IEnumerator GatherUntilQuestCompletedByKeyboard(RuntimeRefs runtime, Keyboard keyboard, WorldStateGatherRoute route, ItemDefinition item)
        {
            int questGuard = 0;
            while (runtime.QuestLogPanel.QuestLog.GetState(route.Quest) != QuestState.Completed && questGuard < 80)
            {
                int before = runtime.Inventory.Inventory.CountOf(item);
                int interactionGuard = 0;
                while (runtime.Inventory.Inventory.CountOf(item) <= before && interactionGuard < 80)
                {
                    var node = FindTargetResourceNode(route.Objective.TargetResource);
                    Assert.IsNotNull(node);
                    yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, node.transform.position + new Vector3(0.35f, 0f, 0f), 0.3f, "WORLD-USAGE-E2E-003 failed: keyboard movement could not reach target resource node.");
                    runtime.Router.RefreshPromptNow();
                    yield return PressInteractKey(keyboard);
                    yield return null;
                    interactionGuard++;
                }
                Assert.Greater(runtime.Inventory.Inventory.CountOf(item), before);
                questGuard++;
            }
            Assert.AreEqual(QuestState.Completed, runtime.QuestLogPanel.QuestLog.GetState(route.Quest));
        }

        private static ResourceNode FindTargetResourceNode(ResourceNodeDefinition targetResource)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++) if (nodes[i] != null && nodes[i].Definition == targetResource && !nodes[i].IsBroken) return nodes[i];
            return null;
        }

        private static IEnumerator WaitForDialoguePanel(DialoguePanel panel, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static IEnumerator WaitForDayResultPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static WorldStateGatherRoute FindWorldStateGatherRoute(QuestProvider provider)
        {
            Assert.IsNotNull(provider);
            for (int i = 0; i < provider.Quests.Length; i++)
            {
                var quest = provider.Quests[i];
                if (quest == null || quest.Objectives == null || quest.Rewards == null) continue;
                WorldStateChangeQuestReward worldReward = null;
                ItemQuestReward itemReward = null;
                for (int r = 0; r < quest.Rewards.Length; r++)
                {
                    if (quest.Rewards[r] is WorldStateChangeQuestReward candidate && candidate.Flags.Length > 0 && candidate.Flags[0] != null) worldReward = candidate;
                    if (quest.Rewards[r] is ItemQuestReward itemCandidate && itemCandidate.Item != null) itemReward = itemCandidate;
                }
                if (worldReward == null || itemReward == null) continue;
                for (int j = 0; j < quest.Objectives.Length; j++) if (quest.Objectives[j] is GatherQuestObjective objective && objective.TargetResource != null) return new WorldStateGatherRoute(quest, objective, worldReward);
            }
            Assert.Fail("WORLD-USAGE-E2E-002 failed: no gather quest exposes both item and WorldStateChangeQuestReward data.");
            return default;
        }

        private static Button FindFirstChoiceButton(DialoguePanel panel)
        {
            var root = FindChild(panel.transform, "ChoiceButtons");
            Assert.IsNotNull(root);
            var buttons = root.GetComponentsInChildren<Button>(true);
            Assert.Greater(buttons.Length, 0);
            return buttons[0];
        }

        private static ItemDefinition ResolveFirstDropItem(PlayerInventory inventory, ResourceNodeDefinition resource)
        {
            var item = inventory.FindById(resource.Drops[0].ResourceId);
            Assert.IsNotNull(item);
            return item;
        }

        private static ItemQuestReward FindFirstItemReward(QuestDefinition quest)
        {
            for (int i = 0; i < quest.Rewards.Length; i++) if (quest.Rewards[i] is ItemQuestReward reward && reward.Item != null) return reward;
            Assert.Fail("WORLD-USAGE-E2E expected an item reward on the quest.");
            return null;
        }

        private static QuestDefinition FindFirstFollowUpQuest(WorldStateUsageDefinition usage)
        {
            for (int i = 0; i < usage.Outcomes.Count; i++)
            {
                if (usage.Outcomes[i] is WorldStateUsageQuestUnlockOutcome outcome && outcome.Quests.Count > 0 && outcome.Quests[0] != null) return outcome.Quests[0];
            }

            Assert.Fail("WORLD-USAGE-E2E expected a quest unlock outcome on the usage definition.");
            return null;
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
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(string objectName)
        {
            var go = GameObject.Find(objectName);
            Assert.IsNotNull(go, "UI button missing: " + objectName);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button);
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

        private static string SanitizeObjectName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
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
            DestroyAllNamed("WorldStateUsageLogPanel");
            DestroyAllNamed("IntegratedVerticalSliceObjectiveJournal");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--) if (transforms[i] != null && transforms[i].name == objectName) Object.DestroyImmediate(transforms[i].gameObject);
        }

        private readonly struct WorldStateGatherRoute
        {
            public readonly QuestDefinition Quest;
            public readonly GatherQuestObjective Objective;
            public readonly WorldStateChangeQuestReward WorldReward;
            public WorldStateGatherRoute(QuestDefinition quest, GatherQuestObjective objective, WorldStateChangeQuestReward worldReward)
            {
                Quest = quest;
                Objective = objective;
                WorldReward = worldReward;
            }
        }

        private readonly struct RuntimeRefs
        {
            public readonly GameObject Player;
            public readonly GatherInteractor Gather;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent StudentLife;
            public readonly NpcInteractor Npc;
            public readonly QuestProvider Provider;
            public readonly DialoguePanel DialoguePanel;
            public readonly QuestLogPanel QuestLogPanel;
            public readonly StudentDayEndInteractor DayEnd;
            public RuntimeRefs(GameObject player, GatherInteractor gather, PlayerInteractionRouter router, PlayerInventory inventory, StudentLifeProgressComponent studentLife, NpcInteractor npc, QuestProvider provider, DialoguePanel dialoguePanel, QuestLogPanel questLogPanel, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Gather = gather;
                Router = router;
                Inventory = inventory;
                StudentLife = studentLife;
                Npc = npc;
                Provider = provider;
                DialoguePanel = dialoguePanel;
                QuestLogPanel = questLogPanel;
                DayEnd = dayEnd;
            }
        }
    }
}
