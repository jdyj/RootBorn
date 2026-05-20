# Goal: Discovery Route Unlock E2E Diagnostics Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Harden the strict player-facing Discovery route unlock PlayMode tests so future failures identify the exact visible flow step, runtime state, save/load state, or screenshot evidence that failed.

## Non-Negotiable Constraints

- This is a PlayMode test diagnostics/refactor cycle, not a gameplay behavior cycle.
- Do not replace real player-facing movement, interact key input, UI click/selection, scene transition, Objective Journal Tab flow, Daily Plan board selection, or screenshot evidence with direct method calls.
- Do not reduce existing assertions.
- Do not create a new permanent HUD, Canvas, UI ownership surface, or route-specific guidance UI.
- `ObjectiveJournalPanel` remains the owner of route guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for C# tests.
- Do not branch production C# on route, clue, world-state, location, activity, quest, campaign, NPC, crop, or tool IDs.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging` if any RED/GREEN or verification failure is unexpected
- `superpowers:verification-before-completion`

## Required Work

- Improve diagnostics in the existing first and second Discovery route unlock PlayMode E2E tests.
- Add or tighten shared diagnostics helpers for screenshot evidence, visible button lookup, Objective Journal row selection, Daily Plan route row selection, and tracked HUD single-instance checks.
- Cover both strict route unlock flows:
  - `DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload`
  - `DiscoveryRouteUnlockSecondSliceE2EScenarioTests.DISCOVERY_ROUTE_E2E_002_SaveSlotSecondClueInterpretationUnlocksSecondDailyPlanRouteAndReload`
- Improve failure messages around:
  - MainMenu SaveSlot entry into Town
  - keyboard movement to the clue or interpretation source
  - interact key response
  - visible choice button click
  - world-state flag activation
  - Daily Plan board row visibility
  - route selection
  - Objective Journal category and row visibility
  - tracked HUD single-instance state
  - screenshot file existence and non-zero length
  - reload persistence for clue, interpretation, world-state, route availability, route selection, Journal state, and tracked HUD state

## Required TDD

RED:

- Add a diagnostics-focused test first. The RED may be an EditMode contract for a test helper if the existing PlayMode flows already pass behaviorally.
- The RED failure must prove that a missing screenshot, missing visible button, or missing Objective Journal/Daily Plan row reports weak diagnostics before the helper is improved.

GREEN:

- Implement the smallest test-support change needed so the diagnostic contract passes.
- Re-run both route unlock PlayMode E2E tests and confirm they still use the actual player-facing flow.

## Required Verification

Run:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockSecondSliceE2EScenarioTests.DISCOVERY_ROUTE_E2E_002_SaveSlotSecondClueInterpretationUnlocksSecondDailyPlanRouteAndReload
Get-ChildItem Builds/Logs/discovery-route-unlock-e2e.png,Builds/Logs/discovery-route-unlock-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If a diagnostics helper is moved into a separate EditMode-testable class, also run the exact helper test that proves the RED/GREEN contract.

## Acceptance Criteria

- Both route unlock E2E tests still use actual player-facing input/UI flow.
- Both screenshot evidence files are freshly produced or freshly inspected with non-zero length.
- Missing screenshot, missing button, missing Daily Plan row, and missing Objective Journal row failures include available candidates or the expected evidence path.
- No production behavior changes are included unless a real blocking bug is found and fixed in the same TDD cycle.
- The workspace is clean after the cycle commit.

## Commit

Use:

```text
[TEST] Discovery route unlock E2E 진단 강화
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
