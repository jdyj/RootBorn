# School wall window continuation

Goal: Restyle the existing school window using FEVM ComfyUI in the already approved urban oak/navy/ivory dimensional pixel style, then review placement in the free classroom wall gap.

The user has approved this art direction and requested autonomous design, integration and testing. No additional design approval checkpoint is needed.

Keep existing GUID, 64x64 canvas, pivot (32,34), PPU48, binary alpha, Point filtering, no mipmaps and no compression. Use original front elevation as reference. Target visible width48-54px, recess blue glass, raised oak and ivory molding, clean navy outlines. No characters, scenery or illegible lettering.

- [x] Submit one original-window-reference Qwen2511 Comfy job with idempotent submission.
- [x] Inspect raw generation and native 64px nearest/palette export; reject perspective/noisy contours.
- [x] Preserve original png/meta/scene backup; import through Unity MCP and verify metadata.
- [x] Try decorative placement near (5.25,-1.33), with no collider/interactor, checking clock/chalkboard/HUD clearances before save.
- [x] Build Addressables, cold-slot native School entry, inspect actual pixels and bundle hash. Record limitations separately.
- [x] Update art manifest/archive and design coverage only after verified.

Accepted Comfy142; source64x64/pivot32,34/GUID/address retained. Existing School preservation14/14, collider signatures identical, no collider/interactor added. Build265.9918307s; independent actual bundle1/1 hash+geometry+Point/no-mips. Actual cold-slot entry/WASD112nodes and same saved decorative slot, native neighbor and8HUD clearance pass. Original QA source copy hashes unchanged; storage/default builder0 restored. Package74 exports.
