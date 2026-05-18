# Modern UI Style2 Gemini Visual Audit

Reviewed on 2026-05-12 against `docs/art/modern-ui-style2-contact-sheet-4x.png`.

| Scope | Visual evidence | Decision |
|---|---|---|
| CommonPanel `r0_c0~r2_c2` vs `r2_c0~r4_c2` | The contact sheet shows `r0_c0~r2_c2` as a visually coherent 9-slice panel, but repository UI standards require CommonPanel to use `r2_c0~r4_c2` with `r3_c1` as fill. `ModernUiStyle2Sprites.CommonPanel` and every `ModernUiRecipes.CommonPanel` caller already depend on that block. | Preserve `ModernUiStyle2Sprites.CommonPanel` at `r2_c0~r4_c2`; patch catalog entries for those coordinates to `panel.common.*` roles. |
| `r3_c42~r3_c44` | Zoomed contact sheet shows `r3_c40~r3_c42` as plus button states and `r3_c43~r3_c45` as minus button states; the old `button.icon.heightFrame` group crosses two visual controls. | Replace the old height-frame assertion with plus/minus state groups: `r3_c42=button.plus.pressed`, `r3_c43=button.minus.normal`, `r3_c44=button.minus.hover`. |
| `r1_c13` | Zoomed contact sheet shows a chair-shaped furniture icon. | Add/keep `FurnitureChair` at `r1_c13` with `probable` confidence. |
| `r1_c14` | Zoomed contact sheet shows a bed-shaped furniture icon. | Add/keep `FurnitureBed` at `r1_c14` with `probable` confidence. |
