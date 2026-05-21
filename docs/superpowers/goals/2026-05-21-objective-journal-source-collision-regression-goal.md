# Goal: Objective Journal Source Collision Regression

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the Student Life scenario registry coverage gate.

## Objective

Harden Objective Journal source aggregation so Quest, Campaign, Daily Plan, Discovery route hints, Relationship milestones, Career hints, and completed-route entries cannot collide, duplicate, or leak into separate HUD ownership.

## Current Code Reality

- Existing Objective Journal source infrastructure already exists:
  - `ObjectiveJournalAggregator`
  - `ObjectiveJournalSourceCatalog`
  - `DailyPlanObjectiveSource`
  - `DailyPlanRouteHintObjectiveSource`
  - `MilestoneObjectiveSource`
  - `CareerInterestObjectiveSource`
  - `TrackedObjectiveHud`
- Existing regression coverage already includes:
  - `ObjectiveJournalSourceRegressionMatrixTests`
  - `ObjectiveJournalSourceCatalogTests`
  - `ObjectiveJournalUiOwnershipGateTests`
  - `ObjectiveJournalRegressionE2EScenarioTests`
- The remaining gap is naming and contract hardening: the source-collision scenario should be explicit, executable, and tied to the catalog/source ownership rules so future source additions cannot silently bypass it.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first.
- This cycle is a regression hardening cycle, not a new gameplay feature.
- Do not add production entity-ID branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, clue, world-state, recipe, resource, or similar IDs.
- Goal-like UI remains owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Do not introduce a new always-visible HUD, Canvas, panel, or widget for goals, quests, campaigns, hints, milestones, Daily Plan, relationship, or career guidance.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add or tighten EditMode contract coverage under:

```text
Assets/Tests/EditMode/UI/Objectives/
```

- The contract must require:
  - source collision scenario coverage is explicit and discoverable,
  - default source catalog still includes Daily Plan, route hints, quest log, quest chain, campaign, milestones, and career interest sources,
  - duplicate stable keys are owned by the first registered source,
  - route hints, relationship milestones, career hints, and quest rows can coexist,
  - completed Daily Plan routes are not active guidance,
  - source collision PlayMode evidence writes a screenshot.
- Add or update PlayMode source-collision coverage under:

```text
Assets/Tests/PlayMode/EndToEnd/
```

- Prefer reusing existing `ObjectiveJournalRegressionE2EScenarioTests` helpers or creating a narrow canonical wrapper test instead of duplicating large player-flow setup.
- Screenshot evidence path must be explicit:

```text
Builds/Logs/objective-journal-source-collision-e2e.png
```

- If an existing PlayMode scenario already covers the full collision path, add a contract that binds that existing scenario to this goal and avoids duplicate long-running tests.

## Required TDD

RED:

- Add an EditMode contract that fails until source-collision PlayMode coverage is explicit.
- Add or tighten an EditMode test that fails if completed Daily Plan route guidance can remain active.
- Add or tighten an EditMode test that fails if default source catalog omits any required source family.
- Add or tighten an ownership gate test that fails if a new objective-like always-visible HUD/Panel/Widget appears outside reviewed owners.

GREEN:

- Objective Journal EditMode source/collision/ownership tests pass.
- The source-collision PlayMode scenario or canonical existing scenario passes.
- Screenshot evidence exists and is non-zero.

## Required Verification

Run narrow EditMode tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
```

Run the player-facing collision scenario:

```powershell
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalSourceCollisionE2EScenarioTests
```

If the implementation intentionally reuses the existing regression scenario instead of adding a new class, run:

```powershell
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalRegressionE2EScenarioTests
```

Inspect screenshot evidence:

```powershell
Get-ChildItem Builds/Logs/objective-journal-source-collision-e2e*.png,Builds/Logs/objective-journal-regression-e2e*.png | Select-Object Name,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Multiple objective sources coexist without duplicate rows.
- Duplicate stable keys are owned by the first registered source.
- Completed Daily Plan routes are filtered from active guidance.
- Discovery route hints, career hints, relationship milestones, quests, and Daily Plan rows keep stable ownership.
- Only `ObjectiveJournalPanel` and `TrackedObjectiveHud` own objective UI.
- Player-facing PlayMode coverage opens Objective Journal through `Tab`, tracks a row, verifies HUD uniqueness, reloads, and captures screenshot evidence.
- Addressables `link.xml` and `.meta` remain preserved.

## Commit

Use:

```text
[TEST][UI] Objective Journal source collision regression
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
