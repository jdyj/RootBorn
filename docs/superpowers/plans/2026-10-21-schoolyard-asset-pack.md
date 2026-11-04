# ROOTBORN Schoolyard Asset Pack Implementation Plan

> Execution: inline artifact production on Strix Halo. The user approved the scene's atmosphere and composition and requested continuation using Strix Halo. Existing Unity scenes and other ComfyUI jobs are outside this production batch.

**Goal:** Produce an independently composable first schoolyard asset pack from the approved scene, including terrain, transparent static objects, student sprites, reproducible recipes and visual evidence.

**Architecture:** Preserve the approved 384x256 source. Perform extraction, terrain construction, image generation and export on `jdyj-FAEX9` in `/home/jdyj/farmer-art/schoolyard-pack-v1-20261021`. Download results to `output/assetpacks/schoolyard-pack-v1-20261021`. Terrain uses the existing Town LDtk grid of 32 pixels. Object cutouts retain source pixels inside explicit masks; terrain and fence pieces use deterministic pixel recipes and the source palette. New student directions, when generated, are candidates with separately recorded model provenance.

**Tech Stack:** FEVM rootless Docker, existing ROCm ComfyUI image, Pillow/NumPy for explicitly requested server-side asset processing, existing Qwen Image Edit 2511 for a front-facing student candidate.

## Deliverables

- `sprites/`: school facade, complete tree, bench, horizontal/vertical fence pieces and a back-facing student, each as an independent RGBA PNG.
- `tiles/`: 32x32 grass variants and the 47 valid eight-neighbor path masks. Bit order N/E/S/W/NE/SE/SW/NW = 1/2/4/8/16/32/64/128; diagonal bits require both adjacent cardinal bits.
- `terrain-atlas.png` and `terrain-atlas.json`: fixed 32px slicing and mask lookup.
- `layers/` and `scene-layout.json`: separately composited terrain, buildings, props and student, with positions and pivots.
- `scene-preview.png`, `objects-contact-sheet.png`, `terrain-contact-sheet.png`: actual server-produced visual evidence.
- `manifest.json`, `validation.json`, `import-settings.json`, `schoolyard-pack.zip`: provenance, checks, explicit Unity import recommendations and portable deliverables.

## Execution Steps

1. Confirm the currently saved LDtk tilesets use 32px grids. Record the source PNG SHA256 and user approval scope. Create a dedicated remote folder and copy immutable inputs.
2. Write the asset recipe and producer. Extract only visible complete static silhouettes; do not describe a flat sprite as a complete character rig or a disassembled building. Fill the terrain from source-derived palette colors and generate all valid path masks.
3. Run the producer on FEVM CPU and inspect object cutouts over a checkerboard and contrasting backgrounds. Correct mask recipes at source when an object clips, leaks background or has accidental holes.
4. Assemble a new 16x12 tile schoolyard using exported sprites and tile IDs. Verify that layer composition reproduces the scene exactly and that sprites can be independently repositioned.
5. Use the extracted student as an actual image input to an isolated FEVM ComfyUI Qwen job for a front-facing static candidate. Preserve the original back sprite. Log the graph, input hashes, seed, model names, execution host and elapsed time. Do not overwrite or submit to the other session's queue.
6. Validate file decoding, dimensions, alpha values, source palette membership, path-mask count, all compatible shared tile edges, atlas rectangles, deterministic reruns and downloaded hashes. Visually inspect the scene and contact sheets before packaging.

## Scope and Acceptance

- The first pack is complete when exported files can be recomposed into the demonstrated scene and the stated artifact checks pass.
- A student front-facing generation is a new visual candidate, not implied approval of face design. Walking animation and complete layered anatomy are separate stages.
- No Unity runtime integration is claimed. Any future scene or prefab integration must follow `AGENTS.md`, including saved-state and actual player-flow verification.
- New diffusion output, source-pixel extraction and CPU procedural construction must remain clearly labeled in the manifest.

## Execution Record

- Exported 9 independent static sprites (including three grass/flower details), 51 terrain tiles, the atlas and separately composited scene layers.
- Produced a new front-facing student candidate with Qwen Image Edit 2511 on FEVM in 329.556 seconds; retained its workflow, raw output and reference hashes.
- Corrected silhouette background remnants, vertical fence perspective, ground anchor positions and front-shirt palette mapping after inspecting actual PNGs.
- Validated 11,360 shared tile edges, including exhaustive 3x4 local neighborhoods in both orientations, all 51 atlas slices, binary transparency and exact scene recomposition.
- Final packaging verifies deterministic reruns, all PNG decoding and ZIP integrity. Local transfer verification compares each file to the remote SHA256 manifest.
- Scope remains exported art and a demonstration composition; Unity scenes were not modified.
