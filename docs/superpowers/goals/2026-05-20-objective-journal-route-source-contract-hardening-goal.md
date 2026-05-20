# Goal: Objective Journal Route Source Contract Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Lock down the contract between Daily Plan route sources, Discovery route unlock hints, Objective Journal aggregation, and `TrackedObjectiveHud` after the first two Discovery route unlock slices.

## Non-Negotiable Constraints

- `ObjectiveJournalPanel` owns all goal-like UI.
- `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Do not create feature-specific permanent HUDs, route hint canvases, quest hint canvases, or source-owned tracked widgets.
- Do not branch production C# on route, clue, world-state, location, activity, quest, campaign, NPC, crop, or tool IDs.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Every behavior change follows TDD.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging` if any test failure is unexpected
- `superpowers:verification-before-completion`

## Required Work

- Tighten EditMode contract tests for Daily Plan route sources and Objective Journal aggregation.
- Cover the real first and second Discovery route unlock Daily Plan route data in the same source contract:
  - `daily.route.library.archive-research`
  - `daily.route.workshop.repair-ledger`
- Assert that unlocked but unselected routes appear in `Hints`.
- Assert that a selected unlocked route appears in `Goals`.
- Assert that the same selected route is removed from `Hints` while other unlocked routes remain hinted.
- Preserve deterministic source ordering and the existing single `TrackedObjectiveHud` ownership tests.
- Change production UI/source code only if the tightened tests expose a real contract bug.

## Required TDD

RED:

- Add the real-data multi-route contract test first and run it.
- If current behavior is incomplete, confirm the failure identifies duplicate Hints/Goals behavior, missing route data, unstable ordering, or missing multi-route coverage.
- If the new contract already passes, do not change production code; keep the test as a preservation guard and record that no production fix was required.

GREEN:

- Implement the smallest production or test-support fix needed only if RED exposes a real bug.
- Re-run Objective Journal, StudentLife, DiscoveryClues, and strict second route unlock PlayMode verification.

## Required Verification

Run:

```powershell
tests_run EditMode testMethod Rootborn.Tests.EditMode.UI.Objectives.ObjectiveJournalDailyPlanIntegrationHardeningTests.RealDiscoveryRouteUnlocks_UseSingleHintAndGoalSourceContract
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.DiscoveryClues
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockSecondSliceE2EScenarioTests.DISCOVERY_ROUTE_E2E_002_SaveSlotSecondClueInterpretationUnlocksSecondDailyPlanRouteAndReload
Get-ChildItem Builds/Logs/discovery-route-unlock-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Objective Journal route source behavior is deterministic and covered by tests for the first and second real route unlock data.
- Selected routes are not duplicated in Hints.
- Another unlocked but unselected route remains visible in Hints.
- Repeated refresh/reload HUD ownership tests remain green.
- The strict second route unlock PlayMode flow still passes with screenshot evidence.
- No new goal-like UI ownership surface is introduced.
- The workspace is clean after the cycle commit.

## Commit

Use:

```text
[UI][TEST] Objective Journal route source 계약 강화
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
