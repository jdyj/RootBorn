# Goal: Dedicated Smoke Freshness Source Contract Normalization

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Normalize dedicated smoke runner freshness guards so managed DLL timestamps are compared only against compiled autoplay C# sources, while runner script names remain present in failure/remediation output.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Keep this cycle limited to dedicated smoke runner freshness contracts, runner scripts, and documentation.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, or network scenario IDs.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Update `DedicatedDiscoveryRouteUnlockAutoplayContractTests.cs` so it no longer requires `run-dedicated-discovery-route-unlock-smoke.ps1` inside the freshness source comparison block.
- Update `DedicatedResourceRecipeActivityAutoplayContractTests.cs` so it no longer requires `run-dedicated-resource-recipe-activity-smoke.ps1` inside the freshness source comparison block.
- Keep each runner script name present in failure/remediation text.
- Update:
  - `Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1`
  - `Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1`
- Do not touch child autoplay C# behavior.

## Required TDD

RED:

- Contract tests must fail while the runner script appears in `$requiredSources`.

GREEN:

- Remove the runner script from `$requiredSources`.
- Confirm the contract tests pass.
- Confirm PowerShell parser checks pass for both scripts.

## Required Verification

Run:

```powershell
tests_run EditMode testClass DedicatedDiscoveryRouteUnlockAutoplayContractTests
tests_run EditMode testClass DedicatedResourceRecipeActivityAutoplayContractTests
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Discovery and Resource/Recipe smoke runners compare managed DLL freshness only against their compiled autoplay C# source.
- Discovery and Resource/Recipe smoke runner names remain in missing/stale artifact messages.
- Existing dedicated smoke behavior, screenshots, save/reload checks, and marker validation are unchanged.
- Relevant EditMode contracts and baseline gates pass.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated smoke freshness source contract 정규화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

