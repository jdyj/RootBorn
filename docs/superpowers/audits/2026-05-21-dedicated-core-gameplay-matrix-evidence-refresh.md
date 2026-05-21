# Dedicated Core Gameplay Matrix Evidence Refresh

Date: 2026-05-21

## Result

PASS.

The dedicated core gameplay matrix was executed after adding the top-level freshness guard. The first attempt used the default `WaitSeconds=120` with a 15-minute command timeout and was cut off during the discovery slice. The successful run used `WaitSeconds=80` and a longer command timeout, completing all five current slices.

## Successful Command

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-refresh -StartPort 8011 -WaitSeconds 80
```

Runner output:

```text
Result       : Passed
RunName      : network-dedicated-core-gameplay-matrix-refresh
StartPort    : 8011
WaitSeconds  : 80
MatrixLogDir : C:\Users\jdyj\farmer\Builds\Logs\network-dedicated-core-gameplay-matrix-refresh
```

## Evidence Summary

`Builds/Logs/network-dedicated-core-gameplay-matrix-refresh`

| Slice | Files | Logs | Screenshots | Total Bytes |
| --- | ---: | ---: | ---: | ---: |
| `route-relationship` | 6 | 4 | 2 | 73436 |
| `career-daily-plan-route` | 6 | 4 | 2 | 72996 |
| `daily-plan-completion` | 6 | 4 | 2 | 76843 |
| `discovery-route-unlock` | 6 | 4 | 2 | 75516 |
| `resource-recipe-activity` | 6 | 4 | 2 | 73785 |

Each slice contains:

- `server-first-player.log`
- `client-first-player.log`
- `server-reload-player.log`
- `client-reload-player.log`
- action screenshot
- reload screenshot

All screenshots are non-zero PNG files of 24631 bytes.

## Slice Evidence

### route-relationship

- Port: `8011`
- Action marker: `autoplay completed route-relationship`
- Observe marker: `autoplay completed route-relationship-observe`
- State markers:
  - `dailyPlan=day:1,selected:daily.library.self-study`
  - `relationships=relationship.first-guide.trust:2,relationship.teacher.support:2`

### career-daily-plan-route

- Port: `8012`
- Action marker: `autoplay completed career-daily-plan-route`
- Observe marker: `autoplay completed career-daily-plan-route-observe`
- State markers:
  - `careerHints=career.chef,career.interior`
  - `dailyPlan=day:1,selected:daily.route.career.interior-practice`

### daily-plan-completion

- Port: `8013`
- Action marker: `autoplay completed daily-plan-completion`
- Observe marker: `autoplay completed daily-plan-completion-observe`
- State markers:
  - `dailyPlan=day:1,selected:daily.library.self-study,completed:daily.library.self-study`
  - `duplicateRows:0`
  - `hud=instances:1,visible:0,routeTextCount:0,duplicates:0`

### discovery-route-unlock

- Port: `8014`
- Action marker: `autoplay completed discovery-route-unlock`
- Observe marker: `autoplay completed discovery-route-unlock-observe`
- State markers:
  - `clue=id:clue.discovery.play-loop,seen:true`
  - `interpretation=completed:true`
  - `worldState=id:world.library.archive-open,active:true`
  - `dailyPlan=day:1,selected:daily.route.library.archive-research`
  - `journal=visible:true,category:Goals,rows:1,routeRows:0,duplicateRows:0`
  - `hud=instances:1,visible:0,routeTextCount:0,duplicates:0`

### resource-recipe-activity

- Port: `8015`
- Action marker: `autoplay completed resource-recipe-activity`
- Observe marker: `autoplay completed resource-recipe-activity-observe`
- State markers:
  - `recipe=recipe.town.field-notes revealed:true`
  - `resource=Rock seen:true`
  - `journal=resource-recipe duplicateRows:0`
  - `hud=tracked duplicates:0`
  - `save=resource-recipe exists:true`

## Timeout Note

The default matrix wait profile can exceed 15 minutes because each of five slices runs an action pass and a reload pass. The successful run used `WaitSeconds=80`; all required action/reload markers and screenshots were still produced.

## Verification Commands

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-refresh -StartPort 8011 -WaitSeconds 80
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix-refresh -Recurse -File | Select-Object FullName,Length,LastWriteTime
Select-String -Path Builds/Logs/network-dedicated-core-gameplay-matrix-refresh/**/*.log -Pattern "autoplay completed route-relationship|autoplay completed route-relationship-observe|autoplay completed career-daily-plan-route|autoplay completed career-daily-plan-route-observe|autoplay completed daily-plan-completion|autoplay completed daily-plan-completion-observe|autoplay completed discovery-route-unlock|autoplay completed discovery-route-unlock-observe|autoplay completed resource-recipe-activity|autoplay completed resource-recipe-activity-observe|dailyPlan=day:.*selected:|relationships=|careerHints=|duplicateRows:0|hud=.*duplicates:0|recipe=recipe.town.field-notes revealed:true|resource=Rock seen:true|worldState=.*active:true"
```

## Scope Notes

- Runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, and autoplay C# behavior were not changed.
- The run validated the matrix runner and existing dedicated slice scripts only.
- The generated Addressables `link.xml` and content state changes were restored after the run.

