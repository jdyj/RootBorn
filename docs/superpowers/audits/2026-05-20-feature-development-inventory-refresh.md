# Feature Development Inventory Refresh

Date: 2026-05-20

Scope: Objective Journal stabilization is now complete enough to choose the next feature goal from current code reality. This inventory inspected production code, ScriptableObject data, tests, and existing docs for the required domains.

## Inspection Evidence

Commands used:

```powershell
rg --files Assets\Scripts\Game Assets\Scripts\UI Assets\Scripts\Network Assets\Tests docs\superpowers | rg "StudentLife|Career|Quest|Relationship|Npc|Schedule|Exploration|Discovery|Knowledge|House|Interior|Network|Server|Farm|Crop|Generation|Objective|Milestone|Location"
Get-ChildItem Assets\Data -Directory | ForEach-Object { ... }
rg -n "public IEnumerator .*E2E|\[UnityTest\]" Assets\Tests\PlayMode\EndToEnd Assets\Tests\PlayMode\Interiors Assets\Tests\PlayMode\Housing Assets\Tests\PlayMode\Network Assets\Tests\PlayMode\Farming Assets\Tests\PlayMode\Quests -g "*.cs"
```

Code/data/test signal snapshot:

| Area | Production C# signal | Data signal | Test signal |
| --- | ---: | ---: | ---: |
| Student Life | `Assets/Scripts/Game/StudentLife` 143, `Assets/Scripts/UI/StudentLife` 23 | `Assets/Data/StudentLife` 350 | `Assets/Tests/EditMode/StudentLife` 39, many EndToEnd tests |
| Quest / Quest Chain | `Game/Quests` 35, `UI/Quests` 8 | `Assets/Data/Quests` 88 | `EditMode/Quests` 11, `PlayMode/Quests` 9, multiple EndToEnd tests |
| Discovery / Knowledge | `Game/DiscoveryClues` 32, `UI/DiscoveryClues` 5, `Game/Knowledge` 6 | `DiscoveryClues` 102, `Knowledge` 7 | `EditMode/DiscoveryClues` 6, discovery/encyclopedia/world usage E2E |
| Housing / Interiors | `Game/Housing` 10, `UI/Housing` 3, `Game/Interiors` 10, `UI/Interiors` 10 | `Housing` 15, `Interiors` 6434 | `EditMode/Interiors` 15, `PlayMode/Interiors` 13, `PlayMode/Housing` 1 |
| Network | `Assets/Scripts/Network` 13 | no large standalone data domain | `EditMode/Network` 9, `PlayMode/Network` 1 |
| Legacy Farm / Crop / Generation | `Game/Farming` 2, `Game/Generation` 2 | `Crops` 7, `Generations` 4 | `EditMode/Farming` 3, legacy farm/generation PlayMode coverage |

## Domain Inventory

### Student Life Daily Activity Loop

- Production code signal: strong. Daily activity, location activity, outside-school growth, part-time work, self-study, day result, location state, campaign, milestone, relationship conditions, and daily event installers exist.
- ScriptableObject/data signal: strong. `Assets/Data/StudentLife` is the largest gameplay data cluster after Interiors.
- Coverage signal: strong. Examples include `STUDENT_DAY_E2E_001_011`, `OUTSIDE_FOUNDATION_E2E_001_007`, `STUDY_E2E_001_006`, `WORK_E2E_001_006`, `LOCATION_STATE_E2E_001_011`, `MILESTONE_E2E_001_010`.
- Player-facing gap: the loops exist as separate slices, but there is still no explicit daily plan route UI that lets the player choose a non-school growth route, execute it, and see the chosen route reflected through Objective Journal/result UI.
- Risk if next: medium. It touches UI, day state, Objective Journal, save/load, and data balancing, but existing E2E coverage gives a good safety net.
- Recommended next slice: daily plan route selection and result summary integration, with at least two alternative growth routes.
- Minimum PlayMode scenario required: MainMenu -> New Game -> SPUM Confirm -> Town -> open planning UI -> choose non-school route -> walk to location -> interact -> apply activity -> end day -> verify result UI and Objective Journal next hint -> screenshot -> reload same slot.

### Career Interest / Candidate / Job Loop

- Production code signal: strong for interest and candidate. Career interest selection, recommendations, linked quest/candidate progress, and panels exist.
- ScriptableObject/data signal: strong. `Assets/Data/Careers` has 87 files.
- Coverage signal: strong for interest/candidate. Examples include `CAREER_INTEREST_E2E_001_013`, `CAREER_CANDIDATE_E2E_001_010`, `QUEST_CHAIN_E2E_001_014`.
- Player-facing gap: job loop is less visible than interest/candidate. The player can build toward career hints, but a concrete job/part-time work to career bridge is not yet the clearest next step.
- Risk if next: medium-high. It may require new data, reward economy, relationship/location availability, and Objective Journal hints.
- Recommended next slice: connect one part-time work or mentorship route to career candidate deepening without making school the only path.
- Minimum PlayMode scenario required: Town -> select career interest through visible UI -> walk to related location -> perform job/mentorship action -> see candidate progress and Objective Journal hint -> claim/record linked reward safely -> screenshot -> reload.

### Quest And Quest-Chain Loop

- Production code signal: strong. Quest definitions, objectives, conditions, rewards, completion effects, quest chain state, QuestLogPanel, QuestChainLogPanel, Objective Journal sources, and network state broadcaster exist.
- ScriptableObject/data signal: strong. `Assets/Data/Quests` has 88 files.
- Coverage signal: strong. Examples include quest reward idempotency, quest chain E2E, world-state quest E2E, and multiple PlayMode quest regression tests.
- Player-facing gap: quest chain UI and Objective Journal now work, but the next feature should avoid adding more quest UI. A better slice is a quest that meaningfully connects existing discovery/career/housing systems.
- Risk if next: medium. Reward and inventory transaction rules are strict; new quest data must stay SO-driven.
- Recommended next slice: one cross-domain quest chain that starts from discovery, points to a career or help action, and unlocks a housing/interior reward.
- Minimum PlayMode scenario required: Town -> NPC or discovery prompt -> accept quest/chain via visible UI -> track in Objective Journal -> perform required action -> claim reward -> verify inventory/reward idempotency -> screenshot -> reload and retry claim.

### NPC Relationship And Schedule Loop

- Production code signal: moderate. NPC schedule, dialogue, relationship condition and location NPC runtime installers exist.
- ScriptableObject/data signal: moderate. `Assets/Data/NPCs` has 43 files, but `Assets/Data/Relationships` has only 2 files.
- Coverage signal: moderate. Examples include `NPCSCHEDULE_E2E_001_011`, `RELCOND_E2E_001_011`, `LOCNPC_E2E_001_009`.
- Player-facing gap: schedules and relationship effects are present, but player-readable relationship progression and NPC routine expectations are thin.
- Risk if next: medium. It touches time, location state, dialogue, relationship data, and possible network visibility.
- Recommended next slice: one NPC daily routine plus relationship milestone surfaced as Objective Journal hint, without new always-visible HUD.
- Minimum PlayMode scenario required: Town -> wait/advance time through real UI/input -> observe NPC relocation -> interact -> choose help/dialogue action -> relationship changes -> Objective Journal hint updates -> screenshot -> reload and verify NPC/relationship state.

### Exploration, Discovery Clue, And Knowledge Loop

- Production code signal: strong. Exploration interactions, choices, clue discovery, interpretation, encyclopedia, world usage, and knowledge unlock paths exist.
- ScriptableObject/data signal: strong. `Assets/Data/DiscoveryClues` has 102 files and `Assets/Data/Knowledge` has 7 files.
- Coverage signal: strong. Examples include `DISCOVERY_CLUE_E2E_001_011`, `CLUE_INTERPRET_E2E_001_011`, `ENCYCLOPEDIA_E2E_001_008`, `WORLD_USAGE_E2E_001_010`.
- Player-facing gap: discovery and interpretation exist, but the loop can feel detached unless it feeds a next action in Objective Journal and a tangible reward or route.
- Risk if next: medium-low. Systems are well tested, but content/data volume can grow quickly.
- Recommended next slice: discovery clue -> interpretation choice -> unlock a non-school growth route or house reward hint.
- Minimum PlayMode scenario required: Town -> move to discovery point -> interact -> visible clue UI -> open interpretation UI -> choose interpretation -> verify knowledge/encyclopedia/world state -> Objective Journal hint -> screenshot -> reload and dedupe.

### Housing / Interior Reward Loop

- Production code signal: strong for placement and interior mechanics; moderate for progression reward integration. House upgrade and interior placement systems exist.
- ScriptableObject/data signal: very strong for furniture/interiors. `Assets/Data/Interiors` has 6434 files and `Assets/Data/Housing` has 15 files.
- Coverage signal: strong for isolated placement, camera, generation, and availability; weaker for Town gameplay reward integration. There is `HousePlacementSaveLoadE2EScenarioTests`, many interiors PlayMode tests, and one housing PlayMode suite.
- Player-facing gap: the player can place/interact with interiors, but the route from Town growth/quest/discovery reward to a meaningful home upgrade or furniture unlock is not the most visible slice yet.
- Risk if next: high. Visible placement changes require strict PlayMode visual verification, saved scene/asset/runtime state inspection, screenshot evidence, and persistence checks.
- Recommended next slice: unlock one furniture/interior reward from an existing Town action, then place it in House through the real placement UI.
- Minimum PlayMode scenario required: Town -> complete quest/discovery/activity reward -> travel to House -> open placement mode -> select unlocked furniture -> place with mouse -> screenshot Game View -> reload save -> verify furniture persists and locked/unlocked state remains correct.

### Network / Dedicated Server Save-Load Loop

- Production code signal: moderate. Session types, bootstrap, player identity/view/input gates, quest/student-life broadcasters, and world time state exist.
- ScriptableObject/data signal: indirect; network mainly synchronizes existing gameplay data.
- Coverage signal: moderate for EditMode and weak for full player-facing dedicated server. There are 9 EditMode network tests and one host PlayMode test, but the goal docs still warn that full executable server/client validation can be blocked by environment.
- Player-facing gap: host session is covered, but real dedicated server + client save/load loop is not yet a routine green gate.
- Risk if next: high. Requires environment validation, build targets, process orchestration, port handling, and multiplayer determinism.
- Recommended next slice: preflighted host-mode plus documented dedicated-server executable smoke, only after local build path is stable.
- Minimum PlayMode scenario required: Host session -> local player spawn -> perform student/quest action -> verify server-authoritative state and no stale network objects -> stop/restart host -> reload slot; dedicated server/client executable smoke is required before claiming full completion.

### Legacy Farm / Crop / Generation Role

- Production code signal: low and intentionally legacy. Farming and generation runtime files are small and should remain regression-protected rather than drive the main loop.
- ScriptableObject/data signal: small. `Assets/Data/Crops` has 7 files and `Assets/Data/Generations` has 4 files.
- Coverage signal: present. Farming and generation tests still exist.
- Player-facing gap: legacy Farm/Crop/Generation is not the current student-life MVP direction and should not become the next feature unless it blocks migration or regression.
- Risk if next: medium. It can distract from the Town/student-life direction and may reintroduce old UI/data assumptions.
- Recommended next slice: no new feature slice. Keep regression tests and only touch when migration or bugfix requires it.
- Minimum PlayMode scenario required: Farm entry -> crop/tool interaction through real input -> save/load -> verify no regression; generation transition only if directly touched.

## Ranked Next Feature Candidates

### 1. Daily Plan Route Selection And Result Integration

- Player value: gives the player a clear daily intention, action, result, and next hint while preserving free path growth.
- Affected systems: StudentLife, LocationActivity, StudentDayResultPanel, ObjectiveJournalPanel/provider sources, SaveService, Town runtime installers.
- Data assets needed: one or more `LifeActivityDefinition`/`LocationActivityDefinition` route group assets, route label/summary data, optional `MilestoneRouteDefinition` links.
- Tests needed: EditMode route model/selection persistence tests; PlayMode day flow E2E; Objective Journal source regression for route hint.
- Direct PlayMode scenario:
  1. MainMenu -> New Game -> SPUM Confirm -> Town.
  2. Open planning UI through actual UI/input.
  3. Select a non-school growth route.
  4. Walk to the target Town location with keyboard input.
  5. Interact with the location prompt using the interact key.
  6. Apply the activity through the visible UI button or prompt path.
  7. End the day through the day-end board.
  8. Verify day result UI shows planned route, actual action, changed stat, and next Objective Journal hint.
  9. Capture Game View screenshot.
  10. Load same save slot.
  11. Verify Objective Journal/tracked HUD restores the next route.
- Screenshot/evidence requirement: Game View screenshot of route/result UI and Objective Journal after reload.
- Why next: best balance of player value, existing system readiness, and manageable risk.

### 2. Discovery Clue To Route Unlock

- Player value: turns exploration into a clear next action rather than a standalone collectible.
- Affected systems: ExplorationInteraction, DiscoveryClues, Knowledge, WorldState, Objective Journal Hints, StudentLife route availability.
- Data assets needed: one clue, one interpretation, one knowledge/world state unlock, one route availability condition.
- Tests needed: EditMode clue/condition/outcome tests; PlayMode discovery -> interpretation -> route availability E2E.
- Direct PlayMode scenario:
  1. MainMenu -> New Game -> SPUM Confirm -> Town.
  2. Walk to discovery point with keyboard input.
  3. Interact with prompt.
  4. Open visible clue UI.
  5. Choose interpretation through visible button.
  6. Verify knowledge/world state unlock appears in UI.
  7. Open Objective Journal with Tab.
  8. Verify Hints shows the newly unlocked route.
  9. Capture Game View screenshot.
  10. Reload same slot.
  11. Verify the clue/knowledge unlock is not duplicated and the route hint persists.
- Screenshot/evidence requirement: clue UI plus Objective Journal hint screenshot.
- Why not first: valuable, but daily route integration should define where unlocked routes appear before adding more unlock sources.

### 3. Cross-Domain Quest Chain Rewarding Interior Unlock

- Player value: gives quests a tangible home reward and creates a loop from Town action to House customization.
- Affected systems: QuestChain, QuestReward, Inventory, Housing/Interiors, Objective Journal, Save/Load.
- Data assets needed: quest chain, reward definition, furniture unlock or housing stage asset, Objective Journal hint text.
- Tests needed: reward transaction/idempotency tests; placement availability tests; PlayMode quest -> unlock -> House placement E2E.
- Direct PlayMode scenario:
  1. MainMenu -> New Game -> SPUM Confirm -> Town.
  2. Talk to NPC or inspect quest source through real interaction.
  3. Accept and track quest chain in Objective Journal.
  4. Complete required Town action.
  5. Claim reward through visible quest UI.
  6. Travel to House.
  7. Open placement mode and select unlocked furniture.
  8. Place furniture with mouse.
  9. Capture Game View screenshot.
  10. Reload same slot.
  11. Verify furniture and reward claim state persist without duplicate reward.
- Screenshot/evidence requirement: Objective Journal tracked quest and House placed furniture screenshots.
- Why not first: high visual/persistence verification cost; better after daily route goal is solid.

### 4. NPC Routine Relationship Milestone

- Player value: makes Town feel alive and gives relationships a visible progression target.
- Affected systems: NPC schedule, DialoguePanel, RelationshipDefinition, StudentLife day/time, Objective Journal Hints.
- Data assets needed: one NPC schedule expansion, one relationship milestone, one dialogue/help action path.
- Tests needed: EditMode schedule/condition tests; PlayMode time-shift -> NPC interaction -> relationship milestone E2E.
- Direct PlayMode scenario:
  1. MainMenu -> New Game -> SPUM Confirm -> Town.
  2. Advance time through actual day/time UI or activity flow.
  3. Observe NPC relocation.
  4. Walk to NPC and interact.
  5. Choose relationship/help option in visible dialogue UI.
  6. Verify relationship value/milestone changes.
  7. Open Objective Journal and verify next relationship hint.
  8. Capture Game View screenshot.
  9. Reload save.
  10. Verify NPC schedule state and relationship milestone persist without duplicate application.
- Screenshot/evidence requirement: NPC interaction and Objective Journal hint screenshot.
- Why not first: relationship data is thin compared with StudentLife/Career/Discovery data, so it needs more content design.

### 5. Host-Mode Multiplayer Student Life Save Smoke

- Player value: validates that the core loop can survive multiplayer host session before dedicated server investment.
- Affected systems: Network session, player identity, StudentLife broadcaster, Quest broadcaster, SaveService.
- Data assets needed: none beyond existing StudentLife/Quest data.
- Tests needed: EditMode broadcaster coverage; PlayMode host session student action/reload smoke; later executable dedicated server smoke.
- Direct PlayMode scenario:
  1. Start host session through runtime bootstrap.
  2. Spawn owned local player.
  3. Move/interact with a student-life activity through real input.
  4. Verify server-authoritative progress and local UI update.
  5. Stop session.
  6. Start host session again with same save slot.
  7. Verify progress reloads and no stale network objects remain.
  8. Capture Game View screenshot.
  9. Document whether dedicated server executable validation was run or blocked.
- Screenshot/evidence requirement: host session Game View screenshot and network object count/log evidence.
- Why not first: important foundation, but dedicated server validation risk is higher than the next single-player feature slice.

## Single Best Next Goal

Recommended next goal: **Daily Plan Route Selection And Result Integration**.

Reason: it builds directly on the stabilized Objective Journal, existing StudentLife data, and current E2E coverage. It also supports the free path growth rule by making non-school daily routes visible and trackable before adding more discovery, quest, relationship, or housing content that needs a destination in the player-facing loop.

## Static Gate

This document changes docs only. Run before commit:

```powershell
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```
