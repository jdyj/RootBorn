# School pixels: actual Unity integration and play verification

User-authorized scope: integrate the approved v4 pixel assets into ROOTBORN and test the actual game. Do not create character artwork. Preserve the existing dirty House scene and placement overlay changes.

## Implementation sequence

1. Establish a live Unity Editor connection and native Game View control. Diagnose the startup asset-database memory failure before making invasive cache changes.
2. Copy the approved native PNGs and 48-pixel furniture adapters into a distinct SchoolPixels asset folder. Import through Unity with Point filtering, no compression, no mipmaps, and documented pivots. Preserve source PNG hashes.
3. Use the existing FurnitureSpriteAutoImporter and FurnitureDefinition registry to register the eleven floor-furniture adapters, with school-prefixed IDs and explicit footprint dimensions. Keep wall and tabletop sprites out of the floor furniture collision catalog.
4. Create a saved SchoolPixelReview scene through Unity Editor APIs, using the real Player prefab and existing placement system. Reuse the approved classroom arrangement, distinguish floor collision footprints from tall visible art, and isolate placement persistence from normal game saves. Any required new C# must first have failing tests and be created through script-update-or-create.
5. Run targeted EditMode and PlayMode checks for import settings, pivots, furniture definitions, collision footprints, UI selection/placement, and persistence. Verify compilation sourceFiles for new scripts.
6. Use actual Game View input to select and place furniture, walk around objects, inspect overlaps/occlusion, save and reload. Capture visual evidence and runtime Tilemap/Renderer/UI state. Reopen the saved scene and verify serialized art references independently of runtime setup.

## Acceptance evidence

User steering: Unity was manually closed because foreground screen testing interfered with another game. Continue hidden batchmode rendering and InputSystem-driven gameplay tests; do not bring an Editor window to the foreground. Report the foreground UI check as unverified.

Addressables follow-up: explicitly register the 36 school images, 11 furniture definitions, and 11 tiles in the existing local groups, preserve the global registry address, and verify asynchronous loading and release. Integrate repeatable registration into Wire All. Run a local Addressables content build and distinguish it from a standalone player build.

- Approved source image hashes match imported source PNGs.
- 48 PPU adapter sprites retain pixel dimensions; Tilemap anchor pivot is (24,24) pixels, distinct from floor sorting pivots.
- Eleven new data-driven furniture definitions are registered; wall/tabletop images do not create incorrect floor blockers.
- Actual player-facing placement input reaches the selected cell and clears its preview overlay; no stale sample/debug art is visible.
- The real player moves, collides only with the intended floor footprint, and appears on the correct side of tall furniture.
- Save/reload uses an isolated QA slot; the saved scene and asset references survive reopening.
- Report only verified results. Startup, compile, or UI blockers must remain explicit.

## Initial environment findings

- Unity 6000.3.13f1 project at C:/Users/jdyj/farmer.
- First Editor launch failed in DatabasePrefetch.cpp with an out-of-memory allocation. No cache deletion or unrelated process termination performed.
- Library/ArtifactDB is 8 GiB and SourceAssetDB is 4 GiB; these are caches, not source images.
- The installed unity-mcp-cli.cmd supports project-specific HTTP auto-discovery; no global MCP configuration change is needed.
- Native computer-use window enumeration is working.
