# 흐르는 시간의 균형 — 활동 시간 표

- 날짜: 2026-11-12. 상태: 첫 값(사용자 결정 "일단 추천으로 가 보고 플레이하면서 조절해 보자"). 플레이하며 고친다.
- 바탕: 흐르는 시간 설계 `docs/superpowers/specs/2026-11-12-flowing-time-design.md`. 시간 포인트 한도가 없어져, 옛 값(20~60분) 그대로면 체력(12)·집중(10)이 시간보다 먼저 바닥나고 하루 큰 활동이 10~12개가 됐다.
- 목표: **하루 큰 활동 4~6개**, **시간이 체력·집중보다 먼저(또는 비슷하게) 막는다** — "무엇을 할지 고르는" 하루.
- 바꾼 것: `data/`의 `timeCostMinutes`만. 미니게임 활동 19개는 미니게임 시간 규칙이 생긴 뒤(4단계) 목표 값으로 되돌렸다(원칙 5). 체력·집중 비용, 보상, 조건, 이야기 순서는 그대로다.

## 1. 원칙

1. 하루의 깨어 있는 시간은 07:00~22:00(15시간, `game/time.json`)이다. 하루 길이는 실제 600초라 **게임 1분 = 실제 0.4167초, 실제 1초 = 게임 2.4분**이다.
2. **큰 활동**(예전에 시간 포인트 1을 쓰던 것)은 2~3시간대, 30분 단위. 옛 값의 순서를 뒤집지 않는다(짧던 것은 같거나 더 짧게):
   - 옛 값 30분 이하 → **2시간**(120)
   - 31~59분 → **2시간 30분**(150)
   - 60분 이상 → **3시간**(180)
3. **학교 수업**(정책 `free-morning`: 평일만, 하루 한 번)은 **4시간**(240).
4. **가벼운 것**(시간 포인트 0: 장소 소개 `opening.introduce.*`, 하루 이벤트 거절 `square-decline`)은 그대로(0분).
5. **미니게임이 있는 활동**(알바, 진로 연습, 성인 시설 일)도 원칙 2의 목표 값을 그대로 쓴다. 규칙이 미니게임을 하는 동안 흐른 시간을 뺀다(설계 3.2절, Core `StudentLifeProgress.ReserveActivityTime`, 흐르는 시간 4단계 2026-11-12): 시작할 때 시계를 건너뛰지 않고 끝나는 시각만 정하고, 미니게임 화면은 혼자 하기에서도 시계를 멈추지 않으며(화면 쪽 `KeepsClockRunning`), 정산·포기 때 남은 몫(0 아래로는 안 감)만 쓴다. 그래서 실제로 걸리는 시간 = max(데이터 값, 미니게임을 하는 동안 흐른 시간)이다.
   - 이 규칙이 생기기 전(첫 값)에는 "예상 플레이 시간(제한 시간 합 × 0.5 × 2.4분, 30분 단위 내림)"을 데이터에서 미리 뺐다(최소 30분). 규칙이 생긴 뒤 그 값을 목표 값으로 되돌렸다. 표의 "예상 플레이"는 그 계산의 기록이다.
6. 하루 이벤트 선택지 중 시간 포인트 1을 쓰던 것(`library-ask-guide` 등)도 원칙 2를 따른다. 마감 시각이 있는 하루 이벤트는 지금 데이터에 없다(`dueTimeMinutes` 0, `isTimed` 0).

## 2. 표(정의 id, 옛 값 → 새 값, 분)

"목표"는 원칙 2·3의 값, "예상 플레이"는 원칙 5의 미니게임 시간(분, 내리기 전). 체력·집중은 바꾸지 않은 값(시설 일은 체력·집중을 쓰지 않는다 — Core `AdultCareerService`가 시간만 쓴다). 44개 중 39개를 바꿨다.

| 종류 | 정의 id | 시간 정책 | 옛 값 → 새 값 | 목표 | 예상 플레이 | 체력 | 집중 | 비고 |
|---|---|---|---|---|---|---|---|---|
| FacilityWork | `work.chef.level1` | (직접) | 60 → **180** | 180 | 49 | - | - | minigame.chef_work1 제한 41.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 150에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.chef.level2` | (직접) | 60 → **180** | 180 | 51 | - | - | minigame.chef_work2 제한 42.2초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 150에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.chef.level3` | (직접) | 60 → **180** | 180 | 52 | - | - | minigame.chef_work3 제한 43.5초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 150에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.emergency-care.level1` | (직접) | 60 → **180** | 180 | 68 | - | - | minigame.emergencycare_work1 제한 57.0초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 120에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.emergency-care.level2` | (직접) | 60 → **180** | 180 | 84 | - | - | minigame.emergencycare_work2 제한 69.8초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 120에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.emergency-care.level3` | (직접) | 60 → **180** | 180 | 80 | - | - | minigame.emergencycare_work3 제한 66.8초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 120에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.interior.level1` | (직접) | 60 → **180** | 180 | 60 | - | - | minigame.interior_work1 제한 50.0초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 120에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.interior.level2` | (직접) | 60 → **180** | 180 | 54 | - | - | minigame.interior_work2 제한 45.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 150에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.interior.level3` | (직접) | 60 → **180** | 180 | 48 | - | - | minigame.interior_work3 제한 40.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 150에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| FacilityWork | `work.soldier.level1` | (직접) | 60 → **180** | 180 | 21 | - | - | minigame.soldier_work1 제한 17.9초 (첫 값 계산: 예상 플레이 0분 뺌) |
| FacilityWork | `work.soldier.level2` | (직접) | 60 → **180** | 180 | 20 | - | - | minigame.soldier_work2 제한 16.4초 (첫 값 계산: 예상 플레이 0분 뺌) |
| FacilityWork | `work.soldier.level3` | (직접) | 60 → **180** | 180 | 18 | - | - | minigame.soldier_work3 제한 14.9초 (첫 값 계산: 예상 플레이 0분 뺌) |
| DailyEventChoice | `daily-event-choice.library-ask-guide` | paid-activity | 15 → **120** | 120 | — | 0 | 1 |  |
| DailyEventChoice | `daily-event-choice.library-read-to-end` | paid-activity | 30 → **120** | 120 | — | 1 | 1 |  |
| DailyEventChoice | `daily-event-choice.square-decline` | free-interaction | 0 → **0** (그대로) | 0 | — | 0 | 0 | 가벼운 것(시간 포인트 0) |
| DailyEventChoice | `daily-event-choice.square-help-now` | paid-activity | 20 → **120** | 120 | — | 1 | 0 |  |
| LifeActivity | `practice.chef` | paid-activity | 45 → **150** | 150 | 49 | 1 | 1 | minigame.chef_practice 제한 41.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 120에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| LifeActivity | `practice.emergency-care` | paid-activity | 45 → **150** | 150 | 68 | 1 | 1 | minigame.emergencycare_practice 제한 57.0초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 90에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| LifeActivity | `practice.interior` | paid-activity | 45 → **150** | 150 | 60 | 1 | 1 | minigame.interior_practice 제한 50.0초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 90에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| LifeActivity | `practice.soldier` | paid-activity | 45 → **150** | 150 | 21 | 1 | 1 | minigame.soldier_practice 제한 17.9초 (첫 값 계산: 예상 플레이 0분 뺌) |
| LocationActivity | `location.activity.library-hard-book` | paid-activity | 40 → **150** | 150 | — | 1 | 2 |  |
| LocationActivity | `location.activity.school-class` | free-morning | 45 → **240** | 240 | — | 1 | 2 | 수업(평일 하루 한 번) |
| LocationActivity | `location.activity.square-help-request` | paid-activity | 30 → **120** | 120 | — | 1 | 1 |  |
| LocationActivity | `location.activity.store-browse` | paid-activity | 20 → **120** | 120 | — | 0 | 1 |  |
| LocationActivity | `location.activity.workshop-shift` | paid-activity | 60 → **180** | 180 | — | 2 | 1 |  |
| LocationActivity | `opening.clue.archive-door` | paid-activity | 20 → **120** | 120 | — | 0 | 1 |  |
| LocationActivity | `opening.clue.repair-ledger` | paid-activity | 20 → **120** | 120 | — | 0 | 1 |  |
| LocationActivity | `opening.introduce.design-studio` | free-interaction | 0 → **0** (그대로) | 0 | — | 0 | 0 | 가벼운 것(시간 포인트 0) |
| LocationActivity | `opening.introduce.medical-clinic` | free-interaction | 0 → **0** (그대로) | 0 | — | 0 | 0 | 가벼운 것(시간 포인트 0) |
| LocationActivity | `opening.introduce.restaurant` | free-interaction | 0 → **0** (그대로) | 0 | — | 0 | 0 | 가벼운 것(시간 포인트 0) |
| LocationActivity | `opening.introduce.training-ground` | free-interaction | 0 → **0** (그대로) | 0 | — | 0 | 0 | 가벼운 것(시간 포인트 0) |
| OutsideSchoolActivity | `outside.help-neighbor` | paid-activity | 30 → **120** | 120 | — | 1 | 0 |  |
| OutsideSchoolActivity | `outside.self-study.library` | paid-activity | 45 → **150** | 150 | — | 1 | 2 |  |
| PartTimeWork | `work.clinic-helper` | paid-activity | 30 → **120** | 120 | 68 | 1 | 1 | minigame.emergencycare_practice 제한 57.0초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 60에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| PartTimeWork | `work.errand-helper` | paid-activity | 30 → **120** | 120 | 49 | 1 | 1 | minigame.chef_practice 제한 41.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 90에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| PartTimeWork | `work.fitness-helper` | paid-activity | 30 → **120** | 120 | 21 | 1 | 1 | minigame.soldier_practice 제한 17.9초 (첫 값 계산: 예상 플레이 0분 뺌) |
| PartTimeWork | `work.restaurant-helper` | paid-activity | 30 → **120** | 120 | 49 | 1 | 1 | minigame.chef_practice 제한 41.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 90에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| PartTimeWork | `work.shift.corner-store` | paid-activity | 45 → **150** | 150 | 42 | 2 | 1 | minigame.cornerstore_intro 제한 35.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 120에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| PartTimeWork | `work.studio-helper` | paid-activity | 30 → **120** | 120 | 60 | 1 | 1 | minigame.interior_practice 제한 50.0초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 60에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| PartTimeWork | `work.supply-helper` | paid-activity | 30 → **120** | 120 | 68 | 1 | 1 | minigame.emergencycare_practice 제한 57.0초 (첫 값 계산: 예상 플레이 60분 뺌) — 첫 값 60에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| PartTimeWork | `work.training-helper` | paid-activity | 30 → **120** | 120 | 21 | 1 | 1 | minigame.soldier_practice 제한 17.9초 (첫 값 계산: 예상 플레이 0분 뺌) |
| PartTimeWork | `work.workshop-helper` | paid-activity | 30 → **120** | 120 | 54 | 1 | 1 | minigame.workshop_intro 제한 45.0초 (첫 값 계산: 예상 플레이 30분 뺌) — 첫 값 90에서 목표로 되돌림(규칙이 미니게임 시간을 뺀다) |
| TownHelpAction | `town.help.first-guide-errand` | paid-activity | 20 → **120** | 120 | — | 1 | 0 |  |
| TownHelpAction | `town.help.teacher-study-prep` | paid-activity | 20 → **120** | 120 | — | 1 | 0 |  |

## 3. 하루 흐름 예시(실제 데이터, 자동 테스트 Game `DayBalanceTests`)

욕심쟁이 플레이어: 앞 목록(위주로 하는 것)을 먼저 보고, 목록 안에서는 돌아가며 고른다. 활동마다 걷는 시간 30분(실제 12.5초)을 먼저 보내고, 미니게임은 제한 시간의 절반을 실제로 한다고 본다. 취침 시각(22:00)이 되면 멈춘다. 22:00 전에 시작한 활동까지 센다.

| 하루 | 큰 활동 | 멈춘 때(이유) | 남은 체력·집중 | 한 것 |
|---|---|---|---|---|
| 수업 있는 평일(1일째) | 5 | 22:30(취침 시각) | 7·4 | 수업 1, 이웃 돕기 2, 도서관 자습 2 |
| 학교 밖 위주 평일 | 6 | 23:30(취침 시각) | 5·4 | 이웃 돕기 3, 도서관 자습 3 |
| 알바 위주 평일 | 6 | 22:30(취침 시각) | 5·4 | 알바 6곳 한 번씩 |
| 진로 연습 위주 평일 | 5 | 22:00(취침 시각) | 7·5 | 요리 2, 응급·인테리어·군인 1씩 |
| 주말(6일째) | 6 | 23:30(취침 시각) | 5·4 | 이웃 돕기 3, 도서관 자습 3 |

수업 있는 평일의 흐름: 07:00 → 걸어서 07:30 수업 시작 → 11:30 → 12:00 이웃 돕기 → 14:00 → 14:30 자습 → 17:00 → 17:30 이웃 돕기 → 19:30 → 20:00 자습 → 22:30.

- 다섯 경우 모두 **시간(취침 시각)이 먼저 막고**, 체력 4~7·집중 4~5가 남는다. 집중 2짜리(자습·어려운 책)만 고르면 집중과 시간이 거의 같이 막는다(07:00부터 자습 5번 = 집중 10, 끝 22:00).
- 마지막 활동은 22:00 전에 시작해 22:30~23:55에 끝난다. 23:00을 넘기면 밤 체력 감소 1(원칙: 취침 뒤 다 채운 1시간마다 1).
- 성인 시설 일(테스트 없음, 계산): 체력·집중을 쓰지 않아 시간만 막는다. max(일, 예상 플레이) + 걷기 = 180+30 = 3시간 30분 → 하루 4~5번(네 시설 같다).
- 걷는 시간을 빼면(같은 자리에서 같은 활동만 되풀이) 2시간짜리는 하루 8번이다(`FlowingTimeBalanceTests`의 제때 잠: 이웃 돕기 8번, 07:00~23:00). 범위 4~6은 활동 사이를 걸어 다니는 것을 전제로 한다.

## 4. 기존 시각 조건과 부딪히는 곳(규칙은 바꾸지 않았다)

| 조건 | 어디 | 새 길이와 | 결과 |
|---|---|---|---|
| 선생님은 아침(12:00 전)에만 학교에 있다 | `data/npcs` `Schedule_Teacher`(학교 아침, 낮엔 광장) | 4시간 수업을 08:00 뒤에 시작하면 12:00 넘어 끝난다 | 수업 뒤 학교에서 선생님과 이야기 못 함(광장에는 있다). 메인 체인 001(선생님과 대화)은 장소를 따지지 않아 막히지 않는다 |
| 사서·가게 주인·일터 관리인은 낮(12~17시)에만 그 자리에 있다 | `Schedule_Librarian`·`Shopkeeper`·`WorkManager` | 14:30 이후 시작한 2.5~3시간 활동은 저녁에 끝난다 | 활동 뒤 그 NPC는 저녁 자리(광장·가게)로 옮겨 있다 |
| 장소 상태의 시간대(아침 <12, 낮 <17, 저녁 <21, 밤) | Core `LocationStateTimeSlots.FromMinutes`, 도서관 저녁 상태 | 활동 하나가 시간대 하나를 거의 덮는다(낮 5시간 = 활동 2개) | 시간대마다 보이는 것을 보려면 그 사이에 활동을 안 해야 한다. 막히는 활동은 없다(어려운 책은 세 상태 모두에서 된다) |
| 취침 시각 22:00 | `game/time.json` | 21:59에도 3시간 활동을 시작할 수 있다 → 00:59에 끝, 밤 체력 −2 | 시작을 막는 규칙은 없다. 경고는 화면 쪽 |
| 하루 끝 07:00(다음 날) | Core `TimeRules.DayEndMinutes` | 04:00 뒤 3시간 활동 → 07:00을 넘겨 다음 날 아침으로 이어진다(흐르는 시간 1단계 규칙) | 늦은 밤 활동만 해당 |
| 하루 이벤트 마감 | `DailyEventKind.dueTimeMinutes` | 지금 데이터에 마감 있는 종류가 없다 | 부딪힘 없음 |
| 일과 계획판 칸 | 평일 칸 "방과 후·저녁", 주말 "아침·낮·저녁" | 칸 기록은 시각이 아니라 일과 순서로 한다. 지금 칸은 `CurrentDailyPlanSlotIndex`(시각) | 평일 아침(07~12)엔 칸이 없다 — 수업 4시간이 그 자리를 채운다 |

## 5. 바꾸려면 어디를 고치나

- **활동 하나의 길이**: 그 정의의 `timeCostMinutes`(`data/` — 2절 표의 파일). 이제 `data/`가 원본이다(변환기를 다시 돌리지 않는다).
- **원칙 전체**(티어 2·2.5·3시간, 수업 4시간): 1절 원칙대로 다시 계산해 2절 표와 함께 고친다. 표를 만든 계산은 1절이 전부다.
- **목표 범위와 가정**: `game/tests/Rootborn.Game.Tests/day-balance.json`(게임은 읽지 않는다, 검사만 읽는다. 배포와 데이터 지문에 들어가지 않으므로 고쳐도 같이 하기가 갈리지 않는다 — 2026-11-12에 `game/time.json`의 `"balance"`에서 옮겼다):
  - `bigActivitiesPerDay` `{ "min": 4, "max": 6 }` — 하루 큰 활동 수 범위
  - `bodyMayStopFrom` `"19:00"` — 체력·집중이 하루를 막아도 되는 가장 이른 때(그 전에 막히면 "시간이 먼저 막는다"가 깨진 것)
  - `travelMinutesPerActivity` `30` — 활동마다 걷는 시간 가정
  - `minigamePlayShareOfTimeLimit` `0.5` — 미니게임을 제한 시간의 몇 몫만큼 한다고 볼지(검사가 그만큼 시계를 돌린 뒤 정산한다)
- 값을 고친 뒤 `dotnet test game/tests/Rootborn.Game.Tests --filter "FullyQualifiedName~DayBalanceTests|FullyQualifiedName~FlowingTimeBalanceTests"` — 범위를 벗어나면 어느 하루가 몇 개였는지 실패 문구에 나온다.
- 하루의 깨어 있는 시간(`dayStart`·`bedtime`)과 하루 길이(`data/time` `realSecondsPerGameDay`)를 바꾸면 1절 2.4분/초와 원칙 5의 값도 바뀐다.
