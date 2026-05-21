# Boot Town House-Style Tiles Design

## Goal

When the game starts through `Boot` and reaches `Town`, the first player-facing Town view should look tile-based like the House scene instead of relying on temporary color cue sprites or empty tilemap layers.

## Scope

This work targets the saved `Assets/Scenes/Town.unity` scene. The scene should contain visible, saved Tilemap content for the Town ground and decoration layers so the result exists before runtime generation.

Out of scope:

- MainMenu background changes.
- House scene changes.
- House interior placement, construction, upgrade, or save-slot state changes.
- Reusing House-only runtime placement UI for Town.

## Current Context

`Assets/Scripts/Game/Bootstrap/TownTilemapFallbackInstaller.cs` currently ensures a `Grid`, `TownGroundTilemap`, `TownDecorationTilemap`, and several colored cue `SpriteRenderer` objects at runtime. This helps avoid a blank scene, but it makes the player-facing visual result depend on runtime fallback objects instead of saved scene tile data.

House scenes already use named Tilemap layers such as `HouseGroundTilemap`, `HouseWallTilemap`, `HouseDecorationTilemap`, and `HouseCollisionTilemap`, plus an `InteriorTilemapApplier`. The Town work should borrow the level of tile-based visual completeness, not the House-specific interior systems.

## Design

`Assets/Scenes/Town.unity` is the source of truth for the visible Town tile layout. The saved scene should include a `Grid` and visible Tilemap layers for the first Town view:

- Ground layer: broad walkable floor/path surface.
- Decoration layer: visual accents that make the area read as a composed place.

The runtime fallback installer should remain narrow. It may ensure required Tilemap GameObjects exist when missing, but it should not create or preserve stale sample/debug visual objects that compete with the saved Tilemaps. If a saved Town tile layout exists, Boot-to-Town should show that layout directly.

The tile choices should use existing project Tile assets. The implementation should avoid entity ID branching and should not introduce gameplay-specific hardcoded entity logic. This is a scene visual composition change, not a new gameplay entity system.

## Player Flow

The required player-facing verification path is:

1. Enter PlayMode from `Boot`.
2. Follow the existing game start flow until the active scene is `Town`.
3. Observe the Game View or active Camera output.
4. Confirm that the visible Town first view contains the saved Tilemap layout.
5. Confirm that stale colored cue/sample/debug visuals are not visible over the intended tiles.

## Tests

Add or update automated tests before implementation:

- EditMode saved-scene test: load or inspect `Town.unity` and verify the expected Tilemap layer names, non-zero tile counts, and absence of stale sample/debug cue objects that would be visible in the first view.
- PlayMode Boot-flow test: start through `Boot`, reach `Town` through the normal flow, inspect runtime Tilemap state, and verify visible tile counts and layer names.

For the final visual gate, also capture or inspect a Game View or Camera screenshot after Boot-to-Town. Automated test results alone are not sufficient for this work.

## Verification

Completion requires all of the following evidence:

- The saved `Town.unity` scene contains the expected Tilemap data after reopening or re-inspection.
- The relevant EditMode test passes.
- The relevant PlayMode Boot-flow test passes.
- Runtime Tilemap state reports the selected/visible layer names and non-zero tile counts.
- A Game View or Camera screenshot confirms the visible player-facing result.

If screenshot capture or direct Boot-flow visual verification is blocked, the final report must state the blocker and separate verified saved-scene state from unverified player-facing visuals.
