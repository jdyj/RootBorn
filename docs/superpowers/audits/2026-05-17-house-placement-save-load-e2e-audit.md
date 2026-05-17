# House Placement Save/Load E2E Audit

## Scope

- Flow: MainMenu -> New Game -> SPUM Confirm -> Town -> House -> furniture palette selection -> world click placement -> Save -> exit to MainMenu -> Load slot -> Town -> House.
- Persistence target: the placed House furniture object tile and occupancy tile must survive leaving the scene and reloading from the same save slot.
- Visual gate: runtime Tilemap state and Game View/Camera screenshot must be inspected for the restored result.

## Current Code Findings

- `SaveSlotSelectPanel` already drives the player-facing new game path through `NewGameButton`, SPUM creator `ConfirmButton`, metadata save, `ActiveSaveContext.Set`, and `Town` scene load.
- `InteriorFurniturePlacementPanel` already exposes player-facing `SaveFurnitureLayoutButton`, `ClearFurnitureLayoutButton`, and `LoadFurnitureLayoutButton`.
- `InteriorPlacementPreviewOverlay` already supports UI selection plus world mouse placement and writes object/occupancy Tilemaps.
- Existing save/load only keeps furniture layout in a static runtime list. It does not write the furniture layout through `SaveService`, so it cannot survive an actual save-slot reload/process-style flow.

## Scenario

1. Open `MainMenu`.
2. Open save slots and click `NewGameButton`.
3. Confirm the SPUM character creator with `ConfirmButton`.
4. Wait for `Town`, and verify `ActiveSaveContext` points to `slot-0`.
5. Enter/load `House`.
6. Select a generated Modern Interiors shadowless furniture palette button through the UI event path.
7. Click a visible valid placement cell in world space through the Input System mouse path.
8. Verify the placed furniture tile name, cell, and occupancy cell in `HouseFurnitureObjectTilemap` and `HouseFurnitureOccupancyTilemap`.
9. Click `SaveFurnitureLayoutButton`, and verify a save-slot JSON file exists.
10. Simulate leaving the game by returning to `MainMenu` and clearing active runtime context.
11. Load `slot-0` through the save slot UI load button, wait for `Town`, and re-enter/load `House`.
12. Verify the same furniture tile name and cell are restored with occupancy, without stale sample/debug Tilemaps or duplicate transient overlays.

## Test ID

- `HOUSE_PLACEMENT_E2E_001_NewGamePlaceSaveExitLoadRestoresHouseFurniture`
