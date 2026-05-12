# Encyclopedia Discovery UI Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학생이 학교/학교 밖 활동, 탐험, 퀘스트, 관계, 진로 힌트, 지식 해금을 진행하면서 발견한 장소·NPC·지식·진로 단서·활동·아이템/자원을 도감 UI에서 열람할 수 있고, 해금 상태가 저장/로드 후 유지되며, 대량 항목 확장에도 버티는 최적화된 도감 시스템"을 설계/구현/검증해줘.

이 goal의 도감은 생활기록장이 아니다. 플레이어의 일기식 히스토리나 모든 행동 로그를 나열하지 않는다. 도감은 발견/해금/정보 열람 중심의 시스템이며, 마일스톤과 퀘스트를 대체하지 않는다.

구분:
- 퀘스트 = 오늘 수행할 구체적 사건/의뢰
- 마일스톤 = 며칠 동안 쌓아가는 장기 목표/진행도/보상
- 도감 = 발견하거나 해금한 정보의 기록/열람/힌트 UI

핵심 의도:
1. 자유 경로 플레이에서 발견한 정보가 사라지지 않고 도감에 남아야 한다.
2. 장소, NPC, 지식, 진로 힌트, 활동, 아이템/자원을 카테고리별로 볼 수 있어야 한다.
3. 미해금 항목은 `???` 또는 흐린 상태로 표시해 탐색 동기를 만든다.
4. 도감 UI는 목업 수준으로 끝나지 않고, 최소 1차 실제 UI로 PlayMode에서 열고 닫고 탭을 전환하고 상세를 볼 수 있어야 한다.
5. 처음부터 항목 수 증가를 고려해 성능/최적화를 함께 설계한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI, 실제 이동/상호작용으로 도감 항목 해금, 실제 입력/버튼으로 도감 UI 열기, 카테고리 탭 전환, 상세 패널 확인, 저장/로드 재진입을 거쳐야 한다.
- 테스트에서 EncyclopediaProgress, KnowledgeProgress, Location/NPC/Item unlock 상태를 직접 세팅해서 도감 UI만 확인하면 실패다.
- Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- encyclopediaId, categoryId, locationId, npcId, knowledgeId, careerId, activityId, itemId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 도감 항목, 카테고리, 해금 조건, 표시 규칙, 힌트 공개 단계는 SO 데이터와 전략 배열로 표현한다.
- 도감 UI는 Modern UI Style2 공통 패널 규약을 지킨다.
- 성능·최적화 동시 설계 원칙을 따른다. 대량 항목 UI는 가상화, 페이징, 필터 캐싱, 지연 로딩, dirty 갱신 중 적절한 방식을 선택한다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

도감 카테고리:
- 장소 도감
  - 발견한 장소, 장소 설명, 가능한 활동, 만날 수 있는 NPC, 관련 퀘스트/마일스톤 힌트
- NPC 도감
  - 만난 NPC, 기본 소개, 관계 단계, 좋아하는 활동/도움 요청 힌트, 해금된 대사/퀘스트 힌트
- 지식 도감
  - 독학/탐험/퀘스트로 얻은 지식, 생활 팁, 컨디션/관계/진로 정보
- 진로 힌트 도감
  - 발견한 진로 후보, 영향을 준 활동, 필요한 성향/스킬/관계 힌트, 미해금 단서 `???`
- 활동 도감
  - 수행해본 학교 활동, 도움 행동, 독학, 알바, 탐험, 성장 방향, 시간/컨디션 비용
- 아이템/자원 도감
  - 얻어본 아이템/자원, 사용처, 관련 퀘스트/활동

권장 구조:
- `EncyclopediaCategoryDefinition`
  - 장소/NPC/지식/진로/활동/아이템 같은 카테고리를 SO로 정의한다. C# enum을 쓰지 않는다.
- `EncyclopediaEntryDefinition`
  - 표시명, 설명, 카테고리, 아이콘, 미해금 표시명, 상세 섹션, 관련 엔티티 참조, 해금 조건 배열을 정의한다.
- `EncyclopediaUnlockConditionBase[]`
  - 장소 발견, NPC 만남, 지식 해금, 진로 힌트 발견, 활동 수행, 아이템 획득, 퀘스트 완료, 마일스톤 달성 등을 일반 조건으로 평가한다.
- `EncyclopediaProgress`
  - 플레이어별 해금 상태, 첫 발견 일차, 공개 단계, new 표시 여부를 저장한다.
- `EncyclopediaProgressPersistence`
  - saveSlot + player identity 기준으로 저장/로드한다.
- `EncyclopediaIndex`
  - Registry 전체 순회를 반복하지 않도록 카테고리/entry lookup 캐시를 구성한다.
- `EncyclopediaPanel`
  - 실제 UI. 카테고리 탭, 목록, 잠금/해금 표시, 상세 패널, 검색/필터 또는 페이징 중 최소 하나를 지원한다.
- `EncyclopediaUnlockNotifier`
  - 항목 해금 시 HUD/토스트 또는 하루 결과 UI에 "새 도감 항목"을 표시한다.

UI 요구:
- 실제 열람 가능한 1차 UI를 만든다. 목업 이미지나 문서 설명만으로 완료하지 않는다.
- 열기/닫기 입력 또는 버튼이 있어야 한다.
- 카테고리 탭: 장소 / NPC / 지식 / 진로 / 활동 / 아이템
- 목록 항목은 해금/미해금 상태를 구분한다.
- 미해금 항목은 `???` 또는 흐린 상태로 표시한다.
- 선택한 항목의 상세 패널을 표시한다.
- 새로 해금된 항목에는 new 표시 또는 강조가 있어야 한다.
- Modern UI Style2 공통 패널과 기존 UI 스타일을 따른다.
- PC 1920x1080 기준에서 텍스트 겹침/버튼 잘림이 없어야 한다.

성능/최적화 요구:
- 도감 항목 수가 100~500개로 늘어나는 상황을 고려한다.
- 탭 전환이나 검색/필터 때 매번 전체 GameObject를 Destroy/Instantiate하지 않는다.
- 목록은 페이징, 가상화, pool 재사용 중 현재 프로젝트에 맞는 방식을 선택한다.
- Registry 전체 순회는 초기화/캐시 구성 시점으로 제한하고, UI 갱신마다 반복하지 않는다.
- UI는 해금 이벤트, 탭 전환, 검색어 변경, 저장/로드 완료 같은 dirty 트리거에서만 갱신한다.
- Addressables/sprite는 필요한 항목만 로드하고 중복 로드를 피한다.
- PlayMode 또는 EditMode에서 최소 대량 더미 항목 UI 갱신 테스트를 추가해, 항목 수 증가 시 구조가 무너지지 않는지 확인한다.
- 최종 보고에는 성능 검증 범위, 남은 병목 가능성, 후속 최적화 과제를 포함한다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 플레이어가 학교 또는 학교 밖 활동/탐험/퀘스트/독학 중 하나를 실제 이동/상호작용으로 수행한다.
3. 수행 결과로 장소, NPC, 지식, 진로 힌트, 활동, 아이템/자원 중 하나 이상의 도감 항목이 해금된다.
4. 해금 피드백이 HUD/토스트/하루 결과 UI 중 하나에 표시된다.
5. 플레이어가 실제 입력 또는 UI 버튼으로 도감 UI를 연다.
6. 카테고리 탭을 전환한다.
7. 해금된 항목과 미해금 항목 표시가 다르게 보인다.
8. 해금된 항목을 선택하면 상세 패널이 표시된다.
9. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 해금 상태가 유지된다.
10. 같은 발견/해금 이벤트를 재호출해도 중복 해금 보상이나 new 표시 폭증이 발생하지 않는다.

멀티플레이 확장 고려:
- 도감 해금 상태는 기본적으로 플레이어별 개인 상태로 저장한다.
- 공유 월드 발견이 필요한 경우 개인 도감과 별도 공유 발견 상태를 분리한다.
- 멀티플레이에서는 서버 권한으로 해금 이벤트를 확정하고 해당 플레이어의 도감 상태만 갱신할 수 있어야 한다.
- 이번 goal에서 멀티플레이 직접 검증이 범위 밖이면 최종 보고에 후속 검증 항목과 리스크를 명시한다.

필수 저장 대상:
- saveSlot
- player identity
- 도감 카테고리별 해금 항목 id
- 항목별 공개 단계
- 첫 발견 일차 또는 발견 순서
- new 표시 여부
- 검색/탭 UI 상태는 저장 필수가 아니며, 필요하면 로컬 UI 상태로만 유지한다.

먼저 확인할 파일:
- `.claude/rules/game-design.md`
- `docs/superpowers/goals/2026-05-12-open-ended-milestone-growth-goal.md`
- `docs/superpowers/goals/2026-05-11-town-exploration-discovery-goal.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/Modern/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Data/Knowledge/`
- `Assets/Data/Locations/`
- `Assets/Data/NPCs/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/`
- `Assets/Tests/PlayMode/`

필수 EditMode 테스트:
- ENCYCLOPEDIA-EDIT-001: 도감 카테고리와 항목 정의가 Registry 또는 지정 데이터 경로에서 로드된다.
- ENCYCLOPEDIA-EDIT-002: 해금 조건은 엔티티 ID별 분기 없이 장소/NPC/지식/진로/활동/아이템 상태를 평가한다.
- ENCYCLOPEDIA-EDIT-003: 해금 상태와 new 표시가 saveSlot + player identity 기준으로 저장/로드된다.
- ENCYCLOPEDIA-EDIT-004: 같은 해금 이벤트를 재적용해도 중복 해금/중복 new 로그가 발생하지 않는다.
- ENCYCLOPEDIA-EDIT-005: 카테고리/entry lookup 캐시가 UI 갱신마다 Registry 전체 순회를 요구하지 않는다.
- ENCYCLOPEDIA-EDIT-006: 100개 이상 더미 항목으로 목록 모델/페이징/풀링 구조가 동작한다.

필수 PlayMode E2E 시나리오:
- ENCYCLOPEDIA-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- ENCYCLOPEDIA-E2E-002: 실제 이동/상호작용으로 도감 항목 하나 이상을 해금한다.
- ENCYCLOPEDIA-E2E-003: 실제 입력 또는 UI 버튼으로 도감 UI를 연다.
- ENCYCLOPEDIA-E2E-004: 장소/NPC/지식/진로/활동/아이템 중 최소 2개 이상 카테고리 탭을 전환한다.
- ENCYCLOPEDIA-E2E-005: 해금 항목과 미해금 항목이 화면에서 구분된다.
- ENCYCLOPEDIA-E2E-006: 해금 항목 상세 패널이 실제 텍스트를 표시한다.
- ENCYCLOPEDIA-E2E-007: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 해금 상태가 유지된다.
- ENCYCLOPEDIA-E2E-008: 같은 해금 이벤트를 재확인해도 중복 해금/중복 new 표시가 발생하지 않는다.
- ENCYCLOPEDIA-E2E-009: 도감 UI가 1920x1080 기준에서 텍스트/탭/상세 패널 겹침 없이 표시된다.

완료 조건:
- 도감 데이터, 해금 상태, 저장/로드, UI 열람 흐름이 구현된다.
- 도감은 생활기록장이 아니라 발견/해금/정보 열람 중심으로 동작한다.
- 장소/NPC/지식/진로/활동/아이템 카테고리 구조가 있다.
- 실제 플레이 경로로 항목을 해금하고 UI에서 확인하는 PlayMode E2E가 통과한다.
- 미해금 항목과 해금 항목이 UI에서 명확히 구분된다.
- 저장/로드 후 해금 상태가 유지되고 중복 해금이 발생하지 않는다.
- 대량 항목 확장을 고려한 UI 갱신/캐싱/페이징 또는 풀링 구조가 있다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, UI 목업/실제 UI 구현 범위, 실행한 테스트, 실제 플레이 검증 흐름, 성능 검증 범위, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

