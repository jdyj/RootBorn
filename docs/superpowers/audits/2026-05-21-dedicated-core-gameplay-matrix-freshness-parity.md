# Dedicated Core Gameplay Matrix Freshness Parity

Date: 2026-05-21

## Result

PASS.

`Scripts/qa/run-dedicated-core-gameplay-matrix.ps1` now performs a top-level artifact and compiled-source freshness check before invoking child dedicated smoke runners.

## RED Evidence

Before the change, this contract check failed:

```powershell
powershell.exe -NoProfile -Command "if (-not (Select-String -Path 'Scripts/qa/run-dedicated-core-gameplay-matrix.ps1' -Pattern 'Assert-FreshBuildArtifacts' -Quiet)) { throw 'Matrix freshness guard missing.' }"
```

Failure:

```text
Matrix freshness guard missing.
```

## Change

Updated:

```text
Scripts/qa/run-dedicated-core-gameplay-matrix.ps1
```

The matrix runner now:

- accepts `ServerExecutable` and `ClientExecutable` parameters,
- resolves those artifacts before any slice starts,
- validates required server/client executables,
- validates server/client `Rootborn.Game.dll`,
- validates server/client Addressables runtime `settings.json`,
- compares managed DLL freshness against compiled autoplay C# sources used by matrix slices,
- passes the resolved server/client executable paths through to child smoke runners.

The matrix runner does not compare managed DLL freshness against PowerShell runner timestamps.

## Freshness Sources

The matrix compares `Rootborn.Game.dll` timestamps against:

- `Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs`
- `Assets/Scripts/Game/Common/DirectValidationCareerDailyPlanAutoplayInstaller.cs`
- `Assets/Scripts/Game/Common/DirectValidationDailyPlanCompletionAutoplayInstaller.cs`
- `Assets/Scripts/Game/Common/DirectValidationDiscoveryRouteUnlockAutoplayInstaller.cs`
- `Assets/Scripts/Game/Common/DirectValidationResourceRecipeActivityAutoplayInstaller.cs`

## GREEN Evidence

Contract check:

```powershell
powershell.exe -NoProfile -Command "if (-not (Select-String -Path 'Scripts/qa/run-dedicated-core-gameplay-matrix.ps1' -Pattern 'Assert-FreshBuildArtifacts' -Quiet)) { throw 'Matrix freshness guard missing.' }"
```

Result: exit code 0.

Parser check:

```powershell
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-core-gameplay-matrix.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
```

Result: exit code 0.

Source inspection confirmed the guard contains `Assert-RequiredArtifact`, `Assert-FreshBuildArtifacts`, all required artifacts, all compiled autoplay sources, and server/client executable forwarding to child smoke runners.

## Scope Notes

- The full matrix was intentionally not executed in this cycle. The next recommended cycle is a dedicated matrix evidence refresh.
- Runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, and autoplay C# behavior were not changed.
- The change is limited to QA runner preflight validation and documentation.

