# Inventory Browse UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Continue under the user-approved ongoing work; do not stop to ask whether to continue.

**Goal:** Make the existing urban inventory search/category controls useful, show all32 model slots, and display a reviewed Comfy empty-state drawer without changing any items or progression.

**Architecture:** Keep ModernUiInventoryPanel and the shared Addressables theme/skin. Add a pure InventoryUiFilter UI helper; keep item categories and all game data unchanged. Pool32 existing slot views rather than destroy/recreate them on each typed character. Legacy ItemCategory has Resource,Tool,Misc,Seed (no Consumable); tabs therefore mean All,Tools,Resources+Seeds,Misc. Keep the established inventory-consumables sprite key as the backwards-compatible water/resource icon alias.

**Tech Stack:** Unity6000,UGUI/InputSystem,existing GameText/UrbanUiIcon/ModernUiTileImage,FEVM ComfyUI193,existing qa_step.py/run_suite.py wrappers.

---

## Constraints and files

Do not modify Unity while the preceding school-chalkboard build/native worker is running. Finish its actual native/cold/source checks and100-image checkpoint first. Shared dirty checkout: no reset/commit. All Assets C# writes must use MCP update tools;stage .cs.txt under output/design-comfy-20261031.

- Create Assets/Scripts/UI/Modern/InventoryUiFilter.cs: pure name/category predicate,never mutate Inventory or ItemDefinition.
- Modify Assets/Scripts/UI/Modern/ModernUiInventoryPanel.cs: actual InputField,owned data filtering,32 pooled slots,empty-state presentation,localized selected-category label and header Close button.
- Create Assets/Tests/EditMode/UI/Modern/InventoryUiFilterTests.cs and UrbanInventoryBrowseTests.cs.
- Migrate only old hard-coded20-slot UI assertions to the real Inventory.MaxSlots32 contract;retain slot geometry,frame,icon,count,popup behavior tests.
- Add Assets/Art/UI/UrbanMolding/inventory_empty.png64x64/16PPU/pivot32,32/Point/no-mip/uncompressed/binary2px/<=32colors from reviewed Comfy193.
- Append theme inventory-empty (23->24),address sprites/ui/urban-life/inventory-empty. Reuse existing close glyph183/search glyph179/frame;no duplicate loaders/character art.
- Append6 bilingual catalog entries549->555:ui.inventory.search,ui.inventory.empty,ui.inventory.category.all/tools/resources/misc. Korean texts:이름으로 검색,표시할 아이템이 없어요,전체,도구,자원과 씨앗,기타. English:Search by name,No items to show,All,Tools,Resources & seeds,Miscellaneous.
- Scene,registry,item IDs/categories/costs/stacks,all previous549 translations and23 theme entries stay exact. Delete/advanced-filter controls retain their existing presentation-only scope;no item use/delete/reward path.

## Task1: Preserve context and write failing tests

- [ ] Snapshot production UI/helper absence,catalog/theme/meta and all scene/data/art originals after the School100 checkpoint. Copy its earned QA save to saves-inventory-browse-verification;hash original root and never route user saves there.
- [ ] Pure tests:All covers every category;Tools only Tool;Resources includes Resource+Seed;Misc only Misc;null false;query trims/case-insensitive and uses localized display name;unknown tab safely All;item data byte/field identity unchanged.
- [ ] Presenter tests:real InputField and32 slots/7 rows under same5-column/3-visible-row frame;tab plus query combine;match in slot31 discoverable;zero results show decorative empty icon+localized hint and no clickable stale popup;clear restores ordered originals;owner rebind clears previous query/view;slot GameObjects reused across filtering;Count labels/callbacks updated without stale item;KO/EN labels/font inside molding;header Close hides panel without touching item counts/student DTO;focused text prevents I/C/Tab shortcuts using existing UiTextInputFocus contract;retain/run TextInputGameplayFocusPlayModeTests for actual I/C/Tab/Escape routing.
- [ ] Run new fixtures through run_suite.py before implementation and inspect every failure. Expected missing helper/InputField/32-slot/empty-state/Close;do not weaken existing data assertions.

## Task2: Accept art and author assets

- [ ] Fetch Comfy193 history/workflow/raw via existing strict SSH helpers;inspect raw and native64px export. Reject malformed/opening-unreadable candidates. Only matte/crop/resample/palette/binaryalpha/nearest processing;no painted motifs.
- [ ] Use MCP author script to import accepted PNG,save16PPU center32 and independent Sprites entry,append inventory-empty to existing theme. Original23 entries and skin colors/definitions remain exact.
- [ ] Append6 text entries using a serialized catalog author script,not full replacement. Verify all old549 values and all other fields exact.

## Task3: Minimal implementation after red tests

- [ ] Implement InventoryUiFilter.Matches(ItemDefinition,int,string) using UI tab/category groups and GameText.Get(item.DisplayKey).IndexOf(query.Trim(),StringComparison.OrdinalIgnoreCase);never branch on entity IDs or invent a Consumable category.
- [ ] Capacity uses Inventory.MaxSlots32;rows=(capacity+4)/5,columns5,visibleRows3. Keep existing frame/slot/stack-count positions,168px slots and16px spacing. Scroll content covers all7 rows and the last2 cells;no extra storage capacity or reordered model slots.
- [ ] Build32 slot views once;Refresh only rebinds icon/count/callback/visibility. Clear listeners/count/icon when rebinding empty slots. Filtering scans model slots in stable order;query never changes stored counts/order. Close stale detail popup on query/tab/owner change.
- [ ] Search row keeps Comfy17932px glyph and existing slot backplate;add single-line legacy InputField with24px shared Korean-capable font,64char limit,non-raycasting label/placeholder and left padding leaving icon clear. onValueChanged changes query and Refresh only;no per-frame allocation or new Addressables request.
- [ ] Add category label in unused header space and48px upper-right Close using Comfy183. Close releases input selection;query resets on Hide/owner change. Do not change UiTextInputFocus unless a failing test/native typing proves it lacks legacy InputField support.
- [ ] Add empty-state callout under viewport using approved panel/slot skin,96px rendered Comfy empty icon and localized hint. It is non-raycasting. Hide grid/scrollbar only when zero real matches;retain32 pooled children. Keep model data immutable.
- [ ] Refresh/Show/query/tab/model events re-localize built labels;no automatic live-language UI is introduced;unsubscribe source callbacks on rebind/destroy as existing panel already does. Verify pooled object identity and no stale callbacks.

## Task4: Regression/source/bundle

- [ ] Run new filter/presenter fixtures and existing inventory frame/category/control/count/detail-close,input-router,text-focus,theme lifecycle fixtures. Inspect old20-slot expectations and migrate only capacity-related assertions to Inventory.MaxSlots.
- [ ] Run named broader fixture set based on school-chalkboard-final-fixtures.json plus new fixtures;check fixture-name presence (run_suite silently ignores missing names). Preserve any unrelated house-move failure and its detailed diagnostic;do not claim it fixed by UI work.
- [ ] Source proof:all source scenes/data/items/shared icons exact;old549 catalog and23 theme rows exact;only append6/1 plus reviewed raster and UI/helper behavior. Build Addressables after tests;record/imported GPU hashes,compare actual bundle bytes/dependencies and all555 translations24theme entries/2 current item icons.

## Task5: Actual input and cold verification

- [ ] Menu Load through copied QA root;actual I opens inventory. Capture original counts/whole student DTO and actual bound owner. Expected earned save has Allowance490,Materials75;read actual values rather than assume.
- [ ] Actual mouse4 tabs:All2,Tools0,Resources1,Misc1 for actual Allowance(Misc) and Materials(Resource). Verify all32 pooled slot children,empty tray/hint,selected category and all glyphs/font bounds. Wheel-scroll All to bottom and verify last row inside masked viewport;unit fixtures cover32 populated data slots.
- [ ] Actual mouse focuses SearchField;send InputSystem text events for 자재 and verify only facility materials;no-match text shows empty state;Ctrl+A/Backspace clears/restores both. Type I/C while focused and verify no other panel opens/player moves. Use actual pointer header Close;no direct Show/SetText/Interact/state injection.
- [ ] Reopen and both item popups/actual detail Close remain correct. Full student DTO and independent actual inventory counts/order unchanged throughout. Stop/start/cold menu Load and repeat category/search/Close;query is UI-session state,not saved data.
- [ ] Restore builder0 and QA overrides;hash original earned source and all preserved files again. Inspect actual screenshots before setting verified flags or counting the101st game review PNG. Package accepted art and evidence only;3 unintegrated facade studies remain separate. No full-project/GDD/executable claim.

Verified checkpoint101:410/410 EditMode across103 fixtures;5/5 PlayMode across2 fixtures. Real CUA Unicode text + mouse tabs/query/clear,32 pooled views,wheel slot31,detail/header close/reopen and cold full student/inventory DTO.12/12 actual bundle GPU hashes;555 bilingual values,24 theme deps,2 actual item chains.83 original paths and earned QA source hashes exact. Empty scrollbar LateUpdate bug reproduced red thenfixed by relinquishing ScrollRect while empty;camera test fixture activated placement-control ownership before normal input assertions. Builder0/defaults restored. No full-project or exe claim.
