# Goal: Milestone Completed Route Recommendation Filter

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Prevent completed milestones from continuing to emit their route recommended action IDs into day-result or Objective Journal next-action guidance.

## Non-Negotiable Constraints

- Keep milestone and route behavior ScriptableObject/data-driven.
- Do not branch production C# on specific milestone IDs, objective IDs, relationship IDs, NPC IDs, route IDs, activity IDs, quest IDs, or location IDs.
- Do not create a new goal-like UI, HUD, panel, Canvas, or source-owned tracked widget.
- `ObjectiveJournalPanel` remains the owner of goal-like guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Follow TDD: failing test first, then the smallest production fix.
- Keep unrelated dirty or untracked files untouched and unstaged.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging` if a test or Unity compilation fails unexpectedly
- `superpowers:verification-before-completion`

## Required Work

- Add a focused EditMode regression test proving `MilestoneProgress.ApplyDayResult` does not include a milestone route's recommended action after that milestone is completed.
- Keep active, incomplete milestone recommendations unchanged.
- Ensure the test uses synthetic ScriptableObject milestone data rather than production ID-specific branching.
- Implement the smallest change in milestone recommendation collection so completed milestones are filtered out of recommendation output.
- Do not change player-facing layout or create new UI surfaces in this cycle.

## Required TDD

RED:

- Add a test method:
  `Rootborn.Tests.EditMode.StudentLife.OpenEndedMilestoneGrowthTests.MILESTONE_EDIT_012_CompletedMilestoneRoutesDoNotRemainRecommended`
- Confirm it fails because completed milestones currently still contribute route recommendation IDs.

GREEN:

- Update milestone recommendation collection to include only incomplete milestones.
- Re-run the new test and related StudentLife/UI Objective tests.

## Required Verification

Run:

```powershell
tests_run EditMode testMethod Rootborn.Tests.EditMode.StudentLife.OpenEndedMilestoneGrowthTests.MILESTONE_EDIT_012_CompletedMilestoneRoutesDoNotRemainRecommended
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Active milestones still emit route recommendation IDs.
- Completed milestones do not emit stale route recommendation IDs.
- The fix is generic and contains no entity ID-specific branching.
- Objective Journal ownership rules remain unchanged.
- The cycle is committed separately.

## Commit

Use:

```text
[BUGFIX][TEST] 완료 milestone route 추천 필터링
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
