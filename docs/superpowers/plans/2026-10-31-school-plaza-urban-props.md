# School plaza urban props integration plan

Continue the approved ComfyUI urban pixel-art direction. Native Town inspection places the School portal at (43,30) and confirms the proposed prop footprints are clear. Use the reviewed front-facing bench, lamp, bin and corrected planter. No characters or story changes.

Use existing FurnitureDefinition/Tile assets and the registry. Author one small Tilemap per prop under a dedicated saved Town Grid root, plus one invisible collision Tilemap. This uses the existing static scene art architecture and introduces no new runtime entity code. Each prop's render order follows its ground contact Y, in the existing 10000/48 depth band. The occupied collision cells come from the registered furniture definition.

Proposed anchor cells: bench (38,26), lamp (40,27), planter (47,26), bin (50,26). Bench/planter occupy 2x1 cells; lamp/bin 1x1. Keep the school approach at x43 clear. Tilemap exports use first-cell pivot (24,24); their visible floor line is also y24. Sprite and Tile/Definition addresses are independently registered. Saved Town decorations use scene Tile references, not a dynamic bundle skin; report this accurately.

- [x] Back up saved Town and record an original-content signature excluding the new owned root.
- [x] Add failing checks for four sprite import settings, registered definitions/footprints, saved Tile references, six collision cells and unchanged original Town geometry/portals.
- [x] Import the four reviewed Tilemap exports with Point/48PPU/no mipmaps; register definitions/tiles and addresses.
- [x] Author only SchoolPlazaUrbanProps in saved Town through Unity MCP. Reopen and inspect references and sorting.
- [x] Enter through actual save/UI flow, walk around the props and school approach; test lamp blocking and front/back sorting, and re-enter School.
- [x] Build content once for the school floor and street props, verify exact bundle pixel hashes and dependency loads.
- [x] Refresh manifests, preview/archive and restore the default play builder/storage routing.
