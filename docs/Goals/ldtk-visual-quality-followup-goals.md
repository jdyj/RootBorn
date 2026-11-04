# LDtk Visual Quality Follow-up Goals

## Goal 1 - Hospital Missing Script Warning Cleanup

### Objective
Remove the missing script warning that appears when entering `HospitalInterior` in PlayMode, after identifying the exact broken scene or prefab reference.

### Scope
- Inspect saved scene state in `Assets/Scenes/HospitalInterior.unity`.
- Locate and remove the missing `MonoBehaviour` reference on `Player` or related scene/prefab objects.
- Preserve existing Hospital entry behavior and visual layout.
- Verify the saved scene/prefab state after cleanup.

### Required Verification
- Enter PlayMode through the real player-facing route: Town -> HospitalInterior.
- Capture or inspect a Game View or Camera screenshot.
- Check Console warnings/errors after the run.
- Run and pass:
  - `TOWN_DOOR_PM_003_PlayerWalksIntoHospitalDoorAndSeesHospitalInteriorLdtkLevel`
  - `TOWN_DOOR_PM_007_PlayerWalksIntoHospitalThenReturnsToTownThroughInteriorLdtkDoorWhenPresent`

### Completion Criteria
- Hospital entry no longer logs:
  - `The referenced script (Unknown) on this Behaviour is missing!`
  - `GameObject (named 'Player') references runtime script in scene file. Fixing!`
- `HospitalInterior` still renders correctly in the player-facing Game View.

## Goal 2 - Town Return Screen Visual Cleanup

### Objective
Clean up the player-facing Town screen after returning from House/Shop interiors so it no longer shows debug/test UI, awkward solid-color framing, or incorrect camera presentation.

### Scope
- Inspect Town return spawn and camera position after interior exits.
- Hide or remove debug/test visual UI from normal player-facing return flows.
- Keep intentional gameplay HUD visible.
- Verify camera bounds, background color, visible tilemap area, and player spawn framing.
- Inspect runtime Canvas/UI/Image state and runtime Tilemap/Renderer state.

### Required Verification
- Use actual PlayMode input flow for HouseInterior -> Town return.
- Use actual PlayMode input flow for ShopInterior -> Town return.
- Capture Game View screenshots for both return paths.
- Run and pass:
  - `TOWN_DOOR_PM_005_PlayerWalksIntoHouseThenReturnsToTownThroughInteriorLdtkDoor`
  - `TOWN_DOOR_PM_006_PlayerWalksIntoShopThenReturnsToTownThroughInteriorLdtkDoor`

### Completion Criteria
- Test/debug visual UI is not visible in the player-facing return screen.
- Town tilemaps occupy the expected visible play area after return.
- Player, camera, and HUD are framed naturally for normal gameplay.

## Goal 4 - Strengthen Interior/Town Visual Regression Tests

### Objective
Prevent Town/House/Shop/Hospital LDtk visuals from silently breaking again by strengthening PlayMode visual regression and runtime-state tests.

### Scope
- Cover these scenes:
  - `Town`
  - `HouseInterior`
  - `ShopInterior`
  - `HospitalInterior`
- Verify key Tilemap used tile counts.
- Verify `TilemapRenderer` visibility and sorting order.
- Verify camera orthographic size, especially interior zoom `6.5`.
- Verify placeholder/debug tilemaps or stale sample objects are not visible.
- Standardize screenshot output names and paths for Game View captures.
- Separate runtime visual verification from saved EditMode scene/asset verification.

### Required Verification
- Enter every target scene through real PlayMode flow:
  - Town -> HouseInterior
  - Town -> ShopInterior
  - Town -> HospitalInterior
  - HouseInterior -> Town
  - ShopInterior -> Town
- Capture Game View screenshots for each visual checkpoint.
- Inspect runtime Tilemap/Renderer/UI state at each checkpoint.
- Inspect saved EditMode scene state separately where persistence is expected.

### Completion Criteria
- Tests fail when a key LDtk layer is missing, empty, hidden, or sorted incorrectly.
- Tests fail when camera zoom/framing regresses.
- Tests fail when stale placeholder/debug objects become visible.
- The project no longer relies on tile asset checks alone for visible gameplay completion.

## Goal 5 - Build LDtk Import Validator

### Objective
Build an LDtk import validator that explains why Unity rendering differs from LDtk before visual mismatches reach PlayMode.

### Scope
- Validate LDtk tileset path resolution.
- Validate generated Tile asset sprite rects against original texture pixel coordinates.
- Validate texture import settings:
  - Pixels Per Unit `32`
  - Point filtering
  - no mipmaps
  - uncompressed or otherwise lossless pixel import for pixel art
- Detect duplicate or stale generated tile assets.
- Validate LDtk layer order against Unity Tilemap sorting order.
- Cover both Town and Interior LDtk imports.

### Required Verification
- Add EditMode validator tests.
- Validate generated Town tiles.
- Validate generated Interior tiles.
- Ensure failures clearly identify the broken category:
  - `rect`
  - `PPU`
  - `path`
  - `sorting`
  - `stale asset`
- Run and pass the existing Town/Interior LDtk tile rect tests.
- Run and pass relevant Town/Interior PlayMode door visual tests.

### Completion Criteria
- Broken LDtk imports produce clear, actionable validator failures.
- New LDtk files can be checked before relying on visual PlayMode debugging.
- Town and Interior generated tiles are guarded against rect/path/PPU/sorting/stale-asset regressions.

## Recommended Order

1. Goal 1 - Hospital Missing Script Warning Cleanup
2. Goal 2 - Town Return Screen Visual Cleanup
3. Goal 4 - Strengthen Interior/Town Visual Regression Tests
4. Goal 5 - Build LDtk Import Validator
