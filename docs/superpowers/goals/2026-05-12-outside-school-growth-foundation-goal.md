# Outside School Growth Foundation Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학교 수업 밖의 다양한 성장 루트(마을 도움 행동, 독학/도서관, 알바/일손 돕기, 탐험/발견)가 공통으로 재사용할 수 있는 학교 밖 성장 기반 구조"를 설계/구현/검증해줘.

이 goal은 콘텐츠 루트 4개를 한 번에 구현하는 작업이 아니다. 목적은 4개 루트가 각각 저장 필드, 하루 결과 UI, 중복 방지, 성장 로그, Registry 와이어링을 따로 만들면서 PlayMode 충돌을 일으키지 않도록 공통 기반을 먼저 만드는 것이다.

후속 goal 순서:
1. `2026-05-11-town-help-action-growth-goal.md`
2. `2026-05-11-self-study-library-growth-goal.md`
3. `2026-05-11-part-time-work-growth-goal.md`
4. `2026-05-11-town-exploration-discovery-goal.md`

핵심 의도:
- 자유 경로 성장 원칙을 코드/데이터/테스트 기반으로 받을 수 있는 공통 레이어를 만든다.
- 학교 수업이 아닌 활동도 특성·성향·스킬·진로 힌트·관계·컨디션·퀘스트·인벤토리 변화를 기록하고 하루 결과 UI에 표시할 수 있어야 한다.
- 후속 루트들은 이 기반을 재사용하고, 각자 별도 저장/UI/중복방지 구조를 만들지 않는다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증 가능한 구조를 준비해야 한다.
- 최종 완료 근거에는 최소 1개 이상의 실제 PlayMode 경로가 포함되어야 한다. 단, 이 foundation goal의 실제 경로는 가장 작은 샘플 학교 밖 활동 하나로 검증해도 된다.
- 테스트에서 Trait/Skill/Relationship/Status/QuestLog/Inventory/StudentLifeProgress를 직접 세팅해서 "학교 밖 성장 기반이 동작한다"고 보고하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- routeId, activityId, locationId, questId, traitId, skillId, careerId, relationshipId, statusId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 학교 밖 성장 경로, 조건, 결과, 하루 요약, 중복 방지는 SO 데이터와 일반 전략 배열로 표현한다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 공통 구조:
- `OutsideSchoolActivityDefinition`
  - 학교 밖 활동의 표시명, 설명, 범주, 장소/상호작용 힌트, 요구 조건, 결과 전략 배열을 정의한다.
  - 마을 도움 행동, 독학, 알바, 탐험은 이 정의를 직접 재사용하거나 얇은 도메인별 래퍼로 확장한다.
- `OutsideSchoolActivityCategoryDefinition`
  - Help, SelfStudy, Work, Exploration 같은 범주를 데이터로 정의한다. C# enum 대신 SO를 사용한다.
- `OutsideSchoolRequirementBase[]`
  - 시간대, 장소, 관계, 컨디션, 튜토리얼 단계, 퀘스트 상태, 인벤토리 조건을 일반 전략으로 평가한다.
- `OutsideSchoolOutcomeBase[]`
  - 특성·성향, 스킬, 진로 힌트, 관계, 컨디션, 퀘스트 이벤트, 인벤토리/재화, 지식 해금을 일반 전략으로 적용한다.
- `OutsideSchoolActivityResult`
  - 활동 id, 표시명, category, request id, before/after 또는 delta, 적용된 outcome 로그를 담는 도메인 결과 모델이다.
- `OutsideSchoolDayLog`
  - 오늘 수행한 학교 밖 활동 목록, 결과 로그, 이전 날 요약, 중복 적용 방지 request id를 저장한다.
- `OutsideSchoolGrowthSummary`
  - 하루 결과 UI에 표시할 학교 밖 성장 요약 DTO다.
- `OutsideSchoolActivityInteractor`
  - 샘플 활동을 실제 플레이 경로로 수행하기 위한 범용 상호작용 컴포넌트다.

기존 구조 재사용 원칙:
- `LifeActivityDefinition`이 이미 충분히 일반적이면 새 정의를 과하게 만들지 말고, 학교 밖 활동 범주/로그/요약/중복방지 레이어만 추가한다.
- 기존 `StudentLifeProgress`, `StudentDaySummary`, `StudentDayResultPanel`, `GameDataRegistry`, `QuestLog`, `PlayerInventory`, `RelationshipProgress`, `StatusProgress` 구조를 먼저 읽고 재사용한다.
- `StudentDayResultPanel`에 루트별 UI를 각자 추가하지 말고, 공통 `Outside School` 섹션 또는 공통 summary entry 렌더링을 만든다.
- save data는 후속 루트가 필드를 계속 늘리지 않도록 일반 로그 배열/요약 배열 형태를 우선한다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 플레이어가 학교 수업을 수행하지 않은 상태에서 샘플 학교 밖 활동 지점으로 실제 이동한다.
3. 실제 상호작용 입력과 UI 버튼으로 샘플 학교 밖 활동을 수행한다.
4. 결과로 특성·성향·스킬·진로 힌트·관계·컨디션·퀘스트·인벤토리·지식 중 하나 이상이 변경된다.
5. 변경 결과가 공통 `OutsideSchoolActivityResult`와 오늘 로그에 기록된다.
6. 플레이어가 실제 이동으로 하루 종료 지점에 접근한다.
7. 실제 상호작용 입력으로 하루를 종료한다.
8. 하루 결과 UI에 `Outside School` 또는 동등한 공통 섹션으로 학교 밖 활동과 결과 로그가 표시된다.
9. 결과 UI 버튼으로 다음 날을 시작한다.
10. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 학교 밖 활동 로그와 성장 상태가 유지된다.
11. 같은 request id 또는 같은 하루 결과를 재호출해도 중복 보상/중복 성장/중복 관계/컨디션 변화가 발생하지 않는다.

멀티플레이 확장 고려:
- 학교 밖 성장 결과는 기본적으로 플레이어별 개인 상태로 처리한다.
- 공유 월드 오브젝트의 소모/재사용 여부는 데이터로 구분한다.
- 멀티플레이에서는 서버 권한으로 활동 요청을 검증하고, 개인 상태 변화만 해당 플레이어에게 적용할 수 있어야 한다.
- 월드 시간/스케줄은 공유 상태로 승격 가능해야 하며, 개인 활동 로그와 분리한다.
- 이번 goal에서 멀티플레이 직접 실행 검증이 범위 밖이면 최종 보고에 후속 검증 항목과 리스크를 명시한다.

필수 EditMode 테스트:
- OUTSIDE-FOUNDATION-EDIT-001: 학교 밖 활동 정의와 범주 정의가 Registry 또는 지정 데이터 경로에서 로드된다.
- OUTSIDE-FOUNDATION-EDIT-002: 요구 조건 전략이 엔티티 ID별 분기 없이 평가된다.
- OUTSIDE-FOUNDATION-EDIT-003: 결과 전략이 특성·성향·스킬·진로 힌트·관계·컨디션·퀘스트·인벤토리·지식 중 하나 이상을 일반 outcome 로그로 기록한다.
- OUTSIDE-FOUNDATION-EDIT-004: 같은 request id의 결과는 중복 적용되지 않는다.
- OUTSIDE-FOUNDATION-EDIT-005: 오늘 로그와 이전 날 요약이 저장/로드 데이터에 포함된다.
- OUTSIDE-FOUNDATION-EDIT-006: 하루 결과 summary DTO가 루트별 하드코딩 없이 공통 entry 배열로 생성된다.
- OUTSIDE-FOUNDATION-EDIT-007: 도움 행동/독학/알바/탐험 후속 루트가 같은 결과/로그/요약 모델을 재사용할 수 있다.
- OUTSIDE-FOUNDATION-EDIT-008: `Scripts/ci/check-no-entity-id-branching.sh`가 차단할 route/activity/location ID별 분기가 없다.

필수 PlayMode E2E 시나리오:
- OUTSIDE-FOUNDATION-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- OUTSIDE-FOUNDATION-E2E-002: 학교 수업 없이 실제 이동으로 샘플 학교 밖 활동 지점에 접근한다.
- OUTSIDE-FOUNDATION-E2E-003: 실제 Prompt/Input/Button 경로로 샘플 학교 밖 활동을 수행한다.
- OUTSIDE-FOUNDATION-E2E-004: 활동 결과가 도메인 상태와 유저가 보는 UI 상태에 함께 반영된다.
- OUTSIDE-FOUNDATION-E2E-005: 하루 종료 결과 UI에 공통 학교 밖 성장 요약이 표시된다.
- OUTSIDE-FOUNDATION-E2E-006: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 활동 로그와 성장 상태가 유지된다.
- OUTSIDE-FOUNDATION-E2E-007: 저장/로드 후 같은 결과를 재확인해도 중복 적용되지 않는다.
- OUTSIDE-FOUNDATION-E2E-008: 기존 학생 하루 루프, 퀘스트/인벤토리/관계/컨디션 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- 학교 밖 활동 결과는 도메인 상태만 보지 말고 화면 텍스트, 결과 UI, HUD, QuestLog, Inventory UI, 관계/컨디션 UI 중 관련 화면 상태로 함께 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, Progress, OutsideSchoolDayLog, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "학교 밖 활동 접근 실패", "공통 결과 로그 미기록", "하루 결과 UI 섹션 누락", "저장 후 학교 밖 로그 복원 실패", "중복 적용 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 학교 밖 성장 루트들이 공유할 공통 정의/조건/결과/로그/요약/중복방지 기반이 있다.
- 최소 샘플 학교 밖 활동 1개가 실제 플레이 경로로 수행되고 성장 또는 상태 변화를 만든다.
- 하루 결과 UI가 학교 밖 활동 결과를 공통 섹션 또는 공통 entry 렌더링으로 표시한다.
- 저장/로드 후 학교 밖 활동 로그와 성장 상태가 유지된다.
- 같은 request id 또는 같은 하루 결과가 중복 적용되지 않는다.
- 후속 4개 goal이 이 기반을 재사용할 수 있도록 파일/타입/테스트 경계가 명확하다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 실행한 테스트, 실제 플레이 검증 흐름, 후속 4개 goal 재사용 방법, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

