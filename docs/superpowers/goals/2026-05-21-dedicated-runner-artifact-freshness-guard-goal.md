# Goal: Dedicated Runner Artifact Freshness Guard

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated career Daily Plan smoke exposed stale server artifacts and missing server Addressables runtime data.

## Objective

Make dedicated QA runners fail before launching server/client processes when required build artifacts are missing or stale, so process-level smoke tests do not produce misleading gameplay failures from outdated executables.

## Non-Negotiable Constraints

- Treat the current clean workspace as shared; do not stage or commit unrelated dirty/untracked files if they appear.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# test changes.
- This is a foundation/test cycle, not gameplay behavior work.
- Do not change player-facing UI, scenes, prefabs, gameplay behavior, Addressables groups, or data definitions.
- Keep gameplay/entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity, route, quest, activity, location, NPC, crop, tool, furniture, career, job, or similar IDs.
- If Unity tooling removes `Assets/AddressableAssetsData/link.xml` or `.meta`, restore both before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add or tighten EditMode contract tests for dedicated smoke scripts.
- Require dedicated runner scripts to check:
  - server executable exists,
  - client executable exists,
  - server `Rootborn.Game.dll` exists,
  - client `Rootborn.Game.dll` exists,
  - server `StreamingAssets/aa/settings.json` exists,
  - client `StreamingAssets/aa/settings.json` exists,
  - server/client `Rootborn.Game.dll` timestamps are not older than the source files required by that runner.
- Add the guard to:
  - `Scripts/qa/run-dedicated-route-relationship-smoke.ps1`
  - `Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1`
- Fail before process launch with a clear build remediation message when artifacts are stale or missing.

## Required TDD

RED:

- Add a contract assertion that fails while the dedicated route/relationship and career Daily Plan smoke scripts do not contain artifact freshness guard functions and required artifact checks.

GREEN:

- Add artifact freshness guard functions to both scripts.
- Re-run the Network EditMode tests and confirm the contracts pass.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-smoke.ps1 -RunName network-dedicated-route-relationship-freshness-guard -Port 7875 -WaitSeconds 100
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1 -RunName network-dedicated-career-daily-plan-freshness-guard -Port 7895 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-freshness-guard,Builds/Logs/network-dedicated-career-daily-plan-freshness-guard -File | Select-Object FullName,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

No PlayMode screenshot beyond the dedicated runner screenshots is required because this cycle only changes QA runners and their contract tests.

## Acceptance Criteria

- Dedicated smoke scripts fail before launch if required executables, managed assemblies, or Addressables runtime settings are missing.
- Dedicated smoke scripts fail before launch if `Rootborn.Game.dll` artifacts are older than the runner's required source files.
- Existing route/relationship and career Daily Plan dedicated smokes still pass with fresh artifacts.
- Addressables `link.xml` and `.meta` remain preserved.

## Commit

Use:

```text
[TEST][CHORE][NETWORK] Dedicated runner artifact freshness 가드 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
