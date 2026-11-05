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
    public sealed class TownBootTilemapVisualTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ExpectedTownNpcTextureName = "Premade_Character_32x32_01";
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

        [Ignore(LegacyPremise.AuthoredTown + "after entering Town it waits for old prototype object names")]
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

            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "SaveSlot New Game did not enter Town from Boot/MainMenu player flow.");
            yield return null;

            Tilemap legacyGround = AssertLegacyTilemapEmptyWithRenderer("GroundTilemap", 0);
            AssertLegacyTilemapEmptyWithRenderer("TownDecorationTilemap", 10);
            Tilemap ldtkGround = AssertTownOperationalLdtkMapVisible();
            AssertModernTownNpcsVisible();
            AssertNoVisibleRuntimeCue("TownApartment");
            AssertNoVisibleRuntimeCue("TownStreet");
            AssertNoVisibleRuntimeCue("TownShop");
            AssertNoVisibleRuntimeCue("TownCommunityBoard");
            Assert.IsNotNull(Camera.main, "Town must have a player-facing camera for visual verification.");
            Assert.AreEqual(9f, Camera.main.orthographicSize, 0.01f, "Town camera must be slightly zoomed out for the LDtk town view.");
            GameObject player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose the player reached by the Boot/MainMenu flow.");
            AssertInitialEditorPanelClosed();
            AssertSingleActiveEventSystem();
            Vector3Int playerCell = ldtkGround.WorldToCell(player.transform.position);
            Assert.IsNotNull(ldtkGround.GetTile(playerCell), "Boot-to-Town first view must have an LDtk ground tile under the player spawn cell " + playerCell + ". Legacy count=" + CountTiles(legacyGround));
            Vector3 startPosition = player.transform.position;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, startPosition + new Vector3(0f, -1.0f, 0f), 0.45f, 240, "Town playable map must allow actual keyboard movement from the spawn road.");
            Assert.Greater(Vector3.Distance(startPosition, player.transform.position), 0.35f, "Player must visibly move on the Town playable map.");

            string screenshotPath = Path.Combine(Application.temporaryCachePath, "TownBootTilemapVisual.png");
            if (File.Exists(screenshotPath))
            {
                File.Delete(screenshotPath);
            }

            Debug.Log("TOWN_VISUAL_PM_001 runtime LDtkGround tiles=" + CountTiles(ldtkGround) + " screenshot=" + screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            float screenshotWait = 0f;
            while (!File.Exists(screenshotPath) && screenshotWait < 3f)
            {
                screenshotWait += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(File.Exists(screenshotPath), "Town visual verification screenshot was not captured: " + screenshotPath);
        }

        private static void AssertModernTownNpcsVisible()
        {
            GameObject root = GameObject.Find("[NPCs]");
            Assert.IsNotNull(root, "Town must expose the NPC root at runtime.");
            AssertModernNpc(root, "GuideNpc");
        }

        private static void AssertModernNpc(GameObject root, string name)
        {
            Transform child = root.transform.Find(name);
            Assert.IsNotNull(child, "Missing playable Town NPC: " + name);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, name + " must render with a SpriteRenderer.");
            Assert.IsTrue(renderer.enabled, name + " renderer must be enabled.");
            Assert.IsNotNull(renderer.sprite, name + " must have a Modern character sprite.");
            Assert.AreEqual(ExpectedTownNpcTextureName, renderer.sprite.texture.name, name + " must use the shared Modern NPC character sheet.");
            Assert.IsNotNull(child.GetComponent<Rootborn.Game.Dialogue.NpcInteractor>(), name + " must remain interactable as an NPC.");
            Assert.IsNotNull(child.GetComponent<Rootborn.Game.Dialogue.QuestProvider>(), name + " must keep quest provider wiring.");
            Collider2D collider = child.GetComponent<Collider2D>();
            Assert.IsNotNull(collider, name + " must have an interaction trigger collider.");
            Assert.IsTrue(collider.isTrigger, name + " collider must be an interaction trigger.");
        }

        private static void AssertSingleActiveEventSystem()
        {
            EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.LessOrEqual(systems.Length, 1, "Playable Town must not spawn duplicate active EventSystems.");
        }

        private static void AssertInitialEditorPanelClosed()
        {
            GameObject panel = GameObject.Find("TilePlacementPanel");
            Assert.IsTrue(panel == null || !panel.activeInHierarchy, "Tile placement editor panel must not start open over the playable Town view.");
        }

        private static Tilemap AssertTownOperationalLdtkMapVisible()
        {
            Assert.IsNull(GameObject.Find("Town2LdtkComposite"), "Town must not keep the stale composite preview object now that operational Tilemap layers are the player-facing render path.");

            GameObject root = GameObject.Find("Town2LdtkTilemaps");
            Assert.IsNotNull(root, "Town must expose operational LDtk Tilemap layers at runtime.");
            Tilemap ground = AssertLdtkLayer(root, "Town2LdtkGroundTilemap", 5050, 0);
            AssertLdtkLayer(root, "Town2LdtkGround2Tilemap", 96, 100);
            AssertLdtkLayer(root, "Town2LdtkBeachTilemap", 0, 900);
            AssertLdtkLayer(root, "Town2LdtkFenseTilemap", 96, 300);
            AssertLdtkLayer(root, "Town2LdtkPropsTilemap", 687, 200);
            AssertLdtkLayer(root, "Town2LdtkShopTilemap", 237, 400);
            AssertLdtkLayer(root, "Town2LdtkHospitalTilemap", 605, 500);
            AssertLdtkLayer(root, "Town2LdtkPoliceTilemap", 141, 600);
            AssertLdtkLayer(root, "Town2LdtkBuildingTilemap", 279, 700);
            AssertLdtkLayer(root, "Town2LdtkSchoolTilemap", 47, 800);
            return ground;
        }

        private static Tilemap AssertLdtkLayer(GameObject root, string name, int expectedTiles, int sortingOrder)
        {
            Transform child = root.transform.Find(name);
            Assert.IsNotNull(child, "Missing LDtk Tilemap layer: " + name);
            Tilemap tilemap = child.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name + " must have a Tilemap component.");
            Assert.AreEqual(expectedTiles, CountTiles(tilemap), name + " must keep its renderable LDtk cell count at runtime.");
            TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, name + " must have a TilemapRenderer component.");
            Assert.IsTrue(renderer.enabled, name + " renderer must be enabled for the player-facing view.");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, name + " must keep the expected LDtk draw order.");
            return tilemap;
        }

        private static Button FindButton(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.IsNotNull(go, "UI button missing: " + name);
            Button button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
        }

        private static Tilemap AssertLegacyTilemapEmptyWithRenderer(string name, int sortingOrder)
        {
            GameObject tilemapObject = GameObject.Find(name);
            Assert.IsNotNull(tilemapObject, name + " must exist at runtime as a named compatibility layer.");
            Tilemap tilemap = tilemapObject.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name + " must contain a Tilemap component.");
            Assert.AreEqual(0, CountTiles(tilemap), name + " must not render the old pre-LDtk sample tiles.");
            TilemapRenderer renderer = tilemapObject.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, name + " must contain a TilemapRenderer component.");
            Assert.IsTrue(renderer.enabled, name + " renderer must remain enabled for runtime compatibility.");
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, name + " must keep the expected rendering order.");
            return tilemap;
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
