# ROOTBORN Time System Design

## Goal

ROOTBORN의 시간 시스템을 농사, 날짜 진행, 세대 전환, 플레이 체감 밸런스가 함께 참조하는 표준 축으로 정리한다. 1게임일의 초기 기준은 `10분 = 600초`로 둔다. 이후 하루 길이나 세대 길이를 바꾸더라도 작물 성장, 물/비료 만료, 세대 전환 의미가 함께 무너지지 않아야 한다.

완료 기준은 다음과 같다.

- `GameClock`은 1게임일 기본값 `600초`를 기준으로 `Day`, `DayProgress01`, `OnDayRolled`를 결정론적으로 계산한다.
- 시간 밸런스는 `TimeDefinition` ScriptableObject로 데이터화한다.
- 세대 전환은 실시간 초 고정값이 아니라 `GenerationProfile.lifetimeGameDays`로 판단한다.
- 세대별 수명은 SO 인스턴스마다 다르게 튜닝할 수 있고, 초기 기본값은 `14게임일`이다.
- 농사 시스템은 `GameClock` 날짜 롤오버와 연동해 물 리셋, 비료 만료, 성장 조건을 안정적으로 처리한다.
- 작물 조건은 기존 `CropDefinition + GrowthBehaviorBase[]` 전략 구조를 유지한다.
- 신규 동작은 실패 테스트 먼저 작성하고 구현한다.

## Current State

현재 저장소에는 시간 축의 최소 구현이 이미 있다.

- `Assets/Scripts/Game/Time/GameClock.cs`
  - `_realSecondsPerGameDay = 360f`
  - `ElapsedRealSeconds`, `Day`, `DayProgress01`, `OnDayRolled`
  - `Update()`에서 `Time.deltaTime * _timeScale`을 누적한다.
- `Assets/Scripts/Game/Farming/FarmGrid.cs`
  - `GameClock.OnDayRolled`를 구독한다.
  - 날짜가 넘어갈 때 물 상태와 비료 만료를 정리한다.
  - 매 프레임 `CropPlot.Tick()`에 물, 비, 비료 배수를 전달한다.
- `Assets/Scripts/Game/Crops/CropDefinition.cs`
  - 성장 stage duration과 `GrowthBehaviorBase[]`를 가진다.
- `Assets/Scripts/Game/Crops/RequiresDailyWaterBehavior.cs`
  - 물이나 비가 없을 때 성장률을 낮추거나 0으로 만든다.
- `Assets/Scripts/Game/Crops/FertilizerSpeedupBehavior.cs`
  - 비료 배수를 성장률에 적용한다.
- `Assets/Scripts/Game/Generation/GenerationProfile.cs`
  - 현재 `_lifetimeSec = 1800f`를 가진다.
- `Assets/Scripts/Game/Generation/GenerationManager.cs`
  - 현재 `ElapsedSec >= CurrentProfile.LifetimeSec` 기준으로 세대를 넘긴다.

관련 테스트도 일부 존재한다.

- `STATUS-002`: `GameClock.Tick`의 날짜 계산
- `CROP-001~005`: 작물 stage, 물, 비료 조건
- `GEN-001`: `lifetimeSec` 경과 시 세대 전환

따라서 이번 작업은 시간 개념을 새로 만드는 것이 아니라, 기존 `GameClock`을 게임 전체 표준 시간 소스로 승격하고 세대/농사 밸런스를 게임일 기준으로 정리하는 작업이다.

## Recommended Approach

`TimeDefinition` SO를 추가하고, `GameClock`은 이를 읽어 시간 계산을 수행한다. `GenerationProfile`은 실시간 초 대신 `lifetimeGameDays`를 공개 튜닝값으로 가진다. `GenerationManager`는 `GameClock`이 제공하는 날짜 진행 또는 게임일 환산 시간을 기준으로 세대 전환을 판단한다.

이 접근을 추천하는 이유는 다음과 같다.

- 하루 길이를 600초에서 480초나 720초로 조정해도 세대 길이의 의미가 유지된다.
- 농사, 세대, HUD, 저장 시스템이 같은 시간 소스를 참조할 수 있다.
- 헌법의 데이터 드리븐 원칙과 맞는다.
- 기존 `GameClock`, `FarmGrid`, `GrowthBehaviorBase[]` 구조를 크게 흔들지 않는다.

비추천 접근은 기존 `GenerationProfile.LifetimeSec`를 계속 중심으로 두는 것이다. 이 경우 하루 길이를 바꿀 때 세대 밸런스를 모든 SO에서 다시 초 단위로 환산해야 하며, 플레이 체감 기준과 데이터 기준이 어긋난다.

## Time Model

### TimeDefinition

신규 SO:

- 스크립트: `Assets/Scripts/Game/Time/TimeDefinition.cs`
- 기본 인스턴스: `Assets/Data/Time/Time_Default.asset`

필드:

- `realSecondsPerGameDay`: 기본 `600f`
- `startDay`: 기본 `1`
- `initialTimeScale`: 기본 `1f`

초기 범위에서는 계절, 날씨, 시간대, 야간 효과를 넣지 않는다. 단, 이후 확장할 수 있도록 시간 밸런스의 기준점은 이 SO로 모은다.

### GameClock

`GameClock`은 다음 책임을 가진다.

- `TimeDefinition`이 있으면 해당 값을 사용한다.
- 정의가 없으면 테스트와 기존 씬 호환을 위해 fallback 기본값 `600초`, 시작일 `1`, 배속 `1`을 사용한다.
- `Tick(deltaSeconds)`는 외부 테스트에서 직접 호출 가능해야 한다.
- `OnDayRolled`는 날짜가 바뀔 때 정확히 한 번 발화한다.
- 프레임 단위 `Update()`는 `Tick(Time.deltaTime * TimeScale)`만 호출한다.

공개 읽기 값:

- `ElapsedRealSeconds`
- `Day`
- `DayProgress01`
- `RealSecondsPerGameDay`
- `ElapsedGameDays`
- `TimeScale`

`ElapsedGameDays`는 세대 전환과 밸런스 테스트에서 사용한다. 시작일 `1` 기준 UI 날짜와, 경과 게임일 `0.0` 기준 시뮬레이션 시간을 분리한다.

## Generation Model

### GenerationProfile

`GenerationProfile`은 다음 필드를 가진다.

- `generationIndex`
- `displayKey`
- `lifetimeGameDays`: 기본 `14f`
- `startingKnowledge`
- `startingInventory`
- `nextGeneration`

기존 `_lifetimeSec`는 1차 구현에서 바로 제거하지 않는다. Unity 직렬화와 기존 테스트 영향을 줄이기 위해 다음 중 하나로 처리한다.

- 호환 프로퍼티로 남기되 신규 로직에서는 사용하지 않는다.
- 기존 에셋 마이그레이션이 끝난 뒤 별도 리팩터링에서 제거한다.

1차 설계의 기능 기준은 `lifetimeGameDays`다.

### GenerationManager

`GenerationManager`는 더 이상 `Time.deltaTime`으로만 세대 시간을 직접 판단하지 않는다. 다음 중 하나의 방식으로 `GameClock`을 참조한다.

- Inspector 또는 런타임 탐색으로 `GameClock`을 주입받아 `ElapsedGameDays`를 기준으로 판단한다.
- 테스트에서는 명시적 `TickGameDays(deltaGameDays)` 또는 `Tick(deltaSeconds, realSecondsPerGameDay)` 같은 결정론 API를 사용한다.

권장 구현은 `GameClock` 참조를 두고, 테스트 편의를 위해 순수 계산 경로를 작게 유지하는 방식이다.

세대 전환 조건:

```text
elapsedGameDaysInGeneration += deltaGameDays
if elapsedGameDaysInGeneration >= CurrentProfile.LifetimeGameDays:
    AdvanceGeneration()
```

세대 전환 기록의 elapsed 값은 기존 `AncestorRecord` 호환을 위해 실시간 초 또는 게임일 중 하나로 명확히 이름을 바꿔야 한다. 1차에서는 기존 필드명을 유지하되, 테스트에서 의미를 확인하고 후속 리팩터링 후보로 기록한다.

## Farming Integration

농사는 현재 구조를 유지한다.

- 작물 정의는 `CropDefinition` SO
- 성장 조건은 `GrowthBehaviorBase[]`
- 물 필요 조건은 `RequiresDailyWaterBehavior`
- 비료 반응은 `FertilizerSpeedupBehavior`
- 셀별 물/비료 상태는 `FarmGrid`

1차 구현에서 변경해야 할 점은 다음이다.

- 날짜 롤오버는 `GameClock.OnDayRolled` 기준으로만 처리한다.
- 물 리셋과 비료 만료는 `GameClock.Day` 기준 테스트를 보강한다.
- 작물 stage duration 단위는 1차에서 기존 초 단위를 유지한다.

작물 duration을 `stageDurationGameHours`나 `stageDurationGameDays`로 바꾸는 것은 별도 설계가 필요하다. 이번 goal은 시간 표준화와 세대 전환 기준 변경에 집중한다.

## Data And Registry

`TimeDefinition` 인스턴스는 `Assets/Data/Time/Time_Default.asset`에 둔다. `GameDataRegistry`에 등록할지는 구현 중 다음 기준으로 결정한다.

- 여러 씬에서 동일 시간 정의를 참조해야 하면 registry에 추가한다.
- 단일 `GameClock` prefab/씬 오브젝트에서 직접 참조해도 충분하면 1차에서는 registry 변경을 피한다.

헌법상 런타임 데이터는 SO 인스턴스로 튜닝해야 하므로, 최소 하나의 `TimeDefinition` 에셋은 필요하다.

## UI Impact

`TimeHud`는 `GameClock.Day`와 `DayProgress01`를 계속 표시한다. 1차 구현에서 UI 표현 변경은 필수가 아니다. 다만 하루가 10분으로 길어지므로 퍼센트만 표시하면 체감이 약할 수 있다. 후속 후보는 다음과 같다.

- `Day 3 08:40` 같은 게임 내 시각 표시
- 아침/낮/저녁/밤 구간 표시
- 세대 남은 일수 표시

이번 goal에는 포함하지 않는다.

## Testing

필수 테스트는 다음과 같다.

- `TIME-001`: `GameClock` 기본 하루 길이가 600초이고 600초 경과 시 `Day == 2`가 된다.
- `TIME-002`: `TimeDefinition.realSecondsPerGameDay`를 바꾸면 `Day`와 `DayProgress01` 계산이 결정론적으로 바뀐다.
- `TIME-003`: `OnDayRolled`는 날짜가 바뀔 때 정확히 1회 발화한다.
- `GEN-002`: `GenerationProfile.lifetimeGameDays` 경과 시 세대가 전환된다.
- `GEN-003`: 하루 길이를 바꿔도 동일한 `lifetimeGameDays` 기준 세대 전환 의미가 유지된다.
- `CROP-006`: 날짜 롤오버 후 물 상태가 초기화되고 `RequiresDailyWaterBehavior` 작물은 물 없이는 성장하지 않는다.
- `CROP-007`: 비료 `durationDays`는 `GameClock.Day` 기준으로 만료된다.

기존 회귀 테스트:

- `GameClockTests`
- `GenerationManagerTests`
- `GenerationSimulatorTests`
- `CropGrowthTests`
- `CropPlotWaterTests`
- `FarmGridTests`
- `FarmingScenarioTests`

테스트는 EditMode 우선으로 작성한다. 농사 전체 흐름은 기존 PlayMode 시나리오와 연결해 확인한다.

## Out Of Scope

이번 goal에서 제외한다.

- 계절 시스템
- 날씨 확률과 비 이벤트
- 수면/강제 날짜 넘김 UI
- 작물 duration 단위의 전면 게임일화
- 세대 전환 연출 고도화
- 네트워크 시간 동기화
- 저장 파일에 시간 상태를 영속화하는 전체 저장 시스템

저장 연동은 후속 goal에서 `ElapsedRealSeconds`, `Day`, `DayProgress01`, 현재 세대의 경과 게임일을 어떤 형식으로 저장할지 별도 설계한다.

## Risks

- 기존 `GenerationProfile._lifetimeSec`에 의존하는 테스트와 에셋이 있을 수 있다.
- `GameClock.Instance` 탐색 순서 때문에 `FarmGrid`가 구독을 늦게 할 수 있다. 현재 `FarmGrid.Update()`에서 재구독을 시도하므로 기본 구조는 유지 가능하다.
- 하루 길이를 10분으로 늘리면 현재 작물 stage duration 초 값이 너무 빠르게 느껴질 수 있다. 이번 goal에서는 작물 duration 단위 변환을 하지 않으므로, 밸런스 후속 작업이 필요하다.
- `TimeDefinition`을 registry에 넣으면 `GameDataRegistry` 에셋 와이어링과 validator 수정이 필요하다. 1차 구현에서 정말 필요한지 확인해야 한다.

## Open Decisions

다음 결정은 구현 중 테스트와 영향 범위를 보고 확정한다.

- `TimeDefinition`을 `GameDataRegistry`에 등록할지, `GameClock`에 직접 참조시킬지
- 기존 `_lifetimeSec`를 호환 필드로 얼마나 오래 유지할지
- `AncestorRecord`의 세대 경과 기록 단위를 초로 유지할지 게임일로 바꿀지

초기 기본 결정은 다음과 같다.

- 하루 길이: `600초`
- 세대 길이: `GenerationProfile.lifetimeGameDays`, 기본 `14게임일`
- 작물 stage duration: 이번 goal에서는 기존 초 단위 유지
- 시간 튜닝 데이터: `TimeDefinition` SO
