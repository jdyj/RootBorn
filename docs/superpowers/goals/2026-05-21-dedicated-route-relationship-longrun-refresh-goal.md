# Goal: Dedicated Route Relationship Long-Run Refresh

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Run the existing dedicated route/relationship long-run validation against current build artifacts and record fresh evidence that the real server/client process flow still passes action plus observe-only reload cycles.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes.
- This is a verification and evidence cycle, not a gameplay behavior cycle.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or QA runners unless the run exposes a blocker that must be fixed in a separate cycle.
- Do not claim host-mode coverage as dedicated server/client coverage.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:verification-before-completion`

## Required Work

- Run `Scripts/qa/run-dedicated-route-relationship-longrun.ps1` with a small but valid long-run cycle count.
- Record the exact command, result, log directory, screenshots, timestamps, and any relevant caveats in a new audit document.
- Inspect output files for nonzero screenshots and action/observe markers.
- Keep runtime/code/scripts unchanged if the run passes.

## Required TDD

TDD is not applicable because this cycle does not change behavior or tests. Verification replaces TDD:

- Existing runner contracts already require action plus observe-only cycles, screenshots, cleanup, and marker checks.
- This cycle must execute the runner and inspect generated evidence.

## Required Verification

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun-refresh -Cycles 2 -StartPort 7981 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-longrun-refresh -File | Select-Object Name,Length,LastWriteTime
Select-String -Path Builds/Logs/network-dedicated-route-relationship-longrun-refresh/*.log -Pattern "autoplay completed route-relationship|autoplay completed route-relationship-observe|dailyPlan=day:.*selected:|relationships="
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
git status --short
```

If the long-run fails due to stale build artifacts, missing executable artifacts, port conflicts, or process cleanup issues, stop and report the exact blocker instead of creating a passing audit.

## Acceptance Criteria

- Long-run command exits successfully.
- Log directory contains two nonzero screenshots and server/client logs.
- Logs contain one action completion marker and one observe-only completion marker.
- Logs contain selected Daily Plan route and relationship state evidence.
- Audit document records command, result, evidence paths, and caveats.
- No runtime gameplay or test behavior changes are introduced.

## Commit

Use:

```text
[DOCS][NETWORK] Dedicated route relationship long-run 증거 갱신
```

## Loop Continuation

After this goal is verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
