using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

        // REL-001 in the authored Town: talk to Mom, help out at the town square, and the trust value rises by the data delta
        // once per action, survives a reload, and a second visit adds exactly one more delta.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator REL_001_AuthoredTown_TalkToMomThenSquareHelpRaisesTrustOncePerAction()
        {
            const string TrustId = "relationship.first-guide.trust";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "REL-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var player = GameObject.Find("Player");
            var student = player.GetComponent<StudentLifeProgressComponent>();
            int trustStart = student.Progress.GetRelationshipValueById(TrustId);

            var mom = GameObject.Find("TownNpc_npc_first_guide");
            Assert.IsNotNull(mom, "REL-001 failed: authored Town has no First Guide NPC.");
            var momNpc = mom.GetComponent<Rootborn.Game.Dialogue.NpcInteractor>();
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, mom.transform.position, "REL-001 failed: Mom");
            Assert.IsTrue(momNpc.Session.IsOpen, "REL-001 failed: E next to Mom did not open her dialogue.");
            var dialogue = Object.FindFirstObjectByType<Rootborn.UI.Quests.DialoguePanel>(FindObjectsInactive.Include);
            if (dialogue != null && dialogue.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(dialogue.transform, "CloseButton"));

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

        [Ignore(LegacyPremise.AuthoredTown + "Last state help=False dayEnd=False")]
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator NPC_RELATIONSHIP_E2E_001_ScheduleInteractionMilestoneJournalReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickSpumConfirmIfPresent();
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "NPC-REL-E2E-001 failed: SaveSlot New Game did not enter Town.");
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            TownHelpActionRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var runtime = FindRuntime();
            var relationshipOutcome = runtime.Help.Action.Outcomes.OfType<HelpActionRelationshipDeltaOutcome>().FirstOrDefault(outcome => outcome.Relationship != null && outcome.Relationship.Id == RelationshipId);
            Assert.IsNotNull(relationshipOutcome, "NPC-REL-E2E-001 failed: First Guide help action is missing a data-driven relationship outcome.");
            int initialRelationship = runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship);

            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "NPC-REL-E2E-001 failed: actual Tab input did not open Objective Journal.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, MilestoneId, "NPC-REL-E2E-001"));
            StringAssert.Contains(HelpActionId, runtime.Journal.VisibleText, "NPC-REL-E2E-001 failed: Hints did not expose the First Guide help route before play.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Npc.transform.position, 0.25f, 640, "NPC-REL-E2E-001 failed: actual keyboard movement could not reach scheduled First Guide NPC.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPC-REL-E2E-001 failed: NPC prompt was not visible after player movement.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.IsTrue(runtime.Npc.Session.IsOpen, "NPC-REL-E2E-001 failed: First Guide dialogue did not open through player interaction.");
            Assert.AreEqual(FirstGuideNpcId, runtime.Npc.Npc.Id, "NPC-REL-E2E-001 failed: scheduled NPC object was not the First Guide NPC.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Help.transform.position, 0.25f, 640, "NPC-REL-E2E-001 failed: actual keyboard movement could not reach First Guide help action.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPC-REL-E2E-001 failed: help prompt was not visible after player movement.");
            StringAssert.Contains("First Guide", runtime.Router.PromptText, "NPC-REL-E2E-001 failed: help prompt did not target the First Guide action.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;

            int changedRelationship = runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship);
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Help.LastResult.Kind, "NPC-REL-E2E-001 failed: First Guide help action did not apply through player input.");
            Assert.GreaterOrEqual(changedRelationship, initialRelationship + relationshipOutcome.Delta, "NPC-REL-E2E-001 failed: First Guide relationship did not increase by the outcome delta.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, 640, "NPC-REL-E2E-001 failed: actual keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPC-REL-E2E-001 failed: day-end prompt was not visible after movement.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "NPC-REL-E2E-001 failed: day result panel did not open from actual day-end input.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, MilestoneId, "NPC-REL-E2E-001 failed: result UI did not show completed First Guide milestone.");
            AssertPanelContains(panel.transform, ObjectiveId, "NPC-REL-E2E-001 failed: result UI did not show First Guide relationship objective progress.");
            yield return OpenJournalRowContaining(keyboard, MilestoneId);
            StringAssert.Contains("Completed", FindRuntime().Journal.VisibleText, "NPC-REL-E2E-001 failed: Objective Journal did not show First Guide milestone completion.");
            yield return CaptureScreenshot(ScreenshotPath);

            var milestoneFile = ResolveMilestoneFile(runtime.Student.Progress.PlayerId);
            Assert.IsTrue(File.Exists(milestoneFile), "NPC-REL-E2E-001 failed: milestone save file missing after day end.");
            string savedJson = File.ReadAllText(milestoneFile);
            StringAssert.Contains(MilestoneId, savedJson, "NPC-REL-E2E-001 failed: saved milestone state missing First Guide milestone id.");
            StringAssert.Contains(ObjectiveId, savedJson, "NPC-REL-E2E-001 failed: saved milestone state missing First Guide objective id.");
            StringAssert.Contains("\"Completed\": true", savedJson, "NPC-REL-E2E-001 failed: saved milestone state did not mark completion.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "NPC-REL-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            TownHelpActionRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForRuntime(10f);

            var reloaded = FindRuntime();
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship), "NPC-REL-E2E-001 failed: First Guide relationship value did not survive reload.");
            yield return OpenJournalRowContaining(keyboard, MilestoneId);
            StringAssert.Contains("Completed", FindRuntime().Journal.VisibleText, "NPC-REL-E2E-001 failed: reloaded Journal did not preserve First Guide milestone completion.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, 640, "NPC-REL-E2E-001 failed: actual movement could not return to day-end board after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "NPC-REL-E2E-001 failed: result UI did not reopen after reload.");
            Assert.AreEqual(savedJson, File.ReadAllText(milestoneFile), "NPC-REL-E2E-001 failed: repeated result interaction changed milestone save data.");
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship), "NPC-REL-E2E-001 failed: repeated result interaction reapplied First Guide relationship growth.");
        }

        private IEnumerator OpenJournalRowContaining(Keyboard keyboard, string text)
        {
            var runtime = FindRuntime();
            if (!runtime.Journal.IsVisible)
            {
                yield return PlayerFlowE2EDriver.PressTab(keyboard);
            }

            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, text, "NPC-REL-E2E-001"));
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var runtime = TryFindRuntime(out lastState);
                if (runtime.IsValid) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("NPC-REL-E2E-001 setup failed: runtime objects were not ready. Last state: " + lastState);
        }

        private static FirstGuideRuntime FindRuntime()
        {
            var runtime = TryFindRuntime(out var lastState);
            Assert.IsTrue(runtime.IsValid, "NPC-REL-E2E-001 failed: runtime missing. Last state: " + lastState);
            return runtime;
        }

        private static FirstGuideRuntime TryFindRuntime(out string lastState)
        {
            var player = GameObject.Find("Player");
            var npc = GameObject.Find(LocationNpcRuntimeInstaller.NpcObjectName(FirstGuideNpcId));
            var help = GameObject.Find("TownHelpActionPoint");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            var trackedHud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
            var result = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            lastState = "scene=" + SceneManager.GetActiveScene().name
                + " player=" + (player != null)
                + " npc=" + (npc != null)
                + " help=" + (help != null)
                + " dayEnd=" + (dayEnd != null)
                + " journal=" + (journal != null)
                + " trackedHud=" + (trackedHud != null)
                + " result=" + (result != null)
                + " eventSystem=" + (EventSystem.current != null);
            if (player == null || npc == null || help == null || dayEnd == null || journal == null || trackedHud == null || result == null || EventSystem.current == null)
            {
                return default;
            }

            var runtime = new FirstGuideRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                npc.GetComponent<NpcInteractor>(),
                help.GetComponent<TownHelpActionInteractor>(),
                dayEnd.GetComponent<StudentDayEndInteractor>(),
                journal,
                trackedHud);
            return runtime.IsValid ? runtime : default;
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

        private static IEnumerator ClickSpumConfirmIfPresent()
        {
            yield return null;
            var root = GameObject.Find("SpumCharacterCreatorRoot") ?? GameObject.Find("SpumCharacterCreatorPanel");
            if (root == null) yield break;
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(root.transform, "ConfirmButton"));
        }

        private string ResolveMilestoneFile(string playerId)
        {
            var specific = Path.Combine(_saveRoot, TestSlotId, MilestoneProgressPersistence.FileNameFor(playerId));
            if (File.Exists(specific)) return specific;
            return Path.Combine(_saveRoot, TestSlotId, "milestone-progress.json");
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

        private static void AssertPanelContains(Transform root, string expected, string failure)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }

            Assert.Fail(failure + " Expected text: " + expected);
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
