# Town Locations NPC Foundation Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "Town의 주요 장소(학교, 도서관, 공장/일터, 상점, 광장, 집 등)와 핵심 NPC를 ScriptableObject 데이터 기반으로 설계하고, 실제 플레이어가 이동해 방문/상호작용/도감 해금/기초 대사를 확인할 수 있는 장소·NPC 기초 레이어"를 설계/구현/검증해줘.

이 goal은 장소별 고유 활동을 모두 완성하는 작업이 아니다. 목적은 이후 `장소별 정체성 강화`, `선택지 기반 하루 이벤트`, `NPC 스케줄`, `학교 밖 성장 루트`, `도감`이 재사용할 수 있는 장소/NPC 데이터와 1차 월드 배치를 만드는 것이다.

핵심 의도:
1. Town을 단순 테스트 맵이 아니라 생활 공간으로 만들기 위한 최소 장소/NPC 기반을 만든다.
2. 학교, 도서관, 공장/일터, 상점, 광장, 집 같은 장소가 데이터로 정의되고 Registry에 등록되어야 한다.
3. 각 장소에는 최소 1개 이상의 역할, 방문 힌트, 관련 NPC 또는 상호작용 가능 지점이 있어야 한다.
4. NPC는 단순 말풍선 오브젝트가 아니라 관계/퀘스트/도감/스케줄/장소 정체성의 기준 데이터가 되어야 한다.
5. 실제 플레이어가 장소를 방문하고 NPC와 상호작용해서 장소/NPC 도감 항목 또는 기본 정보를 해금할 수 있어야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI, 실제 키보드 이동, 장소 방문, NPC 접근, 실제 Prompt/Input/Button 상호작용, 도감/대사/UI 확인, 저장/로드 재진입을 거쳐야 한다.
- 테스트에서 LocationProgress, NpcProgress, EncyclopediaProgress, QuestLog, StudentLifeProgress를 직접 세팅해서 성공 처리하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- locationId, npcId, routeId, activityId, questId별 `if/switch` 분기, enum 기반 엔티티 분기, 장소/NPC별 C# 클래스 생성은 금지한다.
- 장소 정의, NPC 정의, 방문 조건, 기본 대사, 도감 해금, 방문 결과는 SO 데이터와 전략 배열로 표현한다.
- UI와 월드 배치는 Modern UI Style2, 실제 플레이 검증, 성능·최적화 동시 설계 원칙을 따른다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 장소 목록:
- `학교`
  - 역할: 수업, 기초 학습, 교사 NPC, 진로 힌트의 공식 루트.
  - 학교 밖 루트와 경쟁하는 유일 정답이 아니라 여러 성장 경로 중 하나로 설계한다.
- `도서관`
  - 역할: 독학, 지식 해금, 집중/피로 비용, 진로 힌트의 자기 주도 루트.
- `공장` 또는 `일터`
  - 역할: 알바, 책임감, 돈, 피로, 직업/기술 힌트.
  - "공장" 명칭이 현재 Town 분위기와 맞지 않으면 `작업장`, `마을 공방`, `상점 창고`처럼 데이터에서 바꿀 수 있게 한다.
- `상점`
  - 역할: 아이템/자원, 알바, 주민 의뢰, 경제 튜토리얼.
- `광장`
  - 역할: NPC 만남, 도움 행동, 게시판, 마을 소식, 탐험/도감 진입점.
- `집`
  - 역할: 휴식, 하루 종료, 컨디션 회복, 개인 도감/목표 확인.
- `골목/외곽`
  - 역할: 탐험, 발견, 작은 이벤트, 학교 밖 성장 루트.

권장 NPC 목록:
- `담임/교사 NPC`
  - 학교 활동과 기초 진로 힌트를 소개한다.
- `사서 NPC`
  - 독학, 지식 도감, 도서관 루트를 소개한다.
- `작업장/공장 관리자 NPC`
  - 알바, 책임감, 피로, 돈 루트를 소개한다.
- `상점 주인 NPC`
  - 아이템/자원/간단한 의뢰/경제 루트를 소개한다.
- `친구/동급생 NPC`
  - 관계, 도움 행동, 선택지 이벤트를 소개한다.
- `마을 안내 NPC`
  - 광장, 게시판, 장소 탐색, 도감 해금을 안내한다.

권장 데이터 구조:
- `LocationDefinition`
  - 표시명, 설명, 카테고리, 월드 앵커, 권장 방문 시간대, 기본 활동 힌트, 관련 NPC, 관련 도감 항목, 관련 마일스톤 힌트를 정의한다.
- `LocationCategoryDefinition`
  - 학교/학습/일터/상점/공공장소/집/탐험 같은 범주를 SO로 정의한다. C# enum을 쓰지 않는다.
- `NpcDefinition`
  - 표시명, 소개, 기본 장소, 관계 축, 기본 대사 세트, 관련 퀘스트/활동/도감 항목, 스케줄 확장 포인트를 정의한다.
- `NpcRoleDefinition`
  - 교사, 사서, 관리자, 상점 주인, 친구, 안내자 같은 역할을 SO로 정의한다.
- `LocationVisitRuleBase[]`
  - 장소 방문 조건, 도감 해금 조건, 시간대 조건, 튜토리얼 조건을 일반 전략으로 평가한다.
- `NpcDialogueConditionBase[]`
  - 기본 대사/해금 대사/관계 대사 조건을 일반 전략으로 평가한다.
- `LocationNpcRuntimeBinder`
  - Registry의 장소/NPC 데이터를 실제 Town 월드 오브젝트와 연결한다.
- `LocationVisitProgress`
  - saveSlot + player identity 기준으로 방문 여부, 첫 방문 일차, 발견/도감 해금 상태를 저장한다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. Town에 최소 4개 이상의 주요 장소가 실제 이동 가능한 지점 또는 명확한 월드 앵커로 존재한다.
3. 플레이어가 실제 이동으로 광장 또는 안내 NPC에게 접근한다.
4. 실제 상호작용 입력으로 안내 NPC 기본 대사를 확인한다.
5. 플레이어가 실제 이동으로 학교, 도서관, 일터/공장, 상점 중 2곳 이상을 방문한다.
6. 장소 방문 시 장소 이름/프롬프트/도감 해금/방문 피드백 중 하나 이상이 화면에 표시된다.
7. 각 방문 장소에서 최소 1명의 NPC 또는 상호작용 지점을 확인한다.
8. 실제 상호작용 입력으로 NPC 기본 대사를 확인한다.
9. 장소/NPC 도감 항목 또는 발견 상태가 해금된다.
10. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 장소 방문 상태와 NPC 발견 상태가 유지된다.
11. 같은 장소/NPC를 다시 방문해도 중복 해금 보상이나 new 표시 폭증이 발생하지 않는다.

도감/마일스톤/후속 루트 연결:
- 장소 발견은 도감의 장소 카테고리에 연결되어야 한다.
- NPC 첫 만남은 도감의 NPC 카테고리에 연결되어야 한다.
- 장소 방문은 후속 마일스톤 목표 조건으로 사용할 수 있어야 한다.
- 장소는 후속 학교 밖 성장 루트의 앵커가 되어야 한다.
  - 도서관 → 독학
  - 공장/일터/상점 → 알바
  - 광장/NPC → 도움 행동
  - 골목/외곽 → 탐험/발견
- 이번 goal에서 후속 활동 전체를 구현하지 않더라도, 데이터 구조와 테스트 이름에서 연결 가능성을 명확히 남긴다.

UI/목업 요구:
- 장소/NPC 발견 피드백은 최소 1차 UI로 제공한다. 문서 설명만으로 완료하지 않는다.
- 도감 시스템이 이미 있다면 장소/NPC 항목이 실제 도감 UI에 나타나야 한다.
- 도감 시스템이 아직 없다면 최소한 임시 발견 패널 또는 HUD/토스트로 장소/NPC 발견을 표시하고, 후속 도감 goal에서 교체 가능한 경계를 둔다.
- Modern UI Style2 공통 패널 규약을 따른다.
- 1920x1080 기준에서 장소 이름, NPC 이름, 대사, 발견 피드백 텍스트가 겹치지 않아야 한다.

성능/최적화 요구:
- 장소/NPC 정의는 Registry에서 반복 전체 순회하지 않도록 런타임 lookup 캐시를 구성한다.
- NPC/장소 UI는 상태 변경, 방문, 탭 전환, 대사 시작 같은 명확한 트리거에서만 갱신한다.
- Town 시작 시 모든 상세 UI를 생성하지 말고, 필요한 패널/항목만 생성하거나 재사용한다.
- NPC/장소 수가 늘어나도 매 프레임 거리 계산/전체 검색이 폭증하지 않도록 상호작용 라우터와 콜라이더 기반 탐지를 우선한다.
- 최종 보고에는 장소/NPC 수 증가 시 남은 성능 리스크를 포함한다.

멀티플레이 확장 고려:
- 장소 정의와 NPC 정의는 공유 월드 데이터다.
- 장소 방문/도감 해금/관계 상태는 기본적으로 플레이어별 개인 상태다.
- NPC 위치/스케줄은 추후 서버 권한 공유 월드 상태로 승격 가능해야 한다.
- 멀티플레이에서는 같은 NPC와 여러 플레이어가 상호작용할 수 있으므로, 대사 UI와 개인 진행 상태가 플레이어별로 분리되어야 한다.
- 이번 goal에서 멀티플레이 직접 검증이 범위 밖이면 후속 검증 항목과 리스크를 최종 보고에 명시한다.

필수 저장 대상:
- saveSlot
- player identity
- 방문한 location id 목록
- 처음 만난 npc id 목록
- 장소별 첫 방문 일차 또는 발견 순서
- NPC별 첫 만남 일차 또는 발견 순서
- 도감/발견 new 표시 여부

먼저 확인할 파일:
- `.claude/rules/game-design.md`
- `docs/superpowers/goals/2026-05-12-encyclopedia-discovery-ui-goal.md`
- `docs/superpowers/goals/2026-05-11-town-exploration-discovery-goal.md`
- `docs/superpowers/goals/2026-05-12-outside-school-growth-foundation-goal.md`
- `Assets/Scripts/Game/World/`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Data/Locations/`
- `Assets/Data/NPCs/`
- `Assets/Data/Dialogue/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- LOCNPC-EDIT-001: LocationDefinition/LocationCategoryDefinition/NpcDefinition/NpcRoleDefinition이 Registry 또는 지정 데이터 경로에서 로드된다.
- LOCNPC-EDIT-002: 최소 4개 이상의 주요 장소 데이터가 존재하고, 각 장소는 표시명/설명/카테고리/앵커/관련 역할 정보를 가진다.
- LOCNPC-EDIT-003: 최소 4명 이상의 핵심 NPC 데이터가 존재하고, 각 NPC는 표시명/소개/기본 장소/역할/기본 대사 참조를 가진다.
- LOCNPC-EDIT-004: 장소 방문 조건과 NPC 대사 조건은 엔티티 ID별 분기 없이 전략 배열로 평가된다.
- LOCNPC-EDIT-005: 장소 방문/NPC 첫 만남 상태가 saveSlot + player identity 기준으로 저장/로드된다.
- LOCNPC-EDIT-006: 같은 장소/NPC 발견 이벤트를 재적용해도 중복 해금/new 표시 폭증이 발생하지 않는다.
- LOCNPC-EDIT-007: 장소/NPC lookup 캐시가 UI 갱신마다 Registry 전체 순회를 요구하지 않는다.

필수 PlayMode E2E 시나리오:
- LOCNPC-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- LOCNPC-E2E-002: 플레이어가 실제 이동으로 안내 NPC 또는 광장 지점에 접근한다.
- LOCNPC-E2E-003: 실제 상호작용 입력으로 안내 NPC 기본 대사를 확인한다.
- LOCNPC-E2E-004: 플레이어가 실제 이동으로 학교/도서관/일터/상점 중 2곳 이상을 방문한다.
- LOCNPC-E2E-005: 방문한 장소에서 장소 이름, 프롬프트, 발견 피드백, 도감 해금 중 하나 이상이 화면에 표시된다.
- LOCNPC-E2E-006: 각 방문 장소에서 NPC 또는 상호작용 지점 하나 이상을 실제 입력으로 확인한다.
- LOCNPC-E2E-007: 장소/NPC 발견 상태가 UI와 도메인 상태에 함께 반영된다.
- LOCNPC-E2E-008: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 방문/발견 상태가 유지된다.
- LOCNPC-E2E-009: 같은 장소/NPC를 다시 확인해도 중복 해금/new 표시가 발생하지 않는다.
- LOCNPC-E2E-010: 기존 학생 하루 루프와 학교 밖 성장 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, NpcInteractor, DialoguePanel 또는 발견 UI, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- 장소/NPC 확인은 도메인 상태만 보지 말고 화면 텍스트, 프롬프트, 대사, 발견 피드백, 도감/임시 UI 중 하나 이상으로 함께 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, LocationVisitProgress, NpcProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "장소 데이터 누락", "NPC 데이터 누락", "실제 이동 방문 실패", "NPC 대사 미표시", "장소 발견 저장 실패", "중복 발견 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- Town 주요 장소와 핵심 NPC가 SO 데이터 기반으로 정의되고 Registry에서 로드된다.
- 최소 4개 이상의 장소와 4명 이상의 NPC가 후속 시스템에서 재사용 가능한 데이터로 존재한다.
- 실제 플레이 경로로 최소 2개 이상의 장소를 방문하고 NPC 또는 상호작용 지점을 확인한다.
- 장소/NPC 발견 상태가 UI에 표시되고 저장/로드 후 유지된다.
- 같은 장소/NPC 발견이 중복 적용되지 않는다.
- 장소/NPC 데이터는 후속 도감, 마일스톤, 학교 밖 성장, 스케줄 시스템과 연결 가능한 구조다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 배치된 장소/NPC 목록, 실행한 테스트, 실제 플레이 검증 흐름, 성능 검증 범위, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

