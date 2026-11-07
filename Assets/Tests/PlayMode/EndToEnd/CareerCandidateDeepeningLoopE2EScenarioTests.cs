using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class CareerCandidateDeepeningLoopE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-career-candidate-e2e", System.Guid.NewGuid().ToString("N"));
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

        // CAREER-001 in the authored Town: helping at the town square gives a career hint toward the service candidate,
        // the hint survives a reload, and the same hint does not count twice from one action.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CAREER_001_AuthoredTown_SquareHelpGivesServiceCareerHintThatSurvivesReload()
        {
            const string CandidateId = "career.candidate.service";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "CAREER-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var candidate = System.Array.Find(Rootborn.Game.Managers.Managers.Data.Registry.CareerCandidates, c => c != null && c.Id == CandidateId);
            Assert.IsNotNull(candidate, "CAREER-001 failed: service career candidate is not registered.");
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int hintsBefore = student.Progress.CareerCandidates.GetHintCount(candidate);
            int understandingBefore = student.Progress.CareerCandidates.GetUnderstandingScore(candidate);

            yield return PlayerFlowE2EDriver.UseLocationActivity(keyboard, "LocationVisit_location_town_square", 0, "CAREER-001 failed:");
            int hintsAfter = student.Progress.CareerCandidates.GetHintCount(candidate);
            int understandingAfter = student.Progress.CareerCandidates.GetUnderstandingScore(candidate);
            Assert.AreEqual(hintsBefore + 1, hintsAfter, "CAREER-001 failed: helping at the square did not add one service career hint.");
            Assert.Greater(understandingAfter, understandingBefore, "CAREER-001 failed: service career understanding did not grow.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "Town", "CAREER-002 failed: Load did not return to Town.");
            var reloaded = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(hintsAfter, reloaded.Progress.CareerCandidates.GetHintCount(candidate), "CAREER-002 failed: the career hint did not survive reload exactly once.");
            Assert.AreEqual(understandingAfter, reloaded.Progress.CareerCandidates.GetUnderstandingScore(candidate), "CAREER-002 failed: career understanding changed on reload.");
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
            DestroyAllNamed("CareerCandidateE2ECanvas");
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

        private sealed class CareerCandidateE2EData
        {
            public CareerCandidateDefinition Learning;
            public CareerCandidateDefinition Technical;
            public CareerCandidateDefinition[] Candidates;
            public CareerHintDefinition[] Hints;

            public static CareerCandidateE2EData Create()
            {
                var libraryRoute = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
                libraryRoute.ConfigureForTests("route.library", "route.library", "desc.route.library", null, null, new[] { "action.visit-library" });
                var workRoute = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
                workRoute.ConfigureForTests("route.workshop", "route.workshop", "desc.route.workshop", null, null, new[] { "action.try-workshop" });
                var learning = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
                learning.ConfigureForTests("candidate.learning", "candidate.learning", "desc.candidate.learning", new[] { libraryRoute }, null, new[] { "action.visit-library" });
                var technical = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
                technical.ConfigureForTests("candidate.technical", "candidate.technical", "desc.candidate.technical", new[] { workRoute }, null, new[] { "action.try-workshop" });
                var source = ScriptableObject.CreateInstance<CareerResultLogHintSource>();
                source.ConfigureForTests("location-activity:");
                var hint = ScriptableObject.CreateInstance<CareerHintDefinition>();
                hint.ConfigureForTests("hint.learning.library", "hint.learning.library", learning, libraryRoute, "insight.learning.library", 1, new CareerHintSourceBase[] { source });
                return new CareerCandidateE2EData { Learning = learning, Technical = technical, Candidates = new[] { learning, technical }, Hints = new[] { hint } };
            }
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly StudentDayEndInteractor DayEnd;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                DayEnd = dayEnd;
            }
        }
    }
}
