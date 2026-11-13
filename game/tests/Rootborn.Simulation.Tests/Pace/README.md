# 플레이 속도 시뮬레이션 (Pace)

설계: `docs/superpowers/specs/2026-11-13-play-pace-simulation-design.md`. 숫자는 모두 **자동 테스트(실제 플레이 아님)** 이고 사람 쪽 시간(읽기·고르기·미니게임 몫)은 가정이다(`PaceAssumptions.cs`).

## 두 층

| 층 | 어디 | 무엇 | 손으로 도는 보고 |
|---|---|---|---|
| 가까운 층 | `Rootborn.Simulation.Tests` | 창 없는 몸이 프레임 단위로 걷고 화면을 누른다. 플레이어 유형 넷(`Play/PlayerTypes.cs`) | `WriteCompareReport` → `output/simulation/pace/compare-<N>days/report.md`, 잰 값 `output/simulation/pace/measured/pace-measured.json` |
| 먼 층 | `Rootborn.Balance.Tests` | 균형 시뮬레이터가 `GameSession` 명령으로 100일. 걷기·화면은 가까운 층에서 잰 값 | `WriteFarReport` → `output/simulation/pace/far-<N>days/report.md` |

속도 기록·분석·보고 파일(`PaceStep`, `PaceAssumptions`, `PaceAnalysis`, `PaceNovelty`, `PaceReport`, `PaceCompare`, `PaceDiff`, `PaceBands`, `PaceMeasured`)은 Balance 테스트가 링크로 같이 컴파일한다. 화면·창 없는 몸에 닿는 것(`PaceRecorder`, `PaceClassifier`)은 Simulation에만 있다.

순서: 가까운 층을 먼저 돌려 잰 값 파일을 만들고, 먼 층이 그 파일을 읽는다. 파일이 없으면 먼 층은 균형 시뮬레이터의 걷기 가정을 쓰고 보고서에 "가정"이라고 적는다.

```bash
dotnet test game/tests/Rootborn.Simulation.Tests --filter "FullyQualifiedName~WriteCompareReport"   # 7일(ROOTBORN_PACE_DAYS)
dotnet test game/tests/Rootborn.Balance.Tests --filter "FullyQualifiedName~WriteFarReport"          # 100일(ROOTBORN_PACE_FAR_DAYS)
```

## 덮어쓰기 전후 비교

`ROOTBORN_DATA_OVERLAY`에 덮어쓰기 폴더를 주면 위 두 보고가 같은 판을 원본과 덮어쓴 것 둘 다 돌려 `…-overlay/report.md`(가까운 층) 또는 `far-<N>days-overlay/report.md`(먼 층) 하나에 전·후·차이로 쓴다. 맨 위에 바뀐 필드 목록이 있다.

덮어쓰기 폴더는 `data/`와 같은 모양(바꿀 엔티티만, 바꿀 필드만)이고, 맨 위의 `economy.json`·`time.json`은 `game/`의 같은 이름 파일을 필드 단위로 덮는다. 예:

```
overlay/
  time.json                                  {"bedtime": "23:00"}
  studentlife/PartTimeWorkDefinition.json    [{"type": "PartTimeWorkDefinition", "id": "work.clinic-helper", "rewardCounts": "0a000000"}]
```

```bash
ROOTBORN_DATA_OVERLAY=/path/to/overlay dotnet test game/tests/Rootborn.Balance.Tests --filter "FullyQualifiedName~WriteFarReport"
```

게임 실행(Host)은 덮어쓰기를 받지 않는다. 마음에 드는 값은 사람이 `data/`·`game/`에 옮긴다.

## 범위 검사 (`pace-bands.json`)

처음에는 비어 있어 아무것도 막지 않는다. 값은 사용자가 정한다. 항목 하나:

```json
{ "metric": "longest-gap-minutes", "layer": "near", "who": "story", "max": 15 }
```

- `metric`: `total-minutes`(판 전체), `day-average-minutes`(1일째부터 하루 평균), `longest-gap-minutes`(가장 긴 새로운 것 없는 구간), `nothing-to-do-share`(할 것 없음의 몫, 0~1), `empty-days`(빈 날 수), `milestone-minutes:<지점 종류>`(그 종류의 첫 지점까지 — 예 `milestone-minutes:성인이 됨`, `milestone-minutes:체인 끝`. 닿지 못하면 범위 밖)
- `layer`: `near`(가까운 층 유형 비교), `far`(먼 층), `*`(둘 다, 없으면 이것)
- `who`: 유형 이름(`story`, `money`, `explorer`, `one-thing`)이나 전략 id(`money-balance.json`), `*`(모두, 없으면 이것)
- `min`, `max`: 분(몫은 0~1). 없는 쪽은 보지 않는다.

모든 숫자는 기준 가정(읽기 보통·고르기 보통·미니게임 0.5)으로 잰다. 모르는 지표는 범위 밖으로 적는다(오타가 조용히 통과하지 않게). 손으로 도는 두 보고와 평소의 먼 층 짧은 판이 범위 밖이면 실패한다.
