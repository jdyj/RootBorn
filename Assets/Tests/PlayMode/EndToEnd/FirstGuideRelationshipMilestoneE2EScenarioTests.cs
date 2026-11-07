using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class FirstGuideRelationshipMilestoneE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string FirstGuideNpcId = "npc.first-guide";
        private const string RelationshipId = "relationship.first-guide.trust";
        private const string HelpActionId = "town.help.first-guide-errand";
        private const string MilestoneId = "milestone.first-guide-trust";
        private const string ObjectiveId = "milestone.objective.first-guide-trust";
        private const string ScreenshotPath = "Builds/Logs/first-guide-relationship-milestone-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 8f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-first-guide-milestone-e2e", System.Guid.NewGuid().ToString("N"));
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
            Time.timeScale = 1f;
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            CleanupResidue();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        // REL-001: Mom lives at home. Walk into the House and talk to her, walk back out and help at the town square, and the
        // trust value rises by the data delta once per action, survives a reload, and a second visit adds exactly one more delta.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator REL_001_AuthoredWorld_TalkToMomAtHomeThenSquareHelpRaisesTrustOncePerAction()
        {
            const string TrustId = "relationship.first-guide.trust";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "REL-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var player = GameObject.Find("Player");
            var student = player.GetComponent<StudentLifeProgressComponent>();
            int trustStart = student.Progress.GetRelationshipValueById(TrustId);

            yield return WaitForMomInScene("Town", false, "REL-001 failed: Mom is still standing in Town.");
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", "REL-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return WaitForMomInScene("House", true, "REL-001 failed: Mom is not at home.");
            var mom = GameObject.Find("TownNpc_npc_first_guide");
            var momNpc = mom.GetComponent<Rootborn.Game.Dialogue.NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, "REL-001 failed: Mom");
            yield return CaptureScreenshot("output/creator-mom-20261107/rel-001-mom-at-home.png");
            Assert.IsTrue(momNpc.Session.IsOpen, "REL-001 failed: E next to Mom did not open her dialogue.");
            var dialogue = Object.FindFirstObjectByType<Rootborn.UI.Quests.DialoguePanel>(FindObjectsInactive.Include);
            if (dialogue != null && dialogue.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(dialogue.transform, "CloseButton"));
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", "REL-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();

            int trustAfterFirst = 0;
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "LocationVisit_location_town_square", 0, "REL-001 failed:");
            trustAfterFirst = student.Progress.GetRelationshipValueById(TrustId);
            Assert.Greater(trustAfterFirst, trustStart, "REL-001 failed: helping at the square did not raise Mom's trust.");
            int delta = trustAfterFirst - trustStart;

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "Town", "REL-002 failed: Load did not return to Town.");
            var reloaded = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(trustAfterFirst, reloaded.Progress.GetRelationshipValueById(TrustId), "REL-002 failed: trust did not survive reload exactly once.");
            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "LocationVisit_location_town_square", 0, "REL-003 failed:");
            Assert.AreEqual(trustAfterFirst + delta, reloaded.Progress.GetRelationshipValueById(TrustId), "REL-003 failed: a second help must add exactly one more delta.");
        }

        // QUEST-DIALOGUE-E2E-001: the quest Mom offers at home is accepted with a mouse click on her dialogue choice, shows in the
        // Tab journal in the House and after walking out to Town, is saved to the slot's quest log file, and loads back once.
        // The hidden Town quest list panel is gone; the player's quest session carries the quest between scenes.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator QUEST_DIALOGUE_E2E_001_AuthoredHouse_ClickMomsAcceptChoiceThenJournalShowsItAcrossScenesAndReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "QUEST-DIALOGUE-E2E-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", "QUEST-DIALOGUE-E2E-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            // Mom offers her quest from the second day: end day one at the home board and press Next Day.
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int firstDay = student.Progress.CurrentDay;
            yield return EndDayAtHomeBoard(keyboard, "QUEST-DIALOGUE-E2E-001 failed:");
            Assert.AreEqual(firstDay + 1, student.Progress.CurrentDay, "QUEST-DIALOGUE-E2E-001 failed: Next Day did not start day two.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return WaitForMomInScene("House", true, "QUEST-DIALOGUE-E2E-001 failed: Mom is not at home.");
            Assert.IsNull(GameObject.Find("QuestLogPanel"), "QUEST-DIALOGUE-E2E-001 failed: the removed hidden quest panel came back.");

            var mom = GameObject.Find("TownNpc_npc_first_guide");
            var momNpc = mom.GetComponent<NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, "QUEST-DIALOGUE-E2E-001 failed: Mom");
            Assert.IsTrue(momNpc.Session.IsOpen, "QUEST-DIALOGUE-E2E-001 failed: E next to Mom did not open her dialogue.");
            int acceptIndex = FindQuestChoice(momNpc.Session.Current, DialogueQuestAction.AcceptQuest);
            Assert.GreaterOrEqual(acceptIndex, 0, "QUEST-DIALOGUE-E2E-001 failed: Mom's dialogue offers no quest to accept.");
            var quest = momNpc.Session.Current.Choices[acceptIndex].Quest;
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(GameObject.Find("Player"), out var quests), "QUEST-DIALOGUE-E2E-001 failed: no quest session for the player.");
            Assert.AreEqual(QuestState.NotStarted, quests.QuestLog.GetState(quest), "QUEST-DIALOGUE-E2E-001 failed: a new game must start with Mom's quest not accepted.");

            var dialogue = FindInActiveScene<DialoguePanel>();
            Assert.IsTrue(dialogue != null && dialogue.IsOpen, "QUEST-DIALOGUE-E2E-001 failed: Mom's dialogue panel is not on screen.");
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(dialogue.transform, "ChoiceButton_" + acceptIndex).gameObject, "QUEST-DIALOGUE-E2E-001 failed: accept choice");
            Assert.AreEqual(QuestState.Active, quests.QuestLog.GetState(quest), "QUEST-DIALOGUE-E2E-001 failed: clicking the accept choice did not accept the quest.");
            Assert.AreEqual(QuestState.Active, SavedQuestState(quest.Id), "QUEST-DIALOGUE-E2E-001 failed: the accepted quest was not saved right away.");
            if (dialogue.IsOpen) yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(dialogue.transform, "CloseButton").gameObject, "QUEST-DIALOGUE-E2E-001 failed: dialogue close");

            yield return AssertJournalShowsQuest(keyboard, quest, "QUEST-DIALOGUE-E2E-001 failed (House):");
            yield return CaptureScreenshot("output/quest-session-20261107/quest-dialogue-e2e-001-house-journal.png");
            yield return PressTabUntilJournal(keyboard, false, "QUEST-DIALOGUE-E2E-001 failed (House):");

            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "Town", "QUEST-DIALOGUE-E2E-001 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return AssertJournalShowsQuest(keyboard, quest, "QUEST-DIALOGUE-E2E-002 failed (Town after House):");
            yield return CaptureScreenshot("output/quest-session-20261107/quest-dialogue-e2e-002-town-journal.png");
            yield return PressTabUntilJournal(keyboard, false, "QUEST-DIALOGUE-E2E-002 failed (Town):");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "Town", "QUEST-DIALOGUE-E2E-003 failed: Load did not return to Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            Assert.IsTrue(PlayerQuestRuntime.TryGetActive(GameObject.Find("Player"), out var reloaded), "QUEST-DIALOGUE-E2E-003 failed: no quest session after reload.");
            Assert.AreNotSame(quests, reloaded, "QUEST-DIALOGUE-E2E-003 failed: reload must read the save, not keep the old session.");
            Assert.AreEqual(QuestState.Active, reloaded.QuestLog.GetState(quest), "QUEST-DIALOGUE-E2E-003 failed: the accepted quest did not load back.");
            yield return AssertJournalShowsQuest(keyboard, quest, "QUEST-DIALOGUE-E2E-003 failed (Town after reload):");
            yield return PressTabUntilJournal(keyboard, false, "QUEST-DIALOGUE-E2E-003 failed (Town):");

            // Back at home the accept choice is no longer offered: an accepted quest cannot be accepted twice.
            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "House", "QUEST-DIALOGUE-E2E-004 failed:");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return WaitForMomInScene("House", true, "QUEST-DIALOGUE-E2E-004 failed: Mom is not at home.");
            mom = GameObject.Find("TownNpc_npc_first_guide");
            momNpc = mom.GetComponent<NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, "QUEST-DIALOGUE-E2E-004 failed: Mom");
            dialogue = FindInActiveScene<DialoguePanel>();
            Assert.IsTrue(dialogue != null && dialogue.IsOpen, "QUEST-DIALOGUE-E2E-004 failed: Mom's dialogue did not open after reload.");
            int againIndex = FindQuestChoice(momNpc.Session.Current, DialogueQuestAction.AcceptQuest, quest);
            Assert.IsTrue(againIndex < 0 || PlayerFlowE2EDriver.FindChild(dialogue.transform, "ChoiceButton_" + againIndex) == null,
                "QUEST-DIALOGUE-E2E-004 failed: an accepted quest is still offered for accepting.");
            Assert.AreEqual(QuestState.Active, reloaded.QuestLog.GetState(quest));
        }

        private static IEnumerator EndDayAtHomeBoard(Keyboard keyboard, string failure)
        {
            var board = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(board, failure + " House has no StudentDayEndBoard.");
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, board.transform.position, 0.9f, 900, failure + " could not walk to the day-end board.");
            var interactor = board.GetComponent<StudentDayEndInteractor>();
            for (int i = 0; i < 300 && !interactor.CanInteract(player); i++) yield return null;
            player.GetComponent<PlayerInteractionRouter>().RefreshPromptNow();
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            StudentDayResultPanel panel = null;
            for (float t = 0f; t < 3f && (panel == null || !panel.IsOpen); t += Time.unscaledDeltaTime)
            {
                panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                yield return null;
            }

            Assert.IsTrue(panel != null && panel.IsOpen, failure + " the day result panel did not open.");
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(panel.transform, "NextDayButton").gameObject, failure + " Next Day");
            yield return null;
        }

        private static int FindQuestChoice(DialogueDefinition dialogue, DialogueQuestAction action, QuestDefinition quest = null)
        {
            var choices = dialogue != null ? dialogue.Choices : null;
            if (choices == null) return -1;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] != null && choices[i].QuestAction == action && choices[i].Quest != null && (quest == null || choices[i].Quest.Id == quest.Id)) return i;
            return -1;
        }

        private QuestState SavedQuestState(string questId)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, PlayerQuestRuntime.LogFileName(PlayerIdentity.DefaultPlayerId));
            Assert.IsTrue(File.Exists(path), "Quest log file missing: " + path);
            var data = JsonUtility.FromJson<QuestLogSaveData>(File.ReadAllText(path));
            foreach (var entry in data.Quests) if (entry.QuestId == questId) return entry.State;
            Assert.Fail("Quest " + questId + " missing from " + path);
            return QuestState.NotStarted;
        }

        // Tab opens the journal; the Quests category and the quest's row are clicked with the mouse.
        private static IEnumerator AssertJournalShowsQuest(Keyboard keyboard, QuestDefinition quest, string failure)
        {
            yield return PressTabUntilJournal(keyboard, true, failure);
            var journal = FindInActiveScene<ObjectiveJournalPanel>();
            yield return ClickWithMouse(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_Quests").gameObject, failure + " Quests category");
            string title = GameText.Get(quest.DisplayNameKey);
            var row = ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, title, failure);
            yield return ClickWithMouse(row.gameObject, failure + " quest row");
            StringAssert.Contains(title, journal.VisibleText, failure + " the journal detail does not show the quest.");
            StringAssert.Contains(GameText.Localized("ui.journal.state.Active", "Active"), journal.VisibleText, failure + " the journal does not show the quest as in progress.");
        }

        private static IEnumerator PressTabUntilJournal(Keyboard keyboard, bool visible, string failure)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var journal = FindInActiveScene<ObjectiveJournalPanel>();
                if (journal != null && journal.IsVisible == visible) yield break;
                yield return PlayerFlowE2EDriver.PressTab(keyboard);
                yield return null;
            }

            var last = FindInActiveScene<ObjectiveJournalPanel>();
            Assert.IsTrue(last != null && last.IsVisible == visible, failure + " Tab did not " + (visible ? "open" : "close") + " the journal. Locks: " + PlayerFlowE2EDriver.InputLockOwners());
        }

        private static T FindInActiveScene<T>() where T : Component
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        // A real mouse device moves to a point the EventSystem raycast hits on the target and presses the left button.
        private static IEnumerator ClickWithMouse(GameObject target, string failure)
        {
            Assert.IsNotNull(target, failure + " target missing.");
            Assert.IsTrue(target.activeInHierarchy, failure + " target is not visible.");
            var eventSystem = EventSystem.current;
            Assert.IsNotNull(eventSystem, failure + " no EventSystem.");
            // This fixture's input guard swaps in a module that ignores devices; a real click needs the module the game uses.
            if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                var passive = eventSystem.GetComponent<Rootborn.Game.Common.PassiveInputModule>();
                if (passive != null) Object.DestroyImmediate(passive);
                eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                yield return null;
                yield return null;
            }
            Assert.IsNotNull(eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(), failure + " pointer clicks need InputSystemUIInputModule.");
            Assert.IsTrue(TryFindRaycastPoint(target, eventSystem, out var point), failure + " nothing on screen lets a click reach " + target.name + ".");
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
        }

        private static bool TryFindRaycastPoint(GameObject target, EventSystem eventSystem, out Vector2 point)
        {
            var results = new System.Collections.Generic.List<RaycastResult>();
            var rect = target.GetComponent<RectTransform>();
            if (rect != null)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                for (int y = 1; y <= 3; y++)
                    for (int x = 1; x <= 3; x++)
                    {
                        var world = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], x / 4f), Vector3.Lerp(corners[1], corners[2], x / 4f), y / 4f);
                        var candidate = RectTransformUtility.WorldToScreenPoint(null, world);
                        results.Clear();
                        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = candidate }, results);
                        if (results.Count > 0 && (results[0].gameObject == target || results[0].gameObject.transform.IsChildOf(target.transform)))
                        {
                            point = candidate;
                            return true;
                        }
                    }
            }

            point = Vector2.zero;
            return false;
        }

        // The schedule places Mom after the scene's data finishes loading, so wait for her to show up (or disappear).
        private static IEnumerator WaitForMomInScene(string sceneName, bool present, string failure)
        {
            float elapsed = 0f;
            while (elapsed < 10f)
            {
                var mom = GameObject.Find("TownNpc_npc_first_guide");
                bool here = mom != null && mom.scene.name == sceneName;
                if (here == present) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static IEnumerator CaptureScreenshot(string relativePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(relativePath));
            if (File.Exists(relativePath)) File.Delete(relativePath);
            ScreenCapture.CaptureScreenshot(relativePath);
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                if (File.Exists(relativePath) && new FileInfo(relativePath).Length > 0L) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("NPC-REL-E2E-001 failed: Game View screenshot evidence was not written: " + relativePath);
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
            PlayerFlowE2EDriver.DestroyAllNamed("LocationNpcCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("TownHelpActionPoint");
        }

        private readonly struct FirstGuideRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly NpcInteractor Npc;
            public readonly TownHelpActionInteractor Help;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly ObjectiveJournalPanel Journal;
            public readonly TrackedObjectiveHud TrackedHud;

            public FirstGuideRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, NpcInteractor npc, TownHelpActionInteractor help, StudentDayEndInteractor dayEnd, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Npc = npc;
                Help = help;
                DayEnd = dayEnd;
                Journal = journal;
                TrackedHud = trackedHud;
            }

            public bool IsValid => Player != null && Router != null && Student != null && Npc != null && Help != null && DayEnd != null && Journal != null && TrackedHud != null;
        }
    }
}
