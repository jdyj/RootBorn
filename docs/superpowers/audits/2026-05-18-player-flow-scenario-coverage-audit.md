# Player Flow Scenario Coverage Audit

Date: 2026-05-18

## Scope

This audit maps player-facing scenario coverage for the `B -> C -> A` implementation order:

- `B`: Town Core Player Flow
- `C`: House/Interiors Player Flow
- `A`: Scenario Coverage Audit

## Repository Counts

- `Assets/Scripts` C# files: 527
- `Assets/Tests/EditMode` C# tests: 209
- `Assets/Tests/PlayMode` C# tests: 90
- Largest runtime domains: `StudentLife`, `Quests`, `DiscoveryClues`, `WorldState`, `Housing`, `Interiors`

## Town Core Player Flow

Scenario IDs:

- `TOWN-FLOW-001`: SaveSlot New Game, character confirmation, Town entry, player-controlled movement and interaction.
- `TOWN-FLOW-002`: Objective Journal and DayResult reflect at least two changed gameplay domains.
- `TOWN-FLOW-003`: Save/load and duplicate interaction checks preserve state without double progress or double reward.

Mapped PlayMode test:

- `Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs`

Direct Visual Play Verification Gate:

- Actual SaveSlot UI click: covered.
- Actual keyboard movement: covered.
- Actual prompt/input path: covered.
- Actual UI button click: covered.
- Runtime state inspection: covered.
- Save/load state inspection: covered.
- Duplicate-action prevention: covered.
- Game View screenshot evidence:
  - `production/qa/evidence/town-core-flow-objective-journal.png`
  - `production/qa/evidence/town-core-flow-day-result.png`

## House/Interiors Player Flow

Scenario IDs:

- `HOUSE-FLOW-001`: New Game to House entry, palette selection, player-facing mouse placement.
- `HOUSE-FLOW-002`: Runtime Tilemap, occupancy, overlay, selected tile name, and stale sample/debug tilemap checks.
- `HOUSE-FLOW-003`: Save/load restores visual furniture state and occupancy without duplicates.

Mapped PlayMode test:

- `Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs`

Direct Visual Play Verification Gate:

- Actual SaveSlot UI click: covered.
- Actual House scene entry: covered.
- Actual palette UI click: covered.
- Actual world mouse placement: covered.
- Runtime Tilemap state inspection: covered.
- Runtime overlay state inspection: covered.
- Save/load state inspection: covered.
- Stale sample/debug tilemap inspection: covered.
- Game View or Camera screenshot evidence:
  - `production/qa/evidence/house-interior-placement-before-reload.png`
  - `production/qa/evidence/house-interior-placement-after-reload.png`
- Runtime probe evidence:
  - `production/qa/evidence/house-interior-placement-before-reload-probe.txt`
  - `production/qa/evidence/house-interior-placement-after-reload-probe.txt`

## Fixture and Source-Audit Tests

These tests are useful but are not sufficient by themselves for user-facing completion:

- EditMode source audit tests that read `.cs` files.
- Registry asset tests that verify ScriptableObject wiring.
- UI surface tests that instantiate panels without SaveSlot/Town/House entry.
- DirectValidation source tests that inspect autoplay code.

They may support Phase B and Phase C, but completion requires the PlayMode paths listed above.

## ScenarioId Mapping

- `TOWN-FLOW-001` -> `IntegratedVerticalSliceFoundationE2ETests`
- `TOWN-FLOW-002` -> `IntegratedVerticalSliceFoundationE2ETests`
- `TOWN-FLOW-003` -> `IntegratedVerticalSliceFoundationE2ETests`
- `HOUSE-FLOW-001` -> `HousePlacementSaveLoadE2EScenarioTests`
- `HOUSE-FLOW-002` -> `HousePlacementSaveLoadE2EScenarioTests`
- `HOUSE-FLOW-003` -> `HousePlacementSaveLoadE2EScenarioTests`
- `COVERAGE-001` -> `ScenarioCoverageMappingTests`
- `COVERAGE-002` -> `ScenarioCoverageMappingTests`
- `COVERAGE-003` -> `ScenarioCoverageMappingTests`

## Remaining Gaps

- Host/client multiplayer validation remains outside this slice.
- Free-form House wall/floor editing beyond the covered furniture placement path remains outside this slice.
- Existing historical tests that use internal setup remain useful but should not be reported as direct visual verification unless paired with a player-flow PlayMode path.

## Verification Results

- EditMode `ScenarioCoverageMappingTests`: PASS, 5 passed, 0 failed, duration `00:00:01.0821011`.
- PlayMode `IntegratedVerticalSliceFoundationE2ETests.TOWN_FLOW_001_003_NewGameTownObjectiveJournalDayResultReloadAndDedupe`: PASS, 1 passed, 0 failed, duration `00:00:17.9917226`.
- PlayMode `HousePlacementSaveLoadE2EScenarioTests.HOUSE_FLOW_001_003_NewGamePlaceSaveExitLoadRestoresHouseFurnitureWithVisualEvidence`: PASS, 1 passed, 0 failed, duration `00:00:20.0391699`.
- Entity branching gate `Scripts/ci/check-no-entity-id-branching.sh`: PASS, `OK: no entity-id branching in system code.`
- Evidence files refreshed:
  - `production/qa/evidence/town-core-flow-objective-journal.png` length `39525`
  - `production/qa/evidence/town-core-flow-day-result.png` length `60090`
  - `production/qa/evidence/house-interior-placement-before-reload.png` length `111507`
  - `production/qa/evidence/house-interior-placement-before-reload-probe.txt` length `251`
  - `production/qa/evidence/house-interior-placement-after-reload.png` length `113739`
  - `production/qa/evidence/house-interior-placement-after-reload-probe.txt` length `251`
