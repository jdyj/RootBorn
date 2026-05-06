# ROOTBORN Time System Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트에 시간 시스템을 정식 설계/구현해줘.

절대 규칙:
- AGENTS.md 헌법을 지킨다.
- Assets/**/*.cs는 디스크 직접 쓰기 금지. Unity MCP script-update-or-create 또는 Editor 안전 경로만 사용한다.
- 반드시 실패 테스트 먼저 작성하고, 구현은 그 다음에 한다.
- 작물/도구/세대 등 엔티티별 if/switch/ID enum 분기는 금지한다. 모든 튜닝은 ScriptableObject 데이터로 처리한다.
- 기존 사용자 변경분을 되돌리지 않는다.

목표:
1. 1게임일 기본 길이를 10분(600초)으로 둔다.
2. 시간 밸런스는 TimeDefinition ScriptableObject로 데이터화한다.
3. GameClock은 TimeDefinition을 읽어 Day, DayProgress01, OnDayRolled를 결정론적으로 계산한다.
4. 세대 전환은 실시간 초 고정값이 아니라 GenerationProfile의 lifetimeGameDays로 판단한다.
5. 세대별 lifetimeGameDays는 SO에서 다르게 튜닝 가능해야 하며, 초기 기본값은 14게임일로 둔다.
6. 농사 시스템은 GameClock 날짜 롤오버와 연동되어 물 리셋/비료 만료/성장 조건이 깨지지 않아야 한다.
7. 기존 CropDefinition + GrowthBehaviorBase[] 전략 구조를 유지한다.

필수 테스트:
- TIME-001: GameClock 기본 하루 길이가 600초이고 600초 경과 시 Day 2가 된다.
- TIME-002: TimeDefinition의 realSecondsPerGameDay를 바꾸면 Day/DayProgress 계산이 결정론적으로 바뀐다.
- TIME-003: OnDayRolled는 날짜가 바뀔 때 정확히 1회 발화한다.
- GEN-002: GenerationProfile.lifetimeGameDays 경과 시 세대가 전환된다.
- GEN-003: 하루 길이를 바꿔도 동일한 lifetimeGameDays 기준 세대 전환 의미가 유지된다.
- CROP-006: 날짜 롤오버 후 물 상태가 초기화되고 RequiresDailyWaterBehavior 작물은 물 없이는 성장하지 않는다.
- CROP-007: 비료 durationDays는 GameClock 날짜 기준으로 만료된다.

권장 작업 순서:
1. 기존 GameClock, GenerationManager, GenerationProfile, FarmGrid, CropPlot 테스트를 읽고 현재 동작을 정리한다.
2. 위 테스트 중 가장 작은 EditMode 실패 테스트부터 추가한다.
3. 테스트 실패를 확인한다.
4. TimeDefinition과 GameClock 변경을 최소 구현한다.
5. 세대 전환을 lifetimeGameDays 기반으로 변경한다.
6. FarmGrid 날짜 연동 회귀 테스트를 보강한다.
7. 관련 EditMode 테스트를 실행한다.
8. 가능하면 Farming PlayMode 시나리오도 실행한다.
9. 마지막 보고에는 수정 파일, 추가 테스트, 실행 결과, 남은 리스크를 적는다.

완료 조건:
- 위 필수 테스트가 모두 존재하고 통과한다.
- 기존 GameClock/Crop/Generation 관련 테스트가 통과한다.
- TimeDefinition SO 인스턴스와 registry 연결 필요 여부가 정리된다.
- 하드코딩된 세대/작물 ID 분기가 없다.
```

## Reference Spec

설계 스펙은 `docs/superpowers/specs/2026-05-06-time-system-design.md`를 참조한다.
