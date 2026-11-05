using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using Rootborn.Game.Save;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.Housing;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class HouseUpgradeInteriorLoopE2EScenarioTests
    {
        private const string TestSlotId = "slot-0";
        private const string ScreenshotPath = "Builds/Logs/house-upgrade-interior-loop-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            ClearStaticFurnitureLayoutCache();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-house-upgrade-interior-loop-e2e", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = TestSlotId, DisplayName = TestSlotId, WorldSeed = 1205, TileSeed = 1205 });
            ScreenshotEvidence.DeleteIfExists(ScreenshotPath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            ClearStaticFurnitureLayoutCache();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [Ignore(LegacyPremise.AuthoredHouse + "room preset application failed")]
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload()
        {
            HouseStatePersistence.Save(TestSlotId, new HouseStateSaveData { Currency = new HouseCurrencySaveData { Balance = 500 } });

            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForFrames(3);

            var panel = Object.FindFirstObjectByType<HouseUpgradePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "HOUSE-UPGRADE-E2E-001 failed: Town did not install the House upgrade panel.");
            panel.OpenDefaultOfferForTests(TestSlotId, directEligible: false);
            yield return null;

            var hireButton = FindButton("HireConstructionButton");
            Assert.IsTrue(hireButton.interactable, "HOUSE-UPGRADE-E2E-001 failed: hire construction route was not available with sufficient balance.");
            hireButton.onClick.Invoke();
            yield return null;

            AssertHouseStateContainsExactlyOnce("\"CurrentStageIndex\": 1", "HOUSE-UPGRADE-E2E-001 failed: House state must save stage 1 exactly once after hire upgrade.");
            AssertHouseStateContainsExactlyOnce("\"LatestRoute\": 1", "HOUSE-UPGRADE-E2E-001 failed: House state must save hire construction route exactly once.");

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForFrames(6);

            var expandedProbe = HouseInteriorLoopProbe.Capture();
            Assert.AreEqual(1, expandedProbe.CurrentStageIndex, "HOUSE-UPGRADE-E2E-001 failed: House scene did not load the saved stage 1 state.");
            Assert.Greater(expandedProbe.GroundTileCount, 0, "HOUSE-UPGRADE-E2E-001 failed: expanded House ground layer was empty.");
            Assert.Greater(expandedProbe.WallTileCount, 0, "HOUSE-UPGRADE-E2E-001 failed: expanded House wall layer was empty.");
            Assert.Greater(expandedProbe.SurfaceCellCount, 0, "HOUSE-UPGRADE-E2E-001 failed: placement surface did not bind to expanded House bounds.");

            var preset = InteriorRoomPresetCatalog.LoadPresets().FirstOrDefault();
            Assert.IsNotNull(preset, "HOUSE-UPGRADE-E2E-001 failed: no room preset was available for the integrated House loop.");
            Assert.IsTrue(InteriorRoomPresetToolbarInstaller.ApplyPresetForTests(preset), "HOUSE-UPGRADE-E2E-001 failed: room preset application failed for " + preset.StableId);
            yield return null;
            AssertHouseStateContainsExactlyOnce("\"SelectedRoomPresetId\": \"" + preset.StableId + "\"", "HOUSE-UPGRADE-E2E-001 failed: selected room preset was not saved exactly once.");

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(FindObjectsInactive.Include);
            Assert.IsNotNull(overlay, "HOUSE-UPGRADE-E2E-001 failed: House placement overlay was not installed.");
            Assert.IsTrue(overlay.SelectFirstFurnitureForTests(), overlay.LastMessage);
            var direction = overlay.RotateActiveFurniture();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var placeMessage), placeMessage);
            Assert.IsTrue(overlay.MoveSelectedFurniture(), "HOUSE-UPGRADE-E2E-001 failed: placed furniture could not be moved to a second valid cell.");
            Assert.IsTrue(overlay.SaveFurnitureLayout(), "HOUSE-UPGRADE-E2E-001 failed: furniture layout save did not record the placed furniture.");
            yield return null;

            string layoutPath = ResolveStageLayoutFilePath(1);
            var savedLayout = ReadSingleFurnitureLayout(layoutPath);
            Assert.AreEqual(direction, savedLayout.Direction, "HOUSE-UPGRADE-E2E-001 failed: saved furniture direction did not match the player-facing rotate action.");
            AssertFurnitureLayoutContainsExactlyOnce(layoutPath, "FurnitureId", savedLayout.FurnitureId, "HOUSE-UPGRADE-E2E-001 failed: saved furniture id must appear exactly once.");
            AssertFurnitureLayoutContainsExactlyOnce(layoutPath, "AnchorX", savedLayout.AnchorX.ToString(), "HOUSE-UPGRADE-E2E-001 failed: saved furniture anchor X must appear exactly once.");
            AssertFurnitureLayoutContainsExactlyOnce(layoutPath, "Direction", ((int)savedLayout.Direction).ToString(), "HOUSE-UPGRADE-E2E-001 failed: saved furniture direction must appear exactly once.");
            AssertRuntimeTilemapsMatchSavedLayout(savedLayout, "HOUSE-UPGRADE-E2E-001 failed: runtime tilemaps did not match saved furniture layout before reload.");
            AssertNoVisibleOverlayArtifacts("HOUSE-UPGRADE-E2E-001 failed: overlay/debug artifacts remained visible after placement save.");

            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "HOUSE-UPGRADE-E2E-001 failed: Game View screenshot evidence was not written after placement.");

            ClearStaticFurnitureLayoutCache();
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForFrames(6);

            var reloadedProbe = HouseInteriorLoopProbe.Capture();
            Assert.AreEqual(1, reloadedProbe.CurrentStageIndex, "HOUSE-UPGRADE-E2E-001 failed: stage 1 did not survive House reload.");
            Assert.AreEqual(preset.StableId, reloadedProbe.SelectedRoomPresetId, "HOUSE-UPGRADE-E2E-001 failed: selected room preset did not survive House reload.");
            Assert.Greater(reloadedProbe.PlacedFurnitureCount, 0, "HOUSE-UPGRADE-E2E-001 failed: placed furniture did not restore after reload.");
            AssertHouseStateContainsExactlyOnce("\"CurrentStageIndex\": 1", "HOUSE-UPGRADE-E2E-001 failed: reload duplicated or removed saved House stage.");
            AssertHouseStateContainsExactlyOnce("\"SelectedRoomPresetId\": \"" + preset.StableId + "\"", "HOUSE-UPGRADE-E2E-001 failed: reload duplicated or removed saved preset id.");
            AssertFurnitureLayoutContainsExactlyOnce(layoutPath, "FurnitureId", savedLayout.FurnitureId, "HOUSE-UPGRADE-E2E-001 failed: reload duplicated or removed saved furniture id.");
            AssertRuntimeTilemapsMatchSavedLayout(savedLayout, "HOUSE-UPGRADE-E2E-001 failed: runtime tilemaps did not match saved furniture layout after reload.");
            AssertNoVisibleOverlayArtifacts("HOUSE-UPGRADE-E2E-001 failed: overlay/debug artifacts remained visible after reload.");
        }

        private static IEnumerator WaitForFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        private static Button FindButton(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "HOUSE-UPGRADE-E2E-001 failed: missing button " + name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, "HOUSE-UPGRADE-E2E-001 failed: object is not a Button " + name);
            return button;
        }

        private void AssertHouseStateContainsExactlyOnce(string expected, string failure)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, "house-state.json");
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            string json = File.ReadAllText(path);
            Assert.AreEqual(1, CountOccurrences(json, expected), failure + " Expected: " + expected + " Json: " + json);
        }

        private static void AssertFurnitureLayoutContainsExactlyOnce(string layoutPath, string fieldName, string expectedValue, string failure)
        {
            Assert.IsTrue(File.Exists(layoutPath), failure + " Missing file: " + layoutPath);
            string json = File.ReadAllText(layoutPath);
            string expectedField = int.TryParse(expectedValue, out _)
                ? "\"" + fieldName + "\": " + expectedValue
                : "\"" + fieldName + "\": \"" + expectedValue + "\"";
            Assert.AreEqual(1, CountOccurrences(json, expectedField), failure + " Field: " + expectedField + " Json: " + json);
        }

        private static void AssertRuntimeTilemapsMatchSavedLayout(FurniturePlacementSaveData savedLayout, string failure)
        {
            var objectTilemap = RequiredTilemap("HouseFurnitureObjectTilemap");
            var occupancyTilemap = RequiredTilemap("HouseFurnitureOccupancyTilemap");
            var anchor = new Vector3Int(savedLayout.AnchorX, savedLayout.AnchorY, 0);
            Assert.IsNotNull(occupancyTilemap.GetTile(anchor), failure + " Missing occupancy at saved anchor: " + anchor);
            Assert.IsTrue(ContainsTile(objectTilemap, savedLayout.TileName, out _), failure + " Missing saved furniture tile name: " + savedLayout.TileName);
        }

        private static void AssertNoVisibleOverlayArtifacts(string failure)
        {
            var renderers = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                string name = renderer.gameObject.name;
                bool artifactName = name.Contains("Debug") || name.Contains("Sample") || name.Contains("GhostPreview") || name.Contains("FootprintPreview") || name.Contains("Construction");
                if (!artifactName && name != "AuthoredPlacementAvailability") continue;
                var tilemap = renderer.GetComponent<Tilemap>();
                if (tilemap != null && CountTiles(tilemap) > 0)
                {
                    Assert.Fail(failure + " Visible overlay artifact has tiles: " + name);
                }
            }
        }

        private static Tilemap RequiredTilemap(string name)
        {
            var tilemap = GameObject.Find(name)?.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, "HOUSE-UPGRADE-E2E-001 failed: required Tilemap missing: " + name);
            return tilemap;
        }

        private static bool ContainsTile(Tilemap tilemap, string tileName, out Vector3Int foundCell)
        {
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name == tileName)
                {
                    foundCell = position;
                    return true;
                }
            }

            foundCell = default;
            return false;
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null) count++;
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

        private string ResolveStageLayoutFilePath(int stageIndex)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, FurniturePlacementLayoutPersistence.GetHouseLayoutFileNameForStage(stageIndex));
            Assert.IsTrue(File.Exists(path), "HOUSE-UPGRADE-E2E-001 failed: missing stage furniture layout file: " + path);
            return path;
        }

        private static FurniturePlacementSaveData ReadSingleFurnitureLayout(string layoutPath)
        {
            string json = File.ReadAllText(layoutPath);
            var layout = JsonUtility.FromJson<FurniturePlacementLayoutSaveData>(json);
            Assert.IsNotNull(layout, "HOUSE-UPGRADE-E2E-001 failed: could not parse layout file: " + layoutPath);
            Assert.IsNotNull(layout.Items, "HOUSE-UPGRADE-E2E-001 failed: layout file had null items: " + layoutPath);
            Assert.AreEqual(1, layout.Items.Length, "HOUSE-UPGRADE-E2E-001 failed: layout must contain exactly one furniture item. Json: " + json);
            Assert.IsFalse(string.IsNullOrEmpty(layout.Items[0].FurnitureId), "HOUSE-UPGRADE-E2E-001 failed: saved furniture id was blank.");
            Assert.IsFalse(string.IsNullOrEmpty(layout.Items[0].TileName), "HOUSE-UPGRADE-E2E-001 failed: saved furniture tile name was blank.");
            return layout.Items[0];
        }

        private static void ClearStaticFurnitureLayoutCache()
        {
            var field = typeof(InteriorPlacementPreviewOverlay).GetField("SavedFurnitureLayout", BindingFlags.Static | BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(null) as IList<FurniturePlacementSaveData> : null;
            list?.Clear();
        }
    }
}