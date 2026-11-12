# 수치 균형 시뮬레이터 2단계 — 모든 경우 훑기 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 1단계에서 값 하나로 정했던 가정(자는 때, 점수, 알바 성공, 걷는 시간, 미니게임 몫, 전략, 날 수)을 축으로 바꿔 모든 조합을 돌리고, 밤샘·자는 때의 효율을 포함한 `sweep.csv`·`sweep-report.md`를 낸다.

**Architecture:** 한 판의 가정을 `RunSetup`(잠 규칙, 점수 모델, 알바 성공 모델, 걷는 분, 미니게임 몫) 하나로 모아 `BalanceRunner`가 받는다. 종류는 이름으로 등록하고 값 목록은 `money-balance.json`의 `axes`에 둔다. `SweepRunner`가 축의 곱을 병렬로 돌려 판마다 시점(7·30·100일) 요약을 만들고 `SweepReport`가 표를 쓴다. 1단계의 한 가지 보고(`report.md`)는 기준 값으로 그대로 돈다.

**Tech Stack:** .NET 8, NUnit 3.14, `System.Threading.Tasks.Parallel`, 기존 `Rootborn.Balance.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-balance-simulator-design.md` 13절(2단계). 1~12절의 제약도 그대로 묶는다.

## Global Constraints

- 1단계 계획(`docs/superpowers/plans/2026-11-12-balance-simulator-stage1.md`)의 Global Constraints가 그대로 묶는다. 바뀌는 것 하나: 걷는 분·미니게임 몫의 **기준 값**은 여전히 `day-balance.json`에서만 읽고, 훑는 **값 목록**은 `money-balance.json`의 `axes`에 둔다(목록에 기준 값이 들어 있어야 한다 — 없으면 `Validate` 실패).
- 잠·지침·회복 규칙을 테스트 쪽에 다시 쓰지 않는다. `game.Sleep()`, `game.StartNextDay()`, `game.AdvanceTime(…)`, `game.IsExhausted`, `game.Student.TimeMinutes`, `game.Student.CurrentDay`, `game.TimeRules`의 시각 값만 쓴다.
- 축의 값(추천, 설계 13.1절 그대로): 자는 때 `finish-by` 20:00~06:00 한 시간 간격 11개, `start-before` 22:00·00:00·02:00, `never`, `stay-up-every` 2·3·7 / 점수 `fixed` 50·60·70·80·90·100, `fail-every` 2·4(실패 50, 성공 80) / 알바 성공 `always`, `fail-every` 2·4 / 걷는 분 0·15·30·60 / 미니게임 몫 0.25·0.5·1.0 / 판은 100일, 시점 7·30·100.
- 기준 값: `finish-by 22:00`, `fixed 80`, `always`, 걷는 분·몫은 `day-balance.json`.
- 자정을 넘긴 시각(00:00~06:59)은 `TimeRules`와 같은 방식으로 그날의 24:00 뒤로 센다(하루 시작보다 이르면 +1440분).
- 같은 입력은 바이트까지 같은 파일. 병렬로 돌려도 결과 순서는 설정 순서(전략 → 대상 id → 자는 때 → 점수/성공 → 걷기 → 몫)로 고정한다.
- 모든 `dotnet test`는 Strix Halo에서(`Scripts/remote-test/run.sh`, 커밋 뒤). 이 PC에서 빌드·테스트하지 않는다.
- 근거 종류는 "자동 테스트, 가정을 넣은 계산". 표의 숫자를 고치려고 시뮬레이터나 게임을 굽히지 않는다 — 놀라운 값은 보고에 적는다.

## Review Focus

1. **자지 않는 판(`never`)** — 하루가 끝나지 않아 멈추거나 무한 루프에 빠지지 않고, 07:00에 날이 넘어가며 100일을 다 돈다. 지쳐서 아무것도 못 하는 날도 한 줄로 남는다(Task 1).
2. **지쳤는데 아직 잘 때가 아닌 날(잠 규칙이 자는 규칙일 때)** — 그 자리에서 자고 멈춘 이유가 `exhausted`로 적힌다. 판이 죽지 않는다(Task 1).
3. **자정을 넘긴 잠 시각(`finish-by 02:00`)** — 02:00을 "그날 아침 02:00"으로 읽어 아무것도 시작 못 하는 일이 없다(Task 1).
4. **행동이 07:00(날 넘김)을 가로지르는 경우** — 밤새는 밤에 07:00을 넘겨 끝나는 행동을 시작하지 않는다(Task 1).
5. **병렬 실행의 결정성** — 같은 설정으로 두 번 돌린 `sweep.csv`가 바이트까지 같다(Task 2).
6. **기준 값이 축 목록에 없는 설정** — 조용히 빈 표를 내지 않고 어느 축인지 말하며 실패한다(Task 1).

---

### Task 1: 한 판의 가정을 축 값으로 — 잠 규칙, 점수·성공 모델, 걷기·몫, 새 전략

**Files:**
- Modify: `game/tests/Rootborn.Game.Tests/DayAssumptions.cs` (몫을 받는 겹쳐 쓰기 추가. 기존 메서드와 `DayBalanceTests` 동작은 그대로)
- Create: `game/tests/Rootborn.Balance.Tests/Run/RunSetup.cs`, `Run/SleepRules.cs`(지금 `BalanceRunner.cs` 안의 `SleepRules`를 옮겨 바꾼다), `Run/ScoreModels.cs`
- Modify: `Run/BalanceRunner.cs`, `Run/BalanceConfig.cs`, `Run/DayRecord.cs`, `Actions/IBalanceAction.cs`, `Actions/FacilityActions.cs`, `Actions/PartTimeActions.cs`, `Actions/BalanceActions.cs`, `money-balance.json`, `Report/BalanceReport.cs`(바뀐 형에 맞추기만)
- Test: `SleepRulesTests.cs`, `ScoreModelsTests.cs`, `RunSetupRunnerTests.cs`, 기존 `BalanceConfigTests.cs`·`BalanceRunnerTests.cs` 보강

**Interfaces (Produces):**

```csharp
// DayAssumptions.cs — 더하는 것
public static float MinigameSeconds(Minigame minigame, float share);
public static void PlayMinigame(GameSession game, Minigame minigame, float share);

// Run/SleepRules.cs
internal readonly struct SleepMoment { public int RunDay; public int NowMinutes, WalkMinutes, ActionMinutes, DayEndMinutes; } // RunDay는 판 안에서 1부터
internal interface ISleepRule
{
    string Id { get; }                       // 설정에 적은 그대로: "finish-by 22:00", "never" …
    bool SleepsTonight(int runDay);          // false면 그 밤은 자지 않고 07:00에 날이 넘어간다
    bool MayStart(SleepMoment m);            // 그 행동을 지금 시작해도 되는가. 자지 않는 밤에는 "걷기 + 행동이 DayEndMinutes 전에 끝난다"
}
internal static class SleepRules
{
    public static bool Knows(string text);   // "종류 인자" 한 줄. 종류: finish-by HH:MM, start-before HH:MM, never, stay-up-every N
    public static ISleepRule Parse(string text, TimeRules rules);   // 모르는 종류·잘못된 인자 → ArgumentException(그 글자를 문구에)
}

// Run/ScoreModels.cs — 판마다 새 인스턴스(차례를 센다)
internal interface IScoreModel { string Id { get; } int Next(); }          // "fixed 80", "fail-every 4"
internal interface ISuccessModel { string Id { get; } bool Next(); }       // "always", "fail-every 2"
internal static class ScoreModels   { public static bool Knows(string text); public static IScoreModel Create(string text, int failScore, int passScore); }
internal static class SuccessModels { public static bool Knows(string text); public static ISuccessModel Create(string text); }

// Run/RunSetup.cs — 한 판의 가정 전부
internal sealed class RunSetup
{
    public string Sleep, Score, PartTime;    // 설정에 적은 글자 그대로
    public int TravelMinutes; public float MinigameShare;
    public static RunSetup Baseline(BalanceConfig config);   // finish-by 22:00, fixed 80, always, day-balance.json의 걷기·몫
}

// Run/BalanceConfig.cs — 더하는 것
public sealed class AxesSpec { public string[] Sleep, Score, PartTime; public int[] TravelMinutes; public float[] MinigameShare; public int Days; public int[] Checkpoints; public int FailScore, PassScore; }
public AxesSpec Axes;                         // money-balance.json "axes"
public RunSetup BaselineSetup;                // "baseline" (걷기·몫은 day-balance.json에서)

// Run/BalanceRunner.cs — 바뀌는 것
public static RunResult Run(StrategySpec strategy, object target, RunSetup setup, BalanceConfig config, int days);   // + 행동 목록·시작 상태를 받는 기존 겹쳐 쓰기도 RunSetup을 받는다
// RunResult: TierId 대신 Setup(RunSetup)을 가진다. 1단계 report.md는 점수 단계 low·mid·max(60·80·100) 세 판을 기준 Setup에서 점수만 바꿔 돈다(표 모양·숫자는 그대로여야 한다).

// Run/DayRecord.cs — 더하는 칸
public bool Slept;            // 그 밤에 잤는가
public int SleptAtMinutes;    // 잔 시각(안 잤으면 -1)
public bool Exhausted;        // 그날 지침(game.IsExhausted)을 한 번이라도 봤는가
// StopReasons에 Exhausted = "exhausted", DayRanOut = "day-ran-out"(자지 않는 밤, 07:00까지 할 것을 다 했거나 지쳐서 기다림)
```

`money-balance.json`에 더하는 것(1단계의 `sleep`·`scoreTiers`는 `baseline`·1단계 보고용으로 정리해도 된다 — 1단계 `report.md`가 같은 숫자를 내는 한에서):

```json
"baseline": { "sleep": "finish-by 22:00", "score": "fixed 80", "partTime": "always" },
"axes": {
  "days": 100, "checkpoints": [7, 30, 100], "failScore": 50, "passScore": 80,
  "sleep": ["finish-by 20:00","finish-by 21:00","finish-by 22:00","finish-by 23:00","finish-by 00:00","finish-by 01:00","finish-by 02:00","finish-by 03:00","finish-by 04:00","finish-by 05:00","finish-by 06:00","start-before 22:00","start-before 00:00","start-before 02:00","never","stay-up-every 2","stay-up-every 3","stay-up-every 7"],
  "score": ["fixed 50","fixed 60","fixed 70","fixed 80","fixed 90","fixed 100","fail-every 2","fail-every 4"],
  "partTime": ["always","fail-every 2","fail-every 4"],
  "travelMinutes": [0, 15, 30, 60],
  "minigameShare": [0.25, 0.5, 1.0]
}
```

새 전략(설계 13.1절)과 행동 종류 — `do`의 항목은 `"종류"` 또는 `"종류 인자"`:

| 전략 id | each | do |
|---|---|---|
| `facility-level1-work-upgrade` / `…level2…` / `…level3…` | facility | `upgrade-facility`, `facility-work-level 1`(/2/3) |
| `facility-master-then-autoskip` | facility | `upgrade-facility`, `auto-skip-best-facility-work`, `best-facility-work` |

- `facility-work-level N`: `MinimumFacilityLevel == N`인 일 중 `CanWork`인 것(같으면 id 순 첫째). 없으면 null.
- `auto-skip-best-facility-work`: 지금 할 수 있는(`CanWork`) 일 중 레벨이 가장 높은 것이 `CanAutoSkip`일 때만 그것을 넘긴다. 아니면 null(다음 행동이 직접 해 숙련을 쌓는다).
- 시설 행동은 점수를 `IScoreModel.Next()`에서, 알바는 성공을 `ISuccessModel.Next()`에서 얻는다(`RunContext`에 둔다). 자동 넘기기는 점수 모델을 쓰지 않는다(차례도 넘기지 않는다).

하루 순서의 바뀌는 곳(나머지는 1단계 그대로):

```
하루 시작: tonightSleeps = rule.SleepsTonight(runDay)
되풀이:
  game.IsExhausted면 → 그날 Exhausted = true.
       tonightSleeps면 Stop = exhausted, 되풀이 끝(잔다).
       아니면 Stop = day-ran-out, 되풀이 끝(날이 넘어갈 때까지 기다린다).
  계획 고르기: 시작할 수 있다 = rule.MayStart(지금, 걷는 분, 행동 분, game.TimeRules.DayEndMinutes)
  (1단계와 같다: 걷기 → Do → 기록. 고른 것이 없으면 계획이 있었으면 Stop = bedtime(자는 밤) / day-ran-out(안 자는 밤), 없었으면 body / nothing-to-do)
하루 끝:
  tonightSleeps: SleptAtMinutes = 지금 시각, game.Sleep().Ended, game.StartNextDay() (1단계와 같다)
  아니면: game.Student.CurrentDay가 바뀔 때까지 game.AdvanceTime(…)으로 시계를 돌린다(한 번에 남은 분만큼의 실제 초. 안전장치: 몇 번 돌려도 안 넘어가면 판 이름·날을 말하며 실패). 날이 넘어간 뒤 DayState가 InProgress인지 본다.
```

- [ ] **Step 1: 실패 테스트** (실제 `data/`, 짧은 날 수. 손 계산이 되는 것은 손 계산으로)

```csharp
// SleepRulesTests — TimeRules는 TestWorld.Definitions.TimeRules
FinishBy_2200_AllowsAnActionEndingAt2200_AndRefusesOneEndingAt2201()
FinishBy_0200_IsAfterMidnightOfTheSameDay()            // 23:00에 걷기 30 + 행동 120 → 25:30 ≤ 26:00 허용, 01:00(=25:00)에 같은 것 → 27:30 거절
StartBefore_2200_AllowsStartingAt2159_WhateverTheLength()
Never_NeverSleeps_AndOnlyRefusesWhatCrossesDayEnd()    // SleepsTonight 전부 false, 끝이 DayEndMinutes 넘으면 거절
StayUpEvery3_StaysUpOnDays3_6_9_AndSleepsBy2200Otherwise()
UnknownOrMalformedRuleSaysItsText()                    // "nope", "finish-by 25:99", "stay-up-every 0" → ArgumentException, 문구에 그 글자

// ScoreModelsTests
Fixed80_AlwaysGives80()
FailEvery4_GivesFailScoreOnEvery4thCall_AndPassScoreOtherwise()   // 80,80,80,50,80,80,80,50
SuccessFailEvery2_FailsEverySecondCall()
UnknownModelSaysItsText()

// RunSetupRunnerTests — 시설 하나(All<Facility>() 첫째), 전략 facility-work-only, 10일
NeverSleeping_RunsAllDays_WithoutHanging_AndNeverRecordsSlept()            // Days.Count == 10, 모든 날 Slept == false, 날 번호가 하나씩 는다
NeverSleeping_GetsExhausted_AndThoseDaysStillHaveARow()                     // Exhausted인 날이 1일 이상(없으면 실패 문구에 날마다 체력), 그날도 줄이 있다
SleepingAt2200_NeverRecordsExhausted()                                      // 기준 Setup: Exhausted인 날 0, 모든 날 Slept, SleptAtMinutes ≤ BedtimeMinutes
FinishBy0200_DoesMoreActionsPerDayThanFinishBy2200_OnDayOne()               // 첫날 Actions 비교(>)
NoActionCrossesDayEnd_WhenStayingUp()                                       // never 판: 모든 날 EndedAtMinutes ≤ DayEndMinutes
TravelZero_FitsMoreActionsThanTravelSixty()                                 // 첫날 Actions: 0분 > 60분
MinigameShare_ChangesMinigameRealSeconds_NotIncome()                        // 몫 0.25 vs 1.0: 첫날 Earned 같고 MinigameRealSeconds는 4배(±0.01)
FailEvery2_EarnsLessThanFixed80()                                           // 10일 번 것 합 <
FacilityWorkLevel1_OnlyEverDoesLevelOneWork_EvenAfterUpgrading()            // facility-level1-work-upgrade: ActionCounts의 일 id가 모두 MinimumFacilityLevel 1인 일(정의에서 확인), 30일
MasterThenAutoSkip_EventuallyAutoSkipsTheHighestWork()                      // 30일, max 점수: 마지막 날 AutoSkips > 0이고 그날 한 일이 그 레벨에서 가장 높은 일

// BalanceConfigTests 보강
AxesAreRead_AndContainTheBaseline()                    // 축마다 기준 값이 목록에 있다, days 100, checkpoints 7·30·100
AnAxisMissingItsBaselineFailsAndNamesTheAxis()
UnknownSleepOrScoreTextInAxesFailsAndNamesIt()

// 기존 테스트: 전부 그대로 통과해야 한다(형이 바뀐 곳만 고친다). Stage 1 report.md의 숫자가 바뀌지 않는다 —
// BalanceReportTests에 "기준 Setup의 30일 표에서 facility-work-only max 하루 수입 == 120.0" 같은 값 고정은 넣지 않는다(데이터가 바뀌면 깨진다). 대신 같은 입력 두 번 = 같은 파일 테스트가 그대로 통과하는지 본다.
```

- [ ] **Step 2: 실패 확인** — 커밋 뒤 원격 실행, 컴파일 실패.
- [ ] **Step 3: 구현** — 위 Interfaces와 하루 순서대로. `never` 판에서 지침이 전혀 생기지 않으면(규칙이 설계 13.1절과 다르면) 규칙을 굽히지 말고 날마다의 체력을 보고에 적고 그 테스트를 사실에 맞게 고친 뒤 DONE_WITH_CONCERNS.
- [ ] **Step 4: 통과 확인** — 원격: `game/tests/Rootborn.Balance.Tests` 전체, `game/tests/Rootborn.Game.Tests --filter "FullyQualifiedName~DayBalanceTests"` 5건. 이 PC: `bash Scripts/ci/run-all.sh --checks-only`. 1단계 전체 보고(`WriteFullReport`)를 원격에서 다시 내 이전 `report.md`(`output/remote-test/20261113-000254-f668bb627-39345-15060/output/balance/report.md`)와 표의 숫자가 같은지 견준다(다르면 무엇이 왜 다른지 보고).
- [ ] **Step 5: 커밋** — `[TEST] 균형 시뮬레이터 한 판의 가정을 축 값으로 — 잠 규칙(밤샘 포함), 점수·성공 모델, 걷기·몫, 레벨별·숙련 뒤 넘기기 전략`

---

### Task 2: 모든 조합 훑기와 보고서

**Files:**
- Create: `game/tests/Rootborn.Balance.Tests/Sweep/SweepRunner.cs`, `Sweep/SweepRow.cs`, `Sweep/SweepReport.cs`, `Sweep/FullSweep.cs`, `SweepTests.cs`
- Modify: `Run/RunSummary.cs` (시점까지의 요약), `game/README.md`·`.claude/rules/testing-discipline.md`·`Scripts/ci/README.md`(한 줄씩), `docs/superpowers/specs/2026-11-12-balance-simulator-open-decisions.md`(조정 담당이 따로 고친다 — 건드리지 않는다)

**Interfaces:**
- Consumes: Task 1의 `RunSetup`, `BalanceConfig.Axes`, `BalanceRunner.Run(strategy, target, setup, config, days)`, `RunResult.Setup`, `DayRecord`(Slept, Exhausted …).
- Produces:

```csharp
// Run/RunSummary.cs
public static RunSummary From(RunResult run, int upToDay);   // 판의 첫 upToDay일만. 기존 From(run)은 전체
// RunSummary에 더하는 것: int ExhaustedDays; int NightsAwake; float AverageEnergyAtEnd; float? AverageSleptAtMinutes;

// Sweep/SweepRow.cs
internal sealed class SweepRow { public string StrategyId, TargetId; public RunSetup Setup; public int Checkpoint; public RunSummary Summary; public bool IsBaseline; }

// Sweep/SweepRunner.cs
internal static class SweepRunner
{
    public static IReadOnlyList<(StrategySpec Strategy, object Target, RunSetup Setup)> Plan(BalanceConfig config);   // 축의 곱. 시설 전략은 score 축, 알바 전략은 partTime 축(쓰지 않는 축은 기준 값 하나)
    public static IReadOnlyList<SweepRow> RunAll(BalanceConfig config, int? maxParallel = null);                      // Plan 순서 × Checkpoints 순서로 고정. Parallel.For + 자리 배열
}

// Sweep/SweepReport.cs
internal static class SweepReport { public static void Write(string directory, BalanceConfig config, IReadOnlyList<SweepRow> rows); }   // sweep.csv, sweep-report.md
```

- 판이 서로 독립이어야 한다: 판마다 `MemorySaveStore`·`GameSession`·행동 인스턴스·모델 인스턴스를 새로 만든다. 공유하는 것은 바뀌지 않는 `TestWorld.Definitions`·설정뿐. `DayAssumptions.Current`처럼 정적 값을 판 도중에 바꾸지 않는다.
- `sweep.csv`: 줄 = 판 × 시점. 칸 = strategy, target, sleep, score, part_time, travel_minutes, minigame_share, checkpoint_day, is_baseline, `RunSummary`의 칸(하루 수입, 게임 시간당, 실제 분당 둘, 레벨 2·3 날, recover·payback, 첫 자동 넘기기 날, 첫 가방 찬 날, 하루 평균 행동, 지친 날 수, 밤샌 날 수, 하루 끝 체력 평균, 평균 잔 시각). 수는 불변 문화권, 줄 끝 `\n`.
- `sweep-report.md`(한글, 설계 13.3절): 맨 위 가정·축 목록·판 수와 "가정을 넣은 계산이다. 실제 플레이가 아니다." 그 아래:
  1. **자는 때** — 전략마다 표 하나: 줄 = 잠 규칙(설정 순서), 칸 = 하루 수입 7일·30일·100일, 하루 평균 행동(100일), 지친 날 수(100일), 밤샌 날 수(100일). 다른 축은 기준 값, 대상은 그 전략의 대상 평균(대상이 하나면 그것). 100일 하루 수입이 가장 큰 줄에 `**굵게**`, 기준 줄에 `(기준)`.
  2. **축마다** — 점수, 알바 성공, 걷는 분, 미니게임 몫 각각: 줄 = 그 축의 값, 칸 = 전략별 30일 하루 수입(그 축을 쓰지 않는 전략은 뺀다). 다른 축은 기준 값.
  3. **가장 많이·적게 버는 조합** — 100일 하루 수입 위 20, 아래 20(모든 축 값과 전략·대상).
  4. **직업끼리 가장 벌어지는 조합** — 같은 전략·같은 Setup에서 시설끼리 30일 "실제 분당(걷기 포함)"의 최대 ÷ 최소가 큰 위 10.
  표를 만들 때 전략 id·대상 종류 이름으로 분기하지 않는다(값이 없는 칸은 빼는 1단계의 방식).

- [ ] **Step 1: 실패 테스트** — 작은 설정을 테스트 안에서 만든다(실제 설정을 복사해 axes만 줄인다: sleep 3개(`finish-by 22:00`, `finish-by 02:00`, `never`), score 2개, partTime 1개, travel 2개, share 1개, days 12, checkpoints [7, 12], 전략 2개).

```csharp
Plan_IsTheCrossProduct_InConfigOrder()                 // 수 == Σ 전략(대상 수 × sleep × (scored ? score : partTime) × travel × share), 첫·끝 항목의 값
RunAll_GivesOneRowPerPlanAndCheckpoint()
RunAll_TwiceInParallel_GivesIdenticalCsvBytes()        // maxParallel 4로 두 번 → sweep.csv 바이트 같음
RunAll_ParallelEqualsSerial()                          // maxParallel 1 vs 4 → 같은 바이트
CheckpointSummary_EqualsSummaryOfAShorterRun()         // 12일 판의 7일 시점 요약 == 같은 Setup으로 7일만 돈 판의 요약(하루 수입, 레벨 날, 지친 날 수)
BaselineRowsAreMarked_AndThereIsOnePerStrategyTargetCheckpoint()
Report_HasASleepTablePerStrategy_WithARowPerSleepRule_AndMarksTheBest()
Report_SaysItIsNotRealPlay_AndListsTheAxes()
SummaryCountsExhaustedDaysAndNightsAwake()             // 손으로 만든 RunResult
```

`Sweep/FullSweep.cs`: `[Test, Explicit("손으로만: 모든 조합을 돌려 output/balance/sweep 에 표를 쓴다")] WriteSweepReport()` — 판 수·걸린 시간·경로를 `TestContext.Out`에.

- [ ] **Step 2: 실패 확인** — 원격, 컴파일 실패.
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인과 재기**
  - 원격: Balance 전체(빠른 검사가 느려졌으면 얼마인지 — 목표 30초 안쪽), `--filter "FullyQualifiedName~WriteSweepReport"`로 전체 훑기. **판 수와 걸린 시간을 적는다.** 30분을 넘길 것 같으면 멈추고 판 수·판당 시간·줄이는 방법(값 목록 중 무엇을 뺄지 후보)을 보고한다 — 스스로 축을 줄이지 않는다.
  - 받아 온 `output/remote-test/<실행 id>/output/balance/sweep/sweep-report.md`를 직접 열어 읽는다: 표가 깨지지 않았는지, 빈 표·전부 0인 줄이 없는지. 고치려고 숫자를 건드리지 않는다.
  - 이 PC: `bash Scripts/ci/run-all.sh --checks-only`.
- [ ] **Step 5: 문서** — `game/README.md` "손으로만 도는 테스트"에 `WriteSweepReport` 한 줄, `Scripts/ci/README.md`·`.claude/rules/testing-discipline.md`는 기존 Balance 줄에 "모든 경우 훑기(`output/balance/sweep/`)"를 덧붙인다.
- [ ] **Step 6: 커밋** — `[TEST] 균형 시뮬레이터 모든 경우 훑기 — 축의 곱을 병렬로 돌려 sweep.csv·sweep-report.md(자는 때 표 포함)`, `[DOCS] 균형 시뮬레이터 훑기 실행법`.

## 끝난 뒤 보고에 넣을 것

- Balance 테스트 건수, 빠른 검사·전체 훑기의 걸린 시간, 판 수.
- 자는 때 표의 요점: 전략마다 가장 많이 버는 잠 규칙, 밤샘(`never`)·가끔 밤샘(`stay-up-every`)이 기준(22:00)보다 버는지, 지친 날 수.
- 축마다 눈에 띄는 것, 가장 많이·적게 버는 조합, 직업끼리 가장 벌어지는 조합.
- 설계와 달랐던 것, 못 한 것.
