# Goal: Dedicated Core Gameplay Matrix Freshness Parity

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add pre-launch freshness protection to `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1` so the matrix fails before invoking child smoke runners when required server/client artifacts are missing or stale relative to compiled autoplay C# sources.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Keep this cycle limited to the matrix QA runner, its goal/audit documentation, and verification.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, or network scenario IDs.
- Goal-like UI ownership remains with `ObjectiveJournalPanel` and the single `TrackedObjectiveHud`; this cycle must not add goal-like UI.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Confirm the matrix runner currently lacks a top-level freshness guard.
- Add `Assert-RequiredArtifact` and `Assert-FreshBuildArtifacts` to `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`.
- Validate these artifacts before any slice process starts:
  - server executable
  - client executable
  - server `Rootborn.Game.dll`
  - client `Rootborn.Game.dll`
  - server Addressables runtime `settings.json`
  - client Addressables runtime `settings.json`
- Compare managed DLL freshness against compiled autoplay C# sources used by the matrix slices.
- Do not compare managed DLL timestamps against PowerShell runner scripts.
- Keep matrix orchestration, slice run names, ports, save slots, evidence checks, and output shape unchanged.
- Document the verification evidence in `docs/superpowers/audits/2026-05-21-dedicated-core-gameplay-matrix-freshness-parity.md`.

## Required TDD

RED:

- Run a PowerShell contract check that fails while `run-dedicated-core-gameplay-matrix.ps1` does not contain `Assert-FreshBuildArtifacts`.
- Confirm the failure occurs before modifying the runner.

GREEN:

- Add the guard.
- Re-run the contract check and confirm it passes.
- Run the PowerShell parser check.

If Unity MCP C# test editing becomes responsive, add a matching EditMode contract test in `DedicatedCoreGameplayMatrixContractTests.cs`. Do not use shell writes for that C# file.

## Required Verification

Run:

```powershell
if (-not (Select-String -Path Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -Pattern "Assert-FreshBuildArtifacts" -Quiet)) { throw "Matrix freshness guard missing." }
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-core-gameplay-matrix.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

Do not run the full matrix in this cycle. Matrix execution after guard parity is a separate evidence-refresh cycle.

## Acceptance Criteria

- The matrix runner fails before invoking child smoke runners if required server/client artifacts are missing.
- The matrix runner fails before invoking child smoke runners if server/client managed DLLs are older than compiled autoplay C# sources.
- The matrix runner does not compare managed DLL freshness against PowerShell runner timestamps.
- Existing matrix slice orchestration remains unchanged.
- Parser, Addressables preservation, whitespace, entity-ID branching, and workspace status gates pass.
- Audit documentation records RED/GREEN contract evidence and scope.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated core gameplay matrix freshness guard 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

