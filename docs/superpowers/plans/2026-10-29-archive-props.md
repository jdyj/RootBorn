# Archive Props Implementation Plan

Goal: Continue the approved ComfyUI pixel-art direction with archive investigation props required by the story design. No new characters.
Architecture: Keep existing school desks/chairs/bookcases. Generate only missing archive variants: document box, ledger stand, locked record cabinet, archive key tray. Export native sprites with explicit floor-contact pivots, Point sampling and shared atlas where appropriate. Preserve existing interactions and registry data.

- [x] Audit existing school/Modern props and current archive activity presentation.
- [x] Generate one four-prop ComfyUI sheet on Strix Halo; preserve other queue jobs.
- [x] Inspect output and revise if perspective, separation or silhouette fails.
- [x] Prepare transparent native sprites and pivot/footprint manifest; validate alpha, padding and pixel grid.
- [ ] Test rendered placement and overlap; integrate only assets with verified fitting gameplay surfaces.
- [ ] Verify saved references, actual player flow and screenshots. Report any Unity connection limitation honestly.

Sources: docs/story/rootborn-city-life-story-direction.md (Library Archive, locked archive door, old records), docs/story/rootborn-career-growth-design.md. Existing generic furniture is reused rather than duplicated. Staging lives under output/design-comfy-20261029; no broad checkout operations.

Result: four candidate sprites, a 512x256 atlas, Tilemap-aligned variants and four passing file tests. v1/v2 rejected; v3 retained. Unity player verification and saved registry/Addressables integration remain pending due to pre-existing editor crash; no Assets files were changed.
