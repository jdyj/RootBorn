using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HomeDesignRoomPresetPlayModeTests
    {
        [UnityTest]
        public IEnumerator HousePlacementPanel_RoomPresetButtonAppliesCondominiumLayer1ToVisibleTilemap()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForFrames(20);

            Button button = null;
            for (int i = 0; i < 80 && button == null; i++)
            {
                button = GameObject.Find("RoomPresetButton_modern.home-designs.condominium-design-2")?.GetComponent<Button>();
                yield return null;
            }

            Assert.IsNotNull(button, "House furniture placement UI should expose an apply button for the imported condominium Home Design preset.");

            button.onClick.Invoke();
            yield return null;
            yield return null;

            var baseLayer = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(baseLayer);
            Assert.AreEqual(84, CountTiles(baseLayer), "Condominium Design 2 layer_1 should apply as exactly 14x6 visible base tiles.");
            Assert.IsNotNull(baseLayer.GetTile(new Vector3Int(-7, -3, 0)));
            Assert.IsNotNull(baseLayer.GetTile(new Vector3Int(6, 2, 0)));

            var status = GameObject.Find("InteriorPlacementStatusText")?.GetComponent<Text>();
            Assert.IsNotNull(status);
            StringAssert.Contains("Condominium Design 2", status.text);

            var mapProbe = Object.FindFirstObjectByType<InteriorRoomPresetRuntimeProbe>();
            Assert.IsNotNull(mapProbe, "Applying a room preset should leave runtime state that visual verification can inspect.");
            Assert.AreEqual("modern.home-designs.condominium-design-2", mapProbe.LastPresetId);
            Assert.AreEqual(84, mapProbe.LastBaseTileCount);
            Assert.AreEqual(new Vector2Int(14, 6), mapProbe.LastPresetSize);
        }

        [UnityTest]
        public IEnumerator HOUSE_LOOP_PM_002_RoomPresetAppliesToExpandedHouseWithoutClearingFurniture()
        {
            const string saveSlot = "house-loop-preset-playmode";
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = saveSlot, DisplayName = saveSlot, WorldSeed = 1205, TileSeed = 1205 });

            var state = new HouseStateSaveData { CurrentStageIndex = 1 };
            HouseStatePersistence.Save(saveSlot, state);

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var applier = Object.FindFirstObjectByType<InteriorTilemapApplier>();
            Assert.IsNotNull(applier);

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(FindObjectsInactive.Include);
            Assert.IsNotNull(overlay);
            Assert.Greater(overlay.ActiveSurfaceCellCountForTests, 0);

            var preset = InteriorRoomPresetCatalog.LoadPresets().FirstOrDefault();
            Assert.IsNotNull(preset, "At least one imported room preset is required for the House loop.");

            Assert.IsTrue(InteriorRoomPresetToolbarInstaller.ApplyPresetForTests(preset));

            var loaded = HouseStatePersistence.Load(saveSlot);
            Assert.AreEqual(preset.StableId, loaded.SelectedRoomPresetId);
            Assert.Greater(overlay.ActiveSurfaceCellCountForTests, 0);
        }
        private static IEnumerator WaitForFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
