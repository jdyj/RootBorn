# Goal: Dedicated Core Gameplay Matrix Smoke Rerun After Freshness Normalization

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Re-run the dedicated core gameplay matrix after normalizing dedicated smoke runner freshness sources, and capture fresh evidence that all five dedicated slices pass with current runner contracts.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Keep this cycle limited to evidence refresh and documentation unless a failing gate requires a separate TDD cycle.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, or network scenario IDs.

## Required Skills

- `superpowers:verification-before-completion`
- `superpowers:systematic-debugging` if the matrix run times out or fails

## Required Work

- Run `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1` after the smoke runner freshness normalization commit.
- Verify all five slices:
  - `route-relationship`
  - `career-daily-plan-route`
  - `daily-plan-completion`
  - `discovery-route-unlock`
  - `resource-recipe-activity`
- Confirm each slice has:
  - action server log
  - action client log
  - reload server log
  - reload client log
  - action screenshot
  - reload screenshot
  - action completion marker
  - reload/observe completion marker
- Write an audit document with the actual evidence path, ports, `WaitSeconds`, screenshot sizes, and notable execution caveats.

## Required Verification

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization-rerun -StartPort 8031 -WaitSeconds 70
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization-rerun -Recurse -File | Select-Object FullName,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- The matrix process exits with code 0.
- The matrix reports `Result: Passed`.
- All five slices report `Result: Passed`.
- Each slice has two non-zero screenshots.
- Rootborn server/client processes are not left running.
- The audit document records the fresh evidence.

## Commit

Use:

```text
[DOCS][NETWORK] Dedicated core gameplay matrix smoke 증거 재검증
```

## Loop Continuation

After this goal is implemented, verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

