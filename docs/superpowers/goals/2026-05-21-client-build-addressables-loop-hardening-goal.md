# Goal: Client Build Addressables Loop Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the dedicated route/relationship smoke foundation was committed, because fresh client artifacts could not be produced reliably.

## Objective

Make `BuildClientWindows64` produce fresh client artifacts without entering repeated Addressables `Generate Bundle Maps` / player script build loops, while preserving Addressables content generation and the `link.xml` preservation guard.

## Current Blocking Evidence

- Unity Editor process `36364` is responsive but stuck in `Generate Bundle Maps`.
- The window title repeatedly advanced through:
  - `Generate Bundle Maps (busy for 04:10)...`
  - `Generate Bundle Maps (busy for 03:11)...`
  - `Generate Bundle Maps (busy for 03:58)...`
- `Editor.log` shows `BuildScript.BuildClientWindows64()` entering `BuildScript.BuildAddressables()` and then `BuildPipeline.BuildPlayer()`.
- During `BuildPipeline.BuildPlayer()`, Addressables player-build processing enters `AddressablesPlayerBuildProcessor.PrepareForBuild`, `DefaultBuild`, `BuildPlayerContent`, `BuildScriptPackedMode`, and `BuildPlayerScripts`.
- `Builds\Client\Windows\rootborn_Data\Managed\Rootborn.UI.dll` was updated to `2026-05-21 04:03:13`, but `Rootborn.Game.dll` remained at `2026-05-21 02:07:02`, so the committed route/relationship observe-only runner is not present in the client artifact.
- Unity tooling modified `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/Windows/addressables_content_state.bin` during the build attempt. These are generated/build-side effects and must not be committed unless a later cycle intentionally owns them.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Do not delete, revert, or move user-owned changes.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`.
- Do not use passing EditMode tests as a substitute for proving fresh build artifacts are produced.
- Do not claim the dedicated route/relationship smoke is complete until the client and server artifacts include the current committed code and the external smoke script passes.

## Required Skills

- `superpowers:systematic-debugging`
- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

## Required Work

- Confirm the build loop root cause from `Editor.log` and current build script behavior.
- Add a narrow EditMode/source contract test for `BuildScript` that prevents duplicate Addressables player-content builds in `BuildClientWindows64`.
- Update `Assets/Scripts/Editor/BuildScripts/BuildScript.cs` through Unity MCP script tools only.
- Prefer one Addressables build path:
  - either rely on the Addressables player-build hook during `BuildPipeline.BuildPlayer`, or
  - explicitly disable the hook and call a single controlled `AddressableAssetSettings.BuildPlayerContent`.
- Keep server build behavior unchanged unless evidence shows it is affected.
- Restore Addressables generated state files if Unity modifies them during the attempt and they are not intentional outputs.

## Required TDD

RED:

- Add a test that fails while client build code can invoke Addressables player content build more than once per client build path.
- Add a test or source assertion that fails if `BuildClientWindows64` can run both a manual Addressables build and the Addressables player-build hook without an explicit guard.

GREEN:

- The build script exposes exactly one Addressables content build route for client builds.
- The test passes without weakening Addressables preservation checks.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Editor
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

Then produce fresh artifacts and verify timestamps:

```powershell
# Via Unity MCP or the project-supported Unity batchmode build command:
# Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64
Get-Item Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll,Builds\Client\Windows\rootborn_Data\Managed\Rootborn.UI.dll | Select-Object FullName,Length,LastWriteTime
```

After fresh client artifacts include current code, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-smoke.ps1 -RunName network-dedicated-route-relationship-e2e -Port 7871 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-e2e -File | Select-Object Name,Length,LastWriteTime
```

If Unity remains busy and MCP cannot edit or test, stop after documenting the root-cause evidence and exact blocked command. Do not claim build or smoke completion.

## Acceptance Criteria

- Client build no longer repeatedly re-enters Addressables map/player script build loops.
- Fresh `Rootborn.Game.dll` and `Rootborn.UI.dll` client artifacts include current committed code.
- Addressables `link.xml` and `.meta` remain present and tracked.
- Dedicated route/relationship smoke script passes against fresh artifacts, or the exact remaining blocker is reported without claiming completion.

## Commit

Use:

```text
[CHORE][TEST] Client build Addressables loop 안정화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
