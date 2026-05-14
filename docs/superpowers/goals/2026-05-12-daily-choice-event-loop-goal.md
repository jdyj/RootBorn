# Daily Choice Event Loop Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "Town의 장소별 조건을 만족하면 선택지 기반 하루 이벤트가 발생하고, 플레이어가 실제 UI 선택으로 대응하면 서로 다른 비용/보상/관계/컨디션/도감/마일스톤 결과가 적용되며, 이벤트를 즉시 수행·보류·거절·기간 내 수행할 수 있는 확장 가능한 하루 이벤트 루프"를 설계/구현/검증해줘.

핵심 원칙:
- 이벤트는 플레이어를 막는 문이 아니라 성장 경로를 여는 기회다.
- MVP에서 이벤트는 강제 진행이 아니라 Optional/Deferrable 중심으로 구현한다.
- 플레이어가 이벤트를 거절하거나 보류해도 핵심 성장/진행이 막히면 안 된다.
- 기간 제한 이벤트는 실패해도 게임 진행을 막지 않고, 보상/관계/대사/도감/마일스톤 반응만 달라지게 한다.
- 나중에 Required/Timed/Repeatable/Conditional 이벤트를 추가할 수 있도록 데이터 필드와 처리 흐름을 확장 가능하게 설계한다.

전제:
- 장소/NPC 기초와 장소별 정체성 goal이 완료되어, 학교/도서관/일터/상점/광장/집/골목 같은 장소와 NPC/활동 앵커가 존재한다.
- 학교 밖 성장, 마일스톤, 도감, 관계/컨디션 루프가 존재하거나, 이 goal에서 최소 샘플 이벤트로 연결 가능해야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI, 실제 키보드 이동, 장소 방문, 이벤트 발생, 실제 UI 선택, 결과 적용, 보류 후 재방문, 하루 종료, 결과 UI, 저장/로드 재진입을 거쳐야 한다.
- 테스트에서 DailyEventProgress, StudentLifeProgress, Relationship/Status/Milestone/Encyclopedia 상태를 직접 세팅해서 성공 처리하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- eventId, choiceId, locationId, npcId, activityId, questId, milestoneId별 `if/switch` 분기, enum 기반 엔티티 분기, 이벤트별 C# 클래스 생성은 금지한다.
- 이벤트 발생 조건, 선택지, 보류/거절/기간 제한 정책, 결과는 SO 데이터와 전략 배열로 표현한다.
- 성능·최적화 동시 설계 원칙을 따른다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 데이터 구조:
- `DailyEventDefinition`
  - 표시명, 설명, 발생 장소, 관련 NPC/활동, 이벤트 성격, 발생 조건, 선택지 배열, 기간/재노출 정책, 결과 요약 표시 규칙을 정의한다.
- `DailyEventKindDefinition`
  - Optional, Deferrable, Timed, Required, Repeatable, Conditional 같은 이벤트 성격을 SO 데이터로 정의한다. C# enum을 쓰지 않는다.
- `DailyEventAvailabilityRuleBase[]`
  - 장소, 시간대, 일차, 요일, 관계, 컨디션, 튜토리얼 단계, 퀘스트 상태, 마일스톤 상태, 도감 해금 상태, 이전 선택 여부를 일반 전략으로 평가한다.
- `DailyEventChoiceDefinition`
  - 선택지 표시명, 설명, 선택 가능 조건, 비용, 결과 전략 배열, 결과 요약 텍스트, 보류/거절 처리 여부를 정의한다.
- `DailyEventOutcomeBase[]`
  - 특성/성향, 스킬, 관계, 컨디션, 지식, 진로 힌트, 퀘스트 이벤트, 인벤토리/재화, 도감 해금, 마일스톤 진행을 일반 전략으로 적용한다.
- `DailyEventProgress`
  - 플레이어별 이벤트 상태를 저장한다.
  - 저장 필드 예:
    - event id
    - state: unseen/available/deferred/completed/declined/expired/cooldown
    - firstSeenDay
    - lastSeenDay
    - dueDay 또는 dueWorldTime
    - selectedChoice id
    - completionRequestId
    - repeatCount
    - cooldownUntilDay
    - resultSummaryLog
- `DailyEventResolver`
  - 현재 장소/시간/플레이어 상태에서 노출 가능한 이벤트를 계산한다.
- `DailyEventPanel`
  - 이벤트 제목, 설명, 2~3개 선택지, 나중에 하기, 거절하기, 기간 표시를 렌더링한다.
- `DailyEventResultSummary`
  - 하루 결과 UI에 표시할 선택 이벤트 결과 요약 DTO다.

이벤트 성격 설계:
- `Optional`
  - 지금 선택하거나 거절할 수 있다.
  - 거절해도 핵심 진행은 막히지 않는다.
- `Deferrable`
  - 지금 하지 않고 나중에 할 수 있다.
  - 장소 재방문, 다음 날, 특정 시간대에 다시 노출될 수 있다.
- `Timed`
  - dueDay/dueTime까지 선택 가능하다.
  - 만료 시 expired 상태가 되며 보상/관계/대사만 달라진다. 핵심 진행은 막지 않는다.
- `Repeatable`
  - cooldown과 repeatCount 제한을 가진다.
  - 반복 보상은 중복/파밍 폭주를 막는 데이터 제한이 있어야 한다.
- `Required`
  - MVP에서는 사용하지 않거나 튜토리얼 안내 수준으로만 사용한다.
  - 사용하더라도 플레이어를 하드락시키지 않고 대체 경로를 제공해야 한다.
- `Conditional`
  - 관계/컨디션/마일스톤/도감/퀘스트/시간대 조건에 따라 노출된다.

초반 이벤트 기획 예시:
1. `도서관: 어려운 책`
   - 성격: Deferrable
   - 발생 조건: 도서관 방문, 집중 기준치 이상
   - 선택지:
     - 끝까지 읽는다: 학습 스킬 증가, 피로 증가
     - 쉬운 책으로 바꾼다: 지식 소폭 증가, 피로 적음
     - 사서에게 물어본다: 사서 관계 증가, 지식 힌트 해금
     - 나중에 읽는다: deferred 상태, 다음 도서관 방문 시 재노출
2. `광장: 친구의 부탁`
   - 성격: Optional 또는 Timed
   - 발생 조건: 광장 방문, 친구 NPC 첫 만남 이후
   - 선택지:
     - 바로 도와준다: 관계 증가, 피로 증가, 성실/사교 증가
     - 방법만 알려준다: 지식/사교 소폭 증가, 피로 적음
     - 오늘은 어렵다고 말한다: 컨디션 유지, 관계 변화 없음 또는 소폭 감소
     - 내일 다시 말하자: dueDay +1, deferred 상태
3. `집: 밤의 선택`
   - 성격: Optional
   - 발생 조건: 하루 종료 전, 활동 1회 이상 수행
   - 선택지:
     - 조금 더 공부한다: 학습 스킬 증가, 피로 증가
     - 일찍 쉰다: 피로 회복, 다음 날 집중 보너스
     - 내일 계획을 세운다: 마일스톤 힌트 표시, 컨디션 소폭 회복
4. `일터: 잔업 제안`
   - 성격: Optional/Timed
   - 발생 조건: 알바 수행 후, 피로가 기준치 이하
   - 선택지:
     - 잔업한다: 돈 증가, 책임감 증가, 피로 크게 증가
     - 동료를 돕고 끝낸다: 관계 증가, 책임감 증가, 돈 적음
     - 정시에 끝낸다: 피로 억제, 컨디션 유지
5. `골목/외곽: 수상한 단서`
   - 성격: Deferrable
   - 발생 조건: 탐험/발견 지점 방문
   - 선택지:
     - 자세히 조사한다: 지식/진로 힌트 해금, 피로 증가
     - 주변 사람에게 묻는다: NPC 관계 또는 퀘스트 단서
     - 표시만 해두고 돌아간다: 도감에 미완료 단서 등록, 다음 방문 시 재노출

첫 구현 범위 추천:
- 처음부터 모든 이벤트를 구현하지 말고 아래 3개를 우선 구현한다.
  1. `도서관: 어려운 책`
  2. `광장: 친구의 부탁`
  3. `집: 밤의 선택`
- 3개 이벤트는 각각 독학/관계/하루 종료 루프를 대표해야 한다.
- 각 이벤트는 서로 다른 선택 결과와 비용/보상을 가져야 한다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 플레이어가 실제 이동으로 도서관 또는 광장 같은 이벤트 장소에 접근한다.
3. 장소 조건을 만족하면 이벤트 프롬프트 또는 이벤트 UI가 표시된다.
4. 플레이어가 실제 UI 버튼으로 선택지 중 하나를 고른다.
5. 선택 결과로 특성·성향·스킬·관계·컨디션·퀘스트·도감·마일스톤 중 하나 이상이 변화한다.
6. 변화 결과가 HUD, 도감, 마일스톤, 관계/컨디션 UI 또는 즉시 피드백 UI 중 하나 이상에 표시된다.
7. 플레이어가 다른 이벤트에서 `나중에 하기`를 선택한다.
8. 해당 이벤트가 deferred 상태로 저장된다.
9. 다음 날 또는 해당 장소 재방문 시 보류 이벤트가 다시 노출된다.
10. 플레이어가 실제 이동으로 하루 종료 지점에 접근해 하루를 종료한다.
11. 하루 결과 UI에 오늘 선택한 이벤트, 선택지, 결과, 보류/만료 상태가 표시된다.
12. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 완료/보류/거절/만료 상태가 유지된다.
13. 같은 이벤트 선택 결과가 저장/로드 후 중복 적용되지 않는다.

자유 경로 검증 요구:
- 이벤트는 학교 수업을 하지 않아도 학교 밖 장소에서 발생하고 성장에 기여해야 한다.
- 이벤트 하나는 같은 목표나 마일스톤을 학교 루트가 아닌 경로로 진행시켜야 한다.
- 이벤트 거절/보류가 핵심 진행을 막지 않음을 테스트 또는 최종 보고에서 설명해야 한다.

UI/목업 요구:
- 이벤트 UI는 실제 1차 UI로 구현한다. 문서 목업만으로 완료하지 않는다.
- 이벤트 제목, 설명, 2~3개 선택지, 나중에 하기, 거절하기 또는 닫기 버튼을 표시한다.
- Timed 이벤트는 남은 일차/마감 정보를 표시할 수 있는 영역을 미리 둔다.
- 선택 불가능한 선택지는 비활성 또는 조건 텍스트를 표시한다.
- Modern UI Style2 공통 패널 규약을 따른다.
- 1920x1080 기준에서 선택지 텍스트, 설명, 버튼이 겹치지 않아야 한다.

성능/최적화 요구:
- 이벤트 후보 계산은 장소 진입, 시간대 변경, 상태 변경, 저장/로드 완료 같은 트리거에서만 수행한다.
- 매 프레임 모든 이벤트 조건을 평가하지 않는다.
- event/location/category lookup은 Registry 전체 반복 순회 대신 캐시를 사용한다.
- 이벤트 UI 버튼은 pool 또는 재사용 구조를 우선한다.
- 이벤트 수가 100개 이상으로 늘어나는 상황을 고려해 조건 평가 범위를 현재 장소/시간대/플레이어 상태로 좁힌다.
- 최종 보고에는 이벤트 수 증가 시 성능 리스크와 후속 최적화 과제를 포함한다.

멀티플레이 확장 고려:
- 이벤트 정의는 공유 데이터다.
- 이벤트 진행 상태와 선택 결과는 기본적으로 플레이어별 개인 상태다.
- 공유 월드 이벤트가 필요한 경우 개인 이벤트와 별도 상태로 분리한다.
- 멀티플레이에서는 서버 권한으로 이벤트 선택 요청을 검증하고 결과를 확정할 수 있어야 한다.
- UI는 각 클라이언트별 개인 이벤트를 표시해야 하며, 다른 플레이어의 선택 이벤트 UI가 강제로 공유되면 안 된다.
- 이번 goal에서 멀티플레이 직접 검증이 범위 밖이면 최종 보고에 후속 검증 항목과 리스크를 명시한다.

필수 저장 대상:
- saveSlot
- player identity
- 이벤트별 state
- firstSeenDay
- lastSeenDay
- dueDay 또는 dueWorldTime
- selectedChoice id
- completionRequestId
- repeatCount
- cooldownUntilDay
- resultSummaryLog
- 오늘 이벤트 결과 요약

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-12-location-identity-gameplay-goal.md`
- `docs/superpowers/goals/2026-05-12-town-locations-npc-foundation-goal.md`
- `docs/superpowers/goals/2026-05-12-open-ended-milestone-growth-goal.md`
- `docs/superpowers/goals/2026-05-12-encyclopedia-discovery-ui-goal.md`
- `.claude/rules/game-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/World/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Locations/`
- `Assets/Data/NPCs/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- DAILYEVENT-EDIT-001: DailyEventDefinition/DailyEventKindDefinition/choice 정의가 Registry 또는 지정 데이터 경로에서 로드된다.
- DAILYEVENT-EDIT-002: 이벤트 발생 조건은 엔티티 ID별 분기 없이 장소/시간/관계/컨디션/마일스톤/도감/퀘스트 상태를 평가한다.
- DAILYEVENT-EDIT-003: 선택지 결과는 특성·성향·스킬·관계·컨디션·도감·마일스톤·퀘스트 중 하나 이상을 일반 전략으로 변경한다.
- DAILYEVENT-EDIT-004: Deferrable 이벤트는 deferred 상태와 재노출 조건을 저장/로드한다.
- DAILYEVENT-EDIT-005: Timed 이벤트는 dueDay/dueTime과 expired 상태를 표현할 수 있다.
- DAILYEVENT-EDIT-006: 같은 completionRequestId의 선택 결과는 중복 적용되지 않는다.
- DAILYEVENT-EDIT-007: 이벤트 후보 lookup/cache가 UI 갱신마다 Registry 전체 순회를 요구하지 않는다.

필수 PlayMode E2E 시나리오:
- DAILYEVENT-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- DAILYEVENT-E2E-002: 플레이어가 실제 이동으로 이벤트 장소에 접근한다.
- DAILYEVENT-E2E-003: 이벤트 프롬프트 또는 이벤트 UI가 실제 화면에 표시된다.
- DAILYEVENT-E2E-004: 실제 UI 버튼으로 선택지를 고른다.
- DAILYEVENT-E2E-005: 선택 결과가 도메인 상태와 유저가 보는 UI 상태에 함께 반영된다.
- DAILYEVENT-E2E-006: 다른 이벤트에서 `나중에 하기`를 선택하고 deferred 상태가 저장된다.
- DAILYEVENT-E2E-007: 다음 날 또는 장소 재방문 시 deferred 이벤트가 다시 노출된다.
- DAILYEVENT-E2E-008: 하루 결과 UI에 오늘 이벤트 선택과 결과가 표시된다.
- DAILYEVENT-E2E-009: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 이벤트 상태가 유지된다.
- DAILYEVENT-E2E-010: 같은 이벤트 선택 결과가 중복 적용되지 않는다.
- DAILYEVENT-E2E-011: 기존 학생 하루 루프, 장소 정체성, 도감, 마일스톤 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, DailyEventPanel, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- 이벤트 선택 결과는 도메인 상태만 보지 말고 화면 텍스트, 버튼 상태, 하루 결과 UI, HUD, 도감/마일스톤/관계/컨디션 UI 중 하나 이상으로 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, DailyEventProgress, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "이벤트 UI 미표시", "선택 결과 미적용", "보류 이벤트 재노출 실패", "하루 결과 이벤트 요약 누락", "중복 이벤트 보상 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 선택지 기반 하루 이벤트 정의/조건/선택지/결과/상태 저장 구조가 있다.
- MVP 이벤트는 Optional/Deferrable 중심이며, Required/Timed/Repeatable/Conditional 확장이 가능한 필드를 가진다.
- 최소 2개 이상의 이벤트가 실제 플레이 경로로 발생하고 선택 가능하다.
- 최소 1개 이벤트는 `나중에 하기` 후 다음 날 또는 장소 재방문 시 다시 노출된다.
- 선택 결과가 하루 결과 UI와 저장/로드 상태에 반영된다.
- 같은 이벤트 선택 결과가 중복 적용되지 않는다.
- 이벤트는 SO 데이터와 전략 배열 기반이며 엔티티 ID별 분기가 없다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 구현한 이벤트 목록, 실행한 테스트, 실제 플레이 검증 흐름, UI 구현 범위, 성능 검증 범위, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

