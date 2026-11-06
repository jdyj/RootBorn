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
            const string saveSlot = "house-loop-reload";
            // Entering as a new game binds the furniture session to the slot owner; a direct scene load does not.
            ActiveSaveContext.Clear();
            HouseStatePersistence.Save(saveSlot, new HouseStateSaveData { CurrentStageIndex = 1 });

            yield return HousePlacementTestDriver.EnterHouse(saveSlot);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            // The placement overlay appears once the player picks a piece from the palette.
            HousePlacementTestDriver.ShowFurniture("chair.black").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(FindObjectsInactive.Include);
            Assert.IsNotNull(overlay, HousePlacementTestDriver.OverlayDiagnostics());
            overlay.RotateActiveFurniture();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var placeMessage), placeMessage);
            // Move picks the piece up; the next click on a free cell puts it down.
            Assert.IsTrue(overlay.MoveSelectedFurniture());
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var moveMessage), moveMessage);
            Assert.IsTrue(overlay.SaveFurnitureLayout());

            // Leave to the main menu and come back, as a player reloading the save would.
            ActiveSaveContext.Clear();
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return HousePlacementTestDriver.EnterHouse(saveSlot);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            var probe = HouseInteriorLoopProbe.Capture();
            Assert.AreEqual(1, probe.CurrentStageIndex);
            Assert.Greater(probe.PlacedFurnitureCount, 0);
            Assert.IsFalse(probe.HasVisibleDebugOverlay);
        }
    }
}
