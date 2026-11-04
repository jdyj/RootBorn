# School Pixel Cleanup and Placement Plan

**Goal:** Correct the muddy classroom assets at their native pixel resolution and deliver tested pivots, floor footprints, tabletop attachment points and placement examples, with no characters.

**Authorization:** The user approved correcting the art and explicitly requested placement/pivot checks. Continue inline without another design approval gate. Preserve v2 as the comparison reference.

**Architecture:** Author deterministic native-pixel art recipes and execute all image production on FEVM in `/home/jdyj/farmer-art/school-pixels-v3-20261025`. Export exact integer-coordinate RGBA PNGs, a small chosen material palette, metadata and a standalone interactive placement reviewer. Download into `output/assetpacks/school-pixels-v3-20261025`. Existing Unity scenes and other ComfyUI queues are outside this artifact batch.

**Tech Stack:** FEVM existing Docker image with Pillow/NumPy; local browser for actual placement-review interactions. No averaging resize or automatic global quantization in final sprite production. Preview enlargement is integer nearest-neighbor only.

- [x] Rebuild the desk at 64x48. Inspect native and 4x views and compare against v2 before extending the style.
- [x] Apply the same material ramps and outline rules to chairs, drawers, bookcase, books and wall fixtures. Use a rear-facing classroom chair as well as a front-facing variant. Also export the school exterior in the same palette.
- [x] Record each asset's PNG top-left pivot, Unity normalized bottom-left pivot, placement surface, floor footprint, support polygon and optional attachment points. A wall fixture has a mounting anchor, not a fake floor pivot.
- [x] Compose a classroom from the exported files. Tabletop objects inherit the furniture group's sort anchor, while furniture groups sort from back to front by floor Y.
- [x] Produce a pivot/footprint/socket overlay, native/4x comparison, metadata and a self-contained placement reviewer. Validate syntax and embedded data. Review movement and depth using PNG compositions.
- [ ] Test dragging, snapping, parented-book movement and overlapping furniture order through the actual UI. Blocked: browser inventory is empty and IAB unavailable. Local preview server launch also rejected by automatic approval review as `blocked by policy`. Do not report browser interaction verification as completed.
- [x] Verify binary alpha, exact palette membership, integer coordinates, valid attachment containment, coordinate conversion round trips, layer recomposition, deterministic reruns and download SHA256 hashes. Package the reviewed output.

**Project-specific discovery:** `FurnitureSpriteAutoImporter` forces 48 PPU and lower-left cell-center pivots, so native floor pivots would be overwritten. Added six `tilemap48/` variants with transparent padding, explicit footprint suffixes and separate placement/sorting anchor metadata. No Unity assets, importer, registry or scenes were modified.

**Evidence:** Six acceptance tests pass. All 36 regenerated output files match byte-for-byte. All 49 downloaded manifest entries match SHA256. ZIP contains 50 files / 32 PNGs; SHA256 `87ff74dbffb7ec3c32ac1a9390fe5a16c49c43588d15a18f5f0d30f40299d2fb`. Output: `output/assetpacks/school-pixels-v3-20261025/`.

**Acceptance boundary:** These are reviewed image assets and a placement demonstration. Do not claim Unity PlayMode or saved scene integration without doing it. Physical collision sizes are proposed footprints for placement, not engine-tested colliders.
