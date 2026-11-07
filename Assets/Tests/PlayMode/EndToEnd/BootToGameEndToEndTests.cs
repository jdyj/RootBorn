using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class BootToGameEndToEndTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            SetTestSaveRoot(NewSaveRoot("rootborn-e2e-default"));
            yield return LoadScene("Boot");
            CleanupDontDestroyResidue();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SetTestSaveRoot(null);
            CleanupDontDestroyResidue();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BOOT_E2E_002_ReenteringBootKeepsSingleManagerAndMainMenuPanels()
        {
            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);
            Assert.IsNotNull(Managers.Instance);
            Assert.AreEqual(1, CountObjectsNamed("@Managers"));
            Assert.AreEqual(1, Object.FindObjectsByType<ModeSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);

            Assert.IsNotNull(Managers.Instance);
            Assert.IsNotNull(Managers.Data);
            Assert.IsNotNull(Managers.Data.Registry);
            Assert.AreEqual(1, CountObjectsNamed("@Managers"), "Boot reentry should not duplicate the manager singleton.");
            Assert.AreEqual(1, Object.FindObjectsByType<ModeSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

            ClickButtonNamed("SinglePlayButton");
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<SaveSlotSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator SAVE_E2E_001_SaveSlotUiUsesInjectedTestRootAndPreservesMetadataOnLoad()
        {
            string root = NewSaveRoot("rootborn-save-e2e");
            SetTestSaveRoot(root);

            yield return LoadScene("MainMenu");
            // A player waits at the menu while the game data loads; entering before it is ready stays on the menu.
            yield return Rootborn.Tests.PlayMode.PlayModeScenarioStart.WaitForGameData();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;

            ClickButtonNamed("NewGameButton");
            yield return null;
            ClickButtonNamed("ConfirmButton");
            // The world entry data decides where a new game starts (home or Town); either means the game was entered.
            yield return WaitForWorld(20f, "new game");
            // Let the arrival finish (player placed and bound) before leaving, as a player would.
            yield return Support.PlayerFlowE2EDriver.WaitForPlayerReady("SAVE-E2E-001 failed: new game arrival did not finish.");

            var service = new SaveService("slot-0", root);
            var metadata = service.LoadMetadata("slot-0");
            Assert.IsNotNull(metadata);
            long createdAt = metadata.CreatedAtUtcTicks;
            int worldSeed = metadata.WorldSeed;
            int tileSeed = metadata.TileSeed;

            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            ClickButtonNamed("LoadButton");
            yield return WaitForWorld(20f, "load");

            var loaded = ActiveSaveContext.Metadata;
            Assert.IsNotNull(loaded);
            Assert.AreEqual("slot-0", loaded.SlotId);
            Assert.AreEqual(createdAt, loaded.CreatedAtUtcTicks);
            Assert.AreEqual(worldSeed, loaded.WorldSeed);
            Assert.AreEqual(tileSeed, loaded.TileSeed);
            Assert.IsTrue(File.Exists(Path.Combine(root, "slot-0", "metadata.json")));
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForWorld(float timeoutSeconds, string step)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != "House" && SceneManager.GetActiveScene().name != "Town" && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            var panel = Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("House").Or.EqualTo("Town"),
                step + ": entry error: '" + (panel != null ? panel.LastEntryError : "no panel") + "' creator open: " + (Object.FindFirstObjectByType<SpumCharacterCreatorPanel>() != null));
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static SaveSlotMetadata Metadata(string slotId, int worldSeed, int tileSeed)
        {
            return new SaveSlotMetadata
            {
                SlotId = slotId,
                DisplayName = slotId,
                CreatedAtUtcTicks = 637000000000000000L,
                UpdatedAtUtcTicks = 637000000000000000L,
                WorldSeed = worldSeed,
                TileSeed = tileSeed,
            };
        }

        private static string NewSaveRoot(string prefix)
        {
            string root = Path.Combine(Application.temporaryCachePath, prefix, System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void SetTestSaveRoot(string root)
        {
            SaveSlotSelectPanel.SetSaveRootForTests(root);
            SaveService.SetRootDirectoryForTests(root);
        }

        private static void ClickButtonNamed(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "Expected button GameObject named " + name + ".");
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " should have a Button component.");
            button.onClick.Invoke();
        }

        private static int CountObjectsNamed(string name)
        {
            int count = 0;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name) count++;
            }
            return count;
        }

        private static void CleanupDontDestroyResidue()
        {
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            DestroyIfFound("@Managers");
            DestroyIfFound("bootstrap");
            DestroyIfFound("[FarmAutoFiller]");
            DestroyIfFound("Player");
        }

        private static void DestroyIfFound(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}