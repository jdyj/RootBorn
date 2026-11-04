# Neighborhood corner-store location marker art

Continue user-approved ComfyUI urban oak/navy/ivory dimensional direction and background/architecture work. Story direction Day2 names restaurant/shop neighborhood visits. Read-only saved Town audit found LocationVisit_location_corner_store at(54,7) currently uses school lockers96x96 as its visual. Replace only this location's visual with a dedicated small neighborhood grocery stall/kiosk, preserving all location/activity references, trigger0.7x0.7, GroundDepthSort, SortingGroup, original world placement and no new doorway/portal. This is the existing visit point, not a new walk-in interior.

Use approved school facade as stylistic reference; front-aligned compact awning, recessed storefront/display, oak/ivory molding and navy canopy. No characters or generated lettering. Native96x96 canvas,48PPU,pivot48,4; visible object within88x86, floor baseline4. Asset separate from original lockers; do not change global lockers sprite or unrelated review scenes. Existing visual local Y0.083333 retained.

- [x] FEVM Comfy original source workflow and native export review.
- [x] Backup Town and preserve all nonvisual fields; targeted authoring via Unity MCP.
- [x] Original trigger/collider/portal/other sprite signatures retained; no scene/entity-ID production code branches.
- [x] Bundle exact pixels plus actual cold-slot input-only walk, visible framing and existing visit interaction.
- [x] Package and record evidence/limitations; no full-design-complete claim.

Verified: latest49-fixture169-test suite passes. Door content build270.7872862s,1/1 exact bundle; original3 tile bindings,2 in camera, actual E exit/automatic walk reentry. Store actual pointer activity3→2TP and cold reload retains request. No source scenes/tile assets changed during door replacement. QA defaults/builder0 restored; archive77 exports.
