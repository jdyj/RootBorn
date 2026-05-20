# Goal: Objective Journal Route Source Regression Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated-server persistence harness feasibility audit.

## Objective

Harden Objective Journal route/source regression coverage so career, quest reward, relationship, completed Daily Plan, and reload/HUD ownership behavior cannot regress silently.

## Non-Negotiable Constraints

- Treat the current clean workspace as shared; do not stage or commit unrelated dirty/untracked files if they appear.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is primarily a regression-hardening cycle. Keep production behavior changes limited to defects exposed by failing tests.
- Do not add production C# branching on entity IDs, route IDs, quest IDs, activity IDs, location IDs, NPC IDs, reward IDs, crop IDs, tool IDs, or similar identifiers.
- `ObjectiveJournalPanel` owns goal-like guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not reintroduce legacy always-visible goal/quest/campaign/hint HUD objects.
- Visible UI regression coverage must include actual PlayMode SaveSlot flow, Tab-opened Objective Journal, tracking, Game View screenshot evidence, and reload.
- If `Assets/AddressableAssetsData/link.xml` or its `.meta` is removed by Unity tooling, restore it immediately before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add a compact EditMode regression matrix for Objective Journal source ownership and stable keys.
- Verify Daily Plan selected route goals, unlocked route hints, relationship milestone hints, quest-chain rows, and completed route filtering do not collide.
- Verify `TrackedObjectiveHud` is unique and hides stale tracked rows when the tracked stable key disappears.
- Add a PlayMode regression flow:
  `OBJECTIVE_JOURNAL_REGRESSION_PM_001_MultiSourceReloadNoDuplicateHud`
- The PlayMode flow must start from MainMenu SaveSlot, enter Town, produce multiple Journal source states, open Journal with Tab, track one row, capture screenshot, reload, and verify no duplicate rows/HUDs or legacy HUD objects.

## Required TDD

Write failing tests first:

- Source matrix test fails if multiple Objective Journal sources emit duplicate stable keys.
- Source matrix test fails if completed Daily Plan route remains active in Goals.
- Source matrix test fails if relationship milestone and Daily Plan route hints collide in Hints.
- Legacy ownership gate fails if legacy HUD object/class names are present.
- PlayMode E2E fails if duplicate Objective rows/HUDs appear after reload or stale legacy HUDs exist.

Expected RED:

- The new regression matrix or PlayMode test is missing, or it exposes a real duplicate/stale row gap.

Expected GREEN:

- Objective Journal aggregates multiple source types without duplicate stable keys, completed routes leave active Goals, `TrackedObjectiveHud` stays unique, and reload does not duplicate UI state.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalRegressionE2EScenarioTests.OBJECTIVE_JOURNAL_REGRESSION_PM_001_MultiSourceReloadNoDuplicateHud
Get-ChildItem Builds/Logs/objective-journal-regression-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

Inspect:

```text
Builds/Logs/objective-journal-regression-e2e.png
```

## Acceptance Criteria

- Objective Journal handles multiple source types without duplicate stable keys.
- Completed Daily Plan route state does not remain as an active Goal.
- Relationship and route hints can coexist without collisions.
- Legacy goal-like HUD ownership does not regress.
- `TrackedObjectiveHud` count remains exactly one.
- SaveSlot reload preserves Journal/tracking state without duplicate rows or HUDs.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[TEST][REFACTOR] Objective Journal source 회귀망 강화
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
