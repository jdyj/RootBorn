# House Upgrade Next Slice Design

## Purpose

This document specifies the next implementation slice after the current House upgrade foundation.

The current code already supports persisted House stages, currency spending, upgrade stage definitions, blueprint validation, route completion services, and saved-stage-driven House generation. The next slice should turn that foundation into a player-facing feature: the player can choose a House expansion route, complete it, and see the expanded House through normal play.

## Current Implemented Baseline

Committed baseline:

- `HouseCurrencyWallet` handles House currency grants and atomic spends.
- `HouseStatePersistence` saves and loads House upgrade state per save slot.
- `HouseUpgradeStageDefinition` defines stage index, costs, profile, tile set, blueprint, route conditions, and route effects.
- `HouseConstructionBlueprintDefinition` defines constrained direct-construction cells.
- `HouseConstructionSession` validates required placed cells against a blueprint.
- `HouseUpgradeService` applies hire and direct completion routes with stage-order, eligibility, blueprint, effect, and payment checks.
- `InteriorTilemapApplier` reads saved `HouseStateSaveData.CurrentStageIndex` and generates a larger House for stage 1+.
- Automated tests cover service transactions, blueprint/session validation, and saved stage generation.

Important boundary:

- The underlying systems exist, but there is not yet a complete player-facing House upgrade UI and direct construction flow.

## Target Player Experience

The first shippable slice should support this flow:

1. Player opens a House upgrade offer from a provider.
2. UI shows the next House expansion stage and available routes.
3. General players can choose `Hire Construction`.
4. Interior-eligible players can choose either `Hire Construction` or `Direct Construction`.
5. Hire route pays immediately, saves stage 1, and the next House entry shows the larger room.
6. Direct route enters a construction mode.
7. Player places required construction tiles in the marked area.
8. Completion deducts the direct-route cost, saves stage 1, clears construction progress, applies direct-route effects, and reloads or regenerates the expanded House.

The feature remains optional. It is a player investment choice, not a mandatory core progression reward.

## Route Rules

### Hire Construction

Available to all players who satisfy the stage's general conditions.

Behavior:

- Shows full cost.
- Uses `HouseUpgradeService.TryHire`.
- Deducts cost only if all validation passes.
- Saves the new stage.
- Clears active direct-construction state.
- Applies hire effects.
- Shows the expanded room on next House load.

### Direct Construction

Available only when the stage has a blueprint and direct conditions pass.

Behavior:

- Shows reduced cost or direct construction benefit.
- Starts construction without raising the stage.
- Persists active construction stage and placed cells.
- Lets the player place only blueprint-allowed construction cells.
- Completion uses `HouseUpgradeService.TryCompleteDirect`.
- Deducts cost only at completion.
- Saves the new stage and clears active construction state.
- Applies direct effects.

Direct construction is not a free-form room editor in this slice. It is a constrained construction task where the player fills required cells.

## UI Specification

### House Upgrade Panel

Create a local panel owned by the House upgrade provider flow.

Required controls:

- Stage summary text.
- Current balance text.
- Hire button.
- Direct construction button.
- Close button.
- Disabled/locked state labels for unavailable routes.

Visibility rules:

- Hire button visible when a next stage exists.
- Hire button disabled when general conditions fail or currency is insufficient.
- Direct button visible only when direct route is eligible.
- Direct button disabled when direct route is eligible but cost/material preflight fails.

The panel must not become a persistent quest or goal HUD. If later tied to objectives, Objective Journal owns that.

### Construction Overlay

House direct construction mode needs a House-only overlay.

Required elements:

- Valid required cells displayed with a visible marker.
- Placed valid cells displayed as complete.
- Invalid attempted cell feedback.
- Required cell progress count.
- Complete button, disabled until blueprint is complete.
- Cancel/exit button that preserves progress.

The overlay should reuse the existing placement camera behavior:

- Mouse wheel zoom.
- `WASD`/arrow key pan.
- `F` full view.

Optional but recommended in the same or following slice:

- Right-click or middle-click drag pan for camera movement.

## Data Asset Specification

Create first-slice assets under `Assets/Data/Housing/`:

- `UpgradeStages/HouseStage_ExpandedRoom_01.asset`
- `Blueprints/HouseBlueprint_ExpandedRoom_01.asset`
- `Conditions/HouseCondition_InteriorEligible_Test.asset`
- `Effects/HouseEffect_DirectConstructionReward.asset`

Stage defaults:

- Stage index: `1`
- Hire cost: `300`
- Direct cost: `120`
- Profile: expanded House profile
- Blueprint: first expansion blueprint
- Direct condition: interior-eligible condition strategy
- Direct effect: data-driven reward effect

Blueprint defaults:

- Bounds: constrained construction area near the expansion edge.
- Required floor cells: at least one.
- Required wall cells: at least one.
- Required door cell: at least one.

These values are first-slice tuning defaults and can be adjusted after visual verification.

## Save and Persistence

The save source of truth remains `HouseStateSaveData`.

During direct construction:

- `ActiveConstructionStageId` stores the active stage.
- `PlacedConstructionCells` stores construction progress.
- `CurrentStageIndex` remains unchanged until completion.

On completion:

- `CurrentStageIndex` becomes the completed stage index.
- `ActiveConstructionStageId` is cleared.
- `PlacedConstructionCells` is cleared.
- `LatestRoute` records the chosen route.

Repeat completion after the stage is already applied must not double charge or double reward.

## Implementation Order

1. Add the House upgrade panel and provider installer.
2. Add PlayMode tests for route visibility:
   - non-interior player sees hire route only
   - interior-eligible player sees hire and direct routes
3. Add first-slice data assets.
4. Wire the panel to `HouseUpgradeService.TryHire`.
5. Add PlayMode test for hire route expanding House after save/load.
6. Add construction overlay and session persistence.
7. Wire direct route start, placement, progress, and completion.
8. Add PlayMode test for direct construction placing required cells and persisting stage.
9. Run direct visual verification in Game View.
10. Record verification notes and screenshot paths.

## Testing Requirements

EditMode:

- Data assets exist and are valid.
- Stage definition has positive stage index and valid costs.
- Blueprint contains required cells in bounds.
- Service rejects invalid or skipped stages.
- Service rejects incomplete direct sessions.
- Service applies effects only after payment succeeds.

PlayMode:

- Upgrade panel appears through the player-facing provider flow.
- Route buttons reflect eligibility.
- Hire route changes saved stage and reloads into larger House.
- Direct route enters construction mode.
- Player-facing placement fills blueprint cells.
- Completion saves stage and clears active construction progress.
- Reloading House shows the expanded room.

Visual verification:

- Capture Game View after direct construction completion.
- Capture Game View after reloading expanded House.
- Inspect runtime tilemaps:
  - ground/wall/door/collision tile counts
  - selected/placed construction cell positions
  - overlay valid/complete state
  - no stale sample/debug tilemaps visible
- Inspect saved `house-state.json` and confirm `CurrentStageIndex = 1`.

## Out of Scope

The next slice does not include:

- Multi-stage House expansion beyond stage 1.
- Full free-form wall/floor editor.
- Refund/cancel after pay-on-start, because this slice uses pay-on-completion for direct construction.
- Network synchronization of House construction state.
- Advanced material inventory costs unless a suitable resource pipeline is already available.

## Open Design Defaults

Use these defaults unless later changed:

- Upgrade provider starts in Town, not inside House.
- Hire completion applies on next House entry.
- Direct construction happens in House.
- Direct construction is specific to interior-eligible data conditions.
- Camera controls keep the current wheel zoom and keyboard pan, with mouse drag pan as a recommended quality improvement.

