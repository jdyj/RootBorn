# House Expansion Interior Loop Verification

Date: 2026-05-19

## Scope

Verified House expansion stage 1, room preset persistence, furniture placement persistence, reload restore, and overlay cleanup.

## Commands

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-house-expansion-interior-loop-simulation.ps1 -RunName house-expansion-loop-verify-20260519 -SaveSlot house-expansion-loop-verify-20260519
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-house-expansion-interior-loop-simulation.ps1 -RunName house-expansion-loop-final-20260519 -SaveSlot house-expansion-loop-final-20260519
```

Focused final EditMode command:

```powershell
$json = '{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Interiors","includePassingTests":false,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force
```

## Result

Result: Passed

- Focused EditMode interiors: Passed, 58 passed, 0 failed.
- QA PlayMode simulation: Passed.
- Direct PlayMode camera evidence captured from House stage 1, preset application, furniture placement, and House reload.

## Evidence

- PlayMode result log: `production/qa/evidence/house-expansion-loop-verify-20260519/playmode-results.txt`
- Probe: `production/qa/evidence/house-expansion-loop-verify-20260519/simulation-probe.json`
- Final PlayMode result log: `production/qa/evidence/house-expansion-loop-final-20260519/playmode-results.txt`
- Final probe: `production/qa/evidence/house-expansion-loop-final-20260519/runtime-probe-final.json`
- Screenshot after expansion: `production/qa/evidence/house-expansion-loop-final-20260519/after-expansion.png`
- Screenshot after preset application: `production/qa/evidence/house-expansion-loop-final-20260519/after-preset.png`
- Screenshot after furniture placement: `production/qa/evidence/house-expansion-loop-final-20260519/after-placement.png`
- Screenshot after House reload: `production/qa/evidence/house-expansion-loop-final-20260519/after-reload.png`

## Probe Values

```json
{
  "CurrentStageIndex": 1,
  "SelectedRoomPresetId": "modern.home-designs.condominium-design",
  "GroundTileCount": 594,
  "WallTileCount": 154,
  "CollisionTileCount": 163,
  "SurfaceCellCount": 504,
  "PlacedFurnitureCount": 8,
  "HasVisibleDebugOverlay": false
}
```

## Notes

The simulation runner executes focused PlayMode methods individually and resets the editor to `Assets/Scenes/Farm.unity` between methods after waiting for PlayMode teardown. This avoids a Unity Test Framework dirty temporary scene blocking the next `tests-run` invocation.

Direct visual evidence was captured from PlayMode using the House scene camera after preparing stage 1 House state, applying a room preset, placing/moving/saving furniture, and reloading House.
