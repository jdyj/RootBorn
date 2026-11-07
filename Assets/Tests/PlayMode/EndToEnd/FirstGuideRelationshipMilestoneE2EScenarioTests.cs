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
