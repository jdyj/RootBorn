# House Upgrade Next Slice Verification

## Automated Tests

- Housing EditMode: PASS
  - Command: `unity-mcp-cli.cmd run-tool tests-run` with `testNamespace=Rootborn.Tests.EditMode.Housing`
  - Summary: 17 passed, 0 failed.
- House upgrade PlayMode: PASS
  - Command: `unity-mcp-cli.cmd run-tool tests-run` with `testNamespace=Rootborn.Tests.PlayMode.Housing`, `testClass=HouseUpgradeFlowPlayModeTests`
  - Summary after mouse-placement closure: 7 passed, 0 failed.
- Camera PlayMode: PASS by method-level runs
  - `HouseScene_InstallsPlacementCameraControllerAndFramesAllTiles`: PASS
  - `HousePlacementCamera_KeyboardZoomAndPanMoveCameraThroughPlayerInput`: PASS
  - `HousePlacementCamera_RightMouseDragPansCameraThroughPlayerInput`: PASS
  - Note: full class run hit the MCP `tests-run` 60 second call limit once, so the methods were verified individually.
- Entity-id branching gate: PASS
  - Output: `OK: no entity-id branching in system code.`

## Direct PlayMode Flow

- Town panel opened through runtime installer: PASS by `HOUSE_UPGRADE_PM_002` and `HOUSE_UPGRADE_PM_003`.
- Non-interior route visibility: PASS by `HOUSE_UPGRADE_PM_002`.
- Direct-eligible route visibility: PASS by `HOUSE_UPGRADE_PM_003`.
- Hire route player-facing button click saves stage and reloads expanded House: PASS by `HOUSE_UPGRADE_PM_004`.
- Direct construction overlay started in House: PASS by `HOUSE_UPGRADE_PM_005`.
- Required construction cells validated and progress displayed: PASS by `HOUSE_UPGRADE_PM_005`.
- Direct completion saved stage 1 and cleared progress: PASS by `HOUSE_UPGRADE_PM_006`.
- Direct construction required cell placed through actual mouse input: PASS by `HOUSE_UPGRADE_PM_007`.
- Reloaded House shows expanded layout: PASS by `HOUSE_UPGRADE_PM_001` and runtime probe after reload.

## Visual Evidence

- Direct construction completion camera capture: `Builds/Logs/house-upgrade-next-slice/direct-construction-complete.png`
- Reloaded expanded House camera capture: `Builds/Logs/house-upgrade-next-slice/reloaded-expanded-house.png`
- Game View screenshot was captured through `screenshot-game-view` at 1013x585 and inspected after the reloaded expanded House flow.

## Runtime State Probe

- Direct completion probe:
  - `floor=380 walls=127 doors=-1 collision=130 stage=1 route=DirectConstruction active= placed=0 screenshot=direct-construction-complete.png`
- Tilemap name probe:
  - `HouseDecorationTilemap=14, HouseGroundTilemap=380, HouseCollisionTilemap=130, HouseWallTilemap=127`
- Reloaded expanded House probe:
  - `floor=380 walls=127 decor=14 collision=130 stage=1 route=DirectConstruction active= placed=0 screenshot=reloaded-expanded-house.png`

## Residual Risk

- Direct construction now has a PlayMode mouse input proof for required-cell placement. The visual screenshot setup still used an overlay helper to fill all required cells before completion, so the screenshot evidence and the mouse-input proof are separate artifacts rather than one continuous captured run.
- The generated House runtime currently has ground, wall, decoration, and collision Tilemaps. There is no `HouseDoorTilemap` object in the inspected runtime scene, so door count was recorded as `doors=-1` and the existing decoration layer count was recorded separately.
