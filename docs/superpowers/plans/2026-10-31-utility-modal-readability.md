# Utility modal readability

Actual input now opens/closes World Log and Encyclopedia, blocks movement while open and restores it afterward. Placement styling preserves menu objects; return cleanup no longer hides gameplay entries or repeatedly closes reopened panels. Actual screenshots still show tiny card text, key suffixes, English category/detail data and frame padding too tight for reading. These screenshots are preliminary, not accepted final polish.

Keep approved ComfyUI urban molding atlas/corners/Point rendering. Enlarge Encyclopedia to1280x800 with three separated columns,32px inner padding,22px row text and bounded scrolling. World Log1120x800 uses wide readable cards and a clipped scrollable history. Preserve small common-card layout used by day results. Resolve complete authored localization keys, including badges, categories, entries and detail lines; author bilingual SO/catalog data without changing IDs, unlock conditions, rewards or save schema. Replace implementation/testing prose in product entries with useful player-facing content.

- [x] Add red UI layout/scroll and bilingual data-resolution regressions.
- [x] Apply responsive card/modal layouts and text resolution.
- [x] Author33 world-state texts plus Encyclopedia category/entry text through the existing catalog/SOs.
- [x] Build actual Addressables data, verify bilingual dependencies, repeat actual menu/scroll/close/input checks and inspect screenshots.

Verified 2026-11-01: final regression142/142 across39 fixtures; focused molding clearance11/11. Bilingual new keys56 (total479), text-only SOs20/fields54. Original423 translations, nontext fields and21 metadata files preserved. Actual packed native pointer open/close, wheel through UI-only8-record fixture to fully visible last card, open movement0 and closed same-key movement1.759998. Encyclopedia category selection and empty-detail clearing visually verified. Accepted screenshots: output/design-comfy-20261031/evidence/utility-*-native-v2*.png. Preliminary v1/border-overlap files are retained as rejected evidence only.
