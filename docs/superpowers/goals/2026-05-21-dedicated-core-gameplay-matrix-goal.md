# Goal: Dedicated Core Gameplay Matrix

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated Daily Plan completion smoke cycle.

## Objective

Add an opt-in dedicated server/client gameplay matrix runner that composes the existing route/relationship, career Daily Plan route, and Daily Plan completion smokes into one slice-by-slice QA entry point with screenshots, reload checks, stale artifact guards, and cleanup evidence.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for the matrix runner contract.
- This cycle is QA orchestration, not new gameplay behavior.
- Do not change player-facing UI, scenes, prefabs, gameplay logic, ScriptableObject data, save formats, or direct validation autoplay behavior unless the matrix exposes a blocker that cannot be isolated to orchestration.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, career IDs, job IDs, crop IDs, tool IDs, furniture IDs, or similar entity identifiers.
- Goal-like UI remains owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add repository runner:

```text
Scripts/qa/run-dedicated-core-gameplay-matrix.ps1
```

- The matrix must run these existing dedicated slices:
  - route/relationship,
  - career Daily Plan route,
  - Daily Plan completion feedback.
- Each slice must use:
  - an isolated run name under the matrix run,
  - an isolated save slot,
  - an explicit port derived from `-StartPort`,
  - the existing dedicated smoke script for that slice,
  - the slice runner's action and observe-only reload passes,
  - screenshot assertions,
  - saved-state or restored-state log assertions delegated to the slice runner,
  - process cleanup assertions delegated to the slice runner.
- The matrix must fail closed if:
  - a slice script is missing,
  - a slice exits non-zero,
  - a slice log directory is missing,
  - required screenshots are missing or zero bytes,
  - required completion markers are missing,
  - required duplicate-state markers are missing for slices that own them.
- The matrix must print a concise per-slice result table and a final `Result = Passed` object when all slices pass.

## Required TDD

RED:

- Add an EditMode contract test that fails while `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1` is missing.
- The contract must require three slices, explicit `-StartPort`, explicit `-WaitSeconds`, isolated save slots, `Assert-SliceEvidence`, screenshot checks, action completion markers, observe completion markers, duplicate-state checks for the Daily Plan completion slice, and no generic `-directValidationAutoplay` launch.

GREEN:

- Network contract tests pass.
- The matrix runner completes all three slices through existing dedicated process smokes.
- Logs and screenshots exist for every slice.

## Required Verification

Run narrow contract tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
```

Run the real dedicated matrix:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7901 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -Recurse -File | Select-Object FullName,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If fresh build artifacts are missing or stale, stop with exact artifact evidence instead of claiming matrix completion.

## Acceptance Criteria

- Matrix covers route/relationship, career Daily Plan route, and Daily Plan completion dedicated slices.
- Every slice emits action, reload, screenshot, save or restored-state, and cleanup evidence through its existing runner.
- Matrix failure output identifies the slice and category of failure.
- No launched server/client process remains after success or failure.
- Addressables `link.xml` and `.meta` remain preserved.
- No production entity-ID branching or direct domain state mutation is introduced.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated core gameplay matrix 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
