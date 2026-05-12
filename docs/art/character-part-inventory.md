# Character Part Inventory

Generated for `docs/superpowers/goals/2026-05-07-character-part-composition-goal.md`.

## Runtime Source Choice

The first runtime wiring uses the adult Modern Farm 16x16 character generator pieces:

`Assets/Modern_Farm_v1.2/Farmer_Generator_Pieces/Character Pieces/`

Reasons:

- It has the five required runtime categories: `Accessories`, `Bodies`, `Eyes`, `Hairstyles`, `Outfits`.
- Sample sheets are `896x352`, evenly divisible by 16 (`56 x 22` cells).
- The visual style matches the current modern farm art direction.
- It avoids Modern Interiors adult body sheets whose sampled files are `927x656`, not an integer 16x16 grid.

Modern Interiors adult and kids folders were inspected and remain reference material for later expansion. Kids folders are not first-scope runtime parts because the current goal targets the player character setup and does not yet define age/body compatibility rules.

## Modern Farm Adult 16x16

Root: `Assets/Modern_Farm_v1.2/Farmer_Generator_Pieces/Character Pieces/`

| Folder | Runtime category | PNG count | Sample | Sample size | 16x16 grid | Current importer note |
| --- | --- | ---: | --- | --- | --- | --- |
| `Accessories/16x16` | `accessory` | 8 | `Accessory_Bamboo_Hat_Brown.png` | `896x352` | yes | sampled metas show mixed/default import state |
| `Bodies/16x16` | `body` | 9 | `Body_1.png` | `896x352` | yes | `Body_1.png` sampled as Single Sprite |
| `Eyes/16x16` | `eyes` | 5 | `Eyes_Blue.png` | `896x352` | yes | sampled metas show mixed/default import state |
| `Hairstyles/16x16` | `hair` | 45 | `Hairstyle_Balding_Blonde.png` | `896x352` | yes | sampled metas show mixed/default import state |
| `Outfits/16x16` | `outfit` | 13 | `Outfit_Braces_Brown.png` | `896x352` | yes | sampled metas show mixed/default import state |

Slice policy for runtime:

- Configure selected sheets as `SpriteImportMode.Multiple`.
- Use 16x16 cells, point filtering, no mipmaps, uncompressed texture compression, `spritePixelsPerUnit = 16`, readable.
- Name sub-sprites deterministically as `<fileBase>_r<row>_c<col>` where row is top-to-bottom and column is left-to-right.
- Runtime definitions should reference a sheet address and sub-sprite name, not a complete character sprite.

## Modern Interiors Adult 16x16

Root: `Assets/moderninteriors-win/2_Characters/Character_Generator/`

| Folder | PNG count | Sample | Sample size | 16x16 grid | Runtime decision |
| --- | ---: | --- | --- | --- | --- |
| `0_Premade_Characters/16x16` | 20 | `Premade_Character_01.png` | `896x656` | yes | excluded; premade complete characters violate part-composition goal |
| `Accessories/16x16` | 84 | `Accessory_01_Ladybug_01.png` | `896x656` | yes | reference only |
| `Bodies/16x16` | 9 | `Body_01.png` | `927x656` | no | excluded until non-grid padding/crop policy is defined |
| `Books/16x16` | 6 | `Book_01.png` | `896x656` | yes | excluded; held-object layer outside first scope |
| `Eyes/16x16` | 7 | `Eyes_01.png` | `896x656` | yes | reference only |
| `Hairstyles/16x16` | 200 | `Hairstyle_01_01.png` | `896x656` | yes | reference only |
| `Outfits/16x16` | 132 | `Outfit_01_01.png` | `896x656` | yes | reference only |
| `Smartphones/16x16` | 5 | `Smartphone_1.png` | `384x192` | yes | excluded; held-object layer outside first scope |

## Modern Interiors Kids 16x16

Root: `Assets/moderninteriors-win/2_Characters/Character_Generator/`

| Folder | PNG count | Sample | Sample size | 16x16 grid | Runtime decision |
| --- | ---: | --- | --- | --- | --- |
| `Bodies_kids/16x16` | 4 | `Body_1_kid.png` | `384x128` | yes | investigated; excluded from first runtime wiring |
| `Eyes_kids/16x16` | 6 | `Eyes_kids_1.png` | `384x96` | yes | investigated; excluded from first runtime wiring |
| `Hairstyles_kids/16x16` | 30 | `Hairstyle_kid_1_1.png` | `384x128` | yes | investigated; excluded from first runtime wiring |
| `Outfits_kids/16x16` | 7 | `Outfit_kid_1.png` | `384x96` | yes | investigated; excluded from first runtime wiring |

## First Definition Set

Minimum SO definitions should use at least two entries per runtime category:

- `body`: `Body_1`, `Body_2`
- `eyes`: `Eyes_Blue`, `Eyes_Brown`
- `hair`: `Hairstyle_Short_Blonde`, `Hairstyle_Short_Brown_Dark`
- `outfit`: `Outfit_Braces_Brown`, `Outfit_Braces_Green`
- `accessory`: `Accessory_Bamboo_Hat_Brown`, `Accessory_Straw_Hat_Black`

The first implementation can use the same frame cell for idle preview across all categories, but the data model must keep sheet/sub-sprite references so movement and direction frame selection can be expanded without changing the save format.
