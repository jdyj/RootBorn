using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseExpansionInteriorLoopPlayModeTests
    {
        [UnityTest]
        public IEnumerator HOUSE_LOOP_PM_001_HireExpansionUpdatesHouseBoundsAndPlacementSurface()
        {
            const string saveSlot = "house-loop-stage-bounds";
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = saveSlot, DisplayName = saveSlot, WorldSeed = 1205, TileSeed = 1205 });
            HouseStatePersistence.Save(saveSlot, new HouseStateSaveData { CurrentStageIndex = 1 });

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var probe = HouseInteriorLoopProbe.Capture();

            Assert.AreEqual(1, probe.CurrentStageIndex);
            Assert.Greater(probe.GroundTileCount, 0);
            Assert.Greater(probe.SurfaceCellCount, 0);
            Assert.IsFalse(probe.HasVisibleDebugOverlay);
        }
        [UnityTest]
        public IEnumerator HOUSE_LOOP_PM_003_FurniturePlaceRotateMovePersistsAfterReload()
        {
            const string saveSlot = "house-loop-furniture-reload";
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = saveSlot, DisplayName = saveSlot, WorldSeed = 1205, TileSeed = 1205 });
            HouseStatePersistence.Save(saveSlot, new HouseStateSaveData { CurrentStageIndex = 1 });

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(FindObjectsInactive.Include);
            Assert.IsNotNull(overlay);

            Assert.IsTrue(overlay.SelectFirstFurnitureForTests(), overlay.LastMessage);
            overlay.RotateActiveFurniture();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var placeMessage), placeMessage);
            Assert.IsTrue(overlay.MoveSelectedFurniture());
            Assert.IsTrue(overlay.SaveFurnitureLayout());

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var probe = HouseInteriorLoopProbe.Capture();
            Assert.AreEqual(1, probe.CurrentStageIndex);
            Assert.Greater(probe.PlacedFurnitureCount, 0);
            Assert.IsFalse(probe.HasVisibleDebugOverlay);
        }
    }
}
