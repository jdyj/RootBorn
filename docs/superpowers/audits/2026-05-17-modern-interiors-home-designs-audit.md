# Modern Interiors Home Designs Audit

## Scope

- Source root: `C:\Users\jdyj\Downloads\moderninteriors-win\6_Home_Designs`
- Target resolution: `48x48`
- Structural source: `layer_1`
- Reference-only source: `layer_2`, `preview`
- Furniture source policy: keep using the imported `1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48` single furniture catalog.
- Exclusion: do not inspect or import the other Theme_Sorter variants.

## Folder Inventory

| Folder | 48x48 designs | Home fit | Notes |
| --- | ---: | --- | --- |
| `Condominium_Designs` | 2 | High | Best first target. Both `layer_1` images are exact 48px multiples. |
| `Generic_Home_Designs` | 1 | High | Strong home-room reference, but image height has an extra 18px remainder. Needs crop/padding policy before cell slicing. |
| `Japanese_Interiors_Home_Designs` | 1 | High | Strong thematic home reference, but image height has an extra 18px remainder. Needs crop/padding policy before cell slicing. |
| `Gym_Designs` | 2 | Medium | Useful later for special room presets, not a normal house first pass. |
| `Ice-Cream_Shop_Designs` | 1 | Low | Shop layout. Can inspire commercial/interior scenes later. |
| `Museum_Designs` | 5 | Low | Large exhibit rooms. Not suitable for first House room preset pass. |
| `Shooting_Range_Designs` | 1 | Low | Specialty scene; height has 18px remainder. |
| `TV_Studio_Designs` | 1 | Low | Specialty scene; exact 48px multiple. |

## 48x48 File Dimensions

| Folder | File | Pixels | Cell estimate | Exact 48 grid |
| --- | --- | ---: | ---: | --- |
| `Condominium_Designs` | `Condominium_Design_2_layer_1_48x48.png` | 672x288 | 14x6 | Yes |
| `Condominium_Designs` | `Condominium_Design_layer_1_48x48.png` | 672x528 | 14x11 | Yes |
| `Generic_Home_Designs` | `Generic_Home_1_Layer_1_48x48.png` | 672x642 | 14x13 + 18px | No |
| `Gym_Designs` | `Gym_2_layer_1_48x48.png` | 576x336 | 12x7 | Yes |
| `Gym_Designs` | `Gym_layer_1_48x48.png` | 912x720 | 19x15 | Yes |
| `Ice-Cream_Shop_Designs` | `Ice_Cream_Shop_Design_layer_1_48x48.png` | 576x480 | 12x10 | Yes |
| `Japanese_Interiors_Home_Designs` | `Japanese_Home_1_Layer_1_48x48.png` | 912x642 | 19x13 + 18px | No |
| `Museum_Designs` | `Museum_entrance_layer_1_48x48.png` | 768x1056 | 16x22 | Yes |
| `Museum_Designs` | `Museum_room_1_layer_1_48x48.png` | 960x816 | 20x17 | Yes |
| `Museum_Designs` | `Museum_room_2_layer_1_48x48.png` | 768x1584 | 16x33 | Yes |
| `Museum_Designs` | `Museum_room_3_layer_1_48x48.png` | 816x1008 | 17x21 | Yes |
| `Museum_Designs` | `Museum_room_4_layer_1_48x48.png` | 768x480 | 16x10 | Yes |
| `Shooting_Range_Designs` | `Shooting_Range_Design_layer_1_48x48.png` | 480x498 | 10x10 + 18px | No |
| `TV_Studio_Designs` | `Tv_Studio_Design_layer_1_48x48.png` | 528x480 | 11x10 | Yes |

## First Implementation Candidates

1. `Condominium_Design_2`
   - Size: 14x6 cells.
   - Best use: compact hallway/lobby style room.
   - Reason: exact 48 grid, small enough for a first visual verification pass.

2. `Condominium_Design`
   - Size: 14x11 cells.
   - Best use: compact apartment / multi-room room shell.
   - Reason: exact 48 grid and closer to a house layout than specialty designs.

## Mapping Policy

- Treat `layer_1` as structural/base room art.
- Slice exact-grid `layer_1` images into 48x48 cell sprites and place one Tile per cell.
- Apply the sliced `layer_1` cells through a room-preset path, not by replacing the furniture catalog.
- Keep furniture placement manual and data-driven through existing `InteriorFurnitureDefinition` assets.
- Use the existing lower-left furniture anchor and footprint/ghost preview behavior unchanged.
- Do not create entity-ID-specific C# branches for individual room names.

## Deferred Policy

The following designs should not be auto-sliced until the importer has an explicit non-48-height policy:

- `Generic_Home_1_Layer_1_48x48.png`: 18px height remainder.
- `Japanese_Home_1_Layer_1_48x48.png`: 18px height remainder.
- `Shooting_Range_Design_layer_1_48x48.png`: 18px height remainder.

Recommended policy for a later pass: crop or ignore the bottom 18px only if visual inspection confirms it is padding/shadow, then document that decision in the importer test.

## Existing System Fit

The current House scene generates an `InteriorGeneratedMap` and applies it through `InteriorTilemapApplier.Apply(map)`. A Home Design preset should therefore either:

- produce an `InteriorGeneratedMap` compatible with the existing applier, or
- add a dedicated preset applier that still writes to the same House tilemaps and clears stale sample/debug layers.

For the first sample implementation, the lowest-risk path is a dedicated room preset definition plus applier for `layer_1` cells. Furniture remains separate and continues to use the existing placement overlay.

## Verification Requirements

- EditMode:
  - Exact-grid layer_1 files parse into the expected cell sizes.
  - Non-exact layer_1 files are reported as requiring policy instead of silently mis-slicing.
  - Generated preset contains the expected tile/cell count.
- PlayMode:
  - House loads through the normal player-facing scene flow.
  - A room preset can be applied to the House tilemaps.
  - Runtime Tilemap state reports selected preset name, floor/base tile count, object tile count, and stale/debug tile absence.
- Visual:
  - Capture Game View or Camera screenshot after applying the room preset.
  - Inspect that the visible House view reflects the applied preset, not only serialized data.
