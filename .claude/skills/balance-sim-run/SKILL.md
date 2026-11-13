---
name: balance-sim-run
description: "ROOTBORN 수치 균형 시뮬레이터(game/tests/Rootborn.Balance.Tests)를 고치고 원격(Strix Halo)에서 돌려 표를 읽고 견준다. '시뮬레이션 돌려', '균형 시뮬레이터', '밸런스 확인', '수치 바꾸면 어떻게 돼', '훑기 다시', '어떤 직업이 돈을 제일 버는지', 전략·잠 규칙·축·가정을 더하거나 data/ 수치를 바꿔 결과를 볼 때 사용."
---

# 수치 균형 시뮬레이터 돌리기

설계: `docs/superpowers/specs/2026-11-12-balance-simulator-design.md`(13절이 모든 경우 훑기). 결정 대기: `docs/superpowers/specs/2026-11-12-balance-simulator-open-decisions.md`. 플레이 시간·지루한 구간(속도) 쪽은 `game/tests/Rootborn.Simulation.Tests/Pace/README.md`.

## 지킬 것

- **돌리는 곳은 원격(Strix Halo)이다.** 이 PC에서 `dotnet build`·`dotnet test`를 돌리지 않는다(메모리가 모자라 꺼진다). 사용자 지시 2026-11-12.
- **가정은 한 값으로 고정하지 않고 축에 둔다.** 새 가정(자는 때, 걷는 분, 점수 …)이 생기면 `money-balance.json`의 `axes`에 값 목록으로 넣어 전부 돌린다. 사용자 지시 2026-11-13.
- **숫자는 "자동 테스트, 가정을 넣은 계산, 실제 플레이 아님"으로 보고한다.** 표를 옮길 때 기준 가정(자는 때·점수·걷는 분·미니게임 몫)을 같이 적는다.
- **수입은 지갑 변화로 읽는다.** 시뮬레이터에 보상 식을 베끼지 않는다. 행동은 `GameSession` 명령만 부른다. 엔티티 id로 분기하지 않는다(대상은 `All<T>()`).
- **기획 수치를 정하지 않는다.** 시뮬레이터는 재기만 한다. 값을 바꾸자는 결론이 나오면 결정 대기 문서에 적고 사용자에게 알린다. `bands`(범위 검사)는 사용자가 값을 정하기 전에는 비워 둔다.

## 무엇이 있나

| 것 | 어디 | 무엇 |
|---|---|---|
| 설정 | `game/tests/Rootborn.Balance.Tests/money-balance.json` | 전략(`strategies`), 기준 가정(`baseline`, `scoreTiers`, `days`), 훑기 축(`axes`) |
| 걷기·미니게임 기준 값 | `game/tests/Rootborn.Game.Tests/day-balance.json` | 걷는 분, 미니게임 몫(축 목록에 이 값이 들어 있어야 한다) |
| 한 판 | `Run/BalanceRunner.cs` | 전략 × 대상 × 가정(`RunSetup`)을 날 단위로. 날마다 `DayRecord`, 요약 `RunSummary` |
| 빠른 검사 | 나머지 테스트(짧은 날 수) | 커밋마다 도는 것. 몇 초 |
| 전체 보고 | `Report/FullReport.cs` `WriteFullReport`(Explicit) | 기준 가정으로 전략마다 `output/balance/report.md`·`summary.csv`·`days.csv` |
| 모든 경우 훑기 | `Sweep/FullSweep.cs` `WriteSweepReport`(Explicit) | 축의 곱 전부(지금 79,248판 × 100일, 원격 22~25분) → `output/balance/sweep/sweep-report.md`·`sweep.csv` |
| 먼 층 속도 보고 | `FarPaceTests.cs` `WriteFarReport`(Explicit) | 100일 동안 새로운 것 사이 간격(Pace README) |

## 돌리는 순서

접속 값은 환경 변수로만 준다(값은 사용자 전역 지침의 FEVM 항목, 저장소에 적지 않는다): `ROOTBORN_REMOTE_HOST`, `ROOTBORN_REMOTE_USER`, `ROOTBORN_REMOTE_KEY`.

1. **고친 것을 커밋한다.** 원격은 커밋된 것만 받는다(푸시는 필요 없다). 실패 테스트를 먼저 커밋해 RED를 보고 구현을 커밋한다 — 중간 커밋 제목에 "(진행 중)"을 붙였으면 합치기 전에 묶는다.
2. **빠른 검사부터.** 1~2분.
   ```bash
   bash Scripts/remote-test/run.sh HEAD -- game/tests/Rootborn.Balance.Tests
   ```
   통과 줄만 보지 말고 건수를 본다(2026-11-13 main: 전체 99, 그중 Explicit 3건은 건너뜀).
3. **보고를 돌린다.** 오래 걸리니 시작만 하고 나중에 받는다.
   ```bash
   bash Scripts/remote-test/run.sh --no-wait HEAD -- game/tests/Rootborn.Balance.Tests --filter "FullyQualifiedName~WriteSweepReport|FullyQualifiedName~WriteFullReport"
   bash Scripts/remote-test/run.sh --resume <실행 id>     # 끝났으면 받고, 아니면 기다린다
   bash Scripts/remote-test/run.sh --status               # 도는 실행과 자리(동시 2개)
   ```
4. **받은 표를 읽는다.** `output/remote-test/<실행 id>/output/balance/report.md`, `…/sweep/sweep-report.md`. 원격 결과 보관은 24시간이니 그 안에 받는다.
5. **앞 실행과 견준다.** 가정·데이터를 안 바꿨으면 `sweep.csv`가 바이트까지 같아야 한다(`cmp`). 다르면 무엇이 바꿨는지 설명할 수 있어야 한다. 코드만 고쳤는데 숫자가 달라졌으면 그것이 발견이다.
6. **합치기 전에 전체 테스트.** `bash Scripts/remote-test/run.sh --no-wait HEAD`(약 22분). 프로젝트별 건수가 줄지 않았는지 본다.

### 기다릴 때

- 배경 프로세스로 기다리지 않는다(이 PC에서 메모리 부족으로 꺼진다). `--no-wait`로 시작하고, 앞에서 `timeout 585 bash Scripts/remote-test/run.sh --resume <id>`를 되풀이한다. 끊겨도 원격은 끝까지 돈다 — 다시 시작하지 말고 `--resume`.
- 같은 커밋·같은 인자의 실행이 있으면 거기에 붙는다. 새로 돌리려면 `--force`.
- 다른 세션의 실행이 자리 둘을 다 쓰면 줄을 선다("다른 실행 2개가 돌고 있어 기다린다"). 남의 실행을 취소하지 않는다.

## 수치를 바꿔 돌리기(data/를 고치지 않고)

덮어쓰기 폴더에 바꿀 엔티티의 `type`·`id`와 바꿀 최상위 필드만 적는다(모양은 `data/`와 같고, 맨 위 `economy.json`·`time.json`은 `game/`의 같은 이름 파일을 필드 단위로 덮는다). 자세한 규칙은 `Scripts/remote-test/README.md` "수치를 덮어써서 돌리기".

원격은 커밋된 것만 받으므로 폴더를 **커밋하고 저장소 뿌리 기준 상대 경로**로 가리킨다. 자리는 `game/tests/Rootborn.Balance.Tests/overlays/<이름>/`.

```bash
bash Scripts/remote-test/run.sh --no-wait HEAD -- game/tests/Rootborn.Balance.Tests \
  --filter "FullyQualifiedName~WriteSweepReport|FullyQualifiedName~WriteFullReport" \
  -e ROOTBORN_DATA_OVERLAY=game/tests/Rootborn.Balance.Tests/overlays/<이름>
```

- 환경 변수는 `dotnet test`의 `-e 이름=값`으로 넘긴다(`run.sh`는 이 PC의 환경 변수를 원격으로 옮기지 않는다). `ROOTBORN_BALANCE_DAYS`(전체 보고의 날 수)도 같다.
- 덮어썼으면 `report.md`·`sweep-report.md` 맨 위에 바뀐 필드(`type/id.필드: 전 -> 후`)가 적힌다. **이 줄이 없으면 덮어쓰기가 안 먹은 것이다** — 숫자를 보기 전에 먼저 확인한다.
- 전후를 견주려면 덮어쓰기 없는 실행과 있는 실행을 둘 다 돌려 `sweep.csv`를 견준다(같은 커밋이어도 인자가 달라 다른 실행이 된다).
- 마음에 드는 값을 `data/`·`game/`에 옮기는 일은 기획 결정이다. 사용자가 정한 뒤에 옮기고, 시험이 끝난 덮어쓰기 폴더는 지운다.

## 무엇을 고치려면 어디를

| 하려는 것 | 고칠 곳 |
|---|---|
| 전략 더하기(행동 차례만 다름) | `money-balance.json` `strategies`에 한 덩어리. `do`는 위에서부터 되는 것을 한다. 조건부 전략은 끝에 늘 되는 행동(`best-facility-work`)을 둔다 — 없으면 그 조건이 열릴 때까지 0을 번다 |
| 새 행동 종류 | `Actions/`에 `IBalanceAction` 클래스 하나 + `BalanceActions`의 `Register(...)`에 한 줄. `PlannedStep`에 걸리는 분·미니게임 분·걷는지를 정확히 넣는다(잠 규칙이 이것으로 판단한다) |
| 새 잠 규칙 | `Run/SleepRules.cs`(`ISleepRule`, `Parse`) + `axes.sleep`에 값 |
| 새 점수·알바 성공 모델 | `Run/ScoreModels.cs` + `axes.score`·`axes.partTime` |
| 새 시작 상태(예: 학생 첫날부터) | `Run/RunStarts.cs` `IRunStart` + `Register`에 한 줄 |
| 새 대상 종류 | `Run/RunTargets.cs` `Register`에 한 줄(정의 형 하나를 `All<T>()`로) |
| 새 가정(축) | `Run/RunSetup.cs`에 값 하나, `Run/BalanceConfig.cs` `Axes`·`Validate`, `Sweep/SweepRunner.cs` `Plan`의 곱, `Sweep/SweepRow.cs` 열, `Sweep/SweepReport.cs` 축 표 |
| 새 요약 값(열) | `Run/DayRecord.cs`에 날마다 적고 `Run/RunSummary.cs`에서 모은 뒤 `SweepRow`·보고서 열 |
| 범위 검사 | `money-balance.json` `bands`(지금은 비어 있지 않으면 실패한다 — 값은 사용자 결정 뒤) |

고칠 때마다:

- **게임 규칙 사실을 테스트로 고정한다.** 시뮬레이터가 기대는 규칙(취침 22:00 뒤 시간당 체력 1 감소, 04:00 뒤 회복 0, 지치면 자야 풀림, 정산만 07:00을 넘기면 보수를 받고 미니게임 도중 07:00이면 잃는다)은 `CarryFactsTests`·`RunSetupRunnerTests`처럼 실제 `GameSession`으로 확인한 테스트에 둔다. 규칙이 바뀌면 이 테스트가 먼저 깨져야 한다.
- **0을 버는 판을 의심한다.** 전략이 실제로 0인지, 행동이 한 번도 안 된 것인지(`ZeroActionDays`, `Stop` 사유: bedtime·bag-full·body·nothing-to-do·exhausted·day-ran-out) 가린다.
- **거절될 일을 시작하지 않는다.** `refused_after_day_end`가 0이 아니면 행동 길이 판정이 틀린 것이다.
- **축을 더하면 판 수가 곱으로 는다.** 값 4개짜리 축 하나면 훑기가 4배(약 1시간 반)다. 판 수와 걸린 시간을 보고에 적고, 너무 길면 값 수를 줄일지 사용자에게 묻는다.
- **병렬 훑기의 결과는 순서와 무관해야 한다.** 판끼리 상태를 나누지 않는다(정의는 `SharedDefinitions`로 읽기만, 저장소는 판마다 `MemorySaveStore`). 같은 커밋을 두 번 돌리면 `sweep.csv`가 같아야 한다.
- 새 환경 변수는 `game/README.md` 인자 표에, 새 보고는 같은 문서의 보고 줄에 적는다.

## 보고할 때

- 판 수, 날 수, 걸린 시간, 실패한 판 수, 실행 id와 표의 전체 경로.
- 기준 가정과 함께 결과. 1등만이 아니라 근소한 차이(1% 안)는 근소하다고 적는다.
- 덮어썼으면 바뀐 필드. 어느 커밋의 데이터인지.
- 게임 규칙 쪽 발견(버그 같아 보이는 것, 의도와 엇갈리는 이득)은 고치지 말고 결정 대기 문서에 번호로 적고, 정해질 때까지 보고마다 다시 알린다.
- 아직 재지 않는 것: 가방이 찼을 때 버리기, 학생 첫날부터 성인까지 이어 도는 판, 무작위 점수, 가격 환산.
