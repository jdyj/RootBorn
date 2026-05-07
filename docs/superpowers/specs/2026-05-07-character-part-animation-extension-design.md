# Character Part Animation Extension Design

## Objective

Extend the character part composition foundation so body, eyes, hair, outfit, and accessory layers can be driven by one shared animation frame coordinate instead of staying locked to each definition's static preview cell.

## Current Baseline

Character appearance already stores stable part definition ids and `CharacterPartComposer` already creates deterministic child `SpriteRenderer` layers per category. Each `CharacterPartDefinition` points at a sliced 16x16 sheet and a preview sub-sprite such as `Body_2_r0_c0`.

## First Extension

The first animation extension keeps the save format unchanged and adds a grid-frame API to the composer:

- `BuildFrameSubSpriteName(definition, row, column)` derives the selected sheet base from the definition preview cell and builds `<base>_r<row>_c<column>`.
- `ApplyAnimationFrame(appearance, definitions, row, column, resolver)` resolves the selected/default definition per category and asks the resolver for the same row/column sub-sprite across every part layer.
- If a requested frame is absent, the composer falls back to the definition preview cell instead of clearing the renderer.

This keeps part identity data-driven while allowing future movement/idle/tool animation drivers to choose pose coordinates from the asset guide.

## Runtime Integration

`FarmAutoFiller` now uses `ApplyAnimationFrame(..., row: 0, column: 0, ...)` for initial player rendering. `ResourceManager.LoadSubSpriteAsync` still preloads the sheet through the definition's address, and cached lookup receives the frame sub-sprite name selected by the composer.

The runtime playback layer is `CharacterPartAnimator`. It is configured with the selected `CharacterAppearance`, registered part definitions, and a sub-sprite resolver. `PlayerController` pushes movement input and facing into this animator every frame, then ticks it with `Time.deltaTime`. The current row convention is:

- row `0`: down-facing frames;
- row `1`: side-facing frames, mirrored by the existing shared `flipX` path;
- row `2`: up-facing frames.

Idle always uses column `0`. Walk frames advance by elapsed time at `FramesPerSecond` and wrap by `WalkFrameCount`, while all part layers use the same row and column.

Tool/action animation is data-driven through `CharacterPartAnimationClipDefinition` ScriptableObjects registered in `GameDataRegistry`. Each clip stores a stable id, row, frame count, playback FPS, and loop flag. `ToolDefinition` can reference one clip through `CharacterPartAnimationClip`, and `PlayerController` plays that clip through `CharacterPartAnimator.PlayClip` when the equipped tool starts an attack. This keeps tool action animation wiring on SO data instead of tool-id or action-id branches.

`ANIMATIONS_GUIDE_CHARACTER.png` is `1040x352`, which is `65 x 22` cells at 16px. Runtime Modern Farm generator part sheets are `896x352`, which is `56 x 22` cells. The shared row index is therefore valid across the guide and runtime sheets, while clip frame counts must stay at or below the runtime sheet's 56 columns.

First registered action clips calibrated from the guide:

- `character.action.harvesting.down`: row `2`, `36` frames
- `character.action.digging.down`: row `3`, `36` frames
- `character.action.watering.down`: row `4`, `56` frames
- `character.action.chopping.side`: row `5`, `40` frames
- `character.action.fishing.side`: row `6`, `40` frames
- `character.action.fishing.throw_hook.side`: row `6`, `40` frames
- `character.action.fishing.idle.side`: row `7`, `24` frames, looping
- `character.action.fishing.pull_hook.side`: row `8`, `8` frames
- `character.action.fishing.caught.side`: row `9`, `40` frames

First wired tools:

- `Tool_WateringCan` -> `character.action.watering.down`
- `Tool_StoneAxe` -> `character.action.chopping.side`
- `Tool_StoneHoe` -> `character.action.digging.down`

## Test Coverage

EditMode tests cover:

- frame sub-sprite name generation from names with underscores;
- same row/column application across multiple selected layers;
- fallback to the definition preview cell when the requested animation frame is missing;
- `CharacterPartAnimator` idle/walk frame progression and timer reset on direction changes;
- data-driven non-looping action clip playback and return to motion state;
- registry and `DataManager` lookup for character part animation clip definitions;
- tool asset wiring to registered action clips;
- guide row/frame calibration and runtime sheet bounds checks;
- Farm initialization configuring `CharacterPartAnimator` with the cached frame resolver;
- `PlayerController` pushing motion state into the part animator.

## Deferred Work

This pass implements generic idle/walk playback, first data-driven action clip plumbing, guide row/frame calibration for down/side examples, and separate fishing sub-action clip assets for throw, idle, pull, and caught. Remaining polish is connecting those fishing sub-actions to a future fishing gameplay state machine and visually tuning directional variants.
