# PlayMode Multiplayer Simulation Gate Audit

Goal prompt: `docs/superpowers/goals/2026-05-18-playmode-multiplayer-simulation-gate-goal.md`

## Deliverable Checklist

| Requirement | Evidence | Status |
| --- | --- | --- |
| Inventory existing PlayMode/E2E tests and separate committed vs dirty/untracked tests | Reviewed `Assets/Tests/PlayMode` via `git ls-files`, `git status --short`, and targeted source reads. Relevant untracked test selected for this gate: `CareerCandidateDeepeningLoopE2EScenarioTests.cs`. | Complete |
| Town entry uses player-facing Boot/MainMenu pointer path, not direct Town load | `MainMenuPointerInputE2ETests.BOOT_E2E_003_MainMenuSinglePlayButtonRespondsToRealPointerClick` failed first, then passed after the mouse frame driver fix. Result: 1/1 passed. | Complete |
| Game View or runtime visual evidence exists for visible flow | `MainMenuPointerInputE2ETests` writes `production/qa/evidence/main-menu-pointer-save-slot.png`; current file exists at 72,496 bytes. Interior tests inspect runtime tilemap/overlay/renderer state directly. | Complete |
| Objective Journal opens through actual input and owns objective UI | `ObjectiveJournalInputPlayModeTests.OBJECTIVE_JOURNAL_PM_001_TabKeyboardInputOpensObjectiveJournalAndClosesInventory`: passed. Verifies Tab input opens `ObjectiveJournalPanel`, closes inventory, and binds `TrackedObjectiveHud`. | Complete |
| At least two non-school growth routes through player-facing PlayMode flow | `OutsideSchoolGrowthFoundationE2EScenarioTests.OUTSIDE_FOUNDATION_E2E_001_007...`: passed. `SelfStudyLibraryGrowthE2EScenarioTests.STUDY_E2E_001_006...`: passed. Both now click `NewGameButton` then SPUM `ConfirmButton`, enter Town, move with keyboard, interact, show result UI, save/load, and dedupe. | Complete |
| Career candidate/hint flow loads and progresses through player-facing route | `CareerCandidateDeepeningLoopE2EScenarioTests.CAREER_CANDIDATE_E2E_001_010...`: passed. It enters Town through save slot + SPUM confirm, moves to library, uses location UI, applies candidate hint, opens career UI, verifies save/load and duplicate suppression. | Complete |
| House/Interior placement uses real UI/pointer flow and verifies tilemap/overlay state | `FurniturePlacementMouseFlowPlayModeTests`: 3/3 passed. Covers palette mouse click selection, world mouse placement through overlay update, occupancy tile write, overlay clear, stale sample tilemap absence, and repeated placement cleanup. | Complete |
| Multiplayer host/client or simulated multiplayer gate exists | Added `NetworkSessionHostPlayModeTests.MULTIPLAYER_PM_001_HostSessionStartsOwnedLocalPlayerAndStopsWithoutStaleNetworkObjects`: failed first on missing transport, then passed. It starts `HostSession`, verifies server/client/listening state, local connection callback, NGO spawned player ownership, `NetworkPlayerIdentityBinder` identity, and no stale spawned network objects after stop. | Complete |
| Multiplayer player state isolation remains covered | `PlayerGlobalStateMultiplayerPlayModeTests`: 2/2 passed. Covers separate inventories across object recreation and separate quest progress in the same save slot. | Complete |
| Shared network session regression remains covered | `NetworkSessionTests`: 3/3 passed after `HostSession` change. | Complete |
| Entity ID branching CI passes | `Scripts/ci/check-no-entity-id-branching.sh`: `OK: no entity-id branching in system code.` | Complete |

## Test Results

- PlayMode `MainMenuPointerInputE2ETests`: passed 1/1.
- PlayMode `ObjectiveJournalInputPlayModeTests`: passed 1/1.
- PlayMode `OutsideSchoolGrowthFoundationE2EScenarioTests`: passed 1/1.
- PlayMode `SelfStudyLibraryGrowthE2EScenarioTests`: passed 1/1.
- PlayMode `CareerCandidateDeepeningLoopE2EScenarioTests`: passed 1/1.
- PlayMode `FurniturePlacementMouseFlowPlayModeTests`: passed 3/3.
- PlayMode `PlayerGlobalStateMultiplayerPlayModeTests`: passed 2/2.
- PlayMode `NetworkSessionHostPlayModeTests`: passed 1/1.
- EditMode `NetworkSessionTests`: passed 3/3.
- CI entity branch gate: passed.

## Red To Green Notes

- `MainMenuPointerInputE2ETests` initially failed because the previous input helper did not drive the real UI module reliably. It now uses a frame-based `MouseState` driver consistent with the furniture placement tests.
- Outside-school, self-study, and career candidate E2E tests initially expected `NewGameButton` to enter Town directly. They now reproduce the current player path by clicking SPUM `ConfirmButton` before waiting for Town.
- `NetworkSessionHostPlayModeTests` initially failed because the test-created `NetworkConfig` did not wire `UnityTransport`. After wiring transport, it exposed a scene-management warning. `HostSession` now skips network scene loading when `EnableSceneManagement` is false.

## Remaining Limits

- The multiplayer executable path with a separate dedicated server process plus external client process was not run here. The current evidence is the strongest in-editor host-mode NGO PlayMode simulation plus player-state isolation. A later build-validation goal should launch `rootborn-server.exe` and `rootborn.exe` together.
- The Career Candidate E2E test was selected from existing untracked PlayMode work and staged specifically for this gate because it directly covers the goal's career candidate/hint requirement.
