using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
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
    public sealed class TownHelpActionGrowthE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ScreenshotPath = "Builds/Logs/town-help-relationship-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-help-e2e", System.Guid.NewGuid().ToString("N"));
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

        // HELP-001 in the authored Town: walk to Mom's errand point and press E. Responsibility and Mom's trust rise by the
        // data deltas, energy pays the cost, the result survives a reload once, and helping again adds exactly one more delta.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator HELP_001_AuthoredTown_ErrandForMomGrowsResponsibilityAndTrustOncePerHelp()
        {
            const string ActionId = "town.help.first-guide-errand";
            const string TraitId = "trait.responsibility";
            const string TrustId = "relationship.first-guide.trust";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "HELP-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int traitStart = student.Progress.GetTraitValueById(TraitId);
            int trustStart = student.Progress.GetRelationshipValueById(TrustId);
            int energyStart = student.Progress.Energy;

            var errand = FindHelpPoint(ActionId);
            Assert.IsNotNull(errand, "HELP-001 failed: authored Town has no errand point for " + ActionId + ".");
            string shownName = Rootborn.Game.Localization.GameText.Get(errand.Action.DisplayNameKey);
            Assert.AreNotEqual(errand.Action.DisplayNameKey, shownName, "HELP-001 failed: the errand has no catalog name.");
            StringAssert.Contains(shownName, errand.InteractionPrompt, "HELP-001 failed: the errand prompt does not show its translated name.");
            var walker = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(walker, keyboard, errand.transform.position, 0.9f, 900, "HELP-001 failed: could not walk to the errand.");
            var errandRouter = walker.GetComponent<PlayerInteractionRouter>();
            errandRouter.RefreshPromptNow();
            Assert.IsTrue(errandRouter.PromptVisible, "HELP-001 failed: no prompt at the errand point.");
            StringAssert.Contains(shownName, errandRouter.PromptText, "HELP-001 failed: the on-screen prompt does not show the translated errand name.");
            yield return null;
            yield return CaptureScreenshot("Builds/Logs/help-001-errand-prompt.png");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual(Rootborn.Game.StudentLife.LifeActivityResultKind.Applied, errand.LastResult.Kind, "HELP-001 failed: E at the errand point did not apply.");
            int traitAfter = student.Progress.GetTraitValueById(TraitId);
            int trustAfter = student.Progress.GetRelationshipValueById(TrustId);
            Assert.Greater(traitAfter, traitStart, "HELP-001 failed: helping did not grow responsibility.");
            Assert.Greater(trustAfter, trustStart, "HELP-001 failed: helping did not raise Mom's trust.");
            Assert.AreEqual(energyStart - errand.Action.EnergyCost, student.Progress.Energy, "HELP-001 failed: the errand did not cost its energy.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "Town", "HELP-002 failed: Load did not return to Town.");
            student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(traitAfter, student.Progress.GetTraitValueById(TraitId), "HELP-002 failed: responsibility did not survive reload exactly once.");
            Assert.AreEqual(trustAfter, student.Progress.GetRelationshipValueById(TrustId), "HELP-002 failed: trust did not survive reload exactly once.");

            errand = FindHelpPoint(ActionId);
            yield return PlayerFlowE2EDriver.WalkAndInteract(keyboard, errand.transform.position, "HELP-003 failed: second errand");
            Assert.AreEqual(Rootborn.Game.StudentLife.LifeActivityResultKind.Applied, errand.LastResult.Kind, "HELP-003 failed: the second errand did not apply (" + errand.LastResult.Kind + ").");
            Assert.AreEqual(traitAfter + (traitAfter - traitStart), student.Progress.GetTraitValueById(TraitId), "HELP-003 failed: a second help must add exactly one more responsibility delta.");
            Assert.AreEqual(trustAfter + (trustAfter - trustStart), student.Progress.GetRelationshipValueById(TrustId), "HELP-003 failed: a second help must add exactly one more trust delta.");
        }

        private static TownHelpActionInteractor FindHelpPoint(string actionId)
        {
            foreach (var candidate in Object.FindObjectsByType<TownHelpActionInteractor>(FindObjectsSortMode.None))
                if (candidate.Action != null && candidate.Action.Id == actionId) return candidate;
            return null;
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

        private static IEnumerator CaptureScreenshot(string relativePath)
        {
            if (File.Exists(relativePath)) File.Delete(relativePath);
            ScreenCapture.CaptureScreenshot(relativePath);
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                if (File.Exists(relativePath) && new FileInfo(relativePath).Length > 0L) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Town help relationship screenshot was not written: " + relativePath);
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

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            if (key == Key.E) return keyboard.eKey;
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

        private readonly struct HelpRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly TownHelpActionInteractor Help;
            public readonly StudentDayEndInteractor DayEnd;

            public HelpRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, TownHelpActionInteractor help, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                Help = help;
                DayEnd = dayEnd;
            }
        }
    }
}