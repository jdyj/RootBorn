# Placed furniture ground depth correction

While checking tall school bookcase placement, source inspection found AuthoredHousePlacementBinding creates the user furniture Tilemap with a constant renderer sortingOrder10000. Individual Tilemap mode only sorts within that order; it cannot put different placed furniture around the player whose GroundDepthSort changes10000 minus worldY*48. Existing growth/activity native tests checked texture/collision/save, not local front/back visual traversal.

Keep the logical Tilemap, placement surface, footprint collision map and save records unchanged. Add an owned runtime presentation component only to the authored user furniture map. It keeps that logical renderer hidden and mirrors occupied visual cells into per-cell SpriteRenderers at the same tile anchor and transform, with material/color/layer preserved and floor-depth sorting. Listen to Tilemap changes and refresh once in LateUpdate, including source transform changes. Remove stale visuals after move/delete/reload and restore the source renderer when disabled. No furniture-ID branches, no new colliders or save fields.

- [x] Capture native placed renderer order evidence.
- [x] Write focused red unit tests for opposite-side depth, tile-anchor position, unchanged logical/collision data, removal/replacement and disable restoration.
- [x] Implement generic presenter and attach only to authored user furniture creation.
- [x] Run unit/placement/session regressions and actual-input front/back traversal around the newly placed bookcase.
- [x] Rebuild content only if runtime dependencies require it; retain prior bundle texture evidence.

Completed: red5/5 then green5/5, full related32/32. Actual packed save entry and input-only drawer front/behind traversals verified original2-cell collisions and bundle source. Tall bookcase front verified; direct bookcase rear target was blocked by the existing desk, so rear traversal uses the same generic presenter on the adjacent drawer. Remaining same-session new placement event verification will be covered by crop-basket placement next. No Windows binary rebuilt.
