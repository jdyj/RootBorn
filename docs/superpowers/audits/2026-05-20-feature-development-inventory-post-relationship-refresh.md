# Feature Development Inventory Post Relationship Refresh

Date: 2026-05-20

Scope: current ROOTBORN code, data, tests, screenshots, recent commits, and recent goal docs after Discovery route unlock hardening, Objective Journal route source contract hardening, First Guide relationship milestone integration, and completed milestone route recommendation filtering.

## Inspection Evidence

Commands run for this refresh:

```powershell
rg -n "class .*Interactor|RuntimeInstaller|ObjectiveJournal|TrackedObjectiveHud|DailyPlan|QuestChain|CareerInterest|Relationship|Knowledge|Discovery|House|Furniture|Network|Dedicated|Farm|Crop|Generation" Assets/Scripts Assets/Tests -g "*.cs"
Get-ChildItem Assets/Data -Recurse -File | Group-Object DirectoryName | Sort-Object Count -Descending | Select-Object -First 40 Count,Name
Get-ChildItem Assets/Tests/EditMode,Assets/Tests/PlayMode -Recurse -File -Filter "*.cs" | Select-Object FullName
Get-ChildItem Builds/Logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 30 Name,Length,LastWriteTime
git log --oneline -12
git diff --check
git status --short
```

Evidence summary:

| Signal | Current evidence |
| --- | --- |
| Current workspace | Clean before this doc cycle; after starting it only this goal/audit documentation is untracked or modified. |
| Broad code scan | 5,645 matching lines across runtime and test code for interactors, installers, Objective Journal, Daily Plan, quest, relationship, discovery, housing, network, farm, crop, and generation surfaces. |
| Recent commits | Latest commits include completed milestone route filtering, First Guide relationship milestone slice, Objective Journal route contract hardening, Discovery route E2E diagnostics, Town Help relationship outcome wiring, and Addressables `link.xml` guard. |
| Data signal | Largest data volume is interiors/furniture, followed by DiscoveryClues, StudentLife, Quests, Careers, NPCs, and DailyPlans. Relationship data remains much thinner than discovery/career/interior data. |
| Test signal | PlayMode EndToEnd now includes Discovery route unlock first and second slices, First Guide relationship milestone E2E, Objective Journal provider/scaling/town flows, Daily Plan route selection, career, quest-chain, house/interior, and network smoke coverage. |
| Screenshot evidence | Latest visible evidence includes `first-guide-relationship-milestone-e2e.png`, `discovery-route-unlock-second-slice-e2e.png`, `discovery-route-unlock-e2e.png`, `town-help-relationship-e2e.png`, `daily-plan-route-e2e.png`, `objective-provider-strict-e2e.png`, and `objective-ui-scaling-e2e.png`. |
| Static status | `git diff --check` exited 0 during this refresh. |

## Domain Inventory

### Daily Plan Route Loop

- Production signal: strong. Daily Plan route definitions, progress, route runner, route board UI, runtime installer, day-end integration, Objective Journal source, and route hint source exist.
- Data signal: active under `Assets/Data/StudentLife/DailyPlans` with route unlock assets already connected to discovery slices.
- Runtime installer signal: strong in Town through Daily Plan board and player-facing E2E.
- EditMode coverage: strong for route context, runner, registry, progress, panel, runtime installer, day-end, and route unlock asset wiring.
- PlayMode coverage: `DailyPlanRouteSelectionE2EScenarioTests` plus Discovery route unlock E2Es that prove routes become visible after unlock.
- Screenshot signal: `Builds/Logs/daily-plan-route-e2e.png`, `discovery-route-unlock-e2e.png`, and `discovery-route-unlock-second-slice-e2e.png`.
- Player-facing gap: route unlocking now works for two discovery slices, but the route loop still needs richer non-discovery sources such as career practice, job work, relationships, or quest rewards.
- Architecture risk: medium. Route availability must stay source-driven and data-driven, not route-ID branches.
- Performance risk: medium as route count grows; route availability and Journal refresh should stay event/refresh driven.
- Recommended next slice: a career/job route source that deepens a candidate and appears through the same Daily Plan and Objective Journal contract.

### Discovery Clue Route Unlock Loop

- Production signal: strong. Discovery clue definitions, interpretation outcomes, world-state/knowledge progression, route unlock wiring, runtime UI, and save paths exist.
- Data signal: strong. DiscoveryClues has many source, interpretation, and outcome assets.
- Runtime installer signal: strong through Town discovery and interpretation flows.
- EditMode coverage: strong across discovery clue, interpretation, persistence, registry, route unlock asset wiring, and second-slice route wiring.
- PlayMode coverage: strict first and second Discovery route unlock E2Es with diagnostics and reload checks.
- Screenshot signal: `Builds/Logs/discovery-route-unlock-e2e.png` and `Builds/Logs/discovery-route-unlock-second-slice-e2e.png`.
- Player-facing gap: discovery can now unlock Daily Plan routes; the next discovery value should connect to a different reward type only when the receiving system is ready.
- Architecture risk: low-medium. The pattern is now tested, but adding many clues will stress catalog and Journal source ordering.
- Performance risk: medium for clue/encyclopedia lists and repeated source refresh.
- Recommended next slice: defer more discovery-route work until another domain becomes the receiver.

### Objective Journal Route And Source Loop

- Production signal: strong. `ObjectiveJournalPanel`, `TrackedObjectiveHud`, runtime provider, source catalog, Daily Plan, quest, quest-chain, campaign, milestone, career, and hint sources exist.
- Data signal: indirect. It aggregates ScriptableObject-backed systems without owning entity data.
- Runtime installer signal: strong through Modern UI auto installer and strict provider E2E.
- EditMode coverage: strong for panel behavior, scaling, provider, source catalog, Daily Plan integration hardening, ownership gates, and runtime source tests.
- PlayMode coverage: Objective Journal town, provider strict, UI scaling, UI quality, route unlock, Daily Plan, quest-chain, career, campaign, and milestone flows.
- Screenshot signal: `objective-provider-strict-e2e.png`, `objective-ui-scaling-e2e.png`, `objective-journal-town-e2e.png`, and the domain-specific Objective Journal screenshots.
- Player-facing gap: Journal ownership is stable; future work should use it, not expand it first.
- Architecture risk: medium if new feature work bypasses sources or reintroduces HUD widgets.
- Performance risk: medium. As row count grows, bounded lists and dirty refresh remain important.
- Recommended next slice: add a static guard that the new post-relationship candidate uses existing Journal sources only if a new domain source is required.

### NPC Relationship And Schedule Loop

- Production signal: improving. NPC schedule, dialogue, Town Help relationship outcome, relationship milestone objective, and Objective Journal milestone source now participate in a strict player-facing flow.
- Data signal: mixed. NPC and schedule data are moderate; relationship data remains small, with First Guide trust now wired into a milestone route.
- Runtime installer signal: strong enough for Town NPC schedule and help-action flows.
- EditMode coverage: First Guide relationship milestone wiring, town help relationship outcome, relationship condition, NPC schedule, and location/NPC foundation tests.
- PlayMode coverage: `FirstGuideRelationshipMilestoneE2EScenarioTests`, `TownHelpActionGrowthE2EScenarioTests`, NPC schedule and location NPC E2Es.
- Screenshot signal: `first-guide-relationship-milestone-e2e.png` and `town-help-relationship-e2e.png`.
- Player-facing gap: relationship progress has one readable slice, but not enough variety to feel like a system.
- Architecture risk: medium. More relationships can quickly become NPC-ID branches unless all actions, thresholds, and routes remain ScriptableObject-defined.
- Performance risk: medium. Schedule refresh must remain time/event driven.
- Recommended next slice: defer a second relationship slice until career/job route source work creates another non-discovery route source.

### Career Interest, Candidate, And Job Loop

- Production signal: strong for career interest/candidate, moderate for jobs. Career panels, progress components, quest links, candidates, hints, and Objective Journal career source exist.
- Data signal: strong. Career data is much richer than relationship data, with interest, candidate, practice skill/trait, and quest route assets.
- Runtime installer signal: strong for career interest/candidate flows in Town.
- EditMode coverage: strong for interest selection, candidate deepening, persistence, quest links, registry, and Objective Journal source behavior.
- PlayMode coverage: career interest selection, candidate deepening, career interest quest-chain state, and Objective Journal screenshots.
- Screenshot signal: `career-interest-objective-journal-e2e.png`.
- Player-facing gap: career selection and candidate deepening exist, but Daily Plan does not yet feel driven by career practice or job work.
- Architecture risk: medium-high. It touches Daily Plan, StudentLife progress, career candidates, jobs, Objective Journal, and save/load.
- Performance risk: medium as candidate recommendations grow.
- Recommended next slice: one career practice or part-time job unlocks or recommends a Daily Plan route and deepens a candidate.

### Quest And Quest-Chain Loop

- Production signal: strong. Quest definitions, objectives, rewards, quest-chain state, UI, Objective Journal sources, and save/isolation tests exist.
- Data signal: strong, especially career-route quest dialogue and quest-chain assets.
- Runtime installer signal: moderate-strong through NPC, quest, and Objective Journal flows.
- EditMode coverage: strong for quest logic, save, rewards, registry, UI, network isolation, quest-chain state, and registry.
- PlayMode coverage: quest-chain E2Es, quest UI flows, portal persistence, dialogue scenarios, and Objective Journal migration flows.
- Screenshot signal: `quest-chain-objective-journal-e2e.png`.
- Player-facing gap: quest rewards should connect to route, career, or housing progression rather than standalone reward loops.
- Architecture risk: medium. Reward transaction and duplicate-claim rules are strict.
- Performance risk: medium as quest lists grow.
- Recommended next slice: defer until career/job or housing is selected as the reward receiver.

### Housing And Interior Reward Loop

- Production signal: strong for house upgrades, construction, interior generation, furniture catalogs, placement mode, camera, and save/load.
- Data signal: very strong and large, especially furniture/interior assets.
- Runtime installer signal: strong in House and Interior scenes.
- EditMode coverage: strong for construction, upgrade definitions, placement models, catalogs, generation, importer, and camera support.
- PlayMode coverage: broad House/interior visual and placement flows.
- Screenshot signal: older but substantial house/interior evidence; any new visible placement work must produce fresh Game View evidence.
- Player-facing gap: Town-origin rewards into home customization remain a high-value future integration.
- Architecture risk: high. Visible placement, camera, tilemap, saved asset state, and catalog scale all need strict verification.
- Performance risk: high due to large furniture catalogs.
- Recommended next slice: defer until a small quest/career reward explicitly needs a home unlock.

### Network And Dedicated Server Loop

- Production signal: moderate. Network session, world time, player identity, quest/student-life interaction sync, and direct validation code exist.
- Data signal: indirect; network transports existing gameplay state.
- Runtime installer signal: moderate. Host-mode PlayMode coverage is present; dedicated executable validation is heavier.
- EditMode coverage: network source and behavior tests exist.
- PlayMode coverage: host/network smoke plus many single-player E2Es that are not dedicated-server proof.
- Screenshot/log signal: prior server/client build logs and long-run logs exist from 2026-05-18/19.
- Player-facing gap: no fresh post-Objective-Journal multiplayer validation of the new route/relationship slices.
- Architecture risk: high due to process orchestration, authority, persistence, and environment configuration.
- Performance risk: high for long-run sessions.
- Recommended next slice: host-mode smoke for Daily Plan route plus relationship milestone before dedicated-server repetition.

### Legacy Farm, Crop, And Generation Loop

- Production signal: intentionally small and legacy-oriented.
- Data signal: small Crop and Generation catalogs.
- Runtime installer signal: Farm and portal paths still exist for regression.
- EditMode coverage: crop growth, farming grid/tool, generation manager/simulator/heir tests.
- PlayMode coverage: Farm flow, seeded farm generation, farming scenarios, and older farm UI/character tests.
- Screenshot signal: older farm visual evidence exists.
- Player-facing gap: not aligned with the current Town/student-life route direction.
- Architecture risk: medium if expanded now because it can pull work back toward legacy assumptions.
- Performance risk: medium for generated tilemaps.
- Recommended next slice: no feature work unless a regression appears.

## Ranked Next Feature Candidates

Exactly one recommended next goal is named here, followed by two runner-up goals.

### 1. Recommended: Career Practice Unlocks A Daily Plan Route

- Player value: career interests become actionable next-day planning, not just a selected preference or candidate list.
- Affected systems: CareerInterest, CareerCandidate, StudentLife activities, Daily Plan route availability, Objective Journal Hints/Goals, SaveService.
- Required data assets: one career practice definition or existing practice asset, one Daily Plan route, one route condition/outcome, one candidate deepening signal, and one Journal hint path.
- Required UI surface and ownership: existing career UI for interest state, Daily Plan board for route selection, Objective Journal for guidance. No new HUD.
- Required EditMode tests: career practice route availability condition, candidate progress update, Daily Plan route source visibility, Objective Journal stable key/category behavior, save/load idempotency.
- Required PlayMode E2E scenario: `CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload`.
- Screenshot/evidence path: `Builds/Logs/career-daily-plan-route-e2e.png`.
- Save/load expectation: selected career interest, candidate progress, route availability, route selection, and tracked Journal state restore without duplication.
- Performance risk: medium; candidate recommendations and route availability should be evaluated on refresh, not per-frame.
- Why next: Discovery has already proven route unlocks twice, relationship has one milestone slice, and career has richer data ready to become another non-discovery Daily Plan source.

### Scenario: Career practice unlocks a Daily Plan route

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement or visible career UI flow to select a named career interest.
3. Use actual interact key at the Daily Plan board and verify the career practice route is locked or absent before the career condition is met.
4. Perform the visible career practice or mentor/work action through player input.
5. Verify runtime state on career interest/candidate progress and Daily Plan route availability.
6. Capture screenshot to `Builds/Logs/career-daily-plan-route-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify selected interest, candidate progress, and route availability restore without duplication.
10. Verify Objective Journal Hints or Goals shows the career practice next action through the existing source contract.

### 2. Runner-up: Quest Chain Rewards A Daily Plan Route

- Player value: completing a quest chain creates a concrete new planning option.
- Affected systems: QuestChain, QuestReward or completion effect, WorldState/Knowledge if needed, Daily Plan, Objective Journal, SaveService.
- Required data assets: one quest chain, one completion effect or route unlock flag, one Daily Plan route, and one Journal hint source path.
- Required UI surface and ownership: Quest/Objective Journal for quest tracking; Daily Plan board for route selection; no quest-specific HUD.
- Required EditMode tests: reward transaction/idempotency, route unlock condition, Objective Journal category/stable key, save/load.
- Required PlayMode E2E scenario: `QUEST_ROUTE_REWARD_E2E_001_CompleteChainUnlockRouteReload`.
- Screenshot/evidence path: `Builds/Logs/quest-route-reward-e2e.png`.
- Save/load expectation: quest state, reward/effect application, route availability, and Journal state restore once.
- Performance risk: medium; quest-chain and route source refresh should avoid repeated full scans outside refresh.
- Why runner-up: strong player value, but quest reward semantics need careful transaction/idempotency handling.

### Scenario: Quest chain rewards a Daily Plan route

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the named NPC or quest marker.
3. Use actual interact key and visible UI buttons to accept and complete the quest-chain step.
4. Verify visible quest/Objective Journal text in Game View.
5. Verify runtime state on quest chain progress and Daily Plan route availability.
6. Capture screenshot to `Builds/Logs/quest-route-reward-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify the route remains unlocked and the quest reward/effect is not duplicated.
10. Verify Objective Journal or day result shows the route next action when applicable.

### 3. Runner-up: Host-Mode Route And Relationship Save Smoke

- Player value: recent route and relationship slices are less likely to regress in multiplayer host mode.
- Affected systems: Network session, StudentLife progress, Daily Plan, relationship milestone, Objective Journal, SaveService.
- Required data assets: existing First Guide relationship milestone and existing Discovery/Daily Plan route data.
- Required UI surface and ownership: existing player-facing Town UI, Daily Plan board, Objective Journal, TrackedObjectiveHud.
- Required EditMode tests: network source audit for new StudentLife route/milestone save surfaces if gaps are found.
- Required PlayMode E2E scenario: `NETWORK_ROUTE_RELATIONSHIP_PM_001_HostProgressSaveReload`.
- Screenshot/evidence path: `Builds/Logs/network-route-relationship-host-e2e.png`.
- Save/load expectation: host progress for route unlock and relationship milestone survives reload with no duplicate application.
- Performance risk: high if expanded to dedicated server; keep first slice host-mode only.
- Why runner-up: important foundation, but heavier and less player-facing than the career route slice.

### Scenario: Host-mode route and relationship save smoke

1. MainMenu -> New Game or Host flow -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach a route unlock or First Guide relationship target.
3. Use actual interact key or visible UI buttons to perform the route or relationship action.
4. Verify visible UI text/state in Game View.
5. Verify runtime state on network player identity, StudentLife progress, route state, and relationship milestone.
6. Capture screenshot to `Builds/Logs/network-route-relationship-host-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI or host reload path.
9. Verify state restores without duplication.
10. Verify Objective Journal or day result shows the correct next action.

### 4. Quest Chain Rewards An Interior Unlock

- Player value: quest progress becomes a tangible home customization unlock.
- Affected systems: QuestChain, reward transaction, inventory/unlock state, housing, interiors, furniture placement, Objective Journal.
- Required data assets: one quest chain, one reward/effect, one furniture unlock, one furniture catalog entry or existing asset reference.
- Required UI surface and ownership: Objective Journal for quest guidance; House placement UI for placement. No new quest HUD.
- Required EditMode tests: reward capacity/idempotency, unlock persistence, furniture availability, placement eligibility.
- Required PlayMode E2E scenario: `QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`.
- Screenshot/evidence path: `Builds/Logs/quest-interior-reward-e2e.png`.
- Save/load expectation: quest state, reward claim, furniture unlock, and placed furniture persist without duplicate reward.
- Performance risk: high because furniture catalogs are large.
- Why deferred: valuable, but visible placement and catalog scale make it riskier than a career route slice.

### Scenario: Quest chain rewards an interior unlock

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the quest NPC or marker.
3. Use actual interact key or visible UI buttons to complete the quest action and claim the reward.
4. Verify visible quest/Objective Journal state in Game View.
5. Verify runtime state on quest chain, reward claim, unlock state, and furniture catalog availability.
6. Capture screenshot to `Builds/Logs/quest-interior-reward-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify reward cannot be claimed twice and the unlock persists.
10. Verify House placement UI or Objective Journal shows the correct next action.

### 5. Second NPC Relationship Route

- Player value: relationship progress starts to feel like a repeatable Town system instead of a single First Guide sample.
- Affected systems: NPC schedule, dialogue/help action, relationship definitions, milestones, Objective Journal, SaveService.
- Required data assets: second relationship definition, schedule/dialogue/help assets, relationship outcome, milestone objective, route, and milestone.
- Required UI surface and ownership: dialogue/help UI and Objective Journal milestone source only.
- Required EditMode tests: asset wiring, relationship outcome, milestone route, registry registration.
- Required PlayMode E2E scenario: `NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload`.
- Screenshot/evidence path: `Builds/Logs/npc-relationship-second-slice-e2e.png`.
- Save/load expectation: relationship and milestone state restore without duplicate delta.
- Performance risk: medium for schedule refresh as NPC count grows.
- Why deferred: better after another Daily Plan source proves route integrations are not discovery-only.

### Scenario: Second NPC relationship route

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the second NPC at the scheduled location.
3. Use actual interact key or visible help/dialogue button to perform the relationship action.
4. Verify visible dialogue/result text in Game View.
5. Verify runtime state on relationship progress, schedule placement, and milestone progress.
6. Capture screenshot to `Builds/Logs/npc-relationship-second-slice-e2e.png`.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify relationship and milestone state restores without duplication.
10. Verify Objective Journal Hints or Goals shows the correct relationship next action.

## Recommendation

The next goal should be **Career Practice Unlocks A Daily Plan Route**.

Reasoning: the project has already proven the route unlock pattern through discovery and stabilized Objective Journal ownership. Career data and tests are mature enough to become the next route source, while the scope can remain a small vertical slice. This gives player value without adding another UI surface and avoids jumping directly into higher-risk interiors or network validation.
