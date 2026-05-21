# Goal: Knowledge/Status Activity Chain Scenario Catalog Activation

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Activate the existing Knowledge/Status Activity Chain PlayMode evidence in the Student Life scenario registry catalog so the scenario sync gates track it as an active player-facing scenario.

## Non-Negotiable Constraints

- Treat unrelated dirty or untracked workspace files as user-owned.
- Do not delete, revert, move, stage, or commit unrelated files.
- Do not edit `Assets/**/*.cs` directly through shell commands; use Unity MCP script tools for Unity C# files.
- Do not add production C# entity ID branching.
- Do not create a new objective HUD. Goal-like guidance remains owned by `ObjectiveJournalPanel`, with only one `TrackedObjectiveHud`.
- This cycle is catalog/test activation for existing evidence. Do not duplicate-implement the Knowledge/Status feature unless verification proves implementation is missing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Inspect the existing Knowledge/Status Activity Chain tests, source, scenario catalog, and screenshot evidence.
- Add an EditMode catalog gate test named `SCENARIO_REGISTRY_EDIT_012_CatalogActivatesKnowledgeStatusActivityChainScenario`.
- Register `KNOW-STATUS-ACTIVITY-001` as an active row in `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`.
- Ensure the catalog row references `Assets/Tests/PlayMode/EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs`.
- Ensure the row references `KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate`.
- Ensure the row references `Builds/Logs/knowledge-status-activity-chain-e2e.png`.

## Required TDD

- RED: add the catalog gate test first and run `StudentLifeScenarioRegistryCoverageGateTests`; it must fail because the active catalog row is missing.
- GREEN: update only the scenario catalog row needed for `KNOW-STATUS-ACTIVITY-001`.
- Re-run the focused EditMode test class and scenario sync gates.

## Required Verification

```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
Get-ChildItem Builds/Logs/knowledge-status-activity-chain-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- `KNOW-STATUS-ACTIVITY-001` is active in the scenario coverage catalog.
- The catalog gate test fails before the row exists and passes after the row is added.
- The scenario sync PowerShell and Bash gates pass.
- Existing screenshot evidence is present and non-zero.
- No production gameplay or UI code changes are introduced.

## Commit

Use:

```text
[TEST][DOCS] Knowledge Status activity scenario catalog 활성화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
