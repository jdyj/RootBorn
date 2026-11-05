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
    public sealed class SecondNpcRelationshipMilestoneE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string TeacherNpcId = "npc.teacher";
        private const string RelationshipId = "relationship.teacher.support";
        private const string HelpActionId = "town.help.teacher-study-prep";
        private const string MilestoneId = "milestone.teacher-support";
        private const string ObjectiveId = "milestone.objective.teacher-support";
        private const string HelpPointName = "TownHelpActionPoint_town_help_teacher_study_prep";
        private const string ScreenshotPath = "Builds/Logs/npc-relationship-second-slice-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 8f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-second-npc-milestone-e2e", System.Guid.NewGuid().ToString("N"));
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

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickSpumConfirmIfPresent();
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "NPC-REL-E2E-002 failed: SaveSlot New Game did not enter Town.");
            EnsureTownRelationshipInstallers();
            yield return WaitForRuntime(10f);

            var runtime = FindRuntime();
            Assert.AreEqual(TeacherNpcId, runtime.Npc.Npc.Id, "NPC-REL-E2E-002 failed: scheduled NPC object was not the Teacher NPC.");
            var relationshipOutcome = FindRelationshipOutcome(runtime);
            int initialRelationship = runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship);

            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "NPC-REL-E2E-002 failed: actual Tab input did not open Objective Journal.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            AssertObjectiveRowsContainExactlyOnce(runtime.Journal, MilestoneId, "NPC-REL-E2E-002 failed: Journal Hints duplicated or omitted the Teacher milestone before play.");
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, MilestoneId, "NPC-REL-E2E-002 failed: Journal Hints did not include Teacher milestone."));
            StringAssert.Contains(HelpActionId, runtime.Journal.VisibleText, "NPC-REL-E2E-002 failed: Hints did not expose the Teacher help route before play.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            AssertOneTrackedHud("NPC-REL-E2E-002 failed: tracking created more than one TrackedObjectiveHud.");
            StringAssert.Contains(MilestoneId, runtime.TrackedHud.VisibleText, "NPC-REL-E2E-002 failed: tracked HUD did not show the Teacher milestone route.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Npc.transform.position, 0.25f, 720, "NPC-REL-E2E-002 failed: actual keyboard movement could not reach scheduled Teacher NPC.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPC-REL-E2E-002 failed: Teacher NPC prompt was not visible after player movement.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.IsTrue(runtime.Npc.Session.IsOpen, "NPC-REL-E2E-002 failed: Teacher dialogue did not open through player interaction.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Help.transform.position, 0.25f, 720, "NPC-REL-E2E-002 failed: actual keyboard movement could not reach Teacher help action.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPC-REL-E2E-002 failed: Teacher help prompt was not visible after player movement.");
            Assert.AreEqual(HelpActionId, runtime.Help.Action.Id, "NPC-REL-E2E-002 failed: reached help point was not bound to the Teacher action.");
            StringAssert.Contains("Teacher", runtime.Router.PromptText, "NPC-REL-E2E-002 failed: visible help prompt did not identify the Teacher action.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;

            int changedRelationship = runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship);
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Help.LastResult.Kind, "NPC-REL-E2E-002 failed: Teacher help action did not apply through player input.");
            Assert.GreaterOrEqual(changedRelationship, initialRelationship + relationshipOutcome.Delta, "NPC-REL-E2E-002 failed: Teacher relationship did not increase by the outcome delta.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, 720, "NPC-REL-E2E-002 failed: actual keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NPC-REL-E2E-002 failed: day-end prompt was not visible after movement.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "NPC-REL-E2E-002 failed: day result panel did not open from actual day-end input.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, MilestoneId, "NPC-REL-E2E-002 failed: result UI did not show completed Teacher milestone.");
            AssertPanelContains(panel.transform, ObjectiveId, "NPC-REL-E2E-002 failed: result UI did not show Teacher relationship objective progress.");
            yield return OpenJournalRowContaining(keyboard, MilestoneId);
            AssertObjectiveRowsContainExactlyOnce(FindRuntime().Journal, MilestoneId, "NPC-REL-E2E-002 failed: Objective Journal duplicated or omitted the Teacher milestone after completion.");
            StringAssert.Contains("Completed", FindRuntime().Journal.VisibleText, "NPC-REL-E2E-002 failed: Objective Journal did not show Teacher milestone completion.");
            AssertOneTrackedHud("NPC-REL-E2E-002 failed: completion created duplicate tracked HUDs.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "NPC-REL-E2E-002 failed: Game View screenshot evidence was not written.");

            var milestoneFile = ResolveMilestoneFile(runtime.Student.Progress.PlayerId);
            Assert.IsTrue(File.Exists(milestoneFile), "NPC-REL-E2E-002 failed: milestone save file missing after day end.");
            string savedJson = File.ReadAllText(milestoneFile);
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, MilestoneId, "NPC-REL-E2E-002 failed: saved milestone state must contain Teacher milestone exactly once.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, ObjectiveId, "NPC-REL-E2E-002 failed: saved milestone state must contain Teacher objective exactly once.");
            StringAssert.Contains("\"Completed\": true", savedJson, "NPC-REL-E2E-002 failed: saved milestone state did not mark completion.");
            AssertStudentSaveContainsExactlyOnce(runtime.Student.Progress.PlayerId, RelationshipId, "NPC-REL-E2E-002 failed: student save must contain Teacher relationship exactly once.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "NPC-REL-E2E-002 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownRelationshipInstallers();
            yield return WaitForRuntime(10f);

            var reloaded = FindRuntime();
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship), "NPC-REL-E2E-002 failed: Teacher relationship value did not survive reload.");
            AssertStudentSaveContainsExactlyOnce(reloaded.Student.Progress.PlayerId, RelationshipId, "NPC-REL-E2E-002 failed: reload duplicated or removed Teacher relationship save data.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, MilestoneId, "NPC-REL-E2E-002 failed: reload duplicated or removed Teacher milestone save data.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, ObjectiveId, "NPC-REL-E2E-002 failed: reload duplicated or removed Teacher objective save data.");
            yield return OpenJournalRowContaining(keyboard, MilestoneId);
            AssertObjectiveRowsContainExactlyOnce(FindRuntime().Journal, MilestoneId, "NPC-REL-E2E-002 failed: reload duplicated or omitted the Teacher milestone row.");
            StringAssert.Contains("Completed", FindRuntime().Journal.VisibleText, "NPC-REL-E2E-002 failed: reloaded Journal did not preserve Teacher milestone completion.");
            AssertOneTrackedHud("NPC-REL-E2E-002 failed: reload created duplicate tracked HUDs.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, 720, "NPC-REL-E2E-002 failed: actual movement could not return to day-end board after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "NPC-REL-E2E-002 failed: result UI did not reopen after reload.");
            Assert.AreEqual(savedJson, File.ReadAllText(milestoneFile), "NPC-REL-E2E-002 failed: repeated result interaction changed milestone save data.");
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship), "NPC-REL-E2E-002 failed: repeated result interaction reapplied Teacher relationship growth.");
            AssertStudentSaveContainsExactlyOnce(reloaded.Student.Progress.PlayerId, RelationshipId, "NPC-REL-E2E-002 failed: repeated result interaction duplicated Teacher relationship save data.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, MilestoneId, "NPC-REL-E2E-002 failed: repeated result interaction duplicated Teacher milestone save data.");
            AssertMilestoneSaveContainsExactlyOnce(milestoneFile, ObjectiveId, "NPC-REL-E2E-002 failed: repeated result interaction duplicated Teacher objective save data.");
        }

        private static void EnsureTownRelationshipInstallers()
        {
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            TownHelpActionRuntimeInstaller.EnsureForActiveScene();
            StudentDayRuntimeInstaller.EnsureForActiveScene();
        }

        private IEnumerator OpenJournalRowContaining(Keyboard keyboard, string text)
        {
            var runtime = FindRuntime();
            if (!runtime.Journal.IsVisible)
            {
                yield return PlayerFlowE2EDriver.PressTab(keyboard);
            }

            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            AssertObjectiveRowsContainExactlyOnce(runtime.Journal, text, "NPC-REL-E2E-002 failed: Journal Hints duplicated or omitted expected row before selection.");
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, text, "NPC-REL-E2E-002"));
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

            Assert.Fail("NPC-REL-E2E-002 setup failed: runtime objects were not ready. Last state: " + lastState);
        }

        private static SecondNpcRuntime FindRuntime()
        {
            var runtime = TryFindRuntime(out var lastState);
            Assert.IsTrue(runtime.IsValid, "NPC-REL-E2E-002 failed: runtime missing. Last state: " + lastState);
            return runtime;
        }

        private static SecondNpcRuntime TryFindRuntime(out string lastState)
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

            var runtime = new SecondNpcRuntime(
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

        private static HelpActionRelationshipDeltaOutcome FindRelationshipOutcome(SecondNpcRuntime runtime)
        {
            var relationshipOutcome = runtime.Help.Action.Outcomes.OfType<HelpActionRelationshipDeltaOutcome>().FirstOrDefault(outcome => outcome.Relationship != null && outcome.Relationship.Id == RelationshipId);
            Assert.IsNotNull(relationshipOutcome, "NPC-REL-E2E-002 failed: Teacher help action is missing a data-driven relationship outcome.");
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
            var buttons = panel.GetComponentsInChildren<Button>(true);
            int count = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || !buttons[i].name.StartsWith("ObjectiveRow_", StringComparison.Ordinal)) continue;
                var text = buttons[i].GetComponentInChildren<Text>(true);
                if (text != null && text.text.Contains(expected)) count++;
            }

            Assert.AreEqual(1, count, failure + "\nVisible Journal Text:\n" + panel.VisibleText + "\n" + PlayerFlowE2EDriver.DescribeButtons(panel.transform));
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

        private readonly struct SecondNpcRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly NpcInteractor Npc;
            public readonly TownHelpActionInteractor Help;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly ObjectiveJournalPanel Journal;
            public readonly TrackedObjectiveHud TrackedHud;

            public SecondNpcRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, NpcInteractor npc, TownHelpActionInteractor help, StudentDayEndInteractor dayEnd, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
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