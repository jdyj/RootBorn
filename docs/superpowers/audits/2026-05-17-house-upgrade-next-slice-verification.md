# House Upgrade Next Slice Verification

## Automated Tests

- Housing EditMode: PASS
  - Command: `unity-mcp-cli.cmd run-tool tests-run` with `testNamespace=Rootborn.Tests.EditMode.Housing`
  - Summary: 17 passed, 0 failed.
- House upgrade PlayMode: PASS
  - Command: `unity-mcp-cli.cmd run-tool tests-run` with `testNamespace=Rootborn.Tests.PlayMode.Housing`, `testClass=HouseUpgradeFlowPlayModeTests`
  - Summary after direct-start/restore closure: 9 method-level runs passed, 0 failed.
  - Note: full class runs hit the MCP `tests-run` 60 second call limit, so methods were verified individually.
- House interior generation PlayMode: PASS by method-level runs
  - `HouseScene_GeneratesOfficeInteriorLayersFromNewPalette`: PASS
  - `HouseScene_UsesTileR02C09ForVerticalWallAccentsAndDoorFrames`: PASS
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
- Direct construction required cells placed and completed through actual mouse input plus complete button click: PASS by `HOUSE_UPGRADE_PM_007`.
- Direct button starts saved construction and loads House overlay: PASS by `HOUSE_UPGRADE_PM_008`.
- Active construction restores placed cells and cancel preserves progress: PASS by `HOUSE_UPGRADE_PM_009`.
- Reloaded House shows expanded layout: PASS by `HOUSE_UPGRADE_PM_001` and runtime probe after reload.

## Visual Evidence

- Direct construction completion camera capture: `Builds/Logs/house-upgrade-next-slice/direct-construction-complete.png`
- Reloaded expanded House camera capture: `Builds/Logs/house-upgrade-next-slice/reloaded-expanded-house.png`
- Reloaded expanded House with dedicated door layer camera capture: `Builds/Logs/house-upgrade-next-slice/reloaded-expanded-house-door-layer.png`
- Game View screenshot was captured through `screenshot-game-view` at 1013x585 and inspected after the reloaded expanded House flow.

## Runtime State Probe

- Direct completion probe:
  - `floor=380 walls=127 doors=-1 collision=130 stage=1 route=DirectConstruction active= placed=0 screenshot=direct-construction-complete.png`
- Tilemap name probe:
  - `HouseDecorationTilemap=14, HouseGroundTilemap=380, HouseCollisionTilemap=130, HouseWallTilemap=127`
- Reloaded expanded House probe:
  - `floor=380 walls=127 decor=14 collision=130 stage=1 route=DirectConstruction active= placed=0 screenshot=reloaded-expanded-house.png`
- Reloaded expanded House door-layer probe:
  - `scene=House floor=594 walls=154 doors=3 decor=19 collision=163 stage=1 active= placed=0 route=DirectConstruction tilemaps=HouseCollisionTilemap,HouseDecorationTilemap,HouseDoorTilemap,HouseGroundTilemap,HouseWallTilemap`

## Residual Risk

- Direct construction now has PlayMode proof for the direct button route, saved active construction restore, cancel preserving progress, mouse placement of all required cells, and completion through the visible complete button.
- `HouseDoorTilemap` is now generated at runtime and verified by PlayMode tests plus the fresh runtime probe. The saved `Assets/Scenes/House.unity` still contains the authored base ground/wall/decoration/collision layers; the door layer is created by the runtime applier during House generation.
