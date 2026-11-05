using System;
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
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class RelationshipActivityCommitmentE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string TeacherNpcId = "npc.teacher";
        private const string RelationshipId = "relationship.teacher.support";
        private const string HelpActionId = "town.help.teacher-study-prep";
        private const string SupportMilestoneId = "milestone.teacher-support";
        private const string SupportObjectiveId = "milestone.objective.teacher-support";
        private const string CommitmentMilestoneId = "milestone.teacher-commitment";
        private const string CommitmentObjectiveId = "milestone.objective.teacher-commitment";
        private const string HelpPointName = "TownHelpActionPoint_town_help_teacher_study_prep";
        private const string ScreenshotPath = "Builds/Logs/relationship-activity-commitment-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 8f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-relationship-commitment-e2e", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            ScreenshotEvidence.DeleteIfExists(ScreenshotPath);
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

        [Ignore(LegacyPremise.AuthoredTown + "after entering Town it waits for old prototype object names")]
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator REL_COMMITMENT_E2E_001_TeacherHelpCommitmentReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            Assert.AreEqual(TeacherNpcId, runtime.Npc.Npc.Id, "REL-COMMITMENT-E2E-001 failed: scheduled NPC object was not the Teacher NPC.");
            var relationshipOutcome = FindRelationshipOutcome(runtime);
            int initialRelationship = runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship);

            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "REL-COMMITMENT-E2E-001 failed: actual Tab input did not open Objective Journal.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            AssertObjectiveRowsContainExactlyOnce(runtime.Journal, SupportMilestoneId, "REL-COMMITMENT-E2E-001 failed: Journal Hints duplicated or omitted Teacher support before play.");
            AssertObjectiveRowsMissing(runtime.Journal, CommitmentMilestoneId, "REL-COMMITMENT-E2E-001 failed: commitment guidance was visible before Teacher support completion.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Npc.transform.position, 0.25f, 720, "REL-COMMITMENT-E2E-001 failed: actual keyboard movement could not reach scheduled Teacher NPC.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "REL-COMMITMENT-E2E-001 failed: Teacher NPC prompt was not visible after player movement.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.IsTrue(runtime.Npc.Session.IsOpen, "REL-COMMITMENT-E2E-001 failed: Teacher dialogue did not open through player interaction.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Help.transform.position, 0.25f, 720, "REL-COMMITMENT-E2E-001 failed: actual keyboard movement could not reach Teacher help action.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "REL-COMMITMENT-E2E-001 failed: Teacher help prompt was not visible after player movement.");
            StringAssert.Contains("Teacher", runtime.Router.PromptText, "REL-COMMITMENT-E2E-001 failed: visible help prompt did not identify the Teacher action.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;

            int changedRelationship = runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship);
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Help.LastResult.Kind, "REL-COMMITMENT-E2E-001 failed: Teacher help action did not apply through player input.");
            Assert.GreaterOrEqual(changedRelationship, initialRelationship + relationshipOutcome.Delta, "REL-COMMITMENT-E2E-001 failed: Teacher relationship did not increase by the outcome delta.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, 720, "REL-COMMITMENT-E2E-001 failed: actual keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "REL-COMMITMENT-E2E-001 failed: day-end prompt was not visible after movement.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "REL-COMMITMENT-E2E-001 failed: day result panel did not open from actual day-end input.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, SupportMilestoneId, "REL-COMMITMENT-E2E-001 failed: result UI did not show completed Teacher support milestone.");
            AssertPanelContains(panel.transform, SupportObjectiveId, "REL-COMMITMENT-E2E-001 failed: result UI did not show Teacher support relationship objective progress.");

            yield return OpenJournalRowContaining(keyboard, CommitmentMilestoneId);
            var current = FindRuntime();
            AssertObjectiveRowsContainExactlyOnce(current.Journal, CommitmentMilestoneId, "REL-COMMITMENT-E2E-001 failed: Objective Journal duplicated or omitted Teacher commitment guidance after support completion.");
            StringAssert.Contains(HelpActionId, current.Journal.VisibleText, "REL-COMMITMENT-E2E-001 failed: commitment guidance did not point back to the data-driven Teacher help route.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(current.Journal.transform, "ObjectiveTrackButton"));
            AssertOneTrackedHud("REL-COMMITMENT-E2E-001 failed: tracking commitment created duplicate tracked HUDs.");
            StringAssert.Contains(CommitmentMilestoneId, FindRuntime().TrackedHud.VisibleText, "REL-COMMITMENT-E2E-001 failed: tracked HUD did not show Teacher commitment guidance.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "REL-COMMITMENT-E2E-001 failed: Game View screenshot evidence was not written.");

            var milestoneFile = ResolveMilestoneFile(runtime.Student.Progress.PlayerId);
            AssertStudentSaveContainsExactlyOnce(runtime.Student.Progress.PlayerId, RelationshipId, "REL-COMMITMENT-E2E-001 failed: student save must contain Teacher relationship exactly once.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, SupportMilestoneId, "REL-COMMITMENT-E2E-001 failed: saved milestone state must contain Teacher support exactly once.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, SupportObjectiveId, "REL-COMMITMENT-E2E-001 failed: saved milestone state must contain Teacher support objective exactly once.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, CommitmentMilestoneId, "REL-COMMITMENT-E2E-001 failed: saved milestone state must contain Teacher commitment exactly once.");
            string savedJson = File.ReadAllText(milestoneFile);

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "REL-COMMITMENT-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownRelationshipInstallers();
            yield return WaitForRuntime(10f);

            var reloaded = FindRuntime();
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship), "REL-COMMITMENT-E2E-001 failed: Teacher relationship value did not survive reload.");
            AssertStudentSaveContainsExactlyOnce(reloaded.Student.Progress.PlayerId, RelationshipId, "REL-COMMITMENT-E2E-001 failed: reload duplicated or removed Teacher relationship save data.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, SupportMilestoneId, "REL-COMMITMENT-E2E-001 failed: reload duplicated or removed Teacher support milestone.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, CommitmentMilestoneId, "REL-COMMITMENT-E2E-001 failed: reload duplicated or removed Teacher commitment milestone.");
            yield return OpenJournalRowContaining(keyboard, CommitmentMilestoneId);
            AssertObjectiveRowsContainExactlyOnce(FindRuntime().Journal, CommitmentMilestoneId, "REL-COMMITMENT-E2E-001 failed: reload duplicated or omitted Teacher commitment row.");
            AssertOneTrackedHud("REL-COMMITMENT-E2E-001 failed: reload created duplicate tracked HUDs.");
            StringAssert.Contains(CommitmentMilestoneId, FindRuntime().TrackedHud.VisibleText, "REL-COMMITMENT-E2E-001 failed: tracked HUD did not restore Teacher commitment guidance.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, 720, "REL-COMMITMENT-E2E-001 failed: actual movement could not return to day-end board after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "REL-COMMITMENT-E2E-001 failed: result UI did not reopen after reload.");
            Assert.AreEqual(savedJson, File.ReadAllText(milestoneFile), "REL-COMMITMENT-E2E-001 failed: repeated result interaction changed milestone save data.");
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship), "REL-COMMITMENT-E2E-001 failed: repeated result interaction reapplied Teacher relationship growth.");
            AssertStudentSaveContainsExactlyOnce(reloaded.Student.Progress.PlayerId, RelationshipId, "REL-COMMITMENT-E2E-001 failed: repeated result interaction duplicated Teacher relationship save data.");
        }

        private static void EnsureTownRelationshipInstallers()
        {
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            TownHelpActionRuntimeInstaller.EnsureForActiveScene();
            StudentDayRuntimeInstaller.EnsureForActiveScene();
        }

        private IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickSpumConfirmIfPresent();
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "REL-COMMITMENT-E2E-001 failed: SaveSlot New Game did not enter Town.");
            EnsureTownRelationshipInstallers();
            yield return WaitForRuntime(10f);
        }

        private IEnumerator OpenJournalRowContaining(Keyboard keyboard, string text)
        {
            var runtime = FindRuntime();
            if (!runtime.Journal.IsVisible)
            {
                yield return PlayerFlowE2EDriver.PressTab(keyboard);
            }

            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            AssertObjectiveRowsContainExactlyOnce(runtime.Journal, text, "REL-COMMITMENT-E2E-001 failed: Journal Hints duplicated or omitted expected row before selection.");
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, text, "REL-COMMITMENT-E2E-001"));
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

            Assert.Fail("REL-COMMITMENT-E2E-001 setup failed: runtime objects were not ready. Last state: " + lastState);
        }

        private static RelationshipCommitmentRuntime FindRuntime()
        {
            var runtime = TryFindRuntime(out var lastState);
            Assert.IsTrue(runtime.IsValid, "REL-COMMITMENT-E2E-001 failed: runtime missing. Last state: " + lastState);
            return runtime;
        }

        private static RelationshipCommitmentRuntime TryFindRuntime(out string lastState)
        {
            var player = GameObject.Find("Player");
            var npc = GameObject.Find(LocationNpcRuntimeInstaller.NpcObjectName(TeacherNpcId));
            var help = GameObject.Find(HelpPointName);
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

            var runtime = new RelationshipCommitmentRuntime(
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

        private static HelpActionRelationshipDeltaOutcome FindRelationshipOutcome(RelationshipCommitmentRuntime runtime)
        {
            var relationshipOutcome = runtime.Help.Action.Outcomes.OfType<HelpActionRelationshipDeltaOutcome>().FirstOrDefault(outcome => outcome.Relationship != null && outcome.Relationship.Id == RelationshipId);
            Assert.IsNotNull(relationshipOutcome, "REL-COMMITMENT-E2E-001 failed: Teacher help action is missing a data-driven relationship outcome.");
            return relationshipOutcome;
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

        private void AssertStudentSaveContainsExactlyOnce(string playerId, string expected, string failure)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, StudentLifeProgressFileNameFor(playerId));
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            string text = File.ReadAllText(path);
            string expectedJsonValue = "\"" + expected + "\"";
            Assert.AreEqual(1, CountOccurrences(text, expectedJsonValue), failure + " File: " + path + " Expected JSON value: " + expectedJsonValue + " Text: " + text);
        }

        private static void AssertMilestoneSaveContainsExactlyOnce(string path, string expected, string failure)
        {
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            string text = File.ReadAllText(path);
            string expectedJsonValue = "\"" + expected + "\"";
            Assert.AreEqual(1, CountOccurrences(text, expectedJsonValue), failure + " File: " + path + " Expected JSON value: " + expectedJsonValue + " Text: " + text);
        }

        private static void AssertObjectiveRowsContainExactlyOnce(ObjectiveJournalPanel panel, string expected, string failure)
        {
            Assert.IsNotNull(panel, failure + " Objective Journal panel missing.");
            int count = CountObjectiveRows(panel, expected);
            Assert.AreEqual(1, count, failure + "\nVisible Journal Text:\n" + panel.VisibleText + "\n" + PlayerFlowE2EDriver.DescribeButtons(panel.transform));
        }

        private static void AssertObjectiveRowsMissing(ObjectiveJournalPanel panel, string expected, string failure)
        {
            Assert.IsNotNull(panel, failure + " Objective Journal panel missing.");
            Assert.AreEqual(0, CountObjectiveRows(panel, expected), failure + "\nVisible Journal Text:\n" + panel.VisibleText);
        }

        private static int CountObjectiveRows(ObjectiveJournalPanel panel, string expected)
        {
            var buttons = panel.GetComponentsInChildren<Button>(true);
            int count = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || !buttons[i].name.StartsWith("ObjectiveRow_", StringComparison.Ordinal)) continue;
                var text = buttons[i].GetComponentInChildren<Text>(true);
                if (text != null && text.text.Contains(expected)) count++;
            }

            return count;
        }

        private static int CountOccurrences(string text, string expected)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(expected)) return 0;
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(expected, index, StringComparison.Ordinal);
                if (found < 0) break;
                count++;
                index = found + expected.Length;
            }

            return count;
        }

        private static void AssertOneTrackedHud(string failure)
        {
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, failure);
        }

        private static string StudentLifeProgressFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId)
            {
                return "student-life-progress.json";
            }

            return "student-life-progress-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
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
            PlayerFlowE2EDriver.DestroyAllNamed(HelpPointName);
        }

        private readonly struct RelationshipCommitmentRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly NpcInteractor Npc;
            public readonly TownHelpActionInteractor Help;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly ObjectiveJournalPanel Journal;
            public readonly TrackedObjectiveHud TrackedHud;

            public RelationshipCommitmentRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, NpcInteractor npc, TownHelpActionInteractor help, StudentDayEndInteractor dayEnd, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
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
