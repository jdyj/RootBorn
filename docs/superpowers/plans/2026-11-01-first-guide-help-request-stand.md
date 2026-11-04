# First Guide Help Request Stand Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. The parent owns review, submission and Unity integration. Preparation does not authorize submission.

**Goal:** Replace the existing First Guide help point's generic book glyph with one Comfy-generated household-help request stand, preserving the existing action and placement.

**Architecture:** One existing SpriteRenderer receives a separately generated sprite after raw and native-scale review. Use the approved archive ledger stand only as a geometry reference; Qwen renders a new oak/navy/ivory/brass object. No delivery mechanic, character, inventory item, new interactable, transformation, trigger or data change is included.

**Tech Stack:** Existing FEVM ComfyUI Qwen Image Edit 2511 workflow; Python/Pillow for reference preparation and technical export; Unity Point-filtered sprite at 48 PPU after parent approval.

---

## Evidence and semantic boundary

### Placement preflight revision (2026-11-01)

Comfy217 raw review accepts a moderate landscape silhouette (matte bbox482x357, ratio1.35014) instead of the approximate2:1 direction. The generated single ivory/navy/brass/oak plaque is complete and distinct from the rejected tall216 candidate. This explicit aesthetic variance changes only the extra raw-aspect gate from1.5 to1.3 for this hash-reviewed raw; preserve the proposed exporter/scope in `backup/first-guide-v2-raw-aspect-review`. Proportional technical export is32x24 visible pixels,25 colors, bbox(8,20,40,44), with every hard placement/grid/palette contract unchanged. No nonuniform stretch or painted repair is permitted. Actual native clearance remains a separate gate.

Comfy216 remains an unimported, visually reviewed candidate. Read-only native measurements in `output/design-comfy-20261031/evidence/first-guide-stand-layout-preflight.json` found the actual 24-screen-pixel prompt bottom at world Y2.675. The proposed portrait silhouette reaches Y2.875, predicting 6.4 screen pixels of bounding-box overlap; the candidate was never rendered. Its manifest now rejects placement and preserves the original visual review separately.

Generate a new low landscape plaque through ComfyUI rather than changing the prompt or compressing the old artwork. Revision target: 48x48 canvas, 48 PPU, pivot (24,8), bottom margin4, visible fit at most40x24, exact2px grid and <=31 colors. This preserves floor Y2.0416667 and caps the top at Y2.5416667, providing a predicted 4.2667-screen-pixel gap at the measured camera scale. Use an approved landscape reference, preserve the blank ivory/navy/brass/oak style, and review the new raw and export before importing. All v1 sizes below describe the retained historical candidate; integration/tests must use the accepted v2 geometry. Native rendering remains required before claiming clearance.

- `docs/story/rootborn-city-life-story-direction.md:221` assigns First Guide tutorial, first-school escort, household chores and family routines. Delivery errands are assigned to Shopkeeper at line 287. A household-help request stand is an art interpretation of those roles, not an explicitly specified prop in the GDD.
- `docs/superpowers/specs/2026-05-22-town-modern-interactable-sprites-design.md:138` recommends a notice/sign/help object for this action point.
- `Assets/Data/StudentLife/TownHelpActions/TownHelp_FirstGuideErrand.asset`, GUID `fbea5e29cdabdf74fb3533af14a045d2`, id/display key `town.help.first-guide-errand`, NPC `npc.first-guide`, location `location.town-square`.
- PaidActivity time policy GUID `877e2af40acc7d749a78882afa991e80` costs **1 TP**; the help action costs **20 minutes and 1 Energy**, with zero Focus cost/stress delta. Requirements are empty; the policy is not once-per-day.
- `HelpOutcome_FirstGuideTrust.asset` GUID `28767a6f67aec174aaf8fd93c1cb44b0` is actually a trait outcome: **Responsibility +2**, trait GUID `82b98f654b803c3468b3aa21026ae8f0`.
- `HelpOutcome_FirstGuideRelationship.asset` GUID `eac1c686d4e9f754996cb89eff6adebd` gives **First Guide trust +2**, relationship GUID `3fe25332108980742b1b38dfa7f31086`.
- Runtime consumer: `Assets/Scripts/Game/StudentLife/TownHelpActionInteractor.cs` -> `TownHelpActionRunner.cs`; successful performance spends resources, applies outcomes, records the activity and saves progress. There is no parcel, delivery destination, recipient selection, inventory consume/grant or minigame.
- The existing First Guide relationship milestone requires trust 2 and recommends this help action. Its existing Objective Journal ownership remains unchanged.

## Exact binding and geometry

Saved scene: `Assets/Scenes/Town.unity`.

| Contract | Current value to preserve |
|---|---|
| Root path | `AuthoredTownBindings/TownHelp_town_help_first_guide_errand` |
| Root GameObject / Transform | `2014801021` / `2014801022` |
| Root position / scale | `(14,2,0)` / `(1,1,1)` |
| Action interactor | `2014801023`, action GUID `fbea5e29cdabdf74fb3533af14a045d2` |
| GroundDepthSort | `2014801024`, foot offset `(0,0)`, units/world 48, base 10000 |
| SortingGroup | `2014801025`, Default layer, order **9904** |
| BoxCollider2D | `2014801026`, trigger true, offset zero, size **0.7 x 0.7** |
| Visual GameObject / Transform | `1401220945` / `1401220946` |
| Visual local position / scale | `(0,0.125,0)` / `(1,1,1)` |
| SpriteRenderer target | **`1401220947`**, Default layer, local sort order 0 |
| Current sprite | `Assets/Art/SchoolPixels/sprites/book_stack.png` |
| Current sprite GUID / fileID | `19633f73cbeb1704dbd23748bf52ec9f` / `21300000` |
| Current PNG / PPU / pivot | `24x18`, 48, bottom-left `(12,6)` |
| Current alpha bounds | Top-left-exclusive box `(2,2,21,16)`; bottom margin 2 |
| Current visible floor | `2 + 0.125 + (2 - 6)/48 = 2.0416666667` |

`TownHelpActionRuntimeInstaller` preserves the authored scene, so the saved 0.7 trigger is authoritative. Do not apply the exploration point's separate trigger rule here. Do not replace the shared `book_stack.png`; other teacher/discovery consumers retain it.

Proposed new asset, not yet created: `Assets/Art/UrbanStreet/Buildings31/first_guide_help_stand.png`.
Proposed address: `sprites/town/comfy31/first-guide-help-stand`. No GUID is assigned during preparation.

Export contract: **48x64 RGBA**, 48 PPU, bottom-left pivot **(24,8)** / normalized **(0.5,0.125)**, fit at most **40x40** visible pixels, bottom margin **4**, exact **2x2 pixel cells**, binary alpha, at most **31 visible colors**, transparent RGB zero. This keeps the floor at `2 + 0.125 + (4 - 8)/48 = 2.0416666667` with no scene transform change. The maximum visible size is 0.8333 x 0.8333 world units. The top is at most Y2.875, below the source-confirmed prompt anchor Y3.05 by 0.175 world units; actual text bounds still require native review. Use a short compact pedestal, not the reference's original tall proportions. Keep one connected stand; use only a technical matte, crop, nearest-grid reduction, palette reduction and placement after review. Do not draw, erase or reconstruct motifs.

## Reference and reuse audit

1. Selected geometry reference: `Assets/Art/SchoolPixels/Archive30/Archive30_ledger_stand_1x1.png`, SHA256 `7ff7bd523b81893e4f700ebb114abd5a6093d4a93afe1f4e14dbb538afc53443`. It is a complete upright wood pedestal and inclined reading surface, 48x96 with alpha bounds `(4,17,44,72)`. `output/design-comfy-20261030/asset-manifest.json` records the approved Comfy ledger stand selection and 1x1 tile variant. Its tile pivot `(24,24)` must not be reused for this SpriteRenderer.
2. `Assets/Art/StudentLifePixels/Sprites/old_town_ledger.png` is a flat 24x20 book, not a stand. Direct reuse would retain the ambiguous book marker.
3. Modern candidates were searched first. `Assets/Modern_Exteriors_32x32/Modern_Exteriors_Complete_Singles_32x32/ME_Singles_Garage_Sales_32x32_Signboard_1.png` is a broad horizontal text sign, not a small help stand. The filename search is not an exhaustive atlas audit.
4. Approved `Assets/Art/UrbanStreet/Buildings31/neighbor_request_box.png` has an envelope/drop-box silhouette and a tall cabinet footprint; directly duplicating it suggests the separate neighbor-help object. `daily_plan_board.png` is a wide three-card planning board; its interface meaning and silhouette should remain distinct. Both support the palette, not this geometry.
5. The reference is never delivered directly. Qwen must replace the open two-page book with one simple navy clipboard containing one blank ivory sheet and a small brass top clip. Avoid retaining a book seam, glyphs or page lines.

## Prepared files

- `output/design-comfy-20261031/submit_first_guide_help_stand.py`: guarded one-job submission; verifies current SSH identity, hashes the reviewed source, prepares an integer-nearest magenta reference, uploads only its unique input, and submits/reuses only its named job.
- `output/design-comfy-20261031/town-first-guide-help-stand-v1-workflow.json`: Qwen2511 graph, unique output prefix, seed `3011019216` (seed does not claim a queue number).
- `output/design-comfy-20261031/fetch_first_guide_help_stand.py`: verifies current SSH identity and fetches only the recorded prompt's history/raw output.
- `output/design-comfy-20261031/first-guide-help-stand-generation-scope.json`: prepared-only geometry/source/provenance contract. Actual generation number comes from the eventual submission response.

## Task 1: Parent review and one isolated generation

- [ ] Review this interpretation, existing reference and staged positive/negative prompts. The single object is a small household-help request stand with a blank sheet, no characters/text/icons, no parcel or letter slot.
- [ ] When the parent elects to launch, run `python -X utf8 output/design-comfy-20261031/submit_first_guide_help_stand.py`. Expected: current `jdyj` / `jdyj-FAEX9` identity verification and exactly one own-name prompt response. No other job is cancelled or altered.
- [ ] After the job completes, run `python -X utf8 output/design-comfy-20261031/fetch_first_guide_help_stand.py`. Expected: only own history/workflow/submission/queue-before/raw files fetched.
- [ ] Inspect `town-first-guide-help-stand-v1-raw.png` before export. Reject book seams, handwritten-like marks, multiple papers, detached pedestal, drop-box slots, extra objects or ground shadow. Preserve a rejected raw and its reason; do not manually repair motifs.

## Task 2: Technical export and review

- [ ] Parent stages a uniquely named export script following `export_alley_exploration_crate.py`, changing only this job's source/output names and the 48x64/40x40/bottom4/pivot24,8 contract. Read raw SHA at execution and generation number from its own submission; do not inherit crate215 provenance.
- [ ] Export using only technical matte, largest connected object, nearest 2px grid and at most31 visible colors; retain raw/workflow/submission/reference hashes. Review native48x64 and nearest-enlarged transparent/checkerboard previews. A muddy or illegible blank-sheet stand remains unimported.

## Task 3: Parent-owned integration and player verification after acceptance

- [ ] Through the existing Unity-authorized path, import only the accepted new sprite/address, then bind only SpriteRenderer1401220947. Check the exact saved scene reference plus all geometry/sort/action/collider values above.
- [ ] Run focused sprite geometry, isolated binding and existing help-action tests. Confirm the shared book and other help/discovery visuals remain unchanged.
- [ ] Enter Town through the normal player flow, walk to `(14,2)`, inspect the stand at gameplay scale, interact with E, and record the action result/resource/outcome changes. Inspect prompt overlap, occlusion and floor alignment. Repeat from persisted state without reset if making a persistence claim.
- [ ] Only after accepted import and native evidence should the parent update inventory/count/archive delivery metadata. Preparation contributes zero accepted images and makes no gameplay-completion claim.

## Source criticism and verification limitations

- The GDD supports household help but does not name a clipboard/pedestal. This design is a scoped visual interpretation, not proof of a fully described household quest.
- The current prompt is humanized English `[E] First Guide Errand`, priority85, offset `(0,1.05,0)`; the art task does not change localization or text.
- `TownHelpActionInteractor` uses an in-memory sequence for request IDs and resets it to zero on `Bind`; `TownHelpActionRunner` rejects already persisted request IDs. A fresh instance can reuse `town.help.first-guide-errand:0`. This is a source-level cold-reload risk, not an observed native failure. Do not claim repeat-after-reload success without testing or silently fix it inside this art task.
- No current SSH/model availability, generation, native gameplay, outcome application or scene integration was verified during preparation. No Unity/Assets files are written by the prepared scripts.
