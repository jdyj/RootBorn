# 수치 균형 시뮬레이터 1단계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 화면 없이 `GameSession` 명령만으로 "한 가지 전략만 되풀이하는 플레이어"를 여러 날 돌려, 직업(시설)·알바별 수입 표(`days.csv`, `summary.csv`, `report.md`)를 낸다.

**Architecture:** 새 테스트 프로젝트 `game/tests/Rootborn.Balance.Tests`. 설정(`money-balance.json`)의 전략을 데이터의 대상(시설마다, 알바마다)과 점수 단계에 곱해 "판"을 만들고, 판마다 `GameSession` 하나를 메모리 저장소 위에서 날 단위로 돌린다. 수입·지출은 보상 식을 베끼지 않고 `GameSession.Wallet`의 잔액 변화로 읽는다.

**Tech Stack:** .NET 8, NUnit 3.14, `System.Text.Json`(테스트 프로젝트에서만), 기존 `Rootborn.Game`·`Rootborn.Data`.

**Spec:** `docs/superpowers/specs/2026-11-12-balance-simulator-design.md` (1단계 = 10절의 1번). 실행하는 쪽은 이 문서와 설계 문서를 둘 다 읽는다.

## Global Constraints

- 게임 코드(`game/Rootborn.*` 실행 쪽)와 `data/`는 고치지 않는다. 예외: 필요한 조회를 `GameSession`에 더하는 것, 테스트 프로젝트에 내부를 보이는 `InternalsVisibleTo` 한 줄.
- 시뮬레이터 코드와 `money-balance.json`에 직업·알바·시설·아이템 id를 적지 않는다. 대상은 `GameDefinitions.All<T>()`에서, 통화는 `game.Inventory.Currency`에서 얻는다.
- 보상 식, 비용, 자동 넘기기 조건을 테스트 쪽에 다시 쓰지 않는다. 할 수 있는지는 `GameSession` 조회(`FacilityStatus`, `CanUpgradeFacility`)와 명령 결과로만 안다.
- 종류(행동, 대상, 시작 상태, 점수 모델)는 클래스 하나 + 등록 한 줄. enum·switch로 닫지 않는다.
- 같은 입력은 같은 결과. 난수·실제 시각을 쓰지 않는다. 미니게임 시드는 상수 `1`.
- 사용자 세이브 폴더를 쓰지 않는다. 판은 메모리 저장소에서 돈다.
- 가정 값(걷기 30분, 미니게임 몫 0.5)은 `game/tests/Rootborn.Game.Tests/day-balance.json`에서만 읽는다. 다른 파일에 다시 적지 않는다.
- 점수 단계는 `low` 60, `mid` 80, `max` 100. 기본 날 수 30.
- 자는 때: 걷는 시간 + 행동 길이를 더해 취침 시각(`game.TimeRules.BedtimeMinutes`)을 넘으면 그 행동을 시작하지 않는다.
- `DayBalanceTests`의 동작과 건수(5건)는 그대로여야 한다.
- 환경 변수: `ROOTBORN_BALANCE_OUT`(출력 폴더, 기본 `<저장소>/output/balance`), `ROOTBORN_BALANCE_DAYS`(전체 보고의 날 수, 기본은 설정 파일의 `days`).
- 커밋: 제목·본문 한글, prefix(`[TEST]`, `[DOCS]`, `[CHORE]`), 끝줄 `Co-Authored-By: <그 커밋을 만든 모델 이름> <noreply@anthropic.com>`(`.claude/rules/commit-conventions.md`). 푸시하지 않는다.
- 테스트 명령 앞에: `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`.
- 보고할 때 근거 종류는 전부 "자동 테스트"다. 표의 숫자를 실제 플레이로 확인했다고 쓰지 않는다.

## Review Focus

1. **가방이 차서 명령이 거절될 때** — 판이 예외로 죽거나 조용히 0원으로 이어지지 않고, 그날 멈춘 이유가 `bag-full`로 적힌다(Task 4).
2. **시작 지갑이 0이 아닌 판** — 준비 단계나 새 게임의 시작 돈이 "번 것"에 섞이지 않는다. 첫날 번 것 = 첫날 끝 지갑 − 시작 지갑(Task 4).
3. **하루에 아무것도 못 한 날** — 0으로 나누지 않고 그날 줄이 그대로 남으며, 요약의 실제 분당 수입은 분모가 0이면 빈칸이다(Task 3).
4. **`ROOTBORN_BALANCE_DAYS`가 숫자가 아니거나 0 이하일 때** — 기본값으로 조용히 메우지 않고 무엇이 잘못됐는지 말하며 실패한다(Task 5).
5. **설정 파일에 등록되지 않은 종류 이름(행동·대상·시작 상태)** — 그 전략을 조용히 건너뛰지 않고, 어느 전략의 어느 이름인지 말하며 실패한다(Task 4).

## File Structure

```
game/tests/Rootborn.Game.Tests/
  DayAssumptions.cs            (새 파일) day-balance.json 읽기 + 걷기·미니게임 시간 보내기. DayBalanceTests에서 옮긴다
  DayBalanceTests.cs           (고침) DayAssumptions를 쓴다
game/tests/Rootborn.Balance.Tests/
  Rootborn.Balance.Tests.csproj  Game·Data 참조. TestWorld.cs·DayAssumptions.cs를 파일 연결로 함께 쓴다
  money-balance.json             전략, 점수 단계, 날 수, bands(빈 배열)
  MemorySaveStore.cs             메모리 ISaveStore
  Run/BalanceConfig.cs           설정 읽기
  Run/RunStarts.cs               시작 상태 종류(new-game, adult) + 등록
  Run/RunTargets.cs              대상 종류(none, facility, part-time-work) + 등록
  Run/DayRecord.cs               하루 한 줄, 한 판의 결과
  Run/RunSummary.cs              판 요약 계산(순수 계산)
  Run/BalanceRunner.cs           한 판 돌리기(하루 루프, 지갑 추적)
  Actions/IBalanceAction.cs      행동 종류 인터페이스, PlannedStep, StepOutcome
  Actions/BalanceActions.cs      등록 한 곳
  Actions/FacilityActions.cs     upgrade-facility, best-facility-work, auto-skip-facility-work
  Actions/PartTimeActions.cs     part-time-work, any-part-time-work
  Report/BalanceReport.cs        days.csv, summary.csv, report.md 쓰기
  *Tests.cs                      Task마다
```

---

### Task 1: 프로젝트 뼈대와 함께 쓰는 가정

**Files:**
- Create: `game/tests/Rootborn.Game.Tests/DayAssumptions.cs`
- Modify: `game/tests/Rootborn.Game.Tests/DayBalanceTests.cs` (`Balance` 클래스, `Target`, `Walk`, `PlayMinigame`, `ReadBalance`를 새 파일로 옮기고 그것을 부른다)
- Create: `game/tests/Rootborn.Balance.Tests/Rootborn.Balance.Tests.csproj`, `MemorySaveStore.cs`, `SharedAssumptionsTests.cs`
- Modify: `game/Rootborn.sln` (프로젝트 추가: `dotnet sln game/Rootborn.sln add game/tests/Rootborn.Balance.Tests`)
- Modify(필요할 때만): `game/Rootborn.Data/Rootborn.Data.csproj`에 `<InternalsVisibleTo Include="Rootborn.Balance.Tests" />`

**Interfaces:**
- Produces (namespace `Rootborn.Game.Tests`, `internal`):
  - `sealed class DayAssumptions { int Min, Max, BodyMayStopFrom, TravelMinutes; float MinigameShare; static DayAssumptions Current { get; } }` — `day-balance.json`을 한 번 읽는다(지금 `ReadBalance`와 같은 경로·같은 값).
  - `static void DayAssumptions.Walk(GameSession game, int minutes)` — 지금 `DayBalanceTests.Walk`와 같은 계산.
  - `static float DayAssumptions.MinigameSeconds(Minigame minigame)` — 제한 시간 합 × `MinigameShare`(실제 초).
  - `static void DayAssumptions.PlayMinigame(GameSession game, Minigame minigame)` — `game.AdvanceTime(MinigameSeconds(minigame), false)`.
- Produces (namespace `Rootborn.Balance.Tests`, `internal`): `sealed class MemorySaveStore : ISaveStore` — `game/tests/Rootborn.Net.Tests/MemorySaveStore.cs`와 같은 모양(JSON 왕복 사본, `IncludeFields = true`).
- csproj: `Rootborn.Simulation.Tests.csproj`의 패키지 버전을 그대로 쓴다. `<Compile Include="../Rootborn.Game.Tests/TestWorld.cs" Link="Shared/TestWorld.cs" />`, 같은 방식으로 `DayAssumptions.cs`. `money-balance.json`은 `CopyToOutputDirectory`로 복사하지 않고 `TestWorld.RepoRoot`에서 경로로 읽는다(`day-balance.json`과 같은 방식).

- [ ] **Step 1: 실패 테스트** — `SharedAssumptionsTests.cs`

```csharp
[Test] public void AssumptionsComeFromTheDayBalanceFile()
{
    var a = DayAssumptions.Current;
    Assert.That(a.TravelMinutes, Is.EqualTo(30));
    Assert.That(a.MinigameShare, Is.EqualTo(0.5f));
}

[Test] public void ANewGameStartsOnAMemoryStore()
{
    var game = TestWorld.Start(new MemorySaveStore());
    Assert.That(game.Student.CurrentDay, Is.EqualTo(1));
    Assert.That(game.Inventory.Currency, Is.Not.Null, "통화는 game/economy.json에서 온다");
}
```

- [ ] **Step 2: 실패 확인** — `dotnet test game/tests/Rootborn.Balance.Tests` → 컴파일 실패(`DayAssumptions` 없음).
- [ ] **Step 3: 구현** — `DayAssumptions.cs`를 만들고 `DayBalanceTests`가 그것을 쓰게 고친다. csproj·`MemorySaveStore`를 만들고 sln에 더한다. `TestWorld`가 Data의 내부를 써서 컴파일이 안 되면 `InternalsVisibleTo` 한 줄을 더한다.
- [ ] **Step 4: 통과 확인** — `dotnet test game/tests/Rootborn.Balance.Tests` 2건 통과. `dotnet test game/tests/Rootborn.Game.Tests --filter "FullyQualifiedName~DayBalanceTests|FullyQualifiedName~FlowingTimeBalanceTests"` 통과, `DayBalanceTests` 5건 그대로. `bash Scripts/ci/run-all.sh --checks-only` 통과.
- [ ] **Step 5: 커밋** — `[TEST] 균형 시뮬레이터 프로젝트 뼈대 — 하루 균형 검사의 가정을 함께 쓴다`

---

### Task 2: 시작 상태 — 새 게임, 그 시설을 고른 성인

**Files:**
- Create: `game/tests/Rootborn.Balance.Tests/Run/RunStarts.cs`, `RunStartsTests.cs`

**Interfaces:**
- Consumes: `MemorySaveStore`, `TestWorld.Start`, `TestWorld.Definitions`.
- Produces:
  - `interface IRunStart { string Kind { get; } GameSession Start(ISaveStore store, object target); }`
  - `static class RunStarts { static IRunStart Get(string kind); }` — 등록: `"new-game"`, `"adult"`. 모르는 이름이면 `ArgumentException`(이름을 문구에 넣는다).
  - `"adult"`: `target`은 `Facility`. 새 게임을 시작해 한 번 저장한 뒤, 저장소의 `AdultCareerSnapshotSaveData`(파일 이름 `SaveFileNames.AdultCareerSnapshot(player)`)를 고쳐 그 시설의 전환 조건을 채우고, 다시 `TestWorld.Start`로 읽어 `ChooseFacility(facility.Id, …)`를 부른다. 이것은 **준비 단계**라고 주석에 적는다.

준비가 채울 것(요리 전용인 `game/tests/Rootborn.Game.Tests/SaveEdits.cs:26-41`을 일반화한다. id는 모두 정의에서 끌어낸다):
- `student.CurrentDay` = 그 시설의 전환 조건을 만족하는 가장 이른 날(지금 데이터는 4). 날을 하나씩 올리며 `CanChooseFacility`가 `Applied`가 되는 날을 찾지 말고, `SaveEdits`처럼 `Math.Max(4, …)`로 두되 4는 시설의 `MinimumDayTransitionRequirement`에서 읽을 수 있으면 읽는다. 읽을 길이 없으면 `CanChooseFacility`가 `Applied`가 될 때까지 날을 1씩 올린다(상한 30, 넘으면 실패 문구에 시설 id).
- `student.DayState = InProgress`, `student.CareerHintIds`에 `facility.CareerId`.
- `student.CareerCandidates.Candidates`에 `facility.Candidate.Id`의 항목 — 이해도는 `ReadyForChoice`가 되는 값, 힌트 id는 그 후보 정의가 가진 것에서 고른다(`CareerCandidate`의 공개 조회를 읽고 정한다).

- [ ] **Step 1: 실패 테스트**

```csharp
[Test] public void EveryFacilityInTheDataCanBeStartedAsAnAdult()
{
    var facilities = TestWorld.Definitions.All<Facility>().ToList();
    Assert.That(facilities, Is.Not.Empty);
    foreach (var facility in facilities)
    {
        var game = RunStarts.Get("adult").Start(new MemorySaveStore(), facility);
        Assert.That(game.Adult.IsAdult, Is.True, facility.Id);
        Assert.That(game.Adult.FacilityId, Is.EqualTo(facility.Id));
        Assert.That(game.Adult.FacilityLevel, Is.EqualTo(1));
        Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
        Assert.That(game.FacilityStatus(facility.Id).Works.Any(w => w.CanWork), Is.True, "첫날에 할 수 있는 일이 있다: " + facility.Id);
    }
}

[Test] public void NewGameStartIsDayOne()
    => Assert.That(RunStarts.Get("new-game").Start(new MemorySaveStore(), null).Student.CurrentDay, Is.EqualTo(1));

[Test] public void UnknownStartKindSaysItsName()
    => Assert.That(() => RunStarts.Get("nope"), Throws.ArgumentException.With.Message.Contains("nope"));
```

- [ ] **Step 2: 실패 확인** — 컴파일 실패(`RunStarts` 없음).
- [ ] **Step 3: 구현** — 위 Interfaces대로.
- [ ] **Step 4: 통과 확인** — `dotnet test game/tests/Rootborn.Balance.Tests --filter FullyQualifiedName~RunStartsTests` 3건 통과.
- [ ] **Step 5: 커밋** — `[TEST] 균형 시뮬레이터 시작 상태 — 데이터의 모든 시설을 성인 첫날로 준비`

---

### Task 3: 하루 기록과 판 요약(순수 계산)

**Files:**
- Create: `game/tests/Rootborn.Balance.Tests/Run/DayRecord.cs`, `Run/RunSummary.cs`, `RunSummaryTests.cs`

**Interfaces:**
- Produces:

```csharp
internal sealed class DayRecord
{
    public int Day; public bool Weekend;
    public int WalletAtEnd, Earned, Spent;           // 통화
    public int Actions;
    public SortedDictionary<string, int> ActionCounts;  // 행동 id(일·알바 id, 올리기는 종류 이름) → 횟수
    public SortedDictionary<string, int> OtherItems;    // 통화가 아닌 아이템 id → 그날 끝 개수
    public int WorkMinutes, TravelMinutes;           // 게임 분
    public float TravelRealSeconds, MinigameRealSeconds;   // 걷기에 쓴 실제 초, 미니게임에 쓴 실제 초
    public int EndedAtMinutes;                       // 그날 마지막 행동이 끝난 시각(game.Student.TimeMinutes)
    public int FacilityLevel; public bool Upgraded; public int AutoSkips;
    public int EnergyAtEnd, FocusAtEnd;
    public string Stop;                              // StopReasons의 값
}

internal static class StopReasons
{
    public const string Bedtime = "bedtime", BagFull = "bag-full", Body = "body", NothingToDo = "nothing-to-do";
}

internal sealed class RunResult
{
    public string StrategyId, TargetId, TierId;      // TargetId는 대상이 없으면 "", TierId는 점수를 안 쓰면 ""
    public int StartDay, StartWallet; public bool StartWeekend;
    public List<DayRecord> Days;
}

internal sealed class RunSummary
{
    public float IncomePerDay;                       // 번 것 합 ÷ 날 수
    public float? IncomePerGameHour;                 // 번 것 합 ÷ (WorkMinutes 합 ÷ 60). 분모 0이면 null
    public float? IncomePerRealMinuteWithTravel;     // 번 것 합 ÷ ((걷기 실제 초 + 미니게임 실제 초) ÷ 60). 분모 0이면 null
    public float? IncomePerRealMinuteMinigameOnly;   // 번 것 합 ÷ (미니게임 실제 초 ÷ 60). 분모 0이면 null
    public int FinalWallet;
    public int? Level2Day, Level3Day;                // 판 안에서 몇째 날(1부터). 닿지 않았으면 null
    public int? FirstAutoSkipDay, FirstBagFullDay;
    public int?[] PaybackDays;                       // 올린 것마다: 그날 쓴 값 ÷ (올린 뒤 하루 평균 수입 − 올리기 전 하루 평균 수입), 올림. 차가 0 이하거나 뒤 날이 없으면 null
    public float ActionsPerDay;
    public static RunSummary From(RunResult run);
}
```

걷기 실제 초는 판을 돌리는 쪽(Task 4)이 `DayRecord.TravelRealSeconds`에 채운다(걷는 분 × `game.TimeRules.RealSecondsPerGameDay` ÷ 1440). 요약은 `Days`만 읽는다.

- [ ] **Step 1: 실패 테스트** — 손 계산 예로 고정한다(실제 데이터를 쓰지 않는다).

```csharp
// 3일: 하루 100씩 벌고, 2일째에 150을 써서 올린 뒤 3일째에 200을 번다.
[Test] public void SummaryOfAHandMadeRun()
{
    var run = Run(Day(1, earned: 100, level: 1), Day(2, earned: 100, spent: 150, level: 2, upgraded: true), Day(3, earned: 200, level: 2));
    var s = RunSummary.From(run);
    Assert.That(s.IncomePerDay, Is.EqualTo(400f / 3).Within(0.001f));
    Assert.That(s.Level2Day, Is.EqualTo(2));
    Assert.That(s.Level3Day, Is.Null);
    Assert.That(s.PaybackDays, Is.EqualTo(new int?[] { 2 }), "150 ÷ (200 − 100) = 1.5 → 2일");
}

[Test] public void ADayWithNothingDoneKeepsItsRowAndLeavesRatesEmpty()
{
    var s = RunSummary.From(Run(Day(1, earned: 0, actions: 0, workMinutes: 0, minigameSeconds: 0)));
    Assert.That(s.IncomePerDay, Is.EqualTo(0f));
    Assert.That(s.IncomePerGameHour, Is.Null);
    Assert.That(s.IncomePerRealMinuteMinigameOnly, Is.Null);
}

[Test] public void UpgradeThatDoesNotRaiseIncomeHasNoPayback()   // 올린 뒤 수입이 같거나, 마지막 날에 올림 → null
[Test] public void FirstAutoSkipAndBagFullDaysAreTheFirstMatchingDay()
[Test] public void MinigameOnlyRateIgnoresTravelSeconds()        // 걷기 포함 값 < 미니게임만 센 값
```

- [ ] **Step 2: 실패 확인** — 컴파일 실패.
- [ ] **Step 3: 구현** — `From`은 `Days`만 읽는 계산이다.
- [ ] **Step 4: 통과 확인** — `--filter FullyQualifiedName~RunSummaryTests` 5건 통과.
- [ ] **Step 5: 커밋** — `[TEST] 균형 시뮬레이터 하루 기록과 판 요약 — 되찾는 날, 시간당 수입 계산`

---

### Task 4: 설정, 대상, 행동, 한 판 돌리기

**Files:**
- Create: `game/tests/Rootborn.Balance.Tests/money-balance.json`
- Create: `Run/BalanceConfig.cs`, `Run/RunTargets.cs`, `Run/BalanceRunner.cs`
- Create: `Actions/IBalanceAction.cs`, `Actions/BalanceActions.cs`, `Actions/FacilityActions.cs`, `Actions/PartTimeActions.cs`
- Create: `BalanceRunnerTests.cs`, `BalanceConfigTests.cs`

**`money-balance.json`** (이 모양 그대로):

```json
{
  "about": "게임은 읽지 않는다. 균형 시뮬레이터(game/tests/Rootborn.Balance.Tests)의 전략과 가정이다. 배포와 데이터 지문에 들어가지 않는다. 걷기·미니게임 가정은 game/tests/Rootborn.Game.Tests/day-balance.json. 설계 docs/superpowers/specs/2026-11-12-balance-simulator-design.md",
  "days": 30,
  "sleep": "finish-by-bedtime",
  "scoreTiers": [
    { "id": "low", "score": 60 },
    { "id": "mid", "score": 80 },
    { "id": "max", "score": 100 }
  ],
  "strategies": [
    { "id": "one-part-time-work", "each": "part-time-work", "start": "new-game", "scored": false, "do": ["part-time-work"] },
    { "id": "rotate-part-time-work", "each": "none", "start": "new-game", "scored": false, "do": ["any-part-time-work"] },
    { "id": "facility-work-only", "each": "facility", "start": "adult", "scored": true, "do": ["best-facility-work"] },
    { "id": "facility-work-upgrade", "each": "facility", "start": "adult", "scored": true, "do": ["upgrade-facility", "best-facility-work"] },
    { "id": "facility-autoskip-upgrade", "each": "facility", "start": "adult", "scored": true, "do": ["upgrade-facility", "auto-skip-facility-work", "best-facility-work"] }
  ],
  "bands": []
}
```

- `"sleep"`: `"finish-by-bedtime"`(걷기 + 행동 길이가 취침 시각을 넘으면 시작 안 함) 또는 `"start-before-bedtime"`(취침 시각 전이면 시작). 다른 값은 실패.
- `"scored": false`인 전략은 점수 단계와 곱하지 않고 한 번만 돈다(`TierId` = `""`). 설계 6.2절은 알바를 "세 단계 모두 성공으로 돈다"고 했지만 세 줄이 같으므로 한 줄만 낸다 — 이 계획이 정한 것이다.

**Interfaces:**
- Consumes: `RunStarts`, `DayRecord`, `RunResult`, `StopReasons`, `DayAssumptions`, `MemorySaveStore`.
- Produces:

```csharp
internal sealed class ScoreTier { public string Id; public int Score; }
internal sealed class StrategySpec { public string Id, Each, Start; public bool Scored; public string[] Do; }
internal sealed class BalanceConfig
{
    public int Days; public string Sleep;
    public IReadOnlyList<ScoreTier> Tiers; public IReadOnlyList<StrategySpec> Strategies;
    public static BalanceConfig Read();             // <저장소>/game/tests/Rootborn.Balance.Tests/money-balance.json
    public void Validate();                          // 모르는 each·start·do·sleep 이름 → InvalidOperationException, 문구에 전략 id와 그 이름
}

internal static class RunTargets                     // 등록: "none" → { null }, "facility" → All<Facility>(), "part-time-work" → All<IMinigamePartTimeWork>() 중 Minigame 있고 TimeCostMinutes > 0
{
    public static bool Knows(string kind);
    public static IReadOnlyList<object> All(string kind, GameDefinitions definitions);
    public static string IdOf(object target);        // null → ""
}

internal sealed class RunContext { public object Target; public int Score; public DayAssumptions Assumptions; }

internal sealed class PlannedStep
{
    public string Id;                                // 기록에 남길 이름(일·알바 id, 올리기는 종류 이름)
    public int Minutes;                              // 게임 분. 취침 판정에 쓴다
    public bool Walks;                               // 시작 전에 걷는 시간을 보내는가(올리기는 false)
    public Func<GameSession, string, StepOutcome> Do; // (game, 요청 id)
}

internal readonly struct StepOutcome
{
    public bool Done; public bool BagFull; public bool AutoSkipped; public float MinigameRealSeconds;
}

internal interface IBalanceAction { string Kind { get; } PlannedStep Plan(GameSession game, RunContext context); } // 지금 할 것이 없으면 null. 아무것도 바꾸지 않는다

internal static class BalanceActions { public static bool Knows(string kind); public static IBalanceAction Get(string kind); }

internal static class BalanceRunner
{
    public static RunResult Run(StrategySpec strategy, object target, ScoreTier tier, BalanceConfig config, int days);
    public static IEnumerable<RunResult> RunAll(BalanceConfig config, int days);   // 전략 × 대상 × (Scored면 Tiers). 순서는 설정 순서, 대상은 id 순
}
```

행동 종류(각 `Plan`이 하는 일):

| Kind | 고르는 법 | Do |
|---|---|---|
| `upgrade-facility` | `game.CanUpgradeFacility() == Applied`일 때만. `Minutes` 0, `Walks` false, `Id` = `"upgrade-facility"` | `game.UpgradeFacility(요청)` |
| `best-facility-work` | `game.FacilityStatus(game.Adult.FacilityId).Works` 중 `CanWork`인 것에서 `Work.MinimumFacilityLevel`이 가장 큰 것(같으면 id 순 첫째). `Minutes` = `Work.TimeCostMinutes` | `ReserveFacilityWork(work.Id, 요청, 1)` → `DayAssumptions.PlayMinigame` → `SettleFacilityWork(요청, context.Score)`. `MinigameRealSeconds` = `DayAssumptions.MinigameSeconds(work.Minigame)` |
| `auto-skip-facility-work` | 같은 목록에서 `CanAutoSkip`인 것 중 `MinimumFacilityLevel`이 가장 큰 것 | `CompleteFacilityWork(work.Id, 요청, context.Score, autoSkip: true)`. `AutoSkipped` true, `MinigameRealSeconds` 0 |
| `part-time-work` | `context.Target`인 알바. `Minutes` = `TimeCostMinutes` | `BeginPartTimeWork(id, 요청, 1)` → `PlayMinigame` → `SettleMinigame(요청, succeeded: true)` |
| `any-part-time-work` | `RunTargets.All("part-time-work", …)`를 돌아가며(판마다 차례를 기억한다). 시작이 거절되면 다음 알바를 본다 | 위와 같다 |

`Do`의 결과: 명령이 `Applied`가 아니면 `Done` false. 거절 이유가 `AdultCareerResultKind.InsufficientCapacity`면 `BagFull` true. 시작(예약)이 됐는데 정산이 거절되면 그것도 `Done` false로 돌려주되, 예약이 남지 않게 그 요청을 포기한다(`ForfeitFacilityWork` / `SettleMinigame(요청, false)`).

`BalanceRunner.Run`의 하루(설계 6.2·6.3절):

```
지갑 추적: 판 시작 때 StartWallet = game.Wallet.Balance. game.Wallet.Changed에서 잔액 차가 +면 Earned에, −면 Spent에 더한다.
하루:
  되풀이:
    전략의 do를 차례로 Plan → 첫 번째로 "시작할 수 있는" 계획을 고른다.
      시작할 수 있다 = (sleep이 finish-by-bedtime) 지금 시각 + (Walks ? 걷는 분 : 0) + Minutes ≤ 취침 시각
                     (start-before-bedtime) 지금 시각 < 취침 시각
    고른 것이 없으면: 계획이 하나라도 있었으면 Stop = bedtime, 하나도 없었으면 Stop = body(체력·집중이 1 이하이거나 game.IsExhausted) 아니면 nothing-to-do. 하루 끝.
    Walks면 DayAssumptions.Walk(game, 걷는 분).
    Do. Done이면 기록(Actions, ActionCounts, WorkMinutes, MinigameRealSeconds, AutoSkips, Upgraded).
        BagFull이면 Stop = bag-full, 하루 끝. 그 밖의 거절이면 그 행동 종류를 이 날 다시 보지 않고 다음으로 간다.
    안전장치: 하루 200번을 넘으면 실패(어느 판, 며칠째).
  하루 끝: DayRecord를 채운다(WalletAtEnd, OtherItems = game.Inventory.Slots에서 통화가 아닌 것, FacilityLevel, Energy, Focus).
  game.Sleep().Ended와 game.StartNextDay()가 true인지 본다(DayBalanceTests.cs:79-83과 같다). 아니면 실패(어느 판, 며칠째).
```

- [ ] **Step 1: 실패 테스트** — 실제 `data/`, 짧은 날 수.

```csharp
// BalanceRunnerTests
[Test] public void WalletChangeEqualsEarnedMinusSpent_ForEveryRun()            // RunAll(config, 6): 판마다 Σ(Earned − Spent) == 마지막 WalletAtEnd − StartWallet, Days.Count == 6
[Test] public void StartingMoneyIsNotCountedAsEarned()                          // 판마다 Days[0].Earned == Days[0].WalletAtEnd − StartWallet + Days[0].Spent
[Test] public void SameInputGivesTheSameRunTwice()                              // 같은 판을 두 번 → 날마다 모든 칸이 같다
[Test] public void FacilityWorkEarnsMoreAtAHigherScore()                        // facility-work-only, 시설마다: low 판 번 것 합 < max 판 번 것 합, low 판 번 것 합 > 0
[Test] public void NothingStartsThatWouldEndAfterBedtime()                      // finish-by-bedtime 판: 날마다 Stop이 bedtime이면 그날 EndedAtMinutes ≤ 취침 시각
[Test] public void UpgradeStrategyReachesLevelTwo_AndRecordsWhatItSpent()       // facility-work-upgrade, max, 30일, 시설마다: Upgraded인 날이 있고 그날 Spent > 0, FacilityLevel이 줄지 않는다
[Test] public void AutoSkipStrategyRecordsAutoSkips()                           // facility-autoskip-upgrade, max, 30일: AutoSkips > 0인 날이 있다(없으면 실패 문구에 시설과 마지막 날 상태)
[Test] public void ARefusedStartForAFullBagStopsTheDayAsBagFull()               // 가짜 IBalanceAction(Do가 BagFull을 돌려준다)을 넣은 전략 → 그날 Stop == bag-full, Actions == 0, 예외 없음, 다음 날로 넘어간다
[Test] public void EveryPartTimeWorkInTheDataGetsItsOwnRun()                    // one-part-time-work 판의 TargetId 집합 == RunTargets.All("part-time-work") id 집합, 판마다 번 것 합 > 0

// BalanceConfigTests
[Test] public void TheCommittedConfigIsValid()                                  // Read().Validate() 통과, Days == 30, Tiers 점수 60·80·100
[Test] public void UnknownKindNamesFailWithTheStrategyAndTheName()              // each·start·do·sleep 각각 모르는 이름 → 문구에 전략 id와 그 이름
```

가짜 행동을 넣을 수 있게 `BalanceRunner.Run`은 행동 목록을 받는 겹쳐 쓴 메서드를 하나 둔다(`Run(…, IReadOnlyList<IBalanceAction> actions)`), 설정 이름으로 찾는 쪽이 그것을 부른다.

- [ ] **Step 2: 실패 확인** — 컴파일 실패.
- [ ] **Step 3: 구현** — 위 Interfaces와 하루 순서대로. 자동 넘기기 테스트가 30일 안에 안 열리면 규칙을 고치지 말고, 왜 안 열리는지(어느 조건) 실패 문구로 보고하고 멈춘다 — 설계 6.2절의 조건(레벨 2 이상, 숙련 100)과 다르면 설계를 고쳐야 한다.
- [ ] **Step 4: 통과 확인** — `dotnet test game/tests/Rootborn.Balance.Tests` 전부 통과. 걸린 시간을 적는다.
- [ ] **Step 5: 커밋** — `[TEST] 균형 시뮬레이터 한 판 돌리기 — 전략·대상·행동 종류, 지갑 변화로 수입 기록`

---

### Task 5: 보고서, 전체 보고, 문서

**Files:**
- Create: `game/tests/Rootborn.Balance.Tests/Report/BalanceReport.cs`, `BalanceReportTests.cs`, `FullReport.cs`
- Modify: `game/README.md` (테스트 프로젝트 표에 한 줄, "손으로만 도는 테스트"에 한 줄, 환경 변수 표에 두 줄), `Scripts/ci/README.md` (`dotnet-tests` 줄의 프로젝트별 건수에 Balance 더하기), `.claude/rules/testing-discipline.md` ("어디에 무엇을" 표에 한 줄)

**Interfaces:**
- Consumes: `RunResult`, `RunSummary`, `BalanceConfig`, `BalanceRunner.RunAll`, `DayAssumptions`.
- Produces:

```csharp
internal static class BalanceReport
{
    public static void Write(string directory, BalanceConfig config, int days, IReadOnlyList<RunResult> runs);  // days.csv, summary.csv, report.md
    public static string OutputDirectory();   // ROOTBORN_BALANCE_OUT 또는 <저장소>/output/balance
    public static int Days(BalanceConfig config);  // ROOTBORN_BALANCE_DAYS 또는 config.Days. 숫자가 아니거나 1 미만이면 InvalidOperationException(변수 이름과 받은 값)
}
```

- `days.csv`: 판(전략, 대상, 단계) + `DayRecord`의 칸. `ActionCounts`·`OtherItems`는 `id×수`를 `;`로 이은 한 칸. 수는 `CultureInfo.InvariantCulture`.
- `summary.csv`: 판 + `RunSummary`의 칸 + 시작 날·요일·지갑. null은 빈칸.
- `report.md`(한글): 맨 위에 가정(날 수, 점수 단계, 걷는 분, 미니게임 몫, 자는 방식)과 **"가정을 넣은 계산이다. 실제 플레이가 아니다."** 한 줄. 그 아래 전략마다 표 하나 — 줄은 대상, 칸은 점수 단계별 하루 수입·실제 분당 수입(걷기 포함/미니게임만)·레벨 2·3에 닿은 날·되찾는 날·가방이 처음 찬 날. 표 아래에 그 전략에서 하루 수입이 가장 큰 대상 ÷ 가장 작은 대상의 비(단계마다). 같은 입력이면 파일 내용이 바이트까지 같다(날짜·시각을 적지 않는다).

- [ ] **Step 1: 실패 테스트**

```csharp
[Test] public void WritesThreeFilesWithOneRowPerDayAndPerRun()     // RunAll(config, 3) → days.csv 줄 수 == 1 + Σ Days.Count, summary.csv 줄 수 == 1 + 판 수, report.md에 "실제 플레이가 아니다"
[Test] public void WritingTwiceGivesIdenticalFiles()               // 두 폴더에 써서 세 파일 바이트 비교
[Test] public void ReportHasATablePerStrategyAndARowPerTarget()    // 전략 id마다 제목, 시설 전략 표에 All<Facility>() 수만큼 줄
[Test] public void EmptyRatesAreBlankNotZero()                     // 손으로 만든 RunResult(아무것도 못 한 판) → summary.csv의 그 칸이 빈칸
[Test] public void BadDaysVariableFailsAndNamesIt()                // ROOTBORN_BALANCE_DAYS = "abc", "0" → 문구에 변수 이름과 값. 테스트 끝에 변수를 원래대로
[Test] public void DaysAndOutputFallBackToConfigAndRepoOutput()    // 변수 없으면 config.Days, <저장소>/output/balance
```

`FullReport.cs`:

```csharp
[Test, Explicit("손으로만: 전체 판을 돌려 output/balance 에 표를 쓴다")]
public void WriteFullReport()   // config = Read(); Validate(); days = BalanceReport.Days(config); runs = RunAll; Write(OutputDirectory()/…); 걸린 시간과 경로를 TestContext.Out에
```

- [ ] **Step 2: 실패 확인** — 컴파일 실패.
- [ ] **Step 3: 구현**
- [ ] **Step 4: 통과 확인과 재기**
  - `dotnet test game/tests/Rootborn.Balance.Tests` 전부 통과. **전체 걸린 시간이 10초 안쪽인지** 적는다. 넘으면 무엇이 느린지(저장, 정의 읽기) 재서 적고, 짧은 날 수를 쓰는 테스트가 판을 한 번만 돌려 함께 쓰게 한다.
  - `dotnet test game/tests/Rootborn.Balance.Tests --filter FullyQualifiedName~WriteFullReport` → 파일 셋이 생긴다. **1분 안쪽인지** 적는다.
  - `output/balance/report.md`를 직접 열어 읽는다: 표가 깨지지 않았는지, 빈 표나 전부 0인 줄이 없는지, 설계 3절의 가설(네 시설의 하루 수입이 같다, 실제 분당 수입은 다르다)과 맞는지. 다르면 고치지 말고 다른 점을 보고에 적는다.
  - `bash Scripts/ci/run-all.sh --checks-only` 통과. `dotnet test game/tests/Rootborn.Game.Tests`와 `game/tests/Rootborn.Data.Tests` 통과, 건수가 줄지 않았다(`DataFingerprintScopeTests` 포함 — `money-balance.json`이 지문에 안 들어간다).
- [ ] **Step 5: 문서** — `game/README.md`, `Scripts/ci/README.md`, `.claude/rules/testing-discipline.md`에 위 Files대로 한 줄씩. `.claude/rules/changelog.md`에 이번 규칙 표 변경을 한 항목으로 적는다.
- [ ] **Step 6: 커밋** — `[TEST] 균형 시뮬레이터 보고서 — days.csv·summary.csv·report.md, 손으로 도는 전체 보고`와 `[DOCS] 균형 시뮬레이터 실행법과 테스트 프로젝트 표` 두 커밋.

---

## 끝난 뒤 보고에 넣을 것

- 프로젝트별 테스트 건수(Balance 새 건수, Game·Data는 그대로).
- 빠른 검사와 전체 보고의 걸린 시간.
- 첫 표의 요점(전략별 가장 많이·적게 버는 대상과 비, 실제 분당 수입의 차이, 레벨 2·3에 닿는 날) — "자동 테스트, 가정을 넣은 계산"이라고 적는다.
- 설계와 달랐던 것, 못 한 것.
- 사용자가 정할 것: 범위 검사의 값(설계 9절).
