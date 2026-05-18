# Modern Interiors Shadowless Singles 48x48 Inventory

## Scope

조사 대상은 아래 폴더 하나로 제한한다.

```text
C:\Users\jdyj\Downloads\moderninteriors-win\1_Interiors\48x48\Theme_Sorter_Shadowless_Singles_48x48
```

이번 goal에서 다음 폴더는 import 대상으로 보지 않는다.

```text
1_Interiors/48x48/Theme_Sorter_Singles_48x48
1_Interiors/48x48/Theme_Sorter_Black_Shadow_Singles_48x48
1_Interiors/48x48/Theme_Sorter_48x48
1_Interiors/48x48/Theme_Sorter_Shadowless_48x48
1_Interiors/48x48/Theme_Sorter_Black_Shadow_48x48
```

## Folder Inventory

`Representative Size` is the first sorted PNG's pixel size. `Single48` means 48x48 exactly. `Multi48` means the image is divisible by 48 in both axes and can derive an exact multi-cell footprint from pixel size. `CeilAdjusted` means the image is not divisible by 48 in at least one axis, so the importer uses `ceil(width / 48)` and `ceil(height / 48)` instead of collapsing it to 1x1. `Wall/Floor/Interactive Candidate` are heuristic planning columns for later curation, not runtime branches.

| Folder | PNG Count | Sample Files | Representative Size | House First | Category | Single48 | Multi48 | CeilAdjusted | Wall Candidate | Floor Candidate | Interactive Candidate |
| --- | ---: | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `2_Living_Room_Singles_Shadowless_48x48` | 122 | `Living_Room_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 96x128 | Yes | LivingRoom | 4 | 53 | 65 | 91 | 13 | 115 |
| `3_Bathroom_Singles_Shadowless_48x48` | 158 | `Bathroom_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 96x96 | Yes | Bathroom | 2 | 33 | 123 | 129 | 16 | 155 |
| `4_Bedroom_Singles_Shadowless_48x48` | 555 | `Bedroom_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 112x112 | Yes | Bedroom | 38 | 224 | 293 | 328 | 18 | 511 |
| `12_Kitchen_Singles_Shadowless_48x48` | 408 | `Kitchen_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x80 | Yes | Kitchen | 185 | 64 | 159 | 139 | 62 | 164 |
| `20_Japanese_Interiors_Singles_Shadowless_48x48` | 131 | `Japanese_Interiors_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x48 | Yes | Japanese | 12 | 42 | 77 | 52 | 42 | 109 |
| `26_Condominium_Singles_Shadowless_48x48` | 86 | `Condominium_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 48x192 | Yes | Condominium | 9 | 40 | 37 | 65 | 9 | 75 |
| `5_Classroom_and_Library_Singles_Shadowless_48x48` | 75 | `Classroom_and_Library_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 48x96 | Later | Study | 1 | 37 | 37 | 68 | 5 | 74 |
| `6_Music_and_Sport_Shadowless_48x48` | 249 | `Music_and_Sport_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 96x128 | Later | MusicSport | 43 | 40 | 166 | 154 | 36 | 196 |
| `7_Art_Singles_Shadowless_48x48` | 46 | `Art_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 48x64 | Later | Art | 0 | 8 | 38 | 33 | 13 | 33 |
| `8_Gym_Singles_Shadowless_48x48` | 209 | `Gym_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x32 | Later | Gym | 81 | 57 | 71 | 79 | 43 | 100 |
| `9_Fishing_Singles_Shadowless_48x48` | 77 | `Fishing_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 48x80 | Later | Fishing | 1 | 21 | 55 | 57 | 6 | 67 |
| `10_Birthday_Party_Singles_Shadowless_48x48` | 29 | `Birthday_Party_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 48x80 | Later | EventDecor | 7 | 9 | 13 | 16 | 5 | 17 |
| `11_Halloween_Singles_Shadowless_48x48` | 240 | `Halloween_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x80 | Later | EventDecor | 32 | 52 | 156 | 170 | 19 | 179 |
| `13_Conference_Hall_Singles_Shadowless_48x48` | 68 | `Conference_Hall_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 64x112 | Later | Conference | 15 | 18 | 35 | 41 | 10 | 38 |
| `14_Basement_Singles_Shadowless_48x48` | 240 | `Basement_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 96x80 | Later | Basement | 37 | 58 | 145 | 154 | 35 | 179 |
| `15_Christmas_Singles_Shadowless_48x48` | 123 | `Christmas_SIngles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 96x112 | Later | EventDecor | 43 | 8 | 72 | 71 | 7 | 61 |
| `16_Grocery_Store_Singles_Shadowless_48x48` | 483 | `Grocery_Store_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x64 | Later | Shop | 71 | 187 | 225 | 338 | 30 | 372 |
| `18_Jail_Singles_Shadowless_48x48` | 344 | `Jail_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x96 | Later | Jail | 92 | 115 | 137 | 219 | 25 | 187 |
| `19_Hospital_SIngles_Shadowless_48x48` | 532 | `Hospital_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x128 | Later | Hospital | 58 | 156 | 318 | 352 | 84 | 426 |
| `21_Clothing_Store_Singles_Shadowless_48x48` | 494 | `Clothing_Store_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 48x80 | Later | Shop | 25 | 31 | 438 | 303 | 157 | 313 |
| `22_Museum_Singles_Shadowless_48x48` | 451 | `Museum_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 288x176 | Later | Museum | 74 | 114 | 263 | 263 | 90 | 344 |
| `23_Television_and_Film_Studio_Singles_Shadowless_48x48` | 80 | `Television_and_FIlm_Studio_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 64x80 | Later | Studio | 17 | 22 | 41 | 45 | 11 | 54 |
| `24_Ice_Cream_Shop_Singles_Shadowless_48x48` | 102 | `Ice_Cream_Shop_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._100.png`, `..._101.png`, `..._102.png` | 96x128 | Later | Shop | 21 | 7 | 74 | 64 | 15 | 81 |
| `25_Shooting_Range_Singles_Shadowless_48x48` | 28 | `Shooting_Range_Singles_Shadowless_48x48_1.png`, `..._10.png`, `..._11.png`, `..._12.png`, `..._13.png` | 48x48 | Later | ShootingRange | 15 | 4 | 9 | 13 | 0 | 8 |

Total folders: 24

Total PNG files: 5,330

House-first PNG files: 1,460

## Application Notes

- First implementation should import only the six `House First Candidate` folders.
- Later folders are useful for school, shop, hospital, event, and town interiors, but they should not be mixed into the first House placement pass.
- All rows come from the shadowless singles source folder. Shadow and black-shadow variants are intentionally excluded to prevent duplicate `FurnitureDefinition` entries.
- Folder categories are import metadata for UI grouping and SO data. Runtime code must not branch on individual furniture ids or folder names.
- The first House pass generated one `FurnitureDefinition` per PNG for the six House-first folders. The importer keeps each source PNG as a single tile part and uses pixel-size-derived footprint cells. Non-48-divisible images now use ceiling footprints, so `96x48` becomes `2x1`, `48x80` becomes `1x2`, and `112x112` becomes `3x3`.
