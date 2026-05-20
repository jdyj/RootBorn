# Goal: Post Objective Journal Four-Track Strict Hardening

Paste this prompt into a goal session and execute it as written.

## Objective

Complete all four post-Objective-Journal foundation tracks in order:

1. Objective Journal migration audit report
2. Objective Journal provider structure refactor
3. Objective Journal UI quality and scaling pass
4. Feature development inventory refresh

This is a foundation-stabilization goal, not a new gameplay feature goal. Do not start another isolated feature until these four tracks are completed, tested, visually verified where applicable, documented, and committed in separate reviewable commits.

## Non-Negotiable Constraints

- `ObjectiveJournalPanel` owns all goal-like UI: goals, quests, campaign, milestones, career hints, Daily Plan, and similar "what should I do now?" surfaces.
- `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Do not create a new permanent HUD, floating objective panel, duplicate Canvas objective widget, or source-specific tracked widget.
- Do not branch production C# on route, quest, campaign, milestone, career, activity, location, NPC, or entity IDs.
- Gameplay/entity definitions remain ScriptableObject/data-driven.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Every behavior change follows TDD: failing test first, then implementation, then regression verification.
- Visible or interactive changes require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence.
- Internal method calls, forced scene-only setup, JSON-only checks, or direct source method calls do not replace player-facing PlayMode verification.
- Keep unrelated dirty files untouched and unstaged:
  - `D Assets/AddressableAssetsData/link.xml`
  - `D Assets/AddressableAssetsData/link.xml.meta`
  - `M production/qa/evidence/registry-provider-town-flow.png`

## Required Skills

Use these skills in order where applicable:

- `superpowers:executing-plans` or `superpowers:subagent-driven-development`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`
- `superpowers:finishing-a-development-branch` only after all work and verification are complete

## Track 1: Objective Journal Migration Audit Report

### Purpose

Create a durable audit record proving the legacy objective HUD migration is complete and future work must not reintroduce separate goal-like HUD ownership.

### Required Output

Create:

- `docs/superpowers/audits/2026-05-20-objective-journal-migration-audit.md`

The audit must include:

- Removed or migrated legacy HUD/widget types:
  - `CampaignHudPanel`
  - `MilestoneHudPanel`
  - `QuestChainHudWidget`
  - `CareerInterestHudWidget`
- Current Objective Journal sources:
  - `student-day-guide`
  - `daily-plan`
  - `quest-log`
  - `quest-chain-log`
  - `campaign-progress`
  - `milestones`
  - `career-interests`
- Current tracked HUD ownership rule.
- Current E2E evidence paths and test methods.
- Remaining unrelated dirty files and confirmation they were not staged or reverted.
- Any remaining risk where docs, tests, or old comments still mention deleted HUD names.

### Static Audit Commands

Run and record results:

```powershell
rg -n "CampaignHudPanel|MilestoneHudPanel|QuestChainHudWidget|CareerInterestHudWidget|CampaignHudRoot|MilestoneHudRoot|QuestChainHudRoot|CareerInterestHudRoot" Assets/Scripts Assets/Tests docs -g "*.cs" -g "*.md"
rg -n "new GameObject\\(.*Hud|AddComponent<.*Hud|TrackedObjectiveHud|ObjectiveJournalPanel" Assets/Scripts Assets/Tests -g "*.cs"
git status --short
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Strict Acceptance Criteria

- No production `Assets/Scripts` reference creates or owns deleted legacy HUD types.
- Remaining matches are documented as tests, docs, or absence assertions.
- `TrackedObjectiveHud` remains the only compact persistent objective surface.
- Audit has no `TBD`, `TODO`, placeholder text, or unsupported claim.
- Commit: `[DOCS] Objective Journal 마이그레이션 감사 작성`

## Track 2: Objective Journal Provider Structure Refactor

### Purpose

Make Objective Journal provider/source composition easier to maintain after Daily Plan integration, without changing player-facing behavior.

### Required Work

Keep `ObjectiveJournalRuntimeProvider` focused on:

- binding `ObjectiveJournalPanel`
- binding `TrackedObjectiveHud`
- resolving registry, active save metadata, and player context
- composing source list
- refreshing panel and tracked HUD

Keep source-specific logic in focused source classes under:

- `Assets/Scripts/UI/Objectives/Sources/`

Required source IDs:

- `student-day-guide`
- `daily-plan`
- `quest-log`
- `quest-chain-log`
- `campaign-progress`
- `milestones`
- `career-interests`

If a source is already extracted, verify and tighten its tests instead of moving code unnecessarily.

### Required TDD

Add or tighten EditMode tests before production refactor:

- each source has the expected source ID and category
- default source catalog contains exactly the expected source IDs in deterministic order
- aggregator keeps all non-empty categories after source composition
- tracked stable key restore finds the intended item after source reorder
- refresh does not create duplicate tracked HUD state
- null registry/player/save context degrades without throwing

Expected RED must be real. If all behavior already exists, add the next missing behavior test first, such as deterministic source order or null-context degradation.

### Strict PlayMode Scenario: `OBJECTIVE_PROVIDER_STRICT_E2E_001`

Create or extend a PlayMode E2E under `Assets/Tests/PlayMode/EndToEnd/`.

Scenario steps:

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Wait for player, Objective Journal, tracked HUD, source provider, Daily Plan board, and at least one quest/campaign/hint source to be installed.
3. Press Tab using actual keyboard input.
4. Click `Goals`, `Quests`, `Campaign`, and `Hints` category buttons.
5. In every non-empty category, click a visible row.
6. Click Track for one selected row.
7. Verify `TrackedObjectiveHud` visible text changes and contains the selected stable item text.
8. Close Objective Journal with Tab.
9. Perform one real action that causes objective refresh:
   - outside-school activity, location activity, quest progress, campaign progress, or day-end board.
10. Reopen Objective Journal with Tab.
11. Verify the tracked HUD still shows one objective and did not duplicate lines or switch to an unrelated source.
12. Capture screenshot:
    - `Builds/Logs/objective-provider-strict-e2e.png`
13. Clear active context.
14. Load the same save slot through SaveSlot UI.
15. Press Tab.
16. Verify tracked objective restores from saved stable key.

Forbidden shortcuts:

- Directly calling source `BuildItems` as the only PlayMode proof.
- Manually setting selected category/row without button clicks.
- Passing only by checking save JSON.

### Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveProviderStrictE2EScenarioTests.OBJECTIVE_PROVIDER_STRICT_E2E_001_SourceCompositionTrackRefreshReload
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
Get-ChildItem Builds/Logs/objective-provider-strict-e2e.png | Select-Object Name,Length,LastWriteTime
```

### Commit

Commit with:

```text
[REFACTOR][UI][TEST] Objective Journal provider 구조 정리
```

## Track 3: Objective Journal UI Quality And Scaling Pass

### Purpose

Make Objective Journal stable with more rows, source refreshes, reordered items, and save/load restore. This track improves quality without adding new UI ownership surfaces.

### Required Work

Implement the smallest necessary improvements for:

- selected stable key survives refresh when still present
- selected item does not jump to unrelated row after source reorder
- tracked item restore works after item reorder
- empty categories show explicit empty text
- repeated refresh does not leave stale row GameObjects
- row list is scrollable, virtualized, paged, or visibly bounded when item count exceeds visible area
- visible row text is readable at 1920x1080
- category IDs remain stable: `Goals`, `Quests`, `Campaign`, `Hints`

Do not add decorative UI, nested cards, marketing-style layout, or a second objective panel.

### Required TDD

Add or tighten EditMode tests for:

- `SelectItem` by stable key survives a refresh with the same item
- selected row falls back predictably when stable key disappears
- tracked stable key restore selects intended item after reorder
- empty category detail text is explicit and not misleading
- repeated `SetItems`/refresh removes stale row objects
- many rows do not create unbounded visible overlap
- row count or content height is bounded/scrollable
- row labels wrap or truncate instead of overflowing button bounds

Expected RED must demonstrate a real missing UI guarantee.

### Strict PlayMode Scenario: `OBJECTIVE_UI_STRICT_E2E_001`

Create or extend a PlayMode E2E.

Scenario steps:

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Press Tab to open Objective Journal.
3. Navigate to a category with multiple rows.
4. Select a non-first row by visible UI click.
5. Click Track.
6. Record visible tracked HUD text and selected row text.
7. Close Objective Journal with Tab.
8. Perform a real player action that refreshes objective state:
   - Daily Plan route selection plus matching activity, or
   - quest progress, or
   - day-end board.
9. Reopen Objective Journal with Tab.
10. Verify the same tracked stable item is still shown and did not jump to the first row.
11. Navigate through all categories.
12. Verify no category displays blank detail text when empty.
13. Verify visible row list does not overlap detail panel or category rail.
14. Capture screenshot:
    - `Builds/Logs/objective-ui-scaling-e2e.png`
15. Load the same save slot.
16. Press Tab.
17. Verify tracked HUD restores exactly one tracked objective.

Additional runtime inspection required:

- selected category ID
- selected stable key
- tracked stable key
- number of row GameObjects
- visible text snapshot

### Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalUiScalingE2EScenarioTests.OBJECTIVE_UI_STRICT_E2E_001_SelectionTrackRefreshReloadScaling
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
Get-ChildItem Builds/Logs/objective-ui-scaling-e2e.png | Select-Object Name,Length,LastWriteTime
```

### Commit

Commit with:

```text
[UI][TEST] Objective Journal 스케일링 안정화
```

## Track 4: Feature Development Inventory Refresh

### Purpose

Refresh the next-feature inventory after Objective Journal, Daily Plan, and hardening work so the next feature is chosen from current code reality.

### Required Output

Create:

- `docs/superpowers/audits/2026-05-20-feature-development-inventory-strict-refresh.md`

Inspect current code, assets, scenes, tests, and docs for at least these domains:

- Student Life daily activity loop
- Daily Plan route loop
- Career interest/candidate/job loop
- Quest and quest-chain loop
- NPC relationship and schedule loop
- Exploration, discovery clue, rumor, knowledge loop
- Housing/interior reward loop
- Network/dedicated server save/load loop
- Legacy Farm/Crop/Generation role

For each domain, record:

- production code signal
- ScriptableObject/data signal
- scene/runtime installer signal
- current EditMode coverage
- current PlayMode coverage
- current screenshot/evidence signal
- player-facing gap
- architecture risk
- data-driven compliance risk
- performance/scaling risk
- recommended next slice
- minimum direct PlayMode scenario

### Required Ranking

Rank at least seven next feature candidates.

Each candidate must include:

- goal title
- player value
- affected systems
- required data assets
- required UI surface and ownership
- required EditMode tests
- required PlayMode E2E scenario
- screenshot/evidence path
- save/load expectation
- performance risk
- why it should be next or deferred

The document must name exactly one recommended next goal and two runner-up goals.

### Strict Scenario Templates

Include concrete scenario templates for at least seven candidates. Each template must use this format:

```md
### Scenario: Candidate-specific scenario name

1. MainMenu -> New Game -> SPUM Confirm -> Town.
2. Use actual keyboard movement to reach the named target for this candidate, such as a Daily Plan board, NPC, location marker, discovery point, job board, house door, or placement surface.
3. Use actual interact key or visible UI button to perform the candidate action, such as route selection, quest acceptance, clue interpretation, relationship action, job action, placement, or day-end.
4. Verify visible UI text/state in Game View.
5. Verify runtime state on the relevant component.
6. Capture screenshot to the candidate-specific `Builds/Logs/...png` path named in the candidate entry.
7. Clear active context.
8. Load same save slot through SaveSlot UI.
9. Verify state restores without duplication.
10. Verify Objective Journal or day result shows the next action when applicable.
```

The scenario templates must not be labels only. They must name the real target interaction, UI surface, save state, and screenshot path.

### Static Inventory Commands

Run and summarize:

```powershell
rg -n "class .*Interactor|RuntimeInstaller|ObjectiveJournal|TrackedObjectiveHud|DailyPlan|QuestChain|CareerInterest|Relationship|Knowledge|Discovery|House|Furniture|Network|Dedicated|Farm|Crop|Generation" Assets/Scripts Assets/Tests -g "*.cs"
Get-ChildItem Assets/Data -Recurse -File | Group-Object DirectoryName | Sort-Object Count -Descending | Select-Object -First 40 Count,Name
Get-ChildItem Assets/Tests/EditMode,Assets/Tests/PlayMode -Recurse -File -Filter "*.cs" | Select-Object FullName
Get-ChildItem docs/superpowers/specs,docs/superpowers/goals,docs/superpowers/plans,docs/superpowers/audits -File | Sort-Object LastWriteTime -Descending | Select-Object -First 40 Name,LastWriteTime
git status --short
git diff --check
```

### Commit

Commit with:

```text
[DOCS] 기능 개발 인벤토리 최신화
```

## Full Goal Verification

After all four tracks are complete, run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveProviderStrictE2EScenarioTests.OBJECTIVE_PROVIDER_STRICT_E2E_001_SourceCompositionTrackRefreshReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalUiScalingE2EScenarioTests.OBJECTIVE_UI_STRICT_E2E_001_SelectionTrackRefreshReloadScaling
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.StudentDayResultLoopE2EScenarioTests.STUDENT_DAY_E2E_001_011_PlayedActivityEndDayResultReloadAndNextDayLoop
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Verify screenshots:

```powershell
Get-ChildItem Builds/Logs/objective-provider-strict-e2e.png,Builds/Logs/objective-ui-scaling-e2e.png,Builds/Logs/daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
```

Expected:

- all listed tests pass
- screenshots exist and have non-zero length
- no static gate errors
- no production entity ID-specific branching
- no unrelated dirty file staged or reverted

## Done Means

- Four tracks are completed in order.
- Each track has a separate commit.
- Audit and inventory docs exist and have no placeholders.
- Provider/source structure is stable and tested.
- Objective Journal selection/tracking/refresh/reload behavior is stable under larger row counts.
- Player-facing PlayMode tests use real input/UI flow and capture screenshots.
- Objective Journal remains the single owner of goal-like UI.
- `TrackedObjectiveHud` remains the only always-visible objective HUD.
- The next feature goal is selected from the refreshed inventory.
- Unrelated dirty files remain untouched unless the user explicitly asks otherwise.
