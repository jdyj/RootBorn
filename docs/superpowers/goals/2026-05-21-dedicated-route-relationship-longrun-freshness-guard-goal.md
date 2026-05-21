# Goal: Dedicated Route Relationship Long-Run Freshness Guard

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add the same pre-launch build freshness protection to `Scripts/qa/run-dedicated-route-relationship-longrun.ps1` that the dedicated route/relationship smoke runner already uses, so long-run validation cannot accidentally pass against stale or incomplete server/client build artifacts.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Keep this cycle limited to the long-run runner and its EditMode contract.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Tighten `Assets/Tests/EditMode/Network/DedicatedRouteRelationshipAutoplayContractTests.cs` so it fails while the long-run runner lacks a freshness guard.
- Update `Scripts/qa/run-dedicated-route-relationship-longrun.ps1` to:
  - validate server/client executables,
  - validate server/client `Rootborn.Game.dll`,
  - validate server/client Addressables runtime settings,
  - compare build artifact timestamps against `DirectValidationRouteRelationshipAutoplayInstaller.cs`,
  - compare build artifact timestamps against `run-dedicated-route-relationship-longrun.ps1`,
  - fail before launching server/client processes with remediation text naming `BuildServerWindows64` and `BuildClientWindows64`.
- Do not rerun the full long-run unless the contract or parser checks indicate the script is invalid.

## Required TDD

RED:

- Add an EditMode contract test that expects `Assert-FreshBuildArtifacts`, `Assert-RequiredArtifact`, artifact paths, source path, runner path, and build remediation text in the long-run script.
- Confirm the test fails before editing the runner.

GREEN:

- Add the freshness guard to the long-run runner.
- Confirm the contract test passes.

## Required Verification

Run:

```powershell
tests_run EditMode testClass DedicatedRouteRelationshipAutoplayContractTests
powershell.exe -NoProfile -Command "$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-route-relationship-longrun.ps1', [ref]$null, [ref]$null)"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Long-run runner fails before process launch when required build artifacts are missing or stale.
- Freshness failure messages name the server and client build remediation methods.
- Existing long-run action/observe cycle behavior remains unchanged.
- Narrow EditMode contract and baseline gates pass.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated route relationship long-run freshness guard 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
