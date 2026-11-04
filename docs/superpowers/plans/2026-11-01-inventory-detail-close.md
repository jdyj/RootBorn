# Inventory detail close control, 2026-11-01

Continue the user's approved ComfyUI dimensional urban UI work. Actual item-detail screenshots show an unlabeled inert ActionButton. Turn that existing button into a clearly labeled Close control, plus one Comfy182 ivory/navy close glyph32x32. Close only the detail popup and leave the inventory visible; no item use/delete/economy action and no new inventory system.

- Keep the existing192x64 button position/frame and existing popup size.
- One bilingual `ui.inventory.close-detail` key only541->542; fallback Close/닫기 when catalog unavailable.
- Shared UrbanUiIcon key `close`, one new32x32/16PPU/center16/binary/2px/<=24color Sprite, append theme20->21. Existing theme loader owns dependency; glyph has no raycast target.
- Tests first: missing art/theme key, localized visible Close label, actual Button callback destroys popup while inventory/counts stay unchanged. Retain existing ActionButton GameObject name for compatibility.
- Actual packed play: I open,click existing saved item,real pointer Close,verify popup closed/inventory still visible and data unchanged;repeat other item and cold reload. Independent glyph texture/theme/catalog/item dependency verification.
- Preserve all original source scenes/item definitions/other catalog values; restore QA defaults/builder0; package only after native review. Existing search/delete/filter controls remain placeholders and must not be reported as working actions.

Completed: Comfy182 plate badge rejected,183 unframed glyph reviewed/32px export imported.5 close tests red->green including KO/EN and missing-catalog fallback;344/344 across88 requested fixtures. Actual2 item pointers/Close pointers and cold repeat preserve inventory visible,all student DTO fields and actual item counts.9/9 exact bundle textures,all542 bilingual values,21 theme dependencies and2 source-exact item-icon chains verified. Original72 scene/data/art/shared-icon/meta files and source QA hashes exact. Source only20->21 theme append,541->542 catalog append,and existing ActionButton label/glyph/ClosePopup callback;192x64 frame preserved. Defaults and builder0 restored.
