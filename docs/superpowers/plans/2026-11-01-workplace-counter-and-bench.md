# Workplace visuals after store goods verification

Continue the approved urban oak/navy/ivory/brass dimensional pixel art without
characters. Current Town store work(54,2) and workshop work(74,2) each reuse the
same teacher_desk sprite. Give their existing activities distinct visual roles:
generate one compact cashier counter for store work;reuse the accepted existing
workshop_bench sprite for workshop work. Do not create new work definitions,
portals,interiors,colliders,or gameplay items.

Generate counter in FEVM ComfyUI from approved front-aligned teacher-desk material/
geometry reference: wide oak worktop,two navy lower panels with stepped ivory
molding,small connected ivory/brass POS terminal with blank dark screen. Keep
simple broad square clusters and dimensional upper-left light,no brands,text,
characters,display shelves,food piles,room,floor or cast shadow. Compare generated
front geometry and native size;reject rotated/cluttered variants rather than paint.

Export96x64/48PPU/pivot48,4/binary alpha/2px clusters/<=32colors. Visible width<=88,
height<=56,bottom transparent margin4. Match teacher_desk floor and width. Existing
workshop_bench96x96/48PPU/pivot48,4 has88x56 visible silhouette and bottom margin4,
so reuse preserves floor without editing transforms. Its existing address is
sprites/town/comfy31/workshop-bench.

Only after store-goods native/cold verification and109 delivery checkpoint:
back up Town and protected sources;red-test two renderer references and geometry;
import reviewed counter viaMCP,register consistent address;replace exactly two
saved Renderer.sprite references. Preserve root andVisual transforms,sorting,
0.7x0.7 triggers,Interactor workSO,existing visit points/global teacher_desk PNG/meta.
Expected rootSortingGroup9904 and visible floorY2.08333337 remain exact.

Review semantic saved-scene diff and actual first/cold world images,depth routes,
normal E/minigame interaction,bundle dependency pixels and unchanged nonvisual
state. Use copied QA saves and real input,no injected progression/pose. Keep
source studies out of game export count until integrated and verified;restore
builder0/defaults. No whole design-completion claim.
