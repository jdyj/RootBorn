using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseInteriorPlacementUiPlayModeTests
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = System.IO.Path.Combine(Application.temporaryCachePath, "rootborn-house-placement-ui-tests-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "house-placement-ui-tests", DisplayName = "house-placement-ui-tests", WorldSeed = 1205, TileSeed = 1205 });
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
        public IEnumerator WorldClickWithoutSelectedFurniture_DoesNotPlaceGenericDeskCluster()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNull(GameObject.Find("Palette_Desk"));
            var decorations = GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(decorations);
            AssertNoLegacyGeneratedFurniture(decorations);

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNull(overlay, "No placement overlay should be active before the player chooses an actual furniture item from the palette.");
        }

        private static void AssertNoLegacyGeneratedFurniture(Tilemap tilemap)
        {
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Desk"), "Manual placement mode must not auto-generate desks.");
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Computer"), "Manual placement mode must not auto-generate computer clusters.");
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Chair"), "Manual placement mode must not auto-generate chairs.");
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Sofa"), "Manual placement mode must not auto-generate sofas.");
        }

        private static int CountTilesContaining(Tilemap tilemap, string text)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name.Contains(text))
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
