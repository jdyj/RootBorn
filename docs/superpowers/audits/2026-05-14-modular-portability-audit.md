# Modular Portability Audit

Date: 2026-05-14
Scope: ROOTBORN Unity project current working tree
Audit type: static/code/data/test/CI evidence review only. No game code, scene, prefab, ScriptableObject, or asset data was changed.

## Executive Summary

Overall rating: **NEEDS WORK**

ROOTBORN has several strongly reusable cores: ScriptableObject-based game data, strategy arrays for conditions/outcomes/effects, inventory/reward transaction primitives, quest state machines, StudentLife progression runners, world-state flags, and Objective Journal shell patterns. These pieces can be moved to another Unity 2D project with modest adapters.

The project is not yet a clean portable module set. Portability is limited by scene-name gates (`Town`, `Farm`, `Boot`, `MainMenu`), runtime installers that procedurally create ROOTBORN-specific canvases and objects, Resources/AssetDatabase fallback loading, direct player/object discovery by name, save path/player identity assumptions, and legacy Farm/Crop bootstrap code. Several newer systems are data-driven, but some registry extensions bypass `GameDataRegistry.asset` in player builds or rely on editor-only folder scans.

Most reusable modules TOP 5:

1. `Rootborn.Game.Common.Inventory` and reward preflight APIs in `Assets/Scripts/Game/Common/GameDataRegistry.cs` and quest reward tests.
2. Quest definitions/runtime in `Assets/Scripts/Game/Quests` with `QuestObjectiveBase[]`, `QuestRewardBase[]`, and `QuestConditionBase[]`.
3. StudentLife core runners/definitions in `Assets/Scripts/Game/StudentLife`, especially `LifeActivityDefinition`, `OutsideSchoolActivityDefinition`, `PartTimeWorkDefinition`, `LocationActivityDefinition`, and their requirement/outcome base arrays.
4. WorldState definitions/progress in `Assets/Scripts/Game/WorldState`, including `WorldStateConditionBase[]`, `WorldStateEffectBase[]`, usage conditions, and usage outcomes.
5. Modern UI common panel primitive in `Assets/Scripts/UI/Modern/ModernUiTileImage.cs`, `ModernUiRecipes.cs`, and Objective Journal shell in `Assets/Scripts/UI/Objectives`.

Highest coupling-risk modules TOP 5:

1. Runtime scene installers under `Assets/Scripts/Game/Bootstrap` and `Assets/Scripts/UI/**RuntimeInstaller.cs`, which hard-code `Town`, `Farm`, canvas names, object names, and procedural layout.
2. Legacy Farm/Crop path under `Assets/Scripts/Game/Farming`, `Assets/Scripts/Game/Crops`, `FarmAutoFiller.cs`, and `StatusHud.cs`, which still carries Farm-specific singleton, canvas, and Resources assumptions.
3. Registry extension loaders such as `GameDataRegistryExplorationExtensions.cs` and `GameDataRegistryLocationStateExtensions.cs`, which use editor `AssetDatabase` folder scans and `Resources.LoadAll` fallback instead of pure registry fields.
4. Network/session/time boundary in `Assets/Scripts/Network`, where scene loading and world-time persistence are tied to `Town`, `GameBootstrap.Config.SaveSlot`, and NGO-specific `NetworkVariable`/RPC implementation.
5. Goal-like UI surfaces outside Objective Journal ownership, including `CampaignHudPanel`, `QuestChainHudWidget`, `MilestoneHudPanel`, `WorldStateLogRuntimeInstaller`, and several feature-specific canvases.

## Module Portability Matrix

| Module | Grade | Reuse scope | Coupling points | Needed adapter/refactor | Evidence files |
|---|---:|---|---|---|---|
| Common inventory/reward primitives | A | Portable C# gameplay utility for item stacks and preflight reward acceptance | `ItemDefinition` SO type and fixed `MaxSlots` | Parameterize slot policy if another game has different inventory shape | `Assets/Scripts/Game/Common/GameDataRegistry.cs`, `Assets/Tests/EditMode/Quests/QuestRewardTests.cs`, `Assets/Tests/EditMode/StudentLife/StudentDayQuestInventorySummaryTests.cs` |
| GameDataRegistry schema | B | Central registry pattern is reusable | ROOTBORN-specific entity fields and one large registry asset | Split into domain registries or registry provider interface | `Assets/Scripts/Game/Common/GameDataRegistry.cs`, `Assets/Data/Registry/GameDataRegistry.asset` |
| GameDataLookupCache | B | Reusable indexed lookup cache pattern | Hard-coded domain dictionaries for ROOTBORN entities | Generate/cache per registry module or add generic index provider | `Assets/Scripts/Game/Common/GameDataLookupCache.cs` |
| Quests | A | SO objectives/rewards/conditions are portable | Display/UI and persistence adapters still project-specific | Keep runtime core, adapt UI/save/event bridge | `Assets/Scripts/Game/Quests`, `Assets/Tests/EditMode/Quests`, `Assets/Tests/PlayMode/Quests` |
| Quest chains | B | Chain state and log panel concepts reusable | `QuestChainCanvas`, `TownSceneName`, installer owns UI placement | Move chain UI into Objective Journal provider | `Assets/Scripts/UI/Quests/QuestChainRuntimeInstaller.cs`, `QuestChainLogPanel.cs`, `QuestChainHudWidget.cs` |
| StudentLife activity core | A | Data-driven activity/choice/career/stat progression reusable | Names and default ids are ROOTBORN student-life vocabulary | Rename domain facade for other games; keep strategy arrays | `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs`, `Assets/Tests/EditMode/StudentLife/StudentLifeActivityTests.cs` |
| Outside-school/open-ended growth | A | Good reusable model for multiple progression routes | Domain nouns are school/town-specific | Adapter around route categories and display text | `OutsideSchoolActivityDefinition.cs`, `TownHelpActionDefinition.cs`, related tests |
| Daily events | B | Definition/choice/outcome structure is reusable | Runtime installer and UI tied to `Town` and `DailyEventCanvas` | Move installer config to scene module; register provider in journal | `DailyEventDefinition.cs`, `DailyEventRunner.cs`, `DailyEventRuntimeInstaller.cs` |
| Location identity/state | B | Location state conditions/effects are reusable | Folder scans, Resources fallback, string object ids for interactables/portals/shop items | Registry-only loading; object capability interfaces instead of raw ids | `LocationStateDefinition.cs`, `LocationStateAvailabilityEffect.cs`, `GameDataRegistryLocationStateExtensions.cs` |
| Exploration interactions | B | Choice/condition/outcome strategy model reusable | Editor-only `AssetDatabase` scan returns empty in player builds | Add fields to `GameDataRegistry` or provider asset | `GameDataRegistryExplorationExtensions.cs`, `ExplorationInteractionDefinition.cs`, tests under `StudentLife` |
| Discovery clues/interpretation | B | Clue/source/condition/outcome pattern reusable | UI creates `ClueInterpretationCanvas`; progress loads multiple save stores directly | Journal provider plus save facade | `Assets/Scripts/Game/DiscoveryClues`, `Assets/Scripts/UI/DiscoveryClues` |
| WorldState flags/usages | A/B | Portable flag/effect/usage core | Usage UI/log runtime creates canvas and buttons; persistence concrete | Keep core, abstract UI/save | `Assets/Scripts/Game/WorldState`, `Assets/Scripts/UI/WorldState` |
| Time/GameClock | A | Pure clock/status tick logic is reusable | Network world time is NGO/saveSlot-bound | Separate core clock from NGO state replication | `Assets/Tests/EditMode/GameClockTests.cs`, `Assets/Scripts/Network/Time/NetworkWorldTimeState.cs` |
| Save services | B | Slot-based JSON persistence can be adapted | File names embed player ids and domain names in many callers | Save repository interface and domain key registry | `Assets/Scripts/Game/Save`, `StudentLifeSceneInteraction.cs`, `WorldStateProgressPersistence.cs` |
| Network sessions | C | NGO implementation useful as ROOTBORN reference | Direct `Town` scene loads, `GameBootstrap` dependency, owner/client-id assumptions | Abstract session scene target, transport, identity, replication model | `ClientSession.cs`, `HostSession.cs`, `DedicatedServerSession.cs`, `NetworkWorldTimeState.cs` |
| Modern UI Style2 primitive | A | `ModernUiTileImage + ModernUiRecipes.CommonPanel` is portable | Sprite address constant and Style2 atlas asset required | Package sprite recipe/atlas as UI module dependency | `ModernUiTileImage.cs`, `ModernUiRecipes.cs`, `ModernUiStyle2Sprites.cs`, tests under `TownConcept` |
| Objective Journal shell | B | Journal + tracked HUD ownership model reusable | Provider model not fully dominant across all goal-like UI | Convert campaign/quest/world-state/daily-event surfaces to providers | `ObjectiveJournalPanel.cs`, `TrackedObjectiveHud.cs`, `ModernUiPanelAutoInstaller.cs` |
| Legacy Farm/Crop/Generation | C/D | Some SO crop/tool/generation logic reusable | `FarmAutoFiller`, `FarmGrid.Instance`, `Farm` scene, crop/resource ids, `StatusHud` | Keep as migration source; isolate from new town modules | `Assets/Scripts/Game/Farming`, `Assets/Scripts/Game/Crops`, `FarmAutoFiller.cs`, `StatusHud.cs` |
| Runtime world/town bootstrap | D | Useful only as ROOTBORN scene auto-repair | Hard-coded scene names, object names, coordinates, colors, canvases | Replace with scene profile SO and installer registry | `TownPlayableBaselineRuntimeInstaller.cs`, `TownTilemapFallbackInstaller.cs`, `TownStudentLifeRuntimeInstaller.cs` |

## Data-Driven Compliance

Strong compliance evidence:

- `GameDataRegistry` explicitly serializes arrays for crops, tools, resources, quests, quest chains, NPCs, dialogue, tutorial stages, character parts, life activities, choices, traits, skills, careers, relationships, locations, time slots, daily events, campaigns, milestones, encyclopedia, world state, discovery clues, and clue interpretations.
- Quest, campaign, milestone, world-state, discovery, clue-interpretation, location-state, location-activity, daily-event, outside-school, part-time-work, and town-help systems use `Base[]` strategy arrays for conditions, requirements, objectives, outcomes, rewards, effects, or completions.
- `Scripts/ci/check-no-entity-id-branching.sh` passed via Git Bash: `OK: no entity-id branching in system code.`
- Tests explicitly guard some newer systems against id switches, for example `DailyEventRegistryTests`, `ExplorationInteractionChoiceTests`, and multiple registry asset tests under `Assets/Tests/EditMode/StudentLife`.

Coupling and data gaps:

- `GameDataRegistryExplorationExtensions.cs` loads exploration assets through `AssetDatabase.FindAssets` under `Assets/Data/StudentLife/ExplorationInteractions` and returns `Array.Empty<T>()` outside the editor. That means player builds do not get those assets from the registry unless another path wires them.
- `GameDataRegistryLocationStateExtensions.cs` scans `Assets/Data/StudentLife/LocationStates` in editor and falls back to `Resources.LoadAll<LocationStateDefinition>("LocationStates")` in player builds. This violates the registry-first rule and weakens portability.
- `GameDataRegistryWorldStateUsageExtensions.cs` and similar extension loaders should be reviewed for the same pattern. The grep showed folder/path loader usage around world-state usage conditions and outcomes.
- Legacy `FarmAutoFiller.cs` still checks resource ids (`Tree`, `Rock`) and tool ids (`BareHand`) in system code. The entity-id CI gate does not flag these because its pattern catalog is narrower than the audit scope and excludes some direct `r.Id == "Tree"` shapes.
- `LocationStateAvailabilityEffect.cs` still stores `_interactableObjectIds`, `_portalIds`, and `_shopItemIds` as raw string ids. These are data fields, not C# branches, but portability would improve with typed SO references or capability tags.

## UI Ownership Audit

Objective Journal compliance:

- `ModernUiPanelAutoInstaller.cs` installs `ObjectiveJournalPanel` and `TrackedObjectiveHud`, binds them, and routes input through `ModernUiPanelInputRouter`.
- `ObjectiveJournalPanel.cs` and `TrackedObjectiveHud.cs` use `ModernUiTileImage` and `ModernUiRecipes.CommonPanel`, matching the Style2 common-panel rule.
- `Scripts/UI/Modern/ModernUiTileImage.cs` defaults to `ModernUiRecipes.CommonPanel`, and many panels set the recipe explicitly.

Ownership gaps:

- Several goal/quest/campaign/hint-style surfaces still create independent panels or canvases: `CampaignHudPanel`, `QuestChainHudWidget`, `MilestoneHudPanel`, `WorldStateLogRuntimeInstaller`, `DailyEventRuntimeInstaller`, `StudentDayRuntimeInstaller`, `LocationNpcRuntimeInstaller`, `QuestChainRuntimeInstaller`, and `ExplorationChoicePanel`.
- `QuestChainRuntimeInstaller.cs` does call `ModernUiPanelAutoInstaller.InstallOnCanvas` and captures `ObjectiveJournalPanel`/`TrackedObjectiveHud`, but it still also creates `QuestChainCanvas`, `QuestChainLogPanel`, and `QuestChainHudWidget`.
- Feature-specific canvas names such as `WorldStateLogCanvas`, `[DailyEventCanvas]`, `[StudentDayCanvas]`, `LocationNpcCanvas`, `QuestChainCanvas`, `ExplorationChoiceCanvas`, `[TownTimeCanvas]`, `[TownCanvas]`, and `[FarmCanvas]` make UI transplantation costly.
- Style2 panel use is generally good, but direct `Image` backgrounds and procedural sizes/coordinates remain common in runtime installers and legacy HUD surfaces.

Recommended UI portability direction: keep `ModernUiTileImage`, `ModernUiRecipes`, `ObjectiveJournalPanel`, and `TrackedObjectiveHud` as the portable UI core; convert campaign, quest chain, milestone, world-state, clue, and daily-event surfaces into Objective Journal providers plus transient modal/toast adapters.

## Network and Save Boundary Audit

Network boundary strengths:

- Network code is separated under `Assets/Scripts/Network` with its own asmdef.
- `NetworkLocalPlayerInputGate`, `NetworkLocalPlayerViewBinder`, and `NetworkPlayerIdentityBinder` clearly separate owner-local behavior from remote players.
- `QuestNetworkStateBroadcaster` records save slot and player id in the emitted state, supporting player isolation.
- `NetworkWorldTimeState` uses server-authoritative RPC entry points and `NetworkVariable<int>` state for shared world time.

Network/save coupling:

- `DedicatedServerSession.cs` and `HostSession.cs` hard-code `TownSceneName = "Town"` and load it through NGO scene management.
- `NetworkWorldTimeState.cs` persists directly to `Application.persistentDataPath/world-time/{saveSlot}.json` and reads `GameBootstrap.Config.SaveSlot`. It is not a reusable time replication module without a save adapter.
- `NetworkWorldTimeState.WeekdayName` uses a C# switch expression returning English display strings. For another game or localization, weekdays should be data/localization keys.
- Save file names are scattered across domain callers: `student-life-progress-{playerId}.json`, `quest-log-{playerId}.json`, `milestone-progress-{playerId}.json`, world-state files, and world-time files. A portable module should centralize domain save keys.
- Default player ids vary across modules (`player`, `local-player`, `PlayerIdentity.DefaultPlayerId`), which complicates multiplayer portability and migration.

## Testing and CI Coverage

Evidence:

- EditMode tests cover quest objectives/rewards/save/registry, StudentLife activity/career/outside-school/help/relationship/day result, discovery clues, clue interpretation, world-state core/persistence/registry/usage, Modern UI recipes, and network source audits.
- PlayMode E2E tests exist for many real user flows: boot-to-game, town interactions, quest resource interaction, student progression save, daily event loop, location state loop, NPC schedule, campaign, career interest/candidate loops, world-state usage, discovery clue, clue interpretation, and Objective Journal input.
- `Scripts/ci/check-no-entity-id-branching.sh` passed with Git Bash.
- `Scripts/ci/check-scenario-registry-sync.sh` passed with warnings: `NET-003` and `NET-004` are doc-only and tracked as E expansion.

Weak or missing coverage:

- The entity-id CI gate is narrower than the constitutional rule and audit scope. It catches selected `cropId/toolId/knowledgeId/traitId` forms but missed observed examples like `r.Id == "Tree"`, `r.Id == "Rock"`, and `TryGetValue("BareHand", ...)` in `FarmAutoFiller.cs`.
- There is no dedicated portability test that asserts runtime systems do not rely on editor-only `AssetDatabase` loading or `Resources.LoadAll` fallback for player data.
- Objective Journal ownership is partially covered by panel/input tests, but no global source gate prevents new goal/quest/campaign/hint UI canvases or HUD widgets.
- Scenario registry sync reports two doc-only scenarios (`NET-003`, `NET-004`), which should remain visible as E/needs-runtime-verification items.

Commands run:

```powershell
rg -n "if\s*\([^\)]*Id\b|if\s*\([^\)]*\.id\b|switch\s*\(" Assets/Scripts/Game Assets/Scripts/UI Assets/Scripts/Network Assets/Tests Scripts/ci docs/superpowers -g "*.cs" -g "*.sh" -g "*.md"
rg -n "Resources\.Load|FindObjectOfType|GameObject\.Find|GetComponent<|GetComponent\(" Assets/Scripts/Game Assets/Scripts/UI Assets/Scripts/Network -g "*.cs"
rg -n "Assets/|\.unity|Boot|MainMenu|Town|Farm|HostLobby|sprites/|Addressables|Addressable" Assets/Scripts/Game Assets/Scripts/UI Assets/Scripts/Network -g "*.cs"
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-scenario-registry-sync.sh
```

Attempted but environment-blocked:

```powershell
bash Scripts/ci/check-no-entity-id-branching.sh
bash Scripts/ci/check-scenario-registry-sync.sh
```

Both failed because `bash` resolved to WSL and `/bin/bash` was unavailable. Git Bash succeeded.

## Performance and Scale Risks

- `GameDataLookupCache` is a good step toward indexed lookup and avoids repeated full scans once constructed.
- Many UI panels are procedurally built and refreshed by rebuilding child trees or scanning scene roots. For large quest/campaign/world-state/encyclopedia lists, portable modules need list virtualization, paging, dirty-refresh models, and row pooling.
- Several installers poll or scan for `Canvas`, `PlayerInventory`, scene roots, or `Player` for up to 15 seconds. That is acceptable for current bootstrap recovery, but not portable as a low-cost module contract.
- `Resources.LoadAll` and editor folder scans hide data dependencies and can create build-time/runtime misses.
- Repeated `GameObject.Find`, `FindFirstObjectByType`, and root traversal patterns are concentrated in runtime installers and legacy HUDs. These should become scene-module dependency injection or installer manifests.
- Save/load currently uses per-domain JSON calls directly from UI/interactors. With many modules, batching or a save repository with dirty state would reduce IO and coupling.

## Recommended Follow-Up Goals

1. **[REFACTOR][TEST] Registry-only data provider sweep**
   - Purpose: Remove editor-only folder scans and `Resources.LoadAll` fallbacks for gameplay data.
   - Impact files: `GameDataRegistryExplorationExtensions.cs`, `GameDataRegistryLocationStateExtensions.cs`, world-state usage registry extensions, `GameDataRegistry.cs`, registry asset tests.
   - Test requirements: EditMode registry asset tests for exploration/location-state/world-state usage; source audit that forbids `AssetDatabase.FindAssets` and `Resources.LoadAll` in runtime data providers.
   - Done when: Player-build code path obtains all data through `GameDataRegistry` or explicit provider SOs.

2. **[REFACTOR][TEST] Scene installer profile abstraction**
   - Purpose: Replace `Town`/`Farm` hard-coded runtime installers with scene profile SOs or installer manifests.
   - Impact files: `TownPlayableBaselineRuntimeInstaller.cs`, `TownStudentLifeRuntimeInstaller.cs`, `TownTilemapFallbackInstaller.cs`, `FarmAutoFiller.cs`, UI runtime installers.
   - Test requirements: Source gate for hard-coded scene names in runtime modules; PlayMode smoke test using a non-Town test scene profile.
   - Done when: Another Unity 2D scene can opt into the same modules without renaming itself `Town` or `Farm`.

3. **[UI][TEST] Objective Journal provider consolidation**
   - Purpose: Move quest chain, campaign, milestone, world-state, clue, and career hint UI into Objective Journal providers, leaving only one tracked HUD.
   - Impact files: `CampaignHudPanel.cs`, `QuestChainHudWidget.cs`, `MilestoneHudPanel.cs`, `WorldStateLogRuntimeInstaller.cs`, `DailyEventRuntimeInstaller.cs`, `ObjectiveJournalPanel.cs`.
   - Test requirements: Source audit forbidding new goal-like canvases/HUD widgets; PlayMode Tab journey for each provider category.
   - Done when: Goal/quest/campaign/hint UI ownership complies with `.claude/rules/objective-journal-ui.md`.

4. **[TEST][CHORE] Expand entity-id branching CI gate**
   - Purpose: Cover all entity domains and direct `.Id == "..."`, `TryGetValue("...")`, and ID enum variants, with allowlists for tests and data tooling.
   - Impact files: `Scripts/ci/check-no-entity-id-branching.sh`, source audit tests, legacy Farm migration files.
   - Test requirements: CI fixture or script self-test proving `r.Id == "Tree"` and `TryGetValue("BareHand")` are caught.
   - Done when: The gate enforces the full constitutional rule, not just crop/tool/knowledge/trait regex subsets.

5. **[NETWORK][REFACTOR][TEST] Save/network boundary adapter**
   - Purpose: Decouple NGO session/time/save from `GameBootstrap.Config`, `Town`, and raw JSON file naming.
   - Impact files: `Assets/Scripts/Network/Session`, `NetworkWorldTimeState.cs`, `SaveService`, player identity components.
   - Test requirements: EditMode tests for save key mapping; PlayMode/network tests with alternate scene target and alternate save repository.
   - Done when: Session, identity, shared-world-state, and per-player persistence can be swapped without changing gameplay modules.

6. **[REFACTOR][TEST] Legacy Farm/Crop isolation**
   - Purpose: Mark or wrap Farm/Crop/Generation as migration source and prevent legacy assumptions from leaking into town modules.
   - Impact files: `FarmAutoFiller.cs`, `FarmGrid.cs`, `CropPlot.cs`, `StatusHud.cs`, legacy tests.
   - Test requirements: Source audit for Farm-only names outside legacy namespace; regression tests for `GEN/HEIR/CROP` remain green.
   - Done when: New town modules do not depend on Farm scene, Farm canvas, Farm grid singleton, or `Tree`/`Rock` resource ids.

## Final PASS/FAIL Checklist

| Requirement | Status | Evidence |
|---|---:|---|
| Audit document written at requested path | PASS | This file: `docs/superpowers/audits/2026-05-14-modular-portability-audit.md` |
| No code/assets/SO edits performed | PASS | Only `docs/superpowers/audits/` was created/edited during this audit session |
| Major systems classified A/B/C/D/E | PASS | Module Portability Matrix covers Common, Registry, Quests, StudentLife, WorldState, UI, Network, Save, Farm/Crop, bootstrap |
| File paths and concrete evidence included | PASS | Matrix and sections cite exact paths and command evidence |
| At least 5 reusable module candidates recorded | PASS | Executive Summary TOP 5 reusable modules |
| At least 5 coupling risks recorded | PASS | Executive Summary TOP 5 risks plus detailed sections |
| Data-driven SO compliance checked | PASS | Registry/strategy evidence, CI gate result, and loader gaps |
| Objective Journal/UI ownership checked | PASS | `ObjectiveJournalPanel`, `TrackedObjectiveHud`, and competing UI surfaces documented |
| Network/save boundary checked | PASS | Session scene loading, world time, save slot/player id coupling documented |
| Tests/CI checked | PASS | Test inventory reviewed; two CI scripts run through Git Bash |
| Performance/scale risks checked | PASS | UI rebuild/scans, registry cache, Resources/folder scans, save IO risks documented |
| Follow-up goals prioritized | PASS | Six concrete goals with purpose, files, tests, done conditions |
| Final judgment on portability | NEEDS WORK | Portable cores exist, but hard-coupled scene/UI/load/save/network boundaries remain |

