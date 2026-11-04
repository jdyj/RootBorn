# School floor art integration plan

Follow the approved ComfyUI pixel-art direction. The native School art audit found that the classroom props use front-facing school pixel sprites while the ground still repeats an older LDtk parquet tile. The new floor will use a calm oak plank texture generated on FEVM ComfyUI, with 48 pixels per one-world-unit cell.

- [x] Review the generated floor as a 48px tile and repeated texture; reject noisy, diagonal, framed or visibly broken-edge candidates.
- [x] Back up the saved School scene and record the 304 occupied ground cells, other Tilemap references, world portal/spawn data and solid collider geometry.
- [x] Add a saved-scene regression for the new tile, pixel import settings, cell coverage and unchanged non-ground structure.
- [x] Import a Point-sampled, opaque 48x48 sprite and Tile SO with no collider; register their Addressables entries.
- [x] Replace only occupied SchoolGroundTilemap cells in the saved School scene through Unity MCP/Editor APIs. Reopen and inspect the saved scene.
- [x] Re-enter through the actual school portal, walk into the classroom and inspect the Game View. Verify the door route and wall/collider geometry remain intact.
- [x] Build and load the new floor Sprite/Tile Addressables and record exact pixel hashes. The authored School ground remains a conventional scene Tile reference; do not claim it is dynamically loaded from bundles.
- [x] Refresh provenance, previews and archive. Keep isolated QA saves and restore default storage routing afterwards.

No changes to story, characters, progression, furniture layout, walls or movement rules. All new raster motifs come from FEVM ComfyUI; export processing is nearest sampling and palette reduction only.
