# House missing decoration art repair

Native House audit found fourteen saved decoration cells whose embedded 4x4 Sprites have null textures. They render as pink blocks. Pure diagnostic reconstruction of Profile_HouseOffice with its saved seed1205 identifies four plants, one south-facing compact sofa, three chairs, three desks and three computers. The seven distinct art variants are plant, sofaSouth, chairWest, chairSouth, deskWest, deskSouth and computer. Preserve their positions and facing semantics.

Continue the approved oak/navy/ivory ComfyUI style. Generate two reviewed sheets through FEVM ComfyUI #122/#123, reject incorrect shapes/projections, and export first-cell pivot(24,24),48PPU,Point,binary-alpha variants. Keep exactly the original fourteen decoration occupancy cells and original solid colliders/door routes. Use saved Tile assets and the existing directional TileSet SO slots; no runtime entity-ID branch or regenerated room application.

- [x] Identify null textures and reconstruct semantic kinds using a pure diagnostic map; do not call Apply/regenerate or move the player.
- [x] Back up House/tileset/floor assets, record saved cells/non-decoration geometry and source hashes, and verify the generated reference geometry against the saved scene.
- [x] Review seven generated variants at final pixel size. Register new Sprite/Tile assets and bind the corresponding directional tileset fields.
- [x] Replace the fourteen broken Tile references and reset their old placeholder tints. Keep their occupied cell set for authored placement validation. Disable only the old decoration renderer and draw each fixed prop through an owned per-prop Tilemap with ground-contact depth sorting.
- [x] Reuse the already reviewed ComfyUI oak floor Sprite in the existing HouseRepair_floor Tile, preserving its GUID and380 saved ground cells. Normalize the old1.5 scale compensation (32px at48PPU) to1 for the new48px at48PPU Sprite; this preserves one-cell world coverage. No new redundant floor texture.
- [ ] Verify original walls, collision, portals, spawn, interactors and user furniture save records are untouched. Enter through real UI, inspect room, test front/back sorting and a home exit/re-entry route.
- [ ] Build/register and verify bundle pixels/dependencies; restore QA defaults, refresh review records and package.

The repaired fixed scenery uses authored scene Tile references. Independently addressable assets must be checked separately and not described as dynamic scene skins. Original user save files remain untouched; use the existing isolated activity QA save. New raster motifs must come from FEVM ComfyUI; nearest sampling/palette/matte processing only.

2026-10-31 checkpoint: 19/19 House art, persistent tile, and placement label tests passed. Native entry through actual slot UI confirms all fourteen durable textures and existing saved furniture. Local per-prop traversal remains incomplete: original House collision layout blocked the proposed routes. Do not count the far-away SortingGroup sample as a front/back gameplay pass. Bundle build is in progress.
