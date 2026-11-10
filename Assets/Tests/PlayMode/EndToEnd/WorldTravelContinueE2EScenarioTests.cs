using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    /// <summary>
    /// TRAVEL-E2E-001 (audit 5.1): one player walks House → Town → School → Town → a career facility through the doors with
    /// the keyboard and E, the camera keeps the player on screen in every place, and Continue on the save card returns to
    /// the facility. The new game and opening MainMenu again are preparation; every move between places is player input.
    /// </summary>
    public sealed class WorldTravelContinueE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string Evidence = "output/travel-e2e/";
        private const string F = "TRAVEL-E2E failed:";
        private const string Facility = "KitchenInterior";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-travel-e2e", System.Guid.NewGuid().ToString("N"));
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
        [Timeout(900000)]
        public IEnumerator TRAVEL_E2E_001_HouseTownSchoolFacilityThenContinueReturnsToFacility()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", F + " New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();

            string[] route = { "House", "Town", "School", "Town", Facility };
            for (int i = 0; i < route.Length; i++)
            {
                yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, route[i], F + " step " + i + " (" + route[i] + ").");
                yield return SettleCamera();
                AssertPlayerOnScreen(route[i]);
                yield return Capture((i + 1).ToString("00") + "-" + route[i] + ".png");
            }
            var facilityPlayer = GameObject.Find("Player");
            Vector3 arrivedAt = facilityPlayer.transform.position;

            // Preparation: back to the title screen (the game has no in-game title button); Continue itself is a mouse click.
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", Facility, F + " Continue did not return to " + Facility + ".");
            yield return SettleCamera();
            AssertPlayerOnScreen(Facility + " after Continue");
            var continued = GameObject.Find("Player");
            Assert.LessOrEqual(Vector3.Distance(arrivedAt, continued.transform.position), 3f,
                F + " Continue put the player far from the facility entrance (" + arrivedAt + " vs " + continued.transform.position + ").");
            yield return Capture("06-continue-" + Facility + ".png");
        }

        private static IEnumerator SettleCamera()
        {
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static void AssertPlayerOnScreen(string place)
        {
            var player = GameObject.Find("Player");
            var camera = Camera.main;
            Assert.IsNotNull(player, F + " no player in " + place + ".");
            Assert.IsNotNull(camera, F + " no main camera in " + place + ".");
            Vector3 view = camera.WorldToViewportPoint(player.transform.position);
            Assert.IsTrue(view.z > 0f && view.x > 0.05f && view.x < 0.95f && view.y > 0.05f && view.y < 0.95f,
                F + " the camera does not show the player in " + place + " (viewport " + view + ").");
            bool drawn = false;
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.enabled && renderer.sprite != null) { drawn = true; break; }
            Assert.IsTrue(drawn, F + " the player has no drawn sprite in " + place + ".");
        }

        private static IEnumerator Capture(string fileName)
        {
            string path = Evidence + fileName;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            for (float t = 0f; t < 3f && !(File.Exists(path) && new FileInfo(path).Length > 0L); t += Time.unscaledDeltaTime) yield return null;
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
    }
}
