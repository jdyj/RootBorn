# Goal: Student Life Scenario Registry and Coverage Gate

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the Discovery route unlock dedicated smoke cycle.

## Objective

Add a stricter scenario registry and coverage gate so Town, Student Life, Discovery, Daily Plan, relationship, career, Objective Journal, and dedicated validation scenarios cannot drift away from documented executable coverage.

## Current Code Reality

- Existing scenario infrastructure already exists:
  - `Assets/Tests/PlayMode/Scenarios/ScenarioId.cs`
  - `Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs`
  - `Scripts/ci/check-scenario-registry-sync.sh`
- The current shell gate only scans selected legacy categories from `.claude/rules/testing-discipline.md` and emits warnings for missing mappings.
- Current coverage is too narrow for the modern Student Life surface:
  - Town flow,
  - Daily Plan,
  - Discovery route unlock,
  - career,
  - relationship,
  - Objective Journal,
  - dedicated network smoke and matrix runners.
- Existing audit docs contain scenario names, but there is no current single catalog that maps active modern scenarios to tests or QA runners.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Write failing tests or failing gate assertions first.
- This cycle is a foundation/testing cycle, not a gameplay feature cycle.
- Do not change runtime gameplay behavior unless a test reveals a stale scenario reference that blocks the gate.
- Keep gameplay and entity behavior ScriptableObject-driven.
- Do not add production C# branching on quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, clue, world-state, recipe, resource, or similar entity IDs.
- Goal-like UI remains owned by `ObjectiveJournalPanel`; `TrackedObjectiveHud` is the only always-visible objective HUD and may show only one tracked objective.
- Preserve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Create a current scenario catalog:

```text
docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md
```

- The catalog must list active and deferred scenarios for:
  - `TOWN`
  - `LIFE`
  - `CAREER`
  - `REL`
  - `JOB`
  - `KNOW`
  - `STATUS`
  - `NET`
  - legacy `GEN`, `HEIR`, `CROP`
- Each active row must include:
  - scenario ID,
  - category,
  - owner domain,
  - evidence type: EditMode, PlayMode, QA runner, or doc-only deferred,
  - concrete test class, test method, or script path,
  - player-facing verification note when applicable.
- Deferred rows must include:
  - reason,
  - owner domain,
  - expected future validation type.
- Add or update a CI gate:

```text
Scripts/ci/check-scenario-registry-sync.ps1
```

- Keep the existing Bash gate unless the new PowerShell gate fully replaces it and all references are updated.
- The new or updated gate must fail closed when:
  - an active scenario row has no concrete mapped test or runner,
  - a mapped test or runner path is missing,
  - a scenario category appears in active test names but is absent from the catalog,
  - a deferred row omits a reason or expected future validation type.
- Add EditMode contract coverage under:

```text
Assets/Tests/EditMode/Scenarios/
```

- Contract tests must verify the catalog, gate script, and existing `ScenarioId.cs` agree for the selected modern categories.

## Required TDD

RED:

- Add or tighten an EditMode test that fails before the new catalog exists.
- Add or tighten an EditMode test that fails before the new gate checks active scenario rows.
- Add or tighten an EditMode test that fails before the gate recognizes modern categories such as `TOWN`, `LIFE`, `CAREER`, `REL`, `JOB`, and `NET`.
- Run the new gate before catalog implementation and confirm it fails for the expected missing catalog or missing mapping reason.

GREEN:

- Catalog exists and includes active/deferred rows for the required categories.
- Gate passes only when every active row maps to a concrete existing test or runner.
- Existing Bash gate still passes or is intentionally delegated to the new PowerShell gate.
- Scenario EditMode tests pass.

## Required Verification

Run narrow scenario tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios
```

If Unity MCP returns an unusable null response, run the equivalent Unity CLI command and inspect the XML result:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe' -batchmode -nographics -projectPath 'C:\Users\jdyj\farmer' -runTests -testPlatform editmode -assemblyNames Rootborn.Tests.EditMode -testFilter 'Rootborn.Tests.EditMode.Scenarios' -testResults Builds/Logs/editmode-scenario-registry-results.xml -logFile Builds/Logs/editmode-scenario-registry.log
Select-String -Path Builds/Logs/editmode-scenario-registry-results.xml -Pattern '<test-run'
```

Run registry gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
```

Run baseline gates:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

This cycle does not require PlayMode or screenshot verification because it does not change visible gameplay, UI, scene, prefab, tilemap, camera, placement, or interaction behavior.

## Acceptance Criteria

- A current scenario catalog exists under `docs/superpowers/audits/`.
- The catalog covers the required modern and legacy categories.
- Every active scenario maps to an existing test or QA runner.
- Every deferred scenario has a reason and future validation type.
- The new gate fails closed for missing active mappings.
- Existing scenario tests and gates pass.
- No runtime gameplay behavior changes are introduced.
- Addressables `link.xml` and `.meta` remain preserved.

## Commit

Use:

```text
[TEST][DOCS][CHORE] Student Life scenario coverage gate
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
