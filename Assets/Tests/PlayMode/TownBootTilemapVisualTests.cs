using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    public sealed class TownBootTilemapVisualTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-visual", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            CleanupResidue();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TOWN_VISUAL_PM_001_BootSaveSlotFlowShowsSavedTownTilemap()
        {
            yield return PlayerFlowE2EDriver.LoadScene("Boot");
            yield return PlayerFlowE2EDriver.WaitForScene("MainMenu", 15f, "Boot did not load the player-facing MainMenu scene.");

            yield return PlayerFlowE2EDriver.ClickButton(FindButton("SinglePlayButton"));
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            GameObject creator = GameObject.Find("SpumCharacterCreatorRoot");
            if (creator != null)
            {
                yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(creator.transform, "ConfirmButton"));
            }

            yield return PlayerFlowE2EDriver.WaitForScene("Town", 15f, "SaveSlot New Game did not enter Town from Boot/MainMenu player flow.");
            yield return null;

            Tilemap ground = GameObject.Find("GroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(ground, "Town runtime must expose the saved GroundTilemap.");
            int groundTileCount = CountTiles(ground);
            Assert.GreaterOrEqual(groundTileCount, 600, "Boot-to-Town runtime must show the saved Town tile layout.");
            AssertTilemapExistsWithRenderer("TownDecorationTilemap", 10);
            AssertNoVisibleRuntimeCue("TownApartment");
            AssertNoVisibleRuntimeCue("TownStreet");
            AssertNoVisibleRuntimeCue("TownShop");
            AssertNoVisibleRuntimeCue("TownCommunityBoard");
            Assert.IsNotNull(Camera.main, "Town must have a player-facing camera for visual verification.");
            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose the player reached by the Boot/MainMenu flow.");
            Vector3Int playerCell = ground.WorldToCell(player.transform.position);
            Assert.IsNotNull(ground.GetTile(playerCell), "Boot-to-Town first view must have a saved ground tile under the player spawn cell " + playerCell + ".");

            string screenshotPath = Path.Combine(Application.temporaryCachePath, "TownBootTilemapVisual.png");
            if (File.Exists(screenshotPath))
            {
                File.Delete(screenshotPath);
            }

            Debug.Log("TOWN_VISUAL_PM_001 runtime GroundTilemap tiles=" + groundTileCount + " screenshot=" + screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            float screenshotWait = 0f;
            while (!File.Exists(screenshotPath) && screenshotWait < 3f)
            {
                screenshotWait += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(File.Exists(screenshotPath), "Town visual verification screenshot was not captured: " + screenshotPath);
        }

        private static Button FindButton(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, "UI button missing: " + name);
            Button button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
        }

        private static void AssertTilemapExistsWithRenderer(string name, int sortingOrder)
        {
            GameObject tilemapObject = GameObject.Find(name);
            Assert.IsNotNull(tilemapObject, name + " must exist at runtime as a named visual layer.");
            Assert.IsNotNull(tilemapObject.GetComponent<Tilemap>(), name + " must contain a Tilemap component.");
            TilemapRenderer renderer = tilemapObject.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, name + " must contain a TilemapRenderer component.");
            Assert.IsTrue(renderer.enabled, name + " renderer must be enabled.");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, name + " must keep the expected rendering order.");
        }

        private static void AssertNoVisibleRuntimeCue(string name)
        {
            GameObject cue = GameObject.Find(name);
            if (cue == null)
            {
                return;
            }

            SpriteRenderer renderer = cue.GetComponent<SpriteRenderer>();
            Assert.IsTrue(renderer == null || !renderer.enabled || !cue.activeInHierarchy, name + " must not visibly cover the saved Town tilemap.");
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            BoundsInt bounds = tilemap.cellBounds;
            foreach (Vector3Int cell in bounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
        }
    }
}