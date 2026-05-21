# Goal: Dedicated Route Relationship Long-Run Freshness Proof

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Prove the dedicated route/relationship long-run freshness guard against current artifacts, correct any guard criterion that prevents valid current artifacts from running, then document fresh two-cycle long-run evidence.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Keep this cycle limited to build freshness proof, long-run runner correction if needed, long-run evidence, and documentation.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, or network scenario IDs.
- Goal-like UI ownership remains with `ObjectiveJournalPanel` and the single `TrackedObjectiveHud`; this cycle must not add goal-like UI.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Verify the existing freshness guard contract before running the long-run.
- Run the long-run once to prove whether the freshness guard accepts the current artifacts.
- If the guard blocks because it compares compiled `Rootborn.Game.dll` timestamps against the PowerShell runner script, narrow the guard to compare managed DLLs only against compiled gameplay/autoplay C# source.
- Keep the runner script name in remediation/failure output so users know which runner produced the failure.
- Confirm the server/client `Rootborn.Game.dll` artifacts are newer than `Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs`.
- Execute `Scripts/qa/run-dedicated-route-relationship-longrun.ps1` with two cycles and a fresh run name.
- Inspect action and observe-only reload logs for completion, selected Daily Plan, and relationship markers.
- Inspect screenshot files for non-zero byte evidence.
- Write `docs/superpowers/audits/2026-05-21-dedicated-route-relationship-longrun-rebuild-proof.md` with command output, artifact timestamps, guard correction notes, evidence files, marker evidence, and scope notes.
- Do not update scenario catalog rows in this cycle unless a verifier fails because the catalog must reference the new audit. If that happens, stop and make catalog update a separate TDD cycle.

## Required TDD

This is a QA runner/proof cycle with no new production gameplay behavior. TDD is satisfied by the existing EditMode freshness guard contract plus the actual failing runner command:

- `DedicatedRouteRelationshipAutoplayContractTests` must pass before the long-run proof.
- RED: the first long-run execution must fail before launch if the guard incorrectly compares `Rootborn.Game.dll` against `run-dedicated-route-relationship-longrun.ps1`.
- GREEN: after narrowing the guard, the same long-run command must pass without weakening C# autoplay source freshness checks.
- If any required evidence contract is missing and Unity test tooling is responsive, add or tighten a failing contract test first, confirm RED, then make the smallest scoped change.

## Required Verification

Run:

```powershell
tests_run EditMode testClass DedicatedRouteRelationshipAutoplayContractTests
```

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun-rebuild-proof -Cycles 2 -StartPort 7991 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-longrun-rebuild-proof -Recurse -File | Select-Object FullName,Length,LastWriteTime
Select-String -Path Builds/Logs/network-dedicated-route-relationship-longrun-rebuild-proof/*.log -Pattern "autoplay completed route-relationship|autoplay completed route-relationship-observe|dailyPlan=day:.*selected:|relationships="
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- The freshness guard contract passes.
- The first long-run attempt documents any invalid freshness failure before process launch.
- Server/client `Rootborn.Game.dll` timestamps are newer than the autoplay installer source.
- The long-run runner does not compare managed DLL freshness against the PowerShell runner script timestamp.
- The long-run passes with exactly two cycles: action and observe-only reload.
- Evidence includes server/client logs and non-zero screenshots for both cycles.
- Logs include route/relationship completion, Daily Plan selected-state, and relationship-state markers.
- The audit document records actual command, result, artifact timestamps, evidence paths, marker evidence, and preserved scope.
- Addressables link.xml preservation, whitespace, entity-ID branching, and workspace status gates pass.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated route relationship long-run freshness proof 갱신
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
