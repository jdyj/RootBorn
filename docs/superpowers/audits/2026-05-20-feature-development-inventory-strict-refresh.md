# Feature Development Inventory Strict Refresh

Date: 2026-05-20

Scope: current code, data, scene/runtime installers, tests, screenshots, and recent design docs after Objective Journal migration, provider hardening, UI scaling hardening, and Daily Plan route E2E coverage.

## Inspection Evidence

Required commands were run:

```powershell
rg -n "class .*Interactor|RuntimeInstaller|ObjectiveJournal|TrackedObjectiveHud|DailyPlan|QuestChain|CareerInterest|Relationship|Knowledge|Discovery|House|Furniture|Network|Dedicated|Farm|Crop|Generation" Assets/Scripts Assets/Tests -g "*.cs"
Get-ChildItem Assets/Data -Recurse -File | Group-Object DirectoryName | Sort-Object Count -Descending | Select-Object -First 40 Count,Name
Get-ChildItem Assets/Tests/EditMode,Assets/Tests/PlayMode -Recurse -File -Filter "*.cs" | Select-Object FullName
Get-ChildItem docs/superpowers/specs,docs/superpowers/goals,docs/superpowers/plans,docs/superpowers/audits -File | Sort-Object LastWriteTime -Descending | Select-Object -First 40 Name,LastWriteTime
git status --short
git diff --check
```

Static summary:

| Signal | Current evidence |
| --- | --- |
| Production code counts | `Game/StudentLife` 147, `UI/StudentLife` 26, `UI/Objectives` 15, `Game/Quests` 35, `UI/Quests` 8, `Game/DiscoveryClues` 32, `Game/Knowledge` 6, `Game/Housing` 10, `Game/Interiors` 10, `UI/Interiors` 10, `Network` 13, `Game/Farming` 2, `Game/Generation` 2 |
| Data counts | `Assets/Data/StudentLife` 359, `Quests` 88, `Careers` 87, `NPCs` 43, `Relationships` 2, `DiscoveryClues` 102, `Knowledge` 7, `Housing` 15, `Interiors` 6434, `Crops` 7, `Generations` 4 |
| Test counts | `EditMode/StudentLife` 42, `UI/StudentLife` 5, `UI/Objectives` 7, `Quests` 11, `DiscoveryClues` 6, `Housing` 3, `Interiors` 15, `Network` 9, `Farming` 3, `PlayMode/EndToEnd` 46 |
| Recent screenshot evidence | `objective-provider-strict-e2e.png`, `objective-ui-scaling-e2e.png`, `objective-journal-town-e2e.png`, `daily-plan-route-e2e.png`, legacy migration screenshots for campaign/milestone/quest-chain/career-interest Objective Journal flows |
| Dirty files | Still unrelated and unstaged: deleted `Assets/AddressableAssetsData/link.xml`, deleted `.meta`, modified `production/qa/evidence/registry-provider-town-flow.png` |
| Static gate | `git diff --check` exited 0. No whitespace errors were reported. |

## Domain Inventory

### Student Life Daily Activity Loop

- Production code signal: strong. Core progress, activity runners, outside-school activity, location activity, part-time work, self-study, day-end, daily events, milestones, relationship conditions, and result summaries exist.
- ScriptableObject/data signal: strong. `Assets/Data/StudentLife` has 359 files.
- Scene/runtime installer signal: strong. `StudentDayRuntimeInstaller`, `OutsideSchoolRuntimeInstaller`, `PartTimeWorkRuntimeInstaller`, `LocationStateRuntimeInstaller`, `TownHelpActionRuntimeInstaller`, and day-end interactors exist.
- Current EditMode coverage: strong, including daily activity, result summary, milestone, location state, outside-school, self-study, part-time work, relationship condition, and registry tests.
- Current PlayMode coverage: strong. Representative flows include `STUDENT_DAY_E2E_001_011`, `OUTSIDE_FOUNDATION_E2E_001_007`, `STUDY_E2E_001_006`, `WORK_E2E_001_006`, `MILESTONE_E2E_001_010`, and `LOCATION_STATE_E2E_001_011`.
- Current screenshot/evidence signal: `daily-plan-route-e2e.png`, Objective Journal screenshots, and several student-life E2E screenshots exist.
- Player-facing gap: daily play now has route selection coverage, but route availability is still mostly static. The next useful player value is making exploration, NPC, quest, or career actions unlock or alter the next route.
- Architecture risk: medium. New work must avoid route ID branching and keep availability/effects as SO strategies.
- Data-driven compliance risk: medium. Route availability and consequences must be expressed with definitions/conditions/outcomes, not `if routeId`.
- Performance/scaling risk: low-medium. Objective Journal source refresh should stay dirty/event driven and avoid full scans each frame.
- Recommended next slice: connect a discovery clue interpretation to a new Daily Plan route availability/hint.
- Minimum direct PlayMode scenario: New Game -> Town -> discover clue -> choose interpretation -> open Daily Plan board -> see newly available route -> select it -> perform matching activity -> end day -> Objective Journal next hint -> screenshot -> reload.

### Daily Plan Route Loop

- Production code signal: strong for the base route loop. `DailyPlanRuntimeInstaller`, `DailyPlanBoardInteractor`, Daily Plan UI tests, Objective Journal `daily-plan` source, and `DailyPlanRouteSelectionE2EScenarioTests` exist.
- ScriptableObject/data signal: strong enough for current slice under `Assets/Data/StudentLife`, but route unlock variety is not yet the main content driver.
- Scene/runtime installer signal: strong. The board is installed in Town and participates in player-facing E2E.
- Current EditMode coverage: `DailyPlanProgressComponentTests`, `DailyPlanRegistryTests`, `DailyPlanRouteRunnerTests`, `DailyPlanPanelTests`, `DailyPlanRuntimeInstallerTests`, and day-end tests.
- Current PlayMode coverage: `DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload`.
- Current screenshot/evidence signal: `Builds/Logs/daily-plan-route-e2e.png`.
- Player-facing gap: route selection exists, but the loop needs meaningful sources that unlock, recommend, or modify routes based on discovery, relationships, career, or quest outcomes.
- Architecture risk: medium. Route source composition should not become another HUD or a parallel quest UI.
- Data-driven compliance risk: medium. Route requirements, rewards, and hints must be SO data and strategy arrays.
- Performance/scaling risk: low now; medium later if route lists grow without paging/filter caching.
- Recommended next slice: route unlock source integration, starting with discovery clue interpretation.
- Minimum direct PlayMode scenario: New Game -> Town -> interact with Daily Plan board -> inspect locked/unlocked route state -> complete unlock source -> return to board -> verify route state and Objective Journal hint -> screenshot -> reload.

### Career Interest, Candidate, And Job Loop

- Production code signal: strong for interest/candidate; moderate for job bridge. Career interest/candidate components, panels, runtime installers, quest links, and Objective Journal source exist.
- ScriptableObject/data signal: strong. `Assets/Data/Careers` has 87 files.
- Scene/runtime installer signal: strong for interest/candidate through `CareerInterestRuntimeInstaller`, `CareerCandidateRuntimeInstaller`, and location/NPC installers.
- Current EditMode coverage: strong for interest selection, candidate deepening, quest links, persistence, and registries.
- Current PlayMode coverage: `CAREER_INTEREST_E2E_001_013`, `CAREER_CANDIDATE_E2E_001_010`, and quest-chain related career route E2E.
- Current screenshot/evidence signal: `career-interest-objective-journal-e2e.png`.
- Player-facing gap: career interest is visible, but career route progression still needs a clearer bridge from part-time work/mentor action to candidate deepening and Daily Plan recommendation.
- Architecture risk: medium-high. It touches career data, quests, relationships, rewards, and Objective Journal.
- Data-driven compliance risk: medium. Career/job-specific branches are forbidden; use definitions and strategy arrays.
- Performance/scaling risk: medium. Candidate lists and route recommendations should be cached/filtered, not rebuilt every frame.
- Recommended next slice: one part-time work or mentorship action deepens a career candidate and updates Objective Journal Hints.
- Minimum direct PlayMode scenario: New Game -> Town -> choose career interest via visible UI -> walk to library or work marker -> interact -> perform mentorship/work action -> verify candidate progress and Objective Journal hint -> screenshot -> reload.

### Quest And Quest-Chain Loop

- Production code signal: strong. Quest definitions, objectives, conditions, rewards, completion effects, quest chain state, panels, Objective Journal sources, and network state hooks exist.
- ScriptableObject/data signal: strong. `Assets/Data/Quests` has 88 files, including career route dialogue data.
- Scene/runtime installer signal: moderate-strong through NPC/location/quest E2E paths and Objective Journal provider composition.
- Current EditMode coverage: strong with quest condition/objective/progress/reward/save/UI/registry tests plus quest-chain state/registry tests.
- Current PlayMode coverage: `QUEST_CHAIN_E2E_001_014`, `QUEST_CHAIN_E2E_009_010`, quest save isolation, dialogue, provider, portal, and resource interaction tests.
- Current screenshot/evidence signal: `quest-chain-objective-journal-e2e.png`.
- Player-facing gap: quest and chain infrastructure is ready, but the best next quest should connect systems instead of adding another standalone panel.
- Architecture risk: medium. Reward transaction idempotency and inventory capacity rules are strict.
- Data-driven compliance risk: medium. No quest ID branching; all conditions/rewards must be SO-defined.
- Performance/scaling risk: medium. Quest log and Objective Journal sources should avoid repeated full data scans as content grows.
- Recommended next slice: one quest chain that starts from a discovery clue and grants a Daily Plan route or interior reward.
- Minimum direct PlayMode scenario: New Game -> Town -> accept quest through NPC/discovery UI -> track in Objective Journal -> perform required action -> claim reward -> verify no duplicate reward -> screenshot -> reload.

### NPC Relationship And Schedule Loop

- Production code signal: moderate. NPC definitions, schedules, dialogue, location NPC runtime installer, relationship conditions/effects, and help actions exist.
- ScriptableObject/data signal: mixed. `Assets/Data/NPCs` has 43 files, but `Assets/Data/Relationships` has only 2 files.
- Scene/runtime installer signal: strong enough for Town slices through `LocationNpcRuntimeInstaller` and schedule E2E.
- Current EditMode coverage: moderate with NPC schedule, relationship condition, help action, and location/NPC foundation tests.
- Current PlayMode coverage: `NPCSCHEDULE_E2E_001_011`, `RELCOND_E2E_001_011`, `LOCNPC_E2E_001_009`, and help action E2E.
- Current screenshot/evidence signal: schedule/relationship E2E evidence exists through test artifacts; no new strict screenshot was generated in this track.
- Player-facing gap: relationship progress is mechanically present but thin as a readable player goal.
- Architecture risk: medium. Time, schedule state, relationship milestones, dialogue, and Objective Journal hints need clear ownership.
- Data-driven compliance risk: medium. NPC-specific branches are forbidden; schedule and milestone behavior must be data/strategy-driven.
- Performance/scaling risk: medium. NPC schedule updates should be event/time-slice driven, not per-frame full NPC scans.
- Recommended next slice: one NPC routine plus relationship milestone surfaced in Objective Journal Hints.
- Minimum direct PlayMode scenario: New Game -> Town -> advance time via actual activity/day flow -> observe NPC relocation -> interact -> choose help/dialogue action -> relationship changes -> Objective Journal hint -> screenshot -> reload.

### Exploration, Discovery Clue, Rumor, And Knowledge Loop

- Production code signal: strong. Exploration interactions, discovery points, clue interpretation, encyclopedia, knowledge progress, world state usage, and UI surfaces exist.
- ScriptableObject/data signal: strong. `Assets/Data/DiscoveryClues` has 102 files and `Assets/Data/Knowledge` has 7 files.
- Scene/runtime installer signal: strong. `TownExplorationDiscoveryRuntimeInstaller`, `TownExplorationInteractionRuntimeInstaller`, clue interpretation interactors, and encyclopedia UI tests exist.
- Current EditMode coverage: strong for clue loop, interpretation, persistence, registry, and knowledge triggers.
- Current PlayMode coverage: `DISCOVERY_CLUE_E2E_001_011`, `CLUE_INTERPRET_E2E_001_011`, `CLUE_INTERPRET_E2E_012`, encyclopedia discovery UI, exploration interaction choice, and world usage E2E.
- Current screenshot/evidence signal: discovery/clue E2E screenshots are generated by related tests; Objective Journal strict screenshots are now current.
- Player-facing gap: discoveries can still feel isolated unless they unlock a clear next route, quest, relationship hint, or house reward.
- Architecture risk: low-medium. Core loop is tested; integration point choice matters.
- Data-driven compliance risk: medium. Clue-to-route mapping must be via definitions/conditions/outcomes.
- Performance/scaling risk: medium. Discovery and encyclopedia lists need filtering/caching as data grows.
- Recommended next slice: discovery clue interpretation unlocks a Daily Plan route and Objective Journal hint.
- Minimum direct PlayMode scenario: New Game -> Town -> move to discovery point -> interact -> choose interpretation -> verify knowledge/world state -> open Daily Plan/Objective Journal -> screenshot -> reload.

### Housing And Interior Reward Loop

- Production code signal: strong for placement/interiors, moderate for reward integration. House upgrades, interior generation, placement, camera controls, availability, and interactors exist.
- ScriptableObject/data signal: very strong. `Assets/Data/Interiors` has 6434 files and `Assets/Data/Housing` has 15 files.
- Scene/runtime installer signal: strong for House/Interior scenes and placement mode; Town reward-to-house integration is weaker.
- Current EditMode coverage: strong for interior generation, placement model/service/save/catalog, house upgrade service, tile sets, and importers.
- Current PlayMode coverage: strong for house placement, interior generation, placement UI, camera, placement mode, cleanup, and save/load.
- Current screenshot/evidence signal: house/interior placement tests generate visual evidence; strict visual verification remains mandatory for any new visible placement work.
- Player-facing gap: home customization exists, but a Town-origin reward path to unlock/place an item is not the clearest current feature yet.
- Architecture risk: high. Visible placement, camera, tilemap, saved state, and asset references must all be verified.
- Data-driven compliance risk: medium. Furniture unlocks must come from data and save state, not hardcoded reward IDs.
- Performance/scaling risk: high. Furniture catalogs are large; UI needs paging/filtering/caching.
- Recommended next slice: defer until a quest/discovery reward needs a tangible home item.
- Minimum direct PlayMode scenario: New Game -> earn unlock from Town action -> enter House -> open placement mode -> choose unlocked furniture -> place with mouse -> Game View screenshot -> reload and verify placed object persists.

### Network And Dedicated Server Save/Load Loop

- Production code signal: moderate. Session bootstrap, network args, local player view, student-life interaction sync, quest state, world time, and direct validation sources exist.
- ScriptableObject/data signal: indirect. Network synchronizes existing gameplay data rather than owning large data catalogs.
- Scene/runtime installer signal: moderate. Host-mode tests exist; dedicated executable validation is still operationally heavier.
- Current EditMode coverage: 9 network tests.
- Current PlayMode coverage: one network PlayMode suite plus many single-player E2E flows that are not dedicated-server proofs.
- Current screenshot/evidence signal: prior multiplayer audit docs exist; no new dedicated executable screenshot was generated in this track.
- Player-facing gap: host session smoke is covered better than real dedicated server/client save/load.
- Architecture risk: high. Requires build, process orchestration, port isolation, authority, and persistence checks.
- Data-driven compliance risk: low-medium. Risks are more synchronization and environment configuration than entity branching.
- Performance/scaling risk: high for long-run sessions and multiple clients.
- Recommended next slice: host-mode student-life save smoke before another dedicated-server executable pass.
- Minimum direct PlayMode scenario: Host session -> spawn local player -> move/interact -> server-authoritative progress -> stop/restart host -> reload same slot -> verify no stale network objects -> screenshot/log evidence.

### Legacy Farm, Crop, And Generation Role

- Production code signal: intentionally low. Farming and generation runtime code are small legacy/regression areas.
- ScriptableObject/data signal: small. `Assets/Data/Crops` has 7 files and `Assets/Data/Generations` has 4 files.
- Scene/runtime installer signal: legacy Farm autofill and portal paths exist.
- Current EditMode coverage: crop growth, farming, generation manager/simulator/heir tests.
- Current PlayMode coverage: Farm flow, seeded farm generation, farming scenario, and older farm UI/character tests.
- Current screenshot/evidence signal: Farm screenshots exist from prior visual tests.
- Player-facing gap: not aligned with the current Town/student-life MVP direction.
- Architecture risk: medium if expanded now, because it can pull work back into legacy assumptions.
- Data-driven compliance risk: medium. Any crop/tool updates must stay SO strategy-driven.
- Performance/scaling risk: medium for generated farm/tilemap work.
- Recommended next slice: no new feature unless regression or migration requires it.
- Minimum direct PlayMode scenario: Farm entry -> crop/tool interaction through real input -> save/load -> verify no regression -> screenshot if visible work changed.

## Ranked Next Feature Candidates

Exactly one recommended next goal is named below, followed by two runner-up goals.

### 1. Recommended: Discovery Clue Unlocks A Daily Plan Route

- Player value: exploration creates a concrete next-day option instead of only adding collection/progress state.
- Affected systems: DiscoveryClues, Knowledge, WorldState, DailyPlan, StudentLife route availability, Objective Journal Hints, SaveService.
- Required data assets: one `DiscoveryClueDefinition`, one interpretation, one knowledge/world-state unlock, one route availability condition, one Daily Plan route/hint definition.
- Required UI surface and ownership: clue/interpretation UI for the choice; Daily Plan board for route selection; Objective Journal `Hints` for the next action. No new persistent HUD.
- Required EditMode tests: clue outcome unlocks route condition; route availability reads world/knowledge state; Objective Journal hint source emits stable key; save data dedupes unlock.
- Required PlayMode E2E scenario: `DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload`.
- Screenshot/evidence path: `Builds/Logs/discovery-route-unlock-e2e.png`.
- Save/load expectation: clue completion, interpretation choice, knowledge/world-state unlock, route availability, and Objective Journal hint restore once with no duplicates.
- Performance risk: medium; route availability checks should be cached or evaluated on refresh, not every frame.
- Why next: Daily Plan and Objective Journal are now stable enough to receive meaningful unlock sources, and discovery already has strong infrastructure.

### Scenario: Discovery clue unlocks a Daily Plan route

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the named discovery point, such as `DiscoveryPoint_*` installed by the Town discovery runtime.
3. Press the interact key and choose the visible clue interpretation button that should unlock the route.
4. Verify visible clue/interpretation UI text in Game View and verify the Daily Plan board now shows the named unlocked route.
5. Verify runtime state on the discovery/clue progress component and Daily Plan progress/registry condition.
6. Capture screenshot to `Builds/Logs/discovery-route-unlock-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify the clue is not duplicated and the route remains available.
10. Verify Objective Journal Hints shows the next route action.

### 2. Runner-up: NPC Routine Relationship Milestone

- Player value: makes Town NPCs feel scheduled and gives relationship growth a readable target.
- Affected systems: NPC schedule, dialogue/help actions, relationships, location state, StudentLife day/time, Objective Journal Hints.
- Required data assets: one NPC schedule expansion, one relationship definition/milestone, one dialogue/help action, one hint item.
- Required UI surface and ownership: dialogue/help UI for action; Objective Journal `Hints` for next objective; no standalone relationship HUD.
- Required EditMode tests: schedule transition, relationship threshold condition, milestone trigger, Objective Journal hint source stable key.
- Required PlayMode E2E scenario: `NPC_RELATIONSHIP_E2E_001_ScheduleInteractionMilestoneJournalReload`.
- Screenshot/evidence path: `Builds/Logs/npc-relationship-milestone-e2e.png`.
- Save/load expectation: NPC schedule state, relationship value, milestone completion, and hint restore without applying the relationship delta twice.
- Performance risk: medium; schedule updates must be time/event driven, not full scans every frame.
- Why runner-up: good player value, but relationship data is thin and should follow one more route/unlock integration slice.

### Scenario: NPC routine relationship milestone

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the named NPC's expected morning location.
3. Use actual interact key or visible dialogue/help button to perform the relationship action.
4. Verify visible dialogue/result UI text and NPC state in Game View.
5. Verify runtime state on relationship progress, NPC schedule, and milestone components.
6. Capture screenshot to `Builds/Logs/npc-relationship-milestone-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify relationship/milestone state restores without duplication.
10. Verify Objective Journal Hints shows the next relationship action when applicable.

### 3. Runner-up: Quest Chain Rewards An Interior Unlock

- Player value: turns quest progression into a tangible home customization reward.
- Affected systems: QuestChain, QuestReward, Inventory, Housing, Interiors, placement, Objective Journal, SaveService.
- Required data assets: one quest chain, one reward definition, one furniture unlock definition, one furniture catalog entry, one Objective Journal hint.
- Required UI surface and ownership: Quest UI/Objective Journal for tracking; House placement UI for placement. No extra quest HUD.
- Required EditMode tests: reward transaction capacity/idempotency, furniture unlock save, placement availability, Objective Journal quest source stable key.
- Required PlayMode E2E scenario: `QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`.
- Screenshot/evidence path: `Builds/Logs/quest-interior-reward-e2e.png`.
- Save/load expectation: quest state, reward claim, furniture unlock, and placed furniture persist without duplicate reward.
- Performance risk: high; furniture catalogs are large and placement UI needs bounded search/list behavior.
- Why runner-up: high player value, but visible placement verification and catalog scale make it riskier than discovery-route work.

### Scenario: Quest chain rewards an interior unlock

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the named NPC, discovery point, or quest marker.
3. Use actual interact key or visible UI button to accept and complete the quest-chain action.
4. Verify visible quest/Objective Journal state in Game View.
5. Verify runtime state on quest chain, reward claim, inventory/unlock, and furniture catalog.
6. Capture screenshot to `Builds/Logs/quest-interior-reward-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify reward cannot be claimed twice and the unlock persists.
10. Verify Objective Journal or day result shows the next action when applicable.

### 4. Career Practice Deepens A Candidate

- Player value: career interest becomes an actionable practice path with visible progression.
- Affected systems: CareerInterest, CareerCandidate, StudentLife activity, Daily Plan, QuestChain, Objective Journal.
- Required data assets: one practice activity, one candidate deepening rule, one career hint, optional quest link.
- Required UI surface and ownership: Career panel for selection; Daily Plan board for practice route; Objective Journal `Hints` for next action.
- Required EditMode tests: practice outcome strategy, candidate progress save, Objective Journal career source ordering, route condition.
- Required PlayMode E2E scenario: `CAREER_PRACTICE_E2E_001_SelectPracticeDeepenCandidateReload`.
- Screenshot/evidence path: `Builds/Logs/career-practice-candidate-e2e.png`.
- Save/load expectation: selected interest, candidate progress, route state, and hint restore once.
- Performance risk: medium; candidate recommendation lists should be cached on data/progress changes.
- Why deferred: valuable, but route unlock from discovery should define the integration pattern first.

### Scenario: Career practice deepens a candidate

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the library or career practice marker.
3. Use actual interact key or visible career UI button to select the practice action.
4. Verify visible career/candidate UI text in Game View.
5. Verify runtime state on career interest, candidate progress, and Daily Plan route state.
6. Capture screenshot to `Builds/Logs/career-practice-candidate-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify career/candidate state restores without duplication.
10. Verify Objective Journal Hints shows the next practice action.

### 5. Part-Time Work Career Bridge

- Player value: work actions become a practical alternative growth path and not just a stat delta.
- Affected systems: PartTimeWork, Career, Relationships, StudentLife progress, Daily Plan, Objective Journal.
- Required data assets: one work definition, one career/candidate outcome strategy, one relationship effect, one route hint.
- Required UI surface and ownership: work prompt/result UI; Objective Journal `Hints`; Daily Plan board if the work route is planned.
- Required EditMode tests: work outcome strategy, relationship/career delta, save summary, Objective Journal source.
- Required PlayMode E2E scenario: `WORK_CAREER_BRIDGE_E2E_001_WorkActionUpdatesCareerHintReload`.
- Screenshot/evidence path: `Builds/Logs/work-career-bridge-e2e.png`.
- Save/load expectation: work result, career/candidate delta, relationship delta, and hint persist once.
- Performance risk: low-medium; work result summaries should not rebuild all career candidates unnecessarily.
- Why deferred: overlaps with candidate deepening and should follow a clearer route-unlock pattern.

### Scenario: Part-time work career bridge

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the named part-time work marker.
3. Use actual interact key or visible UI button to perform the work action.
4. Verify visible result UI text in Game View.
5. Verify runtime state on StudentLife progress, relationship progress, and career/candidate progress.
6. Capture screenshot to `Builds/Logs/work-career-bridge-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify work/career state restores without duplicate deltas.
10. Verify Objective Journal Hints or day result shows the next work/career action.

### 6. Housing Interior Unlock Placement Polish

- Player value: home rewards become visible and persistent.
- Affected systems: Housing, Interiors, FurniturePlacement, SaveService, Quest/Discovery rewards, Objective Journal.
- Required data assets: one unlockable furniture item, one reward/unlock definition, one placement catalog entry.
- Required UI surface and ownership: placement UI in House; Objective Journal only for the next action hint.
- Required EditMode tests: unlock availability, placement save/load, catalog filtering, reward idempotency.
- Required PlayMode E2E scenario: `HOUSE_UNLOCK_PLACEMENT_E2E_001_UnlockPlaceScreenshotReload`.
- Screenshot/evidence path: `Builds/Logs/house-unlock-placement-e2e.png`.
- Save/load expectation: unlock and placed furniture restore exactly once.
- Performance risk: high; catalog is large and needs paging/filtering/caching.
- Why deferred: visible placement changes require the strictest verification and should be driven by a specific reward source.

### Scenario: Housing interior unlock placement

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the reward source that grants the furniture unlock.
3. Use actual interact key or visible UI button to claim the unlock.
4. Verify visible unlock text/state in Game View.
5. Verify runtime state on unlock service, furniture catalog, and placement availability.
6. Capture screenshot to `Builds/Logs/house-unlock-placement-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify unlock and placed furniture restore without duplication.
10. Verify Objective Journal or day result shows the next home action when applicable.

### 7. Host-Mode Student Life Save Smoke

- Player value: reduces risk before heavier dedicated-server validation.
- Affected systems: Network bootstrap, player identity, StudentLife sync, Quest sync, SaveService, world time.
- Required data assets: none beyond existing StudentLife/Quest data.
- Required UI surface and ownership: normal gameplay UI only; no new objective UI.
- Required EditMode tests: broadcaster/source validation, authority/save serialization, duplicate player cleanup.
- Required PlayMode E2E scenario: `NETWORK_HOST_STUDENT_LIFE_E2E_001_ActionSaveRestartReload`.
- Screenshot/evidence path: `Builds/Logs/network-host-student-life-e2e.png`.
- Save/load expectation: host session action persists across stop/restart with no stale network objects.
- Performance risk: high later for multi-client; medium for one-host smoke.
- Why deferred: important foundation, but current player-facing content integration is a better next slice.

### Scenario: Host-mode student life save smoke

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement after starting host-mode runtime to reach an outside-school activity.
3. Use actual interact key to perform the activity.
4. Verify visible UI/player state in Game View.
5. Verify runtime state on network session, owned player, StudentLife progress, and save metadata.
6. Capture screenshot to `Builds/Logs/network-host-student-life-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI or restart host session with the same slot.
9. Verify progress restores and no stale network player/session objects remain.
10. Verify Objective Journal or day result shows the next action when applicable.

### 8. Legacy Farm/Crop Regression Maintenance

- Player value: protects existing legacy play without distracting from Town/student-life direction.
- Affected systems: Farm, Crop, Generation, tools, save/load.
- Required data assets: only touched crop/tool/generation SOs for bugfixes.
- Required UI surface and ownership: existing Farm UI only.
- Required EditMode tests: crop/tool/generation regression matching the touched behavior.
- Required PlayMode E2E scenario: `LEGACY_FARM_REGRESSION_E2E_001_TouchedCropToolSaveReload`.
- Screenshot/evidence path: `Builds/Logs/legacy-farm-regression-e2e.png`.
- Save/load expectation: crop/tool/generation state persists without legacy side effects.
- Performance risk: medium if tilemap/generation is touched.
- Why deferred: not recommended as a feature goal; use only for regression or migration blockers.

### Scenario: Legacy farm regression maintenance

1. MainMenu -> New Game -> SPUM Confirm -> Town, then enter Farm through the existing route if the touched regression requires it.
2. Use actual keyboard movement to reach the named crop/tool/plot target.
3. Use actual interact key or visible UI button to perform the crop/tool action.
4. Verify visible Farm UI/tile state in Game View.
5. Verify runtime state on crop plot, tool effect, generation, or save component.
6. Capture screenshot to `Builds/Logs/legacy-farm-regression-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify touched legacy state restores without duplication.
10. Verify Objective Journal is unaffected unless the regression explicitly touches Town objective flow.

## Recommendation

Recommended next goal: **Discovery Clue Unlocks A Daily Plan Route**.

Runner-up goals:

- **NPC Routine Relationship Milestone**
- **Quest Chain Rewards An Interior Unlock**

Reason: Objective Journal and Daily Plan now have enough foundation to make exploration produce a concrete next action. This improves player-facing direction without adding another HUD, and it exercises discovery, knowledge/world state, route availability, save/load, and Objective Journal Hints with a manageable verification surface.

## Required Verification For This Document

This document is docs-only. Before commit, verify:

```powershell
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

No production code should change for this inventory refresh.
