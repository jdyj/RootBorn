# Goal: Registry Provider 안정화 2차 및 회귀 게이트 정리

## 배경

Registry Provider 단일화 1차 작업은 완료되었고, boot/Town/Farm/DataManager 경로는 `GameDataRegistryProvider` 중심으로 정리되었다.

다음 단계에서는 남은 직접 Registry fallback과 정적 게이트 사각지대를 제거해 재발 방지 수준까지 올린다.

## 목표

런타임 코드에서 `GameDataRegistry`를 직접 `Resources.Load`로 가져오는 경로를 제거하고, non-namespaced 및 namespaced generic 타입 모두를 정적 게이트가 탐지하도록 강화한다.

이후 실제 플레이 흐름과 QA 시뮬레이션을 재실행해 기반 안정화 상태를 검증한다.

## 작업 범위

1. `Assets/Scripts/Game/StudentLife/MilestoneGrowth.cs`의 기존 fallback을 제거하거나 `GameDataRegistryProvider` 경유로 전환한다.
   - `UnityEngine.Resources.Load<Rootborn.Game.Common.GameDataRegistry>("GameDataRegistry")`

2. `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs`의 direct Registry load 정규식을 확장한다.
   - `Resources.Load<GameDataRegistry>`
   - `Resources.Load<Rootborn.Game.Common.GameDataRegistry>`
   - `UnityEngine.Resources.Load<GameDataRegistry>`
   - `UnityEngine.Resources.Load<Rootborn.Game.Common.GameDataRegistry>`

3. Reviewed allowlist를 정리한다.
   - 더 이상 필요한 직접 Registry 로딩 항목이 있으면 제거한다.
   - 기존 reviewed runtime `Find`/일반 `Resources.Load` 항목은 이번 작업과 무관하면 유지한다.

4. 테스트를 보강한다.
   - Registry Provider 관련 EditMode 테스트가 유지/통과해야 한다.
   - 필요하면 static gate 테스트명을 유지한 채 탐지 범위만 확장한다.

5. 실제 플레이 흐름을 검증한다.
   - MainMenu -> Single Play -> New Game -> Confirm -> Town 경로를 PlayMode에서 실제 포인터 입력으로 검증한다.
   - Game View 스크린샷 증거를 남긴다.

6. QA 시뮬레이션을 실행한다.
   - 기존 `Scripts/qa/run-registry-provider-playmode-simulation.ps1`를 최종 run name으로 재실행한다.
   - 로그를 `production/qa/registry-provider/` 아래에 남긴다.

7. 감사 문서를 작성한다.
   - `docs/superpowers/audits/`에 2차 안정화 검증 결과를 기록한다.
   - 테스트 결과, QA 로그 경로, Game View 증거 경로, 잔여 dirty 상태를 명시한다.

## 완료 조건

- `Assets/Scripts/**/*.cs`에서 다음 직접 호출이 없다.
  - `Resources.Load<GameDataRegistry>`
  - `Resources.Load<Rootborn.Game.Common.GameDataRegistry>`
  - `UnityEngine.Resources.Load<GameDataRegistry>`
  - `UnityEngine.Resources.Load<Rootborn.Game.Common.GameDataRegistry>`

- `StaticRuntimeUsageGateTests`가 namespaced/non-namespaced `GameDataRegistry` 직접 로딩을 모두 탐지한다.

- 아래 검증이 모두 PASS한다.
  - Registry Provider EditMode 테스트
  - Static runtime allowlist gate
  - Migrated Registry caller gate
  - MainMenu -> Town PlayMode E2E
  - QA 시뮬레이션 스크립트
  - Entity ID branching gate
  - `git diff --check`

- C# 파일은 반드시 Unity MCP `script-update-or-create`로만 수정한다.

- 기존 dirty 상태인 아래 파일 삭제는 이번 작업 범위에 포함하지 않고, 건드리지 않는다.
  - `Assets/AddressableAssetsData/link.xml`
  - `Assets/AddressableAssetsData/link.xml.meta`

## 검증 명령

```powershell
# Direct Registry load 검색
rg -n "Resources\.Load\s*<\s*(Rootborn\.Game\.Common\.)?GameDataRegistry\s*>" Assets/Scripts -g "*.cs"

# QA 시뮬레이션
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-registry-provider-playmode-simulation.ps1 `
  -RunName registry-provider-followup-final-20260519 `
  -SaveSlot registry-provider-followup-final-20260519

# Entity ID branching gate
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh

# Git whitespace/status
git diff --check
git status --short
```

## 커밋 단위 제안

1. `[REFACTOR][TEST] MilestoneGrowth Registry Provider 경유 정리`
2. `[TEST] Registry Provider 2차 QA 로그 추가`
3. `[DOCS][TEST] Registry Provider 2차 검증 결과 기록`
