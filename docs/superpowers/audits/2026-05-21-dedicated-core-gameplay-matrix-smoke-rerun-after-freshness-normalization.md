# Dedicated Core Gameplay Matrix Smoke Rerun After Freshness Normalization

작성일: 2026-05-21

## Purpose

`518d30573 [NETWORK][TEST] Dedicated smoke freshness source contract 정규화` 이후, dedicated core gameplay matrix가 최신 smoke runner freshness contract 기준에서도 통과하는지 재검증했다.

## Command

초기 실행은 `WaitSeconds=80`으로 진행했으나 외부 shell timeout 15분에 걸렸다. 로그상 resource/recipe reload marker와 screenshot까지 생성되었지만 matrix process의 최종 exit code를 확인하지 못했으므로 완료 증거로 사용하지 않았다.

최종 증거는 아래 재실행에서 수집했다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization-rerun -StartPort 8031 -WaitSeconds 70
```

Result:

- Exit code: `0`
- Matrix result: `Passed`
- Run name: `network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization-rerun`
- Start port: `8031`
- WaitSeconds: `70`
- Evidence root: `Builds/Logs/network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization-rerun`

## Slice Evidence

| Slice | Port | Action Marker | Observe Marker | Action Screenshot | Reload Screenshot |
| --- | ---: | --- | --- | ---: | ---: |
| `route-relationship` | 8031 | `autoplay completed route-relationship` | `autoplay completed route-relationship-observe` | 24631 bytes | 24631 bytes |
| `career-daily-plan-route` | 8032 | `autoplay completed career-daily-plan-route` | `autoplay completed career-daily-plan-route-observe` | 24631 bytes | 24631 bytes |
| `daily-plan-completion` | 8033 | `autoplay completed daily-plan-completion` | `autoplay completed daily-plan-completion-observe` | 24631 bytes | 24631 bytes |
| `discovery-route-unlock` | 8034 | `autoplay completed discovery-route-unlock` | `autoplay completed discovery-route-unlock-observe` | 24631 bytes | 24631 bytes |
| `resource-recipe-activity` | 8035 | `autoplay completed resource-recipe-activity` | `autoplay completed resource-recipe-activity-observe` | 24631 bytes | 24631 bytes |

## Evidence Files

Each slice produced the expected four logs:

- `server-first-player.log`
- `client-first-player.log`
- `server-reload-player.log`
- `client-reload-player.log`

Screenshots:

- `route-relationship/network-dedicated-route-relationship-e2e.png`
- `route-relationship/network-dedicated-route-relationship-e2e-reload.png`
- `career-daily-plan-route/network-dedicated-career-daily-plan-route-e2e.png`
- `career-daily-plan-route/network-dedicated-career-daily-plan-route-e2e-reload.png`
- `daily-plan-completion/network-dedicated-daily-plan-completion-e2e.png`
- `daily-plan-completion/network-dedicated-daily-plan-completion-e2e-reload.png`
- `discovery-route-unlock/network-dedicated-discovery-route-unlock-e2e.png`
- `discovery-route-unlock/network-dedicated-discovery-route-unlock-e2e-reload.png`
- `resource-recipe-activity/network-dedicated-resource-recipe-activity-e2e.png`
- `resource-recipe-activity/network-dedicated-resource-recipe-activity-e2e-reload.png`

## Marker Coverage

The matrix runner verified these patterns through `Assert-SliceEvidence`:

- server readiness: `Dedicated server started - port=<slice port>`
- client readiness: `Client started - joinIp=127.0.0.1 port=<slice port>`
- action completion marker per slice
- observe completion marker per slice
- slice state marker per slice
- extra evidence markers:
  - route relationship: `relationships=(?!empty)`
  - career daily plan route: `careerHints=(?!empty)`
  - daily plan completion: `result=.*Daily Plan`, `duplicateRows:0`, `hud=.*duplicates:0`
  - discovery route unlock: `clue=.*seen:true`, `interpretation=.*completed:true`, `worldState=.*active:true`, `journal=.*duplicateRows:0`, `hud=.*duplicates:0`
  - resource recipe activity: `resource=Rock seen:true`, `journal=resource-recipe duplicateRows:0`, `hud=tracked duplicates:0`, `save=resource-recipe exists:true`

## Process Cleanup

After the successful rerun, `Get-Process rootborn,rootborn-server -ErrorAction SilentlyContinue` returned no game processes.

## Notes

- The first `WaitSeconds=80` attempt generated complete-looking logs but timed out at the shell wrapper level. It was treated as inconclusive because the matrix process exit code was unavailable.
- The successful run used `WaitSeconds=70` and a longer shell timeout. All slices returned `Result: Passed`.
- This cycle does not modify gameplay behavior, ScriptableObject data, scenes, prefabs, UI, save format, or autoplay code.

