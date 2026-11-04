# Town2 LDtk Import Guide

This guide documents how `town2.ldtk` was imported and wired into the playable
Town and Beach scenes so a later session can inspect, repair, or repeat the
same workflow.

## Source Files

- Original LDtk project:
  `C:\Users\jdyj\OneDrive\<Korean Desktop folder>\tileset\town2.ldtk`
- Imported Unity location:
  `Assets/LDtk/Town2/town2.ldtk`
- Imported external level files:
  `Assets/LDtk/Town2/town2/Level_0.ldtkl`,
  `Assets/LDtk/Town2/town2/Level_1.ldtkl`
- Generated LDtkUnity tileset definition files:
  `Assets/LDtk/Town2/town2/*.ldtkt`
- Imported tilesets:
  `Assets/LDtk/Town2/Tilesets/`
- Generated composite/binding sprite:
  `Assets/LDtk/Town2/town2_composite.png`
- Generated Tile assets:
  `Assets/LDtk/Town2/Tiles/`
- Scenes using the imported map:
  `Assets/Scenes/Town.unity`
  `Assets/Scenes/Beach.unity`

The imported LDtk project uses 32x32 tile art and external LDtk levels. The
current combined source bounds are `90 x 93` cells (`2880 x 2976` pixels).
The generated operational Tilemaps are saved into scenes, not generated only at
runtime. `Town.unity` stores LDtk `Level_0`; `Beach.unity` stores LDtk
`Level_1`.

## What Was Created

The Town scene contains a root object named `Town2LdtkTilemaps`. Under it are
the saved player-facing Tilemap layers for `Level_0` only:

| Layer object | Expected tiles | Sorting order | Collision |
| --- | ---: | ---: | --- |
| `Town2LdtkGroundTilemap` | 5050 | 0 | No |
| `Town2LdtkGround2Tilemap` | 96 | 1 | No |
| `Town2LdtkBeachTilemap` | 0 | 2 | No |
| `Town2LdtkFenseTilemap` | 96 | 3 | Yes |
| `Town2LdtkPropsTilemap` | 687 | 4 | No |
| `Town2LdtkShopTilemap` | 237 | 5 | Yes |
| `Town2LdtkHospitalTilemap` | 605 | 6 | Yes |
| `Town2LdtkPoliceTilemap` | 141 | 7 | Yes |
| `Town2LdtkBuildingTilemap` | 279 | 8 | Yes |
| `Town2LdtkSchoolTilemap` | 47 | 9 | No |

The Beach scene also contains `Town2LdtkTilemaps`, but only `Level_1` content
is visible there:

| Layer object | Expected tiles | Sorting order | Collision |
| --- | ---: | ---: | --- |
| `Town2LdtkGroundTilemap` | 396 | 0 | No |
| `Town2LdtkBeachTilemap` | 933 | 2 | No |

The scene also keeps `Town2LdtkComposite` with a disabled `SpriteRenderer`.
That object is a source/debug binding, not the player-facing map. It should
reference `town2_composite` and keep `LdtkTownMapRuntimeBinding`.

Legacy pre-LDtk layers remain for compatibility but should be empty:

- `GroundTilemap` tile count: `0`
- `TownDecorationTilemap` tile count: `0`

Do not delete those legacy objects unless every placement/editor reference is
updated. They are kept as compatibility layers.

## Runtime Scripts And Data

The LDtk map support added these runtime/data helpers:

- `Assets/Scripts/Game/World/LdtkTownMapDefinition.cs`
- `Assets/Scripts/Game/World/LdtkTownMapRuntimeBinding.cs`
- `Assets/Data/Locations/Town2LdtkMapDefinition.asset`

The current Town door interaction layer is separate from the LDtk import but
is positioned against this map:

- `Assets/Scripts/Game/World/TownDoorPortalRuntimeInstaller.cs`
- `Assets/Scripts/Game/World/TownAutoDoorPortal.cs`

The door portals are saved under `[TownDoorPortals]` and are also ensured at
runtime by `TownDoorPortalRuntimeInstaller`.

The external `Level_1` entry transition is saved in `Town.unity` as:

```text
TownLevelTransition_Level0ToLevel1
position = (37.5, -62.6, 0)
component = TownLevelTransitionTrigger
destinationScene = Beach
destinationSpawnId = beach-top-entry
```

When the player walks down into this trigger from Level_0, the trigger performs
a short fade, loads `Beach.unity`, and places the player at `BeachSpawnPoint`
`beach-top-entry` at `(37.5, -70.5, 0)`. Beach also has
`BeachLevelTransition_BeachToTown` at its upper edge for the reverse direction
to Town spawn id `town-bottom-entry`.

## Import Procedure Used

1. Copy the LDtk project into `Assets/LDtk/Town2/`.

   When LDtk is configured with external levels, also copy the external level
   folder exactly as referenced by `externalRelPath`. For the current import:

   ```text
   Assets/LDtk/Town2/town2/Level_0.ldtkl
   Assets/LDtk/Town2/town2/Level_1.ldtkl
   ```

2. Copy every tileset referenced by `town2.ldtk` into
   `Assets/LDtk/Town2/Tilesets/`.

   For `town2.ldtk`, the relevant tilesets include:

   - `2_City_Terrains_32x32.png`
   - `3_City_Props_32x32.png`
   - `4_Generic_Buildings_32x32.png`
   - `5_Floor_Modular_Buildings_32x32.png`
   - `9_Shopping_Center_and_Markets_32x32.png`
   - `12_Hotel_and_Hospital_32x32.png`
   - `13_School_32x32.png`
   - `15_Police_Station_32x32.png`
   - `21_Beach_32x32.png`
   - `1_Terrains_and_Fences_32x32.png`

3. For external LDtk levels, generate `.ldtkt` tileset definition files under
   the external level folder.

   LDtkUnity imports `.ldtkl` files independently and expects one `.ldtkt` file
   per tileset beside the external levels. The files use the tileset identifier
   as the filename, for example:

   ```text
   Assets/LDtk/Town2/town2/_Beach_32x32.ldtkt
   Assets/LDtk/Town2/town2/_School_32x32.ldtkt
   Assets/LDtk/Town2/town2/_Terrains_and_Fences_32x32.ldtkt
   ```

4. Configure imported tileset textures for pixel-perfect tile use.

   Required settings:

   - Sprite mode: multiple or sliced sprites, depending on generation path
   - Pixels per unit: `32`
   - Filter mode: point/no filtering
   - Compression: none
   - Max texture size high enough to avoid downscaling

5. Generate `town2_composite.png` at full LDtk pixel size.

   The current composite is a disabled binding/validation sprite; the
   player-facing map is the saved operational Tilemap layers. It must preserve
   the full LDtk pixel size and must not be downscaled by Unity import settings.

6. Generate Unity `Tile` assets for each used source tile into
   `Assets/LDtk/Town2/Tiles/`.

   Tile asset names follow this pattern:

   ```text
   <tileset-name>_x<source-x>_y<source-y>.asset
   ```

7. Build saved Tilemap GameObjects in `Assets/Scenes/Town.unity` and
   `Assets/Scenes/Beach.unity`.

   The layer order follows the LDtk art order:

   ```text
   Ground -> Ground2 -> Beach -> Fense -> Props -> Shop -> Hospital -> Police -> Building -> School
   ```

8. Add `TilemapCollider2D` to blocking layers:

   - `Town2LdtkFenseTilemap`
   - `Town2LdtkShopTilemap`
   - `Town2LdtkHospitalTilemap`
   - `Town2LdtkPoliceTilemap`
   - `Town2LdtkBuildingTilemap`

9. Disable `Town2LdtkComposite` renderer.

   The composite remains in the scene only for binding/debug validation. The
   actual visible map must be the operational Tilemap layers.

10. Clear old sample/pre-LDtk tile contents.

   Keep the objects but call `ClearAllTiles()` on:

   - `GroundTilemap`
   - `TownDecorationTilemap`

11. Ensure `TownSpawnPoint` is on a valid LDtk ground cell.

Current saved/runtime Town spawn:

    ```text
    TownSpawnPoint = (10.5, -14.5, 0)
    spawnId = town-entry
    ```

12. Add the Level_0 to Beach transition trigger at the lower Level_0 edge.

    ```text
    TownLevelTransition_Level0ToLevel1
    BoxCollider2D.isTrigger = true
    BoxCollider2D.size = (44, 1.4)
    TownLevelTransitionTrigger.destinationScene = Beach
    TownLevelTransitionTrigger.destinationSpawnId = beach-top-entry
    ```

13. Add the Beach return trigger at the upper Beach edge.

    ```text
    BeachLevelTransition_BeachToTown
    BoxCollider2D.isTrigger = true
    BoxCollider2D.size = (44, 1.4)
    TownLevelTransitionTrigger.destinationScene = Town
    TownLevelTransitionTrigger.destinationSpawnId = town-bottom-entry
    ```

14. Save `Assets/Scenes/Town.unity` and `Assets/Scenes/Beach.unity`, then add
    `Assets/Scenes/Beach.unity` to EditorBuildSettings and BuildScript scene
    lists.

## Current Town Camera And Door Follow-Up

After the LDtk map was installed, the Town camera was widened for gameplay:

- `Camera.main.orthographicSize = 9`

Door triggers were manually positioned against the LDtk buildings. Current
door object names:

- `TownDoorPortal_Bakery`
- `TownDoorPortal_Apartment`
- `TownDoorPortal_Office`
- `TownDoorPortal_Cafe`
- `TownDoorPortal_Market`
- `TownDoorPortal_Police`
- `TownDoorPortal_Hospital`
- `TownDoorPortal_BusinessCenter`

They currently auto-enter `House` at `house-entry` using `WorldPortal` after a
short `TownAutoDoorPortal` open overlay animation. Later building-specific
interior scenes can reuse the same portal objects by changing destination scene
and spawn id.

## Verification Checklist

Run these focused tests after changing the LDtk import, Tilemaps, doors, or
Town camera:

```text
EditMode:
Rootborn.Tests.EditMode.World.TownLdtkMapDefinitionTests
Rootborn.Tests.EditMode.TownConcept.TownSavedTilemapSceneTests.TOWN_VISUAL_EDIT_001_TownSceneStoresVisibleTilemapLayout

PlayMode:
Rootborn.Tests.PlayMode.TownBootTilemapVisualTests.TOWN_VISUAL_PM_001_BootSaveSlotFlowShowsSavedTownTilemap
Rootborn.Tests.PlayMode.World.TownLevelTransitionPlayModeTests.TOWN_LEVEL_PM_001_WalkingDownFromLevel0LoadsBeachAtTopEntry
Rootborn.Tests.PlayMode.World.TownDoorPortalPlayModeTests.TOWN_DOOR_PM_001_EachLdtkDoorShowsPromptAndOneDoorEntersHouse
```

For visible changes, do not rely only on tile counts. Capture or inspect the
player-facing Game View. Existing screenshots from the current flow are written
to:

```text
C:\Users\jdyj\AppData\Local\Temp\jdyj\ROOTBORN\TownBootTilemapVisual.png
C:\Users\jdyj\AppData\Local\Temp\jdyj\ROOTBORN\BeachSceneTransitionVisual.png
C:\Users\jdyj\AppData\Local\Temp\jdyj\ROOTBORN\TownDoorAutoOpenVisual.png
```

Expected saved Town Tilemap counts:

```text
GroundTilemap = 0
TownDecorationTilemap = 0
Town2LdtkGroundTilemap = 5050
Town2LdtkGround2Tilemap = 96
Town2LdtkBeachTilemap = 0
Town2LdtkFenseTilemap = 96
Town2LdtkPropsTilemap = 687
Town2LdtkShopTilemap = 237
Town2LdtkHospitalTilemap = 605
Town2LdtkPoliceTilemap = 141
Town2LdtkBuildingTilemap = 279
Town2LdtkSchoolTilemap = 47
```

Expected saved Beach Tilemap counts:

```text
Town2LdtkGroundTilemap = 396
Town2LdtkBeachTilemap = 933
```

## Common Failure Modes

- If the Game View still shows old sample tiles, check that the saved
  `GroundTilemap` and `TownDecorationTilemap` tile counts are both `0`.
- If the composite image is visible over the map, disable the
  `Town2LdtkComposite` `SpriteRenderer`.
- If runtime looks right but reopening `Town.unity` does not, the change was
  runtime-only. Save and verify the scene in EditMode.
- If tiles look blurry or scaled incorrectly, inspect the source tileset import
  settings for PPU `32`, point filtering, no compression, and max texture size.
- If `Texture2D.GetPixels` or generated tile sprites fail for tall tilesets,
  check that `maxTextureSize` is high enough. `13_School_32x32.png` needs the
  importer to preserve its full `1024 x 3712` source size.
- If LDtkUnity logs missing `.ldtkt` errors for external levels, regenerate the
  `Assets/LDtk/Town2/town2/*.ldtkt` files from the tileset definitions in
  `town2.ldtk`.
- If the player spawns on an empty screen or no ground tile, verify
  `TownSpawnPoint` and `WorldEntryPointAutoFiller` use a valid LDtk ground cell.
- If doors do not trigger automatically, verify that the door object has:
  `BoxCollider2D` with `isTrigger = true`, `WorldPortal`, and
  `TownAutoDoorPortal`.

## Unity Editing Constraints

This project forbids direct disk writes for `Assets/**/*.cs`. Use Unity MCP
script update tooling for C# changes. Non-code docs can be edited normally.
