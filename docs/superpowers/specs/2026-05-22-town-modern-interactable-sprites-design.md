# Town Modern Interactable Sprites Design

Date: 2026-05-22

## Goal

Town currently exposes many player-facing interaction targets, but most of them read as placeholder markers or empty sprite slots. The first visual slice should make every Town interaction target distinguishable in play by assigning a suitable Modern-series sprite to each target.

This is a visual clarity pass, not a full Town rebuild. The player should be able to walk through Town, see NPCs, portals, boards, activity points, location points, and help/discovery points, and understand that they are different interactable objects before pressing the interact key.

## Constitutional Constraints

- Use Modern-series assets first:
  - `Assets/Modern_Town/`
  - `Assets/Modern_Exteriors_32x32/`
  - `Assets/Modern_Farm_v1.2/`
  - `Assets/moderninteriors-win/`
  - `Assets/modernuserinterface-win/`
  - `Assets/Tiles/ModernTownOffice/`
  - `Assets/TilePalettes/ModernTownOffice/`
- For career, job, location, building, transit, public facility, shop, school, hospital, police, office, workshop, and exterior door/sign/vehicle visuals, treat `Assets/Modern_Exteriors_32x32/` as the first candidate pack.
- Do not use Pixelwood as the default candidate set for new Town interaction sprites.
- Pixelwood remains limited to legacy Farm/Crop regression, old comparison references, or already-fixed legacy features.
- Visible sprite work is not complete until it is verified through the actual player-facing PlayMode flow and a Game View or Camera screenshot.
- Runtime sprite checks alone are not enough when the result should persist in a scene, prefab, ScriptableObject, or palette asset.
- Do not introduce entity ID-specific C# branches such as `if (npcId == "...")` or `switch(locationId)`.
- If new mapping data is needed, represent it as data or a generic serialized mapping, not gameplay-specific hardcoded branches.

## Current Runtime Inventory

The opened `Town` scene currently reports 30 `IPlayerInteractable` components in the runtime hierarchy:

| Target | Current Prompt | Current Sprite State |
|---|---|---|
| `TownHelpActionPoint` | `[E] First Guide Errand` | `TownHelpActionMarker` |
| `TownDiscoveryPoint` | `[E] Clocktower Notice` | `TownDiscoveryMarker` |
| `TownHelpActionPoint_town_help_teacher_study_prep` | `[E] Teacher Study Prep` | `TownHelpActionMarker` |
| `TownReturnPortal` | `[E] Enter Town Gate` | empty sprite name |
| `DailyPlanBoard` | `[E] Plan Day` | `DailyPlanBoardMarker` |
| `[StudentLife]/SchoolEntryActivity` | `[E] Attend School` | `StudentLifeActivityMarker` |
| `StudentDayEndBoard` | `[E] End Day` | `StudentDayEndMarker` |
| `[StudentLife]/StudyBasicsActivity` | `[E] Study Basics` | `StudentLifeActivityMarker` |
| `[StudentLife]/CareerPracticeBoard` | `[E] Career Practice` | `StudentLifeActivityMarker` |
| `[NPCs]/GuideNpc` | `[E] Talk` | `ModernFarmer_IdleDown_16x16_world` |
| `OutsideSchoolActivityPoint` | `[E] Help Neighbor` | `OutsideSchoolActivityMarker` |
| `TownNpc_npc_teacher` | `[E] Talk` | `TownLocationNpcMarker` |
| `TilePlacementBoard` | `[E] Tile Placement` | `TilePlacementMarker` |
| `OutsideSchoolActivityPoint_1` | `[E] Library` | `OutsideSchoolActivityMarker` |
| `PartTimeWorkPoint` | `[E] Corner Store` | empty sprite name |
| `LocationVisit_location_library` | `[E] Visit Library` | `TownLocationNpcMarker` |
| `LocationVisit_location_library` | `[E] Explore Library` | `TownLocationNpcMarker` |
| `LocationVisit_location_town_square` | `[E] Visit Town Square` | `TownLocationNpcMarker` |
| `LocationVisit_location_town_square` | `[E] Explore Town Square` | `TownLocationNpcMarker` |
| `TownExplorationInteractionPoint` | `[E] Exploration Test Alley Crate` | `TownExplorationInteractionMarker` |
| `TownNpc_npc_librarian` | `[E] Talk` | `TownLocationNpcMarker` |
| `TownNpc_npc_first_guide` | `[E] Talk` | `TownLocationNpcMarker` |
| `LocationVisit_location_workshop` | `[E] Visit Workshop` | `TownLocationNpcMarker` |
| `LocationVisit_location_workshop` | `[E] Explore Workshop` | `TownLocationNpcMarker` |
| `LocationVisit_location_school` | `[E] Visit School` | `TownLocationNpcMarker` |
| `LocationVisit_location_school` | `[E] Explore School` | `TownLocationNpcMarker` |
| `TownNpc_npc_work_manager` | `[E] Talk` | `TownLocationNpcMarker` |
| `LocationVisit_location_corner_store` | `[E] Visit Corner Store` | `TownLocationNpcMarker` |
| `LocationVisit_location_corner_store` | `[E] Explore Corner Store` | `TownLocationNpcMarker` |
| `TownNpc_npc_shopkeeper` | `[E] Talk` | `TownLocationNpcMarker` |

Some rows share the same GameObject because one location target has both visit and activity components. The visual requirement is per visible GameObject, not per component instance.

## Scope

In scope:

- Replace empty or placeholder marker sprites on all saved/runtime Town interaction GameObjects that appear in the first Town scene.
- Use existing Modern assets and imported Modern tile/sprite assets.
- Make NPCs visually distinct from activity boards, location entrances, portals, help actions, discovery points, and work points.
- Preserve existing interaction prompts, colliders, positions, priorities, save behavior, and UI ownership.
- Add tests that list all Town interactables and fail when they render null/empty/known placeholder sprite names.
- Verify in PlayMode by moving the player to representative targets and inspecting prompt plus visible sprite output.

Out of scope:

- Full Town map redesign.
- Creating new gameplay entities, new locations, new quests, new objective HUDs, or new panels.
- Rebuilding all Town tilemaps or decorations.
- Changing interaction radius or routing behavior.
- Replacing the SPUM/player character system.
- Adding Pixelwood fallback sprites for this Town slice.

## Recommended Approach

Use a data-driven visual mapping table for Town interactable markers, backed by Modern sprite references.

The mapping should group targets by visual role instead of gameplay entity IDs:

- NPC visual: human character sprite or generated character part composition from `Modern_Farm_v1.2`.
- Location entrance visual: building/door/sign or office/town tile sprite from `Modern_Exteriors_32x32`, `Modern_Town`, or `Tiles/ModernTownOffice`.
- Activity board visual: bulletin board, book, desk, clipboard, or UI-board-like sprite from `moderninteriors-win` or `modernuserinterface-win`.
- Portal visual: door/gate/entrance sprite from Modern exteriors/town/interior assets.
- Help/discovery visual: notice, crate, clue, sign, or marker object from Modern town/interior assets.
- Career/job visual: office, worksite, school, hospital, police, shop, transit, post-office, fire-station, or public-facility object from `Modern_Exteriors_32x32`, with interior/UI icons used only when a clearer exterior marker is unavailable.
- Work/shop visual: counter, cash register, store shelf, shop sign, market prop, delivery object, or bag icon from Modern exterior/interior/UI assets.

The first implementation can use a small `TownInteractableVisualProfile` ScriptableObject or an equivalent serialized mapping on the relevant runtime installer. The mapping keys should be generic categories or component/role names where possible. If specific scene object paths are needed for scene-only visual assignment, keep those references in data, not in C# branches.

Recommended path:

1. Audit Modern asset candidates and capture selected sprite names.
2. Create a mapping table that assigns one sprite per visible Town interactable GameObject.
3. Apply mapping in EditMode for saved scene objects where possible.
4. For objects created by runtime installers, move their default marker assignment to the same mapping source or a generic installer hook.
5. Keep colliders and prompts unchanged.

## Candidate Mapping

The exact sprite names must be confirmed from the asset database during implementation. The intended visual roles are:

| Visible GameObject | Visual Role | Candidate Source |
|---|---|---|
| `GuideNpc` | guide NPC | `Modern_Farm_v1.2` character sprite already in use, keep or vary if needed |
| `TownNpc_npc_first_guide` | guide NPC duplicate/location NPC | `Modern_Farm_v1.2` character sprite, different palette/frame from main guide if available |
| `TownNpc_npc_teacher` | teacher NPC | `Modern_Farm_v1.2` character or `moderninteriors-win` portrait/character source |
| `TownNpc_npc_librarian` | librarian NPC | `Modern_Farm_v1.2` character, book/library-adjacent palette if available |
| `TownNpc_npc_work_manager` | work manager NPC | `Modern_Farm_v1.2` character, work outfit if available |
| `TownNpc_npc_shopkeeper` | shopkeeper NPC | `Modern_Farm_v1.2` character, shopkeeper-like palette if available |
| `LocationVisit_location_town_square` | town square location | `Modern_Town` sign/plaza/office-town tile |
| `LocationVisit_location_school` | school location | `Modern_Town` building/door/sign tile or desk/board object |
| `LocationVisit_location_library` | library location | `moderninteriors-win` bookcase/book object or `Modern_Town` sign |
| `LocationVisit_location_workshop` | workshop location | `moderninteriors-win` tool/desk/workbench-like object |
| `LocationVisit_location_corner_store` | store location | `moderninteriors-win` counter/shelf/register or `modernuserinterface-win` cart/shop icon |
| `SchoolEntryActivity` | school activity point | school board/door/desk Modern sprite |
| `StudyBasicsActivity` | study activity point | book/desk/notebook Modern sprite |
| `CareerPracticeBoard` | career board | office board/clipboard/desk Modern sprite |
| `LifeActivityBoard` if visible in future audit | activity board | UI board/notice Modern sprite |
| `DailyPlanBoard` | daily plan board | board/calendar/clipboard Modern sprite |
| `StudentDayEndBoard` | end-day board | bed/clock/calendar/home sign Modern sprite |
| `TilePlacementBoard` | tile placement board | tile palette/tool board Modern sprite |
| `PartTimeWorkPoint` | part-time work | shop counter/register/store icon Modern sprite |
| `TownReturnPortal` | portal/gate | door/gate/entrance Modern sprite |
| `TownHelpActionPoint` | help action | notice/sign/help object Modern sprite |
| `TownHelpActionPoint_town_help_teacher_study_prep` | teacher help action | book/clipboard/desk Modern sprite |
| `OutsideSchoolActivityPoint` | outside-school help | neighborhood/help sign Modern sprite |
| `OutsideSchoolActivityPoint_1` | library outside-school activity | book/library sign Modern sprite |
| `TownDiscoveryPoint` | discovery clue | notice/clue/sign Modern sprite |
| `TownExplorationInteractionPoint` | exploration object | crate/object Modern sprite |

## Data And Asset Rules

- Do not create a C# class per NPC, per location, or per activity.
- Do not branch on specific activity/location/NPC IDs.
- If a new ScriptableObject profile is added:
  - class path: `Assets/Scripts/Game/StudentLife/` or `Assets/Scripts/Game/World/`, depending on ownership,
  - instance path: `Assets/Data/StudentLife/` or another existing data folder,
  - register in `Assets/Data/Registry/GameDataRegistry.asset` if it becomes shared game data.
- If only scene visual references are required, scene object serialized references are acceptable, but the saved scene state must be re-inspected after save/reopen.
- Imported sprite sheet slicing must follow `.claude/rules/path-based/sprite-slicing.md`.
- All new/changed `.asset`, prefab, scene, and texture metadata must remain paired with `.meta` files.

## Testing Strategy

### EditMode

Add or update tests before implementation:

- `TownModernInteractableSpriteTests.TownInteractablesHaveNonPlaceholderSprites`
  - open or load `Town.unity`,
  - collect all visible GameObjects with `IPlayerInteractable`,
  - collapse duplicate components by GameObject,
  - assert each has at least one enabled `SpriteRenderer`,
  - assert the assigned sprite is not null,
  - assert the sprite name is not in the known placeholder set:
    - `TownHelpActionMarker`
    - `TownDiscoveryMarker`
    - `DailyPlanBoardMarker`
    - `StudentLifeActivityMarker`
    - `StudentDayEndMarker`
    - `OutsideSchoolActivityMarker`
    - `TownLocationNpcMarker`
    - `TilePlacementMarker`
    - `TownExplorationInteractionMarker`
- `TownModernInteractableSpriteTests.TownInteractableSpritesUseModernAssetPaths`
  - resolve each sprite through `AssetDatabase.GetAssetPath`,
  - assert paths start with approved Modern roots,
  - allow existing `ModernFarmer_IdleDown_16x16_world` if it resolves to an approved Modern root.
- `TownModernInteractableSpriteTests.TownInteractableSpriteAuditMatchesExpectedVisibleTargets`
  - assert expected visible target names are present,
  - assert duplicate component rows do not require duplicate renderers.

### PlayMode

Add or update a focused E2E test:

`Rootborn.Tests.PlayMode.EndToEnd.TownModernInteractableSpriteE2ETests.TOWN_VISUAL_E2E_001_PlayerSeesModernSpritesOnRepresentativeInteractables`

Required flow:

1. Start from MainMenu/New Game or Boot-to-Town, matching the current stable player-facing entry path.
2. Reach `Town`.
3. Use actual keyboard movement to approach representative targets:
   - one NPC,
   - one location visit/activity target,
   - one board,
   - one help/discovery point,
   - the portal or part-time work point.
4. Press or prepare interact key through the normal `PlayerInteractionRouter`.
5. Verify the prompt text appears for each representative target.
6. Inspect runtime `SpriteRenderer` state:
   - sprite name,
   - asset path,
   - renderer enabled,
   - sorting layer/order if needed,
   - target world position.
7. Capture a Game View or Camera screenshot to `Builds/Logs/town-modern-interactable-sprites-e2e.png`.

Forbidden shortcuts:

- Do not only call installers or methods directly.
- Do not only inspect asset names without entering PlayMode.
- Do not manually teleport the scene into a state that skips the player-facing entry path unless movement automation is blocked, and if blocked, report the limitation explicitly.

## Verification Gate

Completion requires:

- EditMode tests pass.
- PlayMode representative player-flow test passes.
- Runtime audit reports every visible Town interactable GameObject with an enabled non-placeholder Modern sprite.
- Saved scene or serialized mapping state is re-inspected after save/reopen when persistence is expected.
- Game View or Camera screenshot exists and shows representative interactables with distinct sprites.
- Final report separates:
  - saved asset/scene verification,
  - runtime PlayMode verification,
  - screenshot evidence,
  - any remaining unverified targets.

## Performance

- Sprite assignment should happen during scene setup/installer binding, not every `Update()`.
- Mapping lookup should be a small dictionary or serialized list converted once at installer time.
- Avoid repeated `AssetDatabase` access in runtime code; asset path checks belong in Editor tests.
- Avoid creating/destroying marker GameObjects repeatedly. Reuse existing renderers and update sprite references.

## Risks

- Some runtime interactables are created by installers, so saved scene inspection may not cover every target. Tests must distinguish saved objects from runtime-created objects.
- Some location GameObjects contain two interaction components. The visual audit must deduplicate by GameObject.
- 48x48 Modern town/object sprites may need scale and pivot adjustment to fit existing 0.5-1.0 world unit marker colliders.
- NPCs may need separate character composition work if single-frame character sprites are not already available as importable sub-sprites.
- UI overlay can hide world objects in screenshots. The PlayMode visual check must verify that representative sprites are visible in the actual Game View.

## Implementation Slice

Keep the first implementation narrow:

1. Build the Modern asset candidate list and selected sprite table.
2. Add failing EditMode audit tests for null/placeholder/non-Modern sprites.
3. Add failing PlayMode representative visual-flow test.
4. Add or wire a generic visual mapping source.
5. Apply Modern sprite references to all current visible Town interactable GameObjects.
6. Run EditMode, PlayMode, runtime audit, and screenshot verification.

Do not add:

- new gameplay systems,
- new quest/objective surfaces,
- full map redesign,
- Pixelwood fallbacks,
- broad scene refactors unrelated to visible interactable sprites.

## Open Decisions Locked For Implementation

- Asset family: Modern only for this Town interaction sprite slice.
- First slice target: all current visible Town `IPlayerInteractable` GameObjects.
- Visual goal: distinguishable, non-placeholder sprites; not final art polish.
- Verification screenshot: `Builds/Logs/town-modern-interactable-sprites-e2e.png`.
- Recommended next implementation goal name: `Town Modern Interactable Sprite Pass`.
