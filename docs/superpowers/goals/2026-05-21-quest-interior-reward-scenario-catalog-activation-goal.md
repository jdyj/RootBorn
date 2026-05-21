# Goal: Quest/Interior Reward Scenario Catalog Activation

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Activate the existing Quest/Interior Reward PlayMode evidence in the Student Life scenario registry catalog so reward transaction and interior placement saved-state coverage is tracked as an active scenario.

## Non-Negotiable Constraints

- Treat unrelated dirty or untracked files as user-owned.
- Do not delete, revert, move, stage, or commit unrelated files.
- Do not edit `Assets/**/*.cs` directly through shell commands; use Unity MCP script tools for Unity C# files.
- Do not add production gameplay or UI code in this catalog activation unless verification proves the evidence is missing.
- Reward and inventory behavior must remain atomic and idempotent.
- Goal-like UI remains owned by `ObjectiveJournalPanel`, with only one `TrackedObjectiveHud`.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Inspect the existing `QuestInteriorRewardE2EScenarioTests` evidence and screenshot.
- Add a catalog gate test named `SCENARIO_REGISTRY_EDIT_014_CatalogActivatesQuestInteriorRewardScenario`.
- Add an active catalog row:
  - ID: `QUEST-INTERIOR-REWARD-001`
  - Category: `LIFE`
  - Owner: `Quest/Interior Reward`
  - Evidence Type: `PlayMode`
  - Evidence: `Assets/Tests/PlayMode/EndToEnd/QuestInteriorRewardE2EScenarioTests.cs`
  - Player-facing verification: `QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`, screenshot `Builds/Logs/quest-interior-reward-e2e.png`, exact-once reward unlock, furniture placement, reload persistence, and duplicate prevention.

## Required TDD

- RED: add the catalog gate test first and run `StudentLifeScenarioRegistryCoverageGateTests`; it must fail because the row is missing.
- GREEN: add only the active catalog row needed for `QUEST-INTERIOR-REWARD-001`.
- Re-run the focused EditMode test class and scenario sync gates.

## Required Verification

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
Get-ChildItem Builds/Logs/quest-interior-reward-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- `QUEST-INTERIOR-REWARD-001` is active in the scenario coverage catalog.
- The catalog gate fails before the row exists and passes after it is added.
- The PlayMode evidence passes and screenshot evidence is present and non-zero.
- Scenario sync gates pass with the refreshed catalog.
- No production gameplay or UI code changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Quest Interior reward scenario catalog 활성화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
