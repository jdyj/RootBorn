# Inventory control icon continuation, 2026-11-01

User-approved continuous ComfyUI dimensional urban UI style. After the four category tabs and real saved item icons, replace the unreadable legacy search/delete/filter graphics with Comfy179 magnifier,180 bin,181 slider glyph. Three decorative32x32/2px/binary/<=24-color icons via existing shared UrbanUiIcon/themed Addressables owner. Keep32x32 UI rects and current layout/data/input unchanged. Preserve loaded vendor fallback through ConfigureWithFallback. This slice does not implement search/delete/filter actions; existing controls are presentation only and must not be reported as working actions.

- [x] Generate,inspect raw and native exports; reject disconnected/cropped/ambiguous silhouettes; keep all3 filter rails if separate.
- [x] Missing-art/theme/control-binding regression first, append only3 theme entries and3 decorative binders.
- [x] Verify actual bundle textures/dependencies and actual inventory opening/control image bounds/cold reload; preserve student progress and inventory counts.
- [x] Restore builder0/overrides and package only verified accepted images.

## Observed stack-count layout bug, same UI polish scope

Actual native332-test checkpoint screenshot `output/design-comfy-20261031/evidence/inventory-category-native-first-open.png` shows counts490/75 extending into the gap below first-row frames. Root cause: count top-left y=-SlotSize+8 with32px height leaves24px below the168px slot; x=SlotSize-44 with44px width leaves no right inset. Add bounds regression for the inner slot (16px minimum inset), then place count inside the lower-right frame with readable navy text/ivory edge. No quantity/data/rule/save changes. Include actual before/after screen and cold count preservation in this next control-icon slice.

Verified339/339 in87 requested fixtures; actual3 control images,4 tab clicks,both localized popups,inner-slot count bounds/contrast/no clipping,cold exact student DTO/counts490/75,8/8 bundle textures and all541/20-theme/2-item dependencies. Three extra32px textures base12KiB. Source other65 files exact; Town correction restores original neutral child orders and keeps original22 parent groups/all other575-object fields, current Town only4 approved Sprite refs differ from planter baseline. No control action implementation claim. Defaults/builder0 restored.
