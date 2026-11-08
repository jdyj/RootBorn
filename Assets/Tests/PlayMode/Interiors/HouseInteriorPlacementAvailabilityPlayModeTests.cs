using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseInteriorPlacementAvailabilityPlayModeTests
    {
        private const string ChairButtonName = "PaletteFurniture_chair.black";
        private const string DeskButtonName = "PaletteFurniture_desk.large";
        private const string ChairTileName = "BlackChair_00";
        private const string DeskTileName = "ShadowlessOffice_Desk_Table_r39_c01";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = System.IO.Path.Combine(Application.temporaryCachePath, "rootborn-placement-availability-tests-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "placement-availability-tests", DisplayName = "placement-availability-tests", WorldSeed = 1205, TileSeed = 1205 });
            ClearStaticFurnitureLayoutCache();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            ClearStaticFurnitureLayoutCache();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectingFurniture_ShowsPlacementAvailabilityOverlayAndCounts()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            yield return Rootborn.Tests.PlayMode.EndToEnd.Support.PlayerFlowE2EDriver.ClickButton(HousePlacementTestDriver.ShowFurniture("chair.black").GetComponent<Button>());
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay, "Selecting an actual furniture object should create an availability overlay Tilemap.");
            Assert.Greater(overlay.ValidCellCount, 0, "Overlay should mark valid cells for the selected furniture.");
            Assert.Greater(overlay.InvalidCellCount, 0, "Overlay should distinguish invalid cells from valid cells.");

            var text = GameObject.Find("PlacementAvailabilityText")?.GetComponent<Text>();
            Assert.IsNotNull(text, "Settings panel should show available/requested placement counts.");
            StringAssert.Contains("available", text.text);
            StringAssert.Contains("requested", text.text);
        }

        [UnityTest]
        public IEnumerator ManualPlacementMode_PlacesSelectedFurnitureObjectOnValidCellAndRefreshesTilemap()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            yield return Rootborn.Tests.PlayMode.EndToEnd.Support.PlayerFlowE2EDriver.ClickButton(HousePlacementTestDriver.ShowFurniture("desk.large").GetComponent<Button>());
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            int before = furnitureTilemap != null ? CountTiles(furnitureTilemap, DeskTileName) : 0;

            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains("desk.large", message);
            furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.Greater(CountTiles(furnitureTilemap, DeskTileName), before, "Manual placement should place the selected registry furniture object, not a generic desk cluster request.");
        }

        [UnityTest]
        public IEnumerator BeforeSelectingFurniture_DoesNotShowGenericRequestPreflight()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            Assert.IsNull(GameObject.Find("Palette_Sofa"), "Generic category request buttons must not be exposed in the mockup furniture picker.");
            Assert.IsNull(Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(), "The availability overlay should wait until the player selects a concrete furniture item.");

            var text = GameObject.Find("PlacementAvailabilityText")?.GetComponent<Text>();
            Assert.IsNotNull(text);
            StringAssert.Contains("가구", text.text);
        }

        private static int CountTiles(Tilemap tilemap, string tileName)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name == tileName)
                {
                    count++;
                }
            }

            return count;
        }

        private static void ClearStaticFurnitureLayoutCache()
        {
            var field = typeof(InteriorPlacementPreviewOverlay).GetField("SavedFurnitureLayout", BindingFlags.Static | BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(null) as IList : null;
            list?.Clear();
        }
    }
}
