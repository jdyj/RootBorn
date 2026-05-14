# NPC Schedule Life Pattern Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "NPC가 일차/요일/시간대/장소 조건에 따라 위치, 대사, 가능 상호작용, 이벤트 후보를 데이터 기반으로 바꾸고, 플레이어가 실제 이동으로 다른 시간대에 같은 NPC를 찾아가면 다른 장소/대사/선택지를 확인할 수 있으며, 저장/로드 후 현재 스케줄 상태가 유지되는 NPC 생활 패턴 루프"를 설계/구현/검증해줘.

현재 NPC 전제:
- `npc.first-guide` — 마을 안내 NPC, 기본 위치 `location.town-square`
- `npc.librarian` — 사서, 기본 위치 `location.library`
- `npc.teacher` — 교사, 기본 위치 `location.school`
- `npc.shopkeeper` — 상점 주인, 기본 위치 `location.corner-store`
- `npc.work-manager` — 작업장/일터 관리자, 기본 위치 `location.workshop`

이번 goal의 기본 대상은 위 5명이다. 친구/동급생, 동료, 가족, 라이벌 같은 신규 NPC는 후속 확장으로 남기고, 필요하면 별도 goal에서 추가한다.

핵심 의도:
1. NPC가 항상 같은 자리에 서 있는 안내판처럼 보이지 않게 한다.
2. Town의 시간대와 장소 정체성이 NPC 위치/대사/상호작용에 반영되게 한다.
3. 플레이어가 "지금 이 NPC가 어디 있을까?"를 생각하게 만들되, 핵심 진행이 특정 시간대 하나에 막히지 않게 한다.
4. 스케줄은 단일 정답 경로가 아니라 장소·시간·관계·컨디션·마일스톤·도감과 연결되는 생활 패턴 데이터여야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI, 실제 키보드 이동, 현재 시간대의 NPC 위치 확인, 시간대/일차 전환, 실제 이동으로 변경된 위치의 NPC 재확인, 다른 대사/상호작용 확인, 저장/로드 재진입을 모두 거쳐야 한다.
- 테스트에서 NpcScheduleProgress, WorldTimeState, StudentLifeProgress, NpcDefinition 상태를 직접 세팅해서 성공 처리하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- npcId, locationId, timeSlotId, scheduleId, dialogueId, eventId별 `if/switch` 분기, enum 기반 엔티티 분기, NPC별 C# 클래스 생성은 금지한다.
- NPC 스케줄, 위치, 대사 세트, 가능 상호작용, 이벤트 후보, 재노출 정책은 SO 데이터와 전략 배열로 표현한다.
- 스케줄은 플레이어를 하드락시키는 장벽이 아니라 장소와 시간에 의미를 주는 장치여야 한다.
- 성능·최적화 동시 설계 원칙을 따른다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 데이터 구조:
- `TimeSlotDefinition`
  - 아침/낮/오후/저녁/밤 같은 시간대를 SO 데이터로 정의한다. C# enum을 쓰지 않는다.
- `NpcScheduleDefinition`
  - NPC 참조, 기본 fallback 위치, schedule entry 배열, 재노출/부재 처리 정책을 정의한다.
- `NpcScheduleEntry`
  - 시간대, 요일, 일차 범위, 장소 참조, 월드 앵커, 대사 세트, 가능 상호작용, 이벤트 후보, 우선순위를 정의한다.
- `NpcScheduleRuleBase[]`
  - 시간대, 요일, 일차, 튜토리얼 단계, 관계, 퀘스트 상태, 마일스톤 상태, 도감 해금 상태를 일반 전략으로 평가한다.
- `NpcDialogueSetDefinition`
  - 장소/시간/관계/컨디션/마일스톤 상태에 따라 사용할 대사 묶음을 정의한다.
- `NpcScheduleResolver`
  - 현재 WorldTimeState와 플레이어 상태를 기준으로 NPC별 active schedule entry를 계산한다.
- `NpcScheduleRuntimeBinder`
  - 현재 active schedule entry에 따라 NPC GameObject 위치, 프롬프트, 대사, 이벤트 후보를 갱신한다.
- `NpcScheduleProgress`
  - 플레이어가 어떤 NPC를 어느 시간/장소에서 만났는지, 첫 만남/재회/new 표시 여부를 저장한다.

NPC별 초기 스케줄 기획:

1. `npc.librarian`
   - 낮: `location.library`
     - 대사: 독학, 지식, 어려운 책 힌트
     - 상호작용: 책 추천, 독학 힌트
     - 이벤트 후보: `도서관: 어려운 책`
   - 저녁: `location.town-square`
     - 대사: 마을 소문, 탐험 힌트
     - 상호작용: 장소/지식 힌트
     - 이벤트 후보: 마을 소문 또는 탐험 단서

2. `npc.teacher`
   - 아침/낮: `location.school`
     - 대사: 수업, 기초 학습, 공식 진로 힌트
     - 상호작용: 수업 안내, 질문하기
   - 오후: `location.town-square`
     - 대사: 학교 밖 경험도 배움이라는 안내
     - 상호작용: 마일스톤 힌트 또는 대체 학습 경로 안내

3. `npc.work-manager`
   - 낮/오후: `location.workshop`
     - 대사: 알바, 책임감, 돈, 피로 관리
     - 상호작용: 알바 시작, 일거리 묻기
     - 이벤트 후보: `일터: 잔업 제안`
   - 저녁: `location.corner-store` 또는 `location.town-square`
     - 대사: 무리하지 말라는 조언, 내일 일거리 힌트
     - 상호작용: 조언 듣기

4. `npc.shopkeeper`
   - 낮/오후: `location.corner-store`
     - 대사: 아이템/자원, 상점 이용, 작은 심부름
     - 상호작용: 물건 둘러보기, 의뢰 힌트
     - 이벤트 후보: `상점: 할인 제안`
   - 저녁: `location.town-square`
     - 대사: 마을 사람 이야기, 내일 필요한 물건 힌트

5. `npc.first-guide`
   - 아침: `location.town-square`
     - 대사: 오늘 갈 수 있는 장소 안내
     - 상호작용: 추천 장소, 튜토리얼/마일스톤 안내
   - 오후/저녁: `location.town-square` 또는 `location.home` 근처
     - 대사: 하루 정리, 다음 날 안내
     - 상호작용: 도감/마일스톤 확인 힌트

스케줄 설계 원칙:
- NPC가 특정 시간대에 없더라도 핵심 정보는 다른 경로로 얻을 수 있어야 한다.
- 보류 가능한 이벤트는 NPC가 다시 해당 장소/시간대에 등장하면 재노출될 수 있어야 한다.
- NPC 부재는 "진행 불가"가 아니라 "다른 장소/다른 시간/다른 경로를 선택"하게 하는 장치여야 한다.
- 중요한 튜토리얼/마일스톤 정보는 first-guide, 도감, 게시판, 장소 UI 같은 대체 경로로도 확인 가능해야 한다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 현재 시간대에서 `npc.librarian` 또는 `npc.teacher` 같은 대상 NPC가 expected location에 스폰되어 있는지 화면에서 확인한다.
3. 플레이어가 실제 이동으로 해당 NPC에게 접근한다.
4. 실제 상호작용 입력으로 현재 장소/시간대 대사를 확인한다.
5. 플레이어가 하루 진행, 시간대 전환, 또는 하루 종료/다음 날 시작을 실제 플레이 경로로 수행한다.
6. 같은 NPC가 다른 location 또는 fallback location으로 이동했는지 화면에서 확인한다.
7. 플레이어가 실제 이동으로 변경된 위치의 NPC에게 접근한다.
8. 실제 상호작용 입력으로 이전과 다른 대사/상호작용/이벤트 후보를 확인한다.
9. NPC를 만난 시간/장소 기록 또는 도감/마일스톤 힌트가 갱신된다.
10. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 현재 시간대 기준 NPC 위치와 만남 기록이 유지된다.
11. 같은 NPC 만남/대사/이벤트 후보가 저장/로드 후 중복 보상이나 new 표시 폭증을 일으키지 않는다.

UI/목업 요구:
- NPC 현재 위치/대사 변경은 실제 화면에서 확인 가능해야 한다.
- 스케줄 변경을 설명하는 디버그 전용 텍스트만으로 완료하지 않는다.
- 필요하면 1차 UI로 NPC 이름, 현재 장소, 대사, 가능 상호작용, 이벤트 힌트를 표시한다.
- Modern UI Style2 공통 패널 규약을 따른다.
- 1920x1080 기준에서 NPC 이름, 대사, 선택지/힌트 텍스트가 겹치지 않아야 한다.

성능/최적화 요구:
- NPC 스케줄 계산은 시간대 변경, 일차 변경, save/load 완료, 관련 상태 변경 시점에만 수행한다.
- 매 프레임 모든 NPC의 모든 schedule entry를 평가하지 않는다.
- NPC/location/timeSlot lookup은 Registry 전체 반복 순회 대신 캐시를 사용한다.
- NPC 위치 갱신은 필요한 NPC만 dirty 처리한다.
- NPC 수와 schedule entry 수가 늘어나는 상황을 고려해 resolver 성능 테스트 또는 구조 검증을 추가한다.
- 최종 보고에는 NPC 수 증가 시 남은 성능 리스크와 후속 최적화 과제를 포함한다.

멀티플레이 확장 고려:
- NPC 스케줄 정의는 공유 데이터다.
- 현재 월드 시간과 NPC 위치는 추후 서버 권한 공유 월드 상태로 승격 가능해야 한다.
- 플레이어별 관계, 대사 해금, 퀘스트, 도감, 마일스톤 진행은 개인 상태로 유지한다.
- 같은 NPC를 여러 플레이어가 동시에 볼 수 있어야 한다.
- 대사 UI와 이벤트 UI는 플레이어별로 분리되어야 하며, Client 1 대화가 Client 2 UI를 강제로 열면 안 된다.
- 멀티플레이에서는 서버/Host가 시간대 전환과 active schedule을 확정하고 모든 클라이언트에 같은 NPC 위치를 보여줄 수 있어야 한다.
- 이번 goal에서 멀티플레이 직접 검증이 범위 밖이면 최종 보고에 후속 검증 항목과 리스크를 명시한다.

필수 저장 대상:
- saveSlot
- player identity
- 현재 WorldTimeState 또는 이를 복원할 일차/요일/시간대
- NPC별 마지막 만남 location id
- NPC별 첫 만남/재회 기록
- NPC schedule 관련 도감/new 표시 여부
- 보류 이벤트가 NPC 스케줄에 연결된 경우 deferred event state

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-12-town-locations-npc-foundation-goal.md`
- `docs/superpowers/goals/2026-05-12-location-identity-gameplay-goal.md`
- `docs/superpowers/goals/2026-05-12-daily-choice-event-loop-goal.md`
- `docs/superpowers/goals/2026-05-12-student-schedule-time-loop-goal.md`
- `.claude/rules/game-design.md`
- `Assets/Scripts/Game/Dialogue/NpcDefinition.cs`
- `Assets/Scripts/Game/Dialogue/NpcInteractor.cs`
- `Assets/Scripts/Game/StudentLife/LocationDefinition.cs`
- `Assets/Scripts/UI/StudentLife/LocationNpcRuntimeInstaller.cs`
- `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- `Assets/Data/NPCs/`
- `Assets/Data/StudentLife/Locations/`
- `Assets/Data/Dialogue/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- NPCSCHEDULE-EDIT-001: TimeSlotDefinition/NpcScheduleDefinition/NpcScheduleEntry가 Registry 또는 지정 데이터 경로에서 로드된다.
- NPCSCHEDULE-EDIT-002: 최소 3명 이상의 NPC가 2개 이상의 schedule entry를 가진다.
- NPCSCHEDULE-EDIT-003: 사서/교사/작업장 관리자 중 최소 2명은 시간대에 따라 다른 location을 반환한다.
- NPCSCHEDULE-EDIT-004: schedule 조건은 npcId/locationId/timeSlotId별 C# 분기 없이 전략 배열로 평가된다.
- NPCSCHEDULE-EDIT-005: NpcScheduleResolver는 현재 WorldTimeState 기준 active entry를 결정한다.
- NPCSCHEDULE-EDIT-006: NPC 만남/재회 기록이 saveSlot + player identity 기준으로 저장/로드된다.
- NPCSCHEDULE-EDIT-007: 같은 NPC 만남/대사/이벤트 후보가 중복 보상/new 표시를 만들지 않는다.
- NPCSCHEDULE-EDIT-008: NPC/location/timeSlot lookup 캐시가 schedule 갱신마다 Registry 전체 순회를 요구하지 않는다.

필수 PlayMode E2E 시나리오:
- NPCSCHEDULE-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- NPCSCHEDULE-E2E-002: 현재 시간대에서 대상 NPC가 기대 장소에 보인다.
- NPCSCHEDULE-E2E-003: 플레이어가 실제 이동으로 대상 NPC에게 접근한다.
- NPCSCHEDULE-E2E-004: 실제 상호작용 입력으로 현재 장소/시간대 대사를 확인한다.
- NPCSCHEDULE-E2E-005: 실제 하루 진행/시간대 전환/다음 날 시작 경로로 WorldTimeState를 변경한다.
- NPCSCHEDULE-E2E-006: 같은 NPC가 다른 장소 또는 fallback 장소로 이동했는지 화면에서 확인한다.
- NPCSCHEDULE-E2E-007: 플레이어가 실제 이동으로 변경된 위치의 NPC에게 접근한다.
- NPCSCHEDULE-E2E-008: 실제 상호작용 입력으로 이전과 다른 대사/상호작션/이벤트 후보를 확인한다.
- NPCSCHEDULE-E2E-009: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 현재 시간대 기준 NPC 위치가 복원된다.
- NPCSCHEDULE-E2E-010: 같은 NPC를 다시 만나도 중복 해금/new 표시/보상이 발생하지 않는다.
- NPCSCHEDULE-E2E-011: 기존 장소/NPC foundation, 장소 정체성, 하루 이벤트 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, NpcInteractor, DialoguePanel, LocationNpcRuntimeInstaller 또는 schedule binder, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- NPC 위치/대사 변경은 도메인 상태만 보지 말고 화면 오브젝트 위치, NPC 이름, 대사 텍스트, 프롬프트, 선택지/이벤트 후보 중 하나 이상으로 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, WorldTimeState, NpcScheduleResolver, NpcInteractor, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "NPC 초기 위치 불일치", "시간대 전환 후 NPC 이동 없음", "대사 변경 없음", "스케줄 저장 복원 실패", "중복 NPC 만남 보상 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 최소 3명 이상의 NPC가 데이터 기반 schedule entry를 가진다.
- 최소 2명 이상의 NPC가 시간대에 따라 위치 또는 대사/상호작용을 바꾼다.
- 실제 플레이 E2E로 같은 NPC를 다른 시간대/장소에서 만나 다른 반응을 확인한다.
- NPC 스케줄은 SO 데이터와 전략 배열 기반이며 엔티티 ID별 분기가 없다.
- 저장/로드 후 현재 시간대 기준 NPC 위치와 만남 기록이 유지된다.
- NPC를 못 만해도 핵심 진행이 막히지 않는 대체 경로 또는 재노출 정책이 있다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, NPC별 스케줄 목록, 실행한 테스트, 실제 플레이 검증 흐름, UI 구현 범위, 성능 검증 범위, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

