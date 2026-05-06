# Modern UI Sprite Map

Generated during the Pixelwood-to-modern conversion pass.

## Source Sheets

The first working set is the top-level LimeZu Modern UI sheets already sliced by `ModernUiSliceSetup`.

| Address | Asset | Grid | Use |
| --- | --- | --- | --- |
| `sprites/ui/modern/16/style-1` | `Assets/modernuserinterface-win/16x16/Modern_UI_Style_1.png` | 61 x 43 | warm/brown UI, primary ROOTBORN HUD direction |
| `sprites/ui/modern/16/style-2` | `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png` | 49 x 34 | cool/gray UI, alternate/disabled state direction |
| `sprites/ui/modern/16/gamepad` | `Assets/modernuserinterface-win/16x16/Modern_UI_Gamepad.png` | 51 x 51 | controller prompts |
| `sprites/ui/modern/32/style-1` | `Assets/modernuserinterface-win/32x32/Modern_UI_Style_1_32x32.png` | 61 x 43 | inventory/detail panels that need crisper scaling |
| `sprites/ui/modern/32/style-2` | `Assets/modernuserinterface-win/32x32/Modern_UI_Style_2_32x32.png` | 49 x 34 | alternate/disabled larger controls |
| `sprites/ui/modern/32/gamepad` | `Assets/modernuserinterface-win/32x32/Modern_UI_Gamepad_32x32.png` | 51 x 51 | larger controller prompts |
| `sprites/ui/modern/48/style-1` | `Assets/modernuserinterface-win/48x48/Modern_UI_Style_1_48x48.png` | 61 x 43 | high-resolution UI source, selected hero/large controls |
| `sprites/ui/modern/48/style-2` | `Assets/modernuserinterface-win/48x48/Modern_UI_Style_2_48x48.png` | 49 x 34 | high-resolution alternate controls |
| `sprites/ui/modern/48/gamepad` | `Assets/modernuserinterface-win/48x48/Modern_UI_Gamepad_48x48.png` | 51 x 51 | high-resolution controller prompts |

## Current Pixelwood UI Replacement Targets

| Current address | Current purpose | Modern source category | Next implementation note |
| --- | --- | --- | --- |
| `sprites/ui/panel/hud` | main HUD panel background | Style 1 framed boxes, left block | Needs named 9-slice rect, not a raw 16x16 grid cell. |
| `sprites/ui/panel/hint` | hint/tooltip panel | Style 2 framed boxes or Style 1 pale box | Needs named 9-slice rect. |
| `sprites/ui/button/small` | Prev/Next and compact commands | Style 1 button/icon button blocks | Can use sub-sprite cells first, then replace with 9-slice rect if stretching artifacts show. |
| `sprites/ui/slot/item` | inventory item slot | Style 1 square slot/frame cells | Prefer 32x32 source for visible inventory slots. |
| `sprites/ui/slot/equipment` | equipment slot | Style 1 locked/outlined slot cells | Prefer 32x32 source, with lock/outline variants available. |
| `sprites/ui/ribbon/items` | inventory section title | Style 1 horizontal labels/bars | Needs named rect slice because the visible label bars are wider than one grid cell. |
| `sprites/ui/ribbon/description` | description section title | Style 1 horizontal labels/bars | Needs named rect slice. |
| `sprites/ui/ribbon/equipment` | equipment/stats title | Style 1 horizontal labels/bars | Needs named rect slice. |
| `sprites/ui/decor/cutter-short` | short divider | Style 1 horizontal divider/progress-strip cells | Use simple colored strip first; refine after screenshot. |
| `sprites/ui/decor/cutter-long` | long divider | Style 1 horizontal divider/progress-strip cells | Needs named rect or tiled Image. |
| `sprites/ui/decor/inscription-plus` | plus icon | Style 1 button icon block | Direct 16x16 sub-sprite replacement. |
| `sprites/ui/sheet/bookmark` | inventory category tabs | Style 1/2 right-side tab and arrow button groups | Replace bookmark sheet with modern tab/button sub-sprites. |
| `sprites/ui/character` | paper-doll silhouette | Portrait generator sheets | Keep deferred until character/equipment UI pass. |
| `sprites/ui/book/page-1..9` | book page flip animation | No direct modern equivalent | Remove book/page dependency from HUD instead of replacing frame-for-frame. |

## Candidate Groups

- **Panels and boxes:** left side of `Modern_UI_Style_1.png` and `Modern_UI_Style_2.png`. These include large framed boxes and small square boxes. The large frames should become named rect slices in the next pass because 16x16 cell slicing only exposes their corners/pieces.
- **Buttons:** central and right-side blocks of Style 1/Style 2. These provide check, x, plus, minus, arrows, playback, list, folder, lock, cart, edit, and social icons.
- **Slots:** square framed/locked cells in Style 1 and Style 2. Use 32x32 versions first for inventory readability.
- **Resource/status icons:** top icon cluster of Style 1/Style 2 includes gift, chest, tools, gems, dice, crops/food, hearts, coins, and check/cancel marks.
- **Controller prompts:** Gamepad sheets are already separated and should not be mixed with mouse/keyboard HUD buttons.

## Evidence

- `ModernUiSliceSetupTests` verifies nine source sheets, dimensions, grid sizes, and first/last sliced sub-sprite names.
- `ModernUiAddressablesSetupTests` verifies every declared modern sheet address maps to a Modern UI source path and that declared sub-sprites exist in the sliced sheets.
- Visual grid reference: `docs/art/modern-ui-style1-left-grid.png`.
