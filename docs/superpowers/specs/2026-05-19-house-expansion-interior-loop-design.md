# House Expansion Interior Loop Design

Date: 2026-05-19

## Purpose

This feature connects the existing House upgrade, room preset, and furniture placement foundations into one player-facing loop.

The target experience is:

1. The player expands the House to the next saved stage.
2. The expanded House generates a larger interior from the saved stage.
3. The player applies a room preset to that expanded space.
4. The player places, rotates, moves, or removes furniture in the expanded space.
5. The House reloads with the same stage, room layout, and furniture layout.

This is not a new free-form construction system. It is a hardening and integration slice that makes already-created Housing and Interiors systems behave as a coherent gameplay feature.

## Current Baseline

The repository already contains the main building blocks:

- `HouseUpgradeStageDefinition`, `HouseConstructionBlueprintDefinition`, `HouseUpgradeService`, and `HouseStatePersistence` in `Rootborn.Game.Housing`.
- `InteriorGeneration`, `InteriorTilemapApplier`, `InteriorRoomPresetDefinition`, and `InteriorRoomPresetRuntime` in `Rootborn.Game.Interiors`.
- `FurnitureDefinition`, `FurniturePlacementRegistry`, `FurniturePlacementService`, and `FurniturePlacementLayoutPersistence` in `Rootborn.Game.Placement`.
- `HouseUpgradePanel`, `HouseConstructionOverlay`, `InteriorPlacementPreviewOverlay`, placement camera controls, and room preset toolbar in UI code.
- EditMode and PlayMode tests for House upgrade, placement, room presets, and visual cleanup.

The risk is that these features exist as adjacent slices, but the player-facing end-to-end flow can still break at the boundaries:

- stage expansion can generate a larger room without the placement surface using the new bounds;
- room presets can repaint floor tiles without preserving placed furniture correctly;
- furniture layout can persist while the generated room changes underneath it;
- overlay/debug tilemaps can remain visible after placement or reload;
- tests can validate data while the Game View still shows stale or hidden results.

## Feature Scope

### In Scope

- One integrated stage-1 House expansion loop.
- Expanded interior bounds drive the placement surface bounds.
- Room preset application is allowed only when it fits the active generated room.
- Furniture placement uses the active stage and active preset surface.
- Furniture layout persistence is scoped to the House save slot and active stage.
- Reload verifies:
  - saved stage;
  - generated tilemap size/counts;
  - selected preset identity;
  - placed furniture tile names and cell positions;
  - absence of stale preview/debug overlays.

### Out of Scope

- Additional House stages beyond the first expanded room.
- Free-form wall editing.
- Multiplayer synchronization of House edits.
- Advanced resource inventory costs for construction.
- Decorative room-rating systems.
- New art direction or new asset import pipeline.

## Architecture

### Stage Source Of Truth

`HouseStateSaveData.CurrentStageIndex` remains the source of truth for the House stage.

On House load:

1. `HouseStatePersistence` loads the saved stage.
2. `InteriorTilemapApplier` resolves the stage's generation profile.
3. The generated map defines the ground/wall/collision layers.
4. The placement surface binds to the generated map bounds.
5. Saved furniture layout is restored only if each item fits the current stage bounds.

Runtime tilemaps are never treated as permanent truth by themselves.

### Room Preset Flow

Room presets are applied after base House generation and before furniture restore when loading, or immediately when the player selects a preset from the toolbar.

Preset application must:

- record the selected preset stable id in House/interior save data;
- validate the preset against active room bounds;
- repaint only the intended floor/base layer;
- avoid clearing furniture/object/occupancy tilemaps unless explicitly requested by the preset operation;
- refresh placement surface availability after application.

If no preset is saved, the generated default room remains active.

### Furniture Placement Flow

Furniture placement uses the active `TilePlacementSurface`.

Required behavior:

- placement bounds match the active House stage;
- occupied cells reject overlap;
- rotation updates footprint and tile parts;
- move preserves instance id where possible;
- delete clears object and occupancy cells;
- save data records furniture id, anchor cell, direction, and optional interaction state;
- restore is idempotent and does not duplicate furniture after repeated reloads.

If a saved furniture entry no longer fits the active stage or definition set, it is skipped and logged as restore failure evidence. It must not block the rest of the House from loading.

## Data Model

### House Interior Save Data

Add or reuse a small saved payload scoped to the House save slot:

- `CurrentStageIndex`
- `SelectedRoomPresetId`
- `FurnitureLayout[]`

If the existing save files already split these values, this slice may keep that storage split. The required behavior is that tests can inspect all three values after reload.

### ScriptableObject Requirements

No entity-specific branching is allowed. Stage, preset, and furniture behavior must come from:

- `HouseUpgradeStageDefinition`
- `InteriorRoomPresetDefinition`
- `FurnitureDefinition`
- strategy arrays or reusable validation helpers

Gameplay code must not contain checks such as `if (presetId == "...")`, `switch(stageId)`, or furniture-specific classes.

## UI And Player Flow

The first shippable player path:

1. Player opens the House upgrade panel through the existing provider flow.
2. Player chooses the hire route for stage 1.
3. Player enters or reloads House and sees the expanded interior.
4. Player opens the room preset toolbar and selects one preset.
5. Player opens placement mode, selects one furniture item, rotates it, places it, moves it once, then deletes or keeps it depending on the test scenario.
6. Player reloads House and sees the saved result.

Placement mode controls remain consistent with the current implementation:

- mouse click to place/select;
- rotation command/button for direction;
- camera wheel zoom;
- keyboard pan/full-view behavior.

The UI must not introduce a permanent quest or goal HUD. Any future objective tracking belongs to `ObjectiveJournalPanel`.

## Error Handling

The system should fail visibly and recoverably:

- no next stage: panel shows no expansion available;
- insufficient currency: hire button disabled;
- preset too large for stage: preset button disabled or operation rejected with status text;
- furniture out of bounds: placement rejected with overlay/status feedback;
- restore entry invalid: skip invalid item, restore valid items, and log a diagnostic line.

No partial save mutation should occur when a stage upgrade, preset application, or furniture placement transaction fails.

## Testing

### EditMode

- Stage 1 profile expands placement bounds.
- Room preset fit validation rejects presets outside bounds.
- Preset application does not clear furniture/occupancy layers.
- Furniture restore skips invalid entries and restores valid entries.
- Save payload records stage, preset id, furniture id, anchor, and direction.

### PlayMode

Named scenarios:

- `HOUSE_LOOP_PM_001_HireExpansionUpdatesHouseBoundsAndPlacementSurface`
- `HOUSE_LOOP_PM_002_RoomPresetAppliesToExpandedHouseWithoutClearingFurniture`
- `HOUSE_LOOP_PM_003_FurniturePlaceRotateMovePersistsAfterReload`
- `HOUSE_LOOP_PM_004_InvalidSavedFurnitureIsSkippedWithoutBlockingHouseLoad`
- `HOUSE_LOOP_PM_005_VisualOverlayCleanupAfterPlacementAndReload`

PlayMode tests must use player-facing flow where practical: interaction/provider UI, route button, toolbar button, placement UI/pointer path, scene reload.

### Direct Visual Verification

Because this is visible House, tilemap, UI, placement, and camera work, automated tests are not sufficient.

Completion requires:

- enter PlayMode;
- use the player-facing House expansion and placement flow;
- capture or inspect Game View or Camera screenshots after expansion, after preset application, and after reload;
- inspect runtime tilemaps for selected preset, tile names/counts, furniture cells, overlay state, and stale preview/debug tilemaps;
- inspect saved state after reload and confirm the same stage, preset, and furniture layout are reflected visibly.

If any visual verification is blocked, the completion report must state what remains unverified.

## Test Simulation

This slice must include a repeatable House loop simulation in addition to normal EditMode and PlayMode tests.

### Simulation Purpose

The simulation should catch integration failures that isolated tests miss:

- stage save data says expanded, but the visible House is still compact;
- preset save data exists, but the Tilemap did not repaint;
- furniture save data exists, but the Game View shows no furniture;
- placement overlay/debug tilemaps remain visible after completion;
- reload restores stale cells from a previous stage or preset.

### Simulation Runner

Add a scriptable QA runner under `Scripts/qa/` or an equivalent Unity test entrypoint.

Required knobs:

- save slot name;
- scene names for Town and House;
- stage target;
- preset target;
- furniture target;
- run name/log directory;
- optional reload count;
- optional screenshot output directory.

The runner must drive the same player-facing path used by the PlayMode scenarios where practical:

1. Start from Town or Boot flow.
2. Open the House upgrade provider/panel.
3. Select hire expansion for stage 1.
4. Enter House.
5. Apply the selected room preset.
6. Enter placement mode.
7. Select furniture.
8. Rotate, place, move, and save.
9. Reload House.
10. Inspect visual/runtime/saved state.

Internal helper calls can be used only for diagnostics and assertions after the player-facing path has run. They must not be the only proof of completion.

### Simulation Assertions

The simulation must assert:

- saved `CurrentStageIndex` equals the target stage;
- runtime House bounds match the target stage profile;
- active placement surface bounds match the generated room bounds;
- selected preset id is saved and reapplied after reload;
- expected floor/wall/collision tile counts are nonzero and within the expanded bounds;
- placed furniture id, anchor, direction, and occupied cells match saved layout;
- invalid/stale furniture entries do not duplicate valid entries;
- no `HousePlacementAvailabilityTilemap`, preview, sample, or debug overlay remains visible after reload;
- screenshot files exist for after expansion, after placement, and after reload.

### Simulation Evidence

The runner should write evidence under `production/qa/evidence/` or `Builds/Logs/<runName>/`:

- textual probe with scene, stage, preset id, furniture id, cell positions, tile counts, overlay state;
- screenshot after House expansion;
- screenshot after furniture placement;
- screenshot after reload;
- path to the save file inspected.

The final audit must list the exact command, result, log directory, and screenshot paths.

## Performance

All updates should be event-driven:

- regenerate House once on load or stage change;
- refresh placement availability after generation, preset change, or placement mutation;
- avoid `Update()` polling for full tilemap scans;
- avoid repeated instantiate/destroy loops for stable UI controls;
- keep restore validation bounded to saved furniture footprint cells.

## Recommended Implementation Slice

Implement the loop with one stage, one room preset, and one furniture item first.

Acceptance criteria:

- stage 1 expansion is visible;
- the placement surface expands with the room;
- one preset applies and persists;
- one furniture item can be rotated, placed, moved, saved, and restored;
- visual overlay/debug artifacts are absent after reload;
- all verification evidence is recorded in an audit document.
