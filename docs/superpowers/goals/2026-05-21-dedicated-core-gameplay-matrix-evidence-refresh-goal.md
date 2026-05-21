# Goal: Dedicated Core Gameplay Matrix Evidence Refresh

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Run the dedicated core gameplay matrix after adding top-level freshness protection and document fresh evidence for all current matrix slices.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage, commit, delete, revert, or move unrelated dirty or untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- Do not change runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, or autoplay behavior.
- Keep this cycle limited to matrix execution evidence and documentation unless the runner fails.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not add production C# branching on entity IDs, route IDs, activity IDs, location IDs, NPC IDs, quest IDs, reward IDs, or network scenario IDs.
- Goal-like UI ownership remains with `ObjectiveJournalPanel` and the single `TrackedObjectiveHud`; this cycle must not add goal-like UI.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:verification-before-completion`
- `superpowers:systematic-debugging` only if the matrix fails unexpectedly.

## Required Work

- Execute `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1` with a fresh run name.
- Confirm the matrix passes top-level freshness validation before running slices.
- Confirm the matrix runs these slices:
  - route/relationship
  - career Daily Plan route
  - Daily Plan completion
  - discovery route unlock
  - resource/recipe activity
- Inspect each slice for server/client logs, action screenshot, reload screenshot, action marker, observe marker, and state evidence.
- Write `docs/superpowers/audits/2026-05-21-dedicated-core-gameplay-matrix-evidence-refresh.md` with command output, evidence files, marker evidence, and scope notes.
- Do not update scenario catalog rows in this cycle unless a verifier fails because the catalog must reference the new audit. If that happens, stop and make catalog update a separate TDD cycle.

## Required TDD

This is an evidence-refresh cycle with no production behavior change. TDD is not applicable unless the matrix fails and requires runner changes. If a runner change is needed:

- capture the failing command and failure output first,
- add or use the narrowest contract check that fails,
- make the smallest scoped fix,
- re-run the matrix.

## Required Verification

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-refresh -StartPort 8001 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix-refresh -Recurse -File | Select-Object FullName,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Matrix runner exits successfully.
- Every current slice has server/client logs, action screenshot, reload screenshot, action marker, observe marker, and required state evidence.
- Fresh audit documentation records actual command, result, evidence paths, and marker evidence.
- Scenario registry sync, Addressables preservation, whitespace, entity-ID branching, and workspace status gates pass.

## Commit

Use:

```text
[DOCS][NETWORK] Dedicated core gameplay matrix 증거 갱신
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.

