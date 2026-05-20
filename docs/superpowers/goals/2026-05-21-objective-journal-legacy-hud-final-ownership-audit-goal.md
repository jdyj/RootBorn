# Goal: Objective Journal Legacy HUD Final Ownership Audit

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the House upgrade/interior loop E2E hardening cycle.

## Objective

Close the final Objective Journal ownership gap by auditing persistent objective-like UI creation, tightening ownership/source tests, and adding a player-facing E2E scenario proving objective guidance is owned by `ObjectiveJournalPanel` and exactly one `TrackedObjectiveHud` after progress and reload.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests first for behavior or regression changes.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, objective, or similar entity IDs.
- `ObjectiveJournalPanel` owns goal-like UI. `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Do not remove transient result, reward, dialogue, location log, quest log, or career detail panels unless they are acting as persistent objective guidance.
- Visible UI work is not complete until verified through actual PlayMode player-facing flow, including opening the Journal with `Tab`, clicking rows/buttons, inspecting Game View or Camera screenshot evidence, and verifying reload state.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`; restore them immediately if Unity tooling removes either file.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Tighten `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs` so persistent objective-like factories remain blocked and reviewed exceptions are explicit.
- Add or tighten Objective Journal source tests under `Assets/Tests/EditMode/UI/Objectives/` so duplicate stable keys across default sources cannot silently produce duplicate rows.
- Add or tighten tests that prove tracking restore reuses the existing `TrackedObjectiveHud` instead of creating another HUD.
- Add a player-facing E2E scenario:

```text
Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalOwnershipE2EScenarioTests.OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
```

- The E2E must use existing player-facing flows to create at least two objective guidance source states, open Objective Journal with `Tab`, select/click rows, track one item, verify exactly one `TrackedObjectiveHud`, capture screenshot evidence, reload the same save slot, and verify no duplicate rows or HUDs.
- If the current code still has reviewed non-objective panels such as `QuestChainLogPanel` or `CareerInterestPanel`, document in test failure messages why they are allowed and ensure they are not counted as persistent objective HUDs.

## Required TDD

RED:

- Add or tighten a source/contract test that fails while the final E2E method is missing or lacks explicit legacy-HUD absence, row duplicate, tracked-HUD uniqueness, screenshot, and reload assertions.
- Add or tighten an EditMode ownership test that fails if a production installer/factory creates a persistent objective-like UI outside `ObjectiveJournalPanel` or `TrackedObjectiveHud`.
- Add or tighten an aggregator/source test that fails when duplicate stable keys are emitted by default Objective Journal sources.

GREEN:

- The ownership/source tests pass with explicit reviewed exceptions only.
- The new E2E proves the player can use the Journal and one tracked HUD through actual UI flow.
- Reload restores Journal/tracked state without duplicate HUDs or duplicate objective rows.

## Required Verification

Run narrow Objective Journal and source tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalOwnershipE2EScenarioTests.OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
Get-ChildItem Builds/Logs/objective-journal-ownership-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If Unity test runs delete `Assets/AddressableAssetsData/link.xml` or `.meta`, restore both and rerun the preservation check before committing.

## Acceptance Criteria

- Persistent objective-like UI is owned only by `ObjectiveJournalPanel` and one `TrackedObjectiveHud`.
- Reviewed non-objective panels remain available only as transient or user-opened detail/log/result surfaces.
- Default Objective Journal sources do not emit duplicate stable keys that can create duplicate rows.
- Tracking uses the existing `TrackedObjectiveHud` and never creates an extra persistent HUD.
- The E2E opens Objective Journal with `Tab`, clicks visible UI, tracks one item, captures `Builds/Logs/objective-journal-ownership-e2e.png`, reloads the save slot, and verifies no duplicate legacy HUDs, rows, stable keys, or tracked HUDs.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[REFACTOR][UI][TEST] Objective Journal HUD ownership 최종 감사
```

If the implementation only tightens tests and no production code changes are required, use:

```text
[TEST] Objective Journal HUD ownership 최종 감사
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
