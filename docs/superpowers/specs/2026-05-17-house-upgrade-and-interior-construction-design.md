# House Upgrade and Interior Construction Design

## Purpose

House expansion is an optional player-choice feature. It is not a mandatory core progression reward, but it should visibly increase the player's living space when the player chooses to invest money or pursue an interior-focused route.

The system supports two routes that converge on the same stable upgrade stages:

- Hire construction: any player can pay the full cost and receive an automatic room expansion.
- Direct construction: only players who satisfy interior-related data conditions can manually complete a tile construction flow for the expansion.

Direct construction is intentionally gated to the interior route. General players should not see it as a normal option.

## Current Context

`Assets/Scenes/House.unity` exists and contains House tilemap layers such as `HouseGroundTilemap`, `HouseWallTilemap`, `HouseDecorationTilemap`, and `HouseCollisionTilemap`.

The current House scene is driven by `InteriorTilemapApplier`, which generates a runtime interior from an `InteriorGenerationProfile` and applies it to the House tilemaps. Existing systems already include:

- `InteriorGenerationProfile` for size and placement rules.
- `InteriorTileSetDefinition` for floor, wall, door, window, furniture, and collision tiles.
- House PlayMode tests for generated tilemap layers.
- Interior furniture placement UI and preview overlay.
- Career practice data for an interior path, including space layout and budget sense signals.

This makes a data-driven stage system preferable to maintaining multiple duplicate House scenes.

## Player Routes

### Hire Construction

The player chooses the hire route from a House upgrade NPC, board, or equivalent interactable.

The route:

1. Shows the next upgrade stage, full cost, resulting room size, and short summary.
2. Verifies that the player can pay.
3. Atomically deducts the cost and records the new House upgrade stage.
4. Applies the new stage on the next House load, or immediately if the player is already in House and the UX calls for it.

This route is available to all players who satisfy general stage prerequisites.

### Direct Construction

The player chooses the direct construction route only when data-driven interior route conditions pass.

The route:

1. Shows the next upgrade stage, reduced cost or material requirements, and direct construction rewards.
2. Verifies cost, materials, and the interior-route eligibility condition.
3. Enters a House construction mode with a blueprint overlay.
4. Lets the player place required floor, wall, and door tiles only within the active construction area.
5. Validates the blueprint after every placement.
6. Enables completion when required cells, connectivity, spawn access, and collision rules pass.
7. Atomically records the upgraded House stage and applies direct construction rewards.

Direct construction must feel like the player completed the room with tiles, but it must not allow arbitrary walls or doors that can break navigation, collision, spawn, camera framing, or future furniture placement.

## Data Model

### `HouseUpgradeStageDefinition`

ScriptableObject path:

`Assets/Data/Housing/UpgradeStages/`

Fields:

- stable id
- display key
- stage index
- full hire cost
- direct construction cost or material requirements
- target `InteriorGenerationProfile`
- target `InteriorTileSetDefinition`
- optional `HouseConstructionBlueprintDefinition`
- general prerequisite condition strategies
- direct construction eligibility condition strategies
- hire completion effect strategies
- direct completion effect strategies

The stage index is the ordering key. Gameplay code must not branch on a specific stage id.

### `HouseConstructionBlueprintDefinition`

ScriptableObject path:

`Assets/Data/Housing/Blueprints/`

Fields:

- stable id
- target stage reference
- construction bounds
- required floor cells
- required wall cells
- required door cells
- optional decoration cells
- allowed tile palette reference
- completion validation policy
- preview/overlay labels

The blueprint describes what the direct route requires. It should represent a constrained construction area, not a full free-form map editor.

### `HouseStateSaveData`

Saved per save slot and player scope according to the existing save architecture.

Fields:

- current upgrade stage index
- active construction stage id, if a direct route is in progress
- placed construction cells for the active blueprint
- chosen route for the latest upgrade
- completion history ids for idempotency

The saved stage is the source of truth for which profile the House applies. Runtime-generated tilemaps are not the source of truth.

### Route Conditions and Effects

Direct construction eligibility must use ScriptableObject condition strategies, not C# id checks.

Examples:

- career candidate condition
- skill threshold condition
- trait threshold condition
- completed practice condition
- quest or world-state condition

Effects must also be ScriptableObject strategies:

- skill delta
- trait delta
- career interest delta
- material spend
- currency spend
- customization unlock
- world-state flag

This preserves the repository's data-driven entity rule and avoids `if (careerId == "interior")` style branching.

## Runtime Flow

### Upgrade Offer Flow

1. Player interacts with the House upgrade provider.
2. UI resolves the next `HouseUpgradeStageDefinition`.
3. UI shows available routes:
   - Hire construction if general prerequisites pass.
   - Direct construction only if direct eligibility conditions pass.
4. Player selects one route.
5. Service preflights all costs and prerequisites.
6. Service applies the selected route atomically.

### Hire Completion Flow

1. Deduct full cost.
2. Save new House stage.
3. Clear any active construction state for that stage.
4. Apply hire effects.
5. Refresh House if already loaded, or apply on next House load.

### Direct Construction Flow

1. Preflight direct-route cost and required materials without deducting them.
2. Save active construction state.
3. Open House construction mode.
4. Render blueprint overlays on valid cells.
5. Player places required tiles through the same player-facing input style used by placement systems.
6. The system validates:
   - all required floor cells are present
   - all required wall cells are present
   - required door cells are present
   - spawn can reach at least one door
   - collision tiles match blocked cells
   - no stale sample/debug tilemaps or placeholder objects are visible
7. On completion, deduct the direct-route cost, save the new House stage, clear active construction state, and apply direct-route effects.

## Transaction Rules

House upgrade payment and completion must be atomic.

If any validation fails, the system must not partially deduct money, consume materials, raise the stage, grant rewards, or mutate saved construction completion history.

Direct construction uses pay-on-completion for the first implementation:

- Pay-on-completion: resources are checked at start and deducted only when completion succeeds.

Pay-on-start can be added later only if cancel/refund behavior is specified in a separate design update.

The operation must be idempotent after save/load. Repeating completion after the stage is already recorded must not double charge or double grant rewards.

## UI Ownership

The upgrade offer UI can be a local interaction panel owned by the upgrade provider flow.

This feature must not create a persistent goal or quest HUD. If upgrade guidance becomes a tracked objective, it belongs in `ObjectiveJournalPanel`, with only one tracked objective allowed in `TrackedObjectiveHud`.

Construction mode may use a House-only placement toolbar and overlay, but it should reuse existing interior placement conventions where practical.

## House Generation

`InteriorTilemapApplier` should resolve the current saved House stage before generating the interior. The stage selects the target `InteriorGenerationProfile` and tile set.

The default fallback remains stage 0, representing the starting one-room or compact House.

Direct construction completion should not rely on runtime tilemap contents alone as permanent truth. Completion updates `HouseStateSaveData`; later House loads rebuild from the saved stage and profile.

If player-placed construction cells are meant to remain visually unique after completion, they must be serialized as customization data and reapplied after the base stage generation.

## Testing and Verification

Automated tests are required but not sufficient for visible House work.

EditMode coverage:

- stage definitions validate ordering and required references
- blueprint definitions validate bounds and required cells
- route eligibility uses condition strategies, not hardcoded entity ids
- transaction service rejects insufficient funds/materials without state mutation
- direct completion is idempotent
- saved stage selects the expected generation profile

PlayMode coverage:

- non-interior player sees hire route only
- interior-eligible player sees hire and direct construction routes
- hire route pays and expands House through the normal interaction flow
- direct route enters House construction mode through the normal interaction flow
- player places required tiles via input/UI path and completes construction
- save/load preserves the upgraded stage and does not double charge

Direct visual verification gate:

- Use the actual player-facing path: interact, select route, pay or construct, place tiles, complete, enter or reload House.
- Capture or inspect Game View or Camera screenshot after visible changes.
- Inspect runtime Tilemap/Renderer/UI state, including selected tile names, cell positions, overlay state, and absence of stale sample/debug tilemaps or placeholder objects.
- Separately verify saved state after completion. Runtime-generated tilemap checks alone are not enough.
- Reopen or reload the House scene/save and verify that the visible expanded room matches the saved stage.

## Performance Considerations

Blueprint validation should be bounded to the active construction cells and nearby connectivity graph, not scan unrelated scene objects every frame.

Construction overlay updates should be dirty-driven after placement, route selection, or active tile changes. Avoid `Update()` polling and repeated full-tilemap scans.

House generation should continue to clear and rebuild tilemap layers in a single controlled pass per load or stage change. Large future stages should consider cached blueprint cell sets and compressed tilemap bounds.

## Recommended First Slice

The first implementation should ship one upgrade from stage 0 to stage 1:

- stage 0: compact House
- stage 1: larger House
- hire route: full cost, automatic expansion
- direct route: interior-eligible only, constrained blueprint, pay-on-completion
- direct rewards: space layout and budget sense progress, plus optional customization unlock

This slice proves the architecture without committing to a full free-form room editor.

## Implementation Defaults

- Currency model: verify whether a dedicated wallet exists before implementation. If it does not, introduce a data-driven currency state instead of modeling money as an ordinary item stack.
- Upgrade provider: use a Town-side upgrade provider first, because it lets the player intentionally purchase the home change outside the House scene.
- Hire completion timing: apply the stage on the next House entry for the first slice. This keeps the visible transition simple and avoids mid-frame tilemap rebuild UX issues.
- First blueprint: author one stage 0 to stage 1 blueprint after inspecting the current House art direction and tile palette. The blueprint must remain constrained to required construction cells.
