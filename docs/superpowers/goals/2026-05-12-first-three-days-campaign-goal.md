# First Three Days Campaign Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학생이 첫 3일 동안 Town 장소, NPC, 학교/학교 밖 성장, 도감, 마일스톤, 하루 이벤트, 진로 힌트를 자연스럽게 경험하되, 각 일차는 하나의 정답 루트가 아니라 여러 대체 경로로 진행 가능한 초반 캠페인 루프"를 설계/구현/검증해줘.

핵심 원칙:
- 초반 3일 캠페인은 강제 튜토리얼 레일이 아니다.
- 플레이어가 학교를 계속 다니든, 중간에 나와 마을 활동을 하든, 독학/알바/탐험/도움 행동을 선택하든 핵심 진행이 가능해야 한다.
- 각 일차는 "오늘 반드시 이 하나를 해라"가 아니라 "이런 방향을 소개하되 여러 경로 중 하나로 달성 가능"해야 한다.
- 캠페인은 자유 경로 성장 원칙을 플레이어에게 실제로 보여주는 첫 경험이다.

전제:
- 학생 하루 루프, 하루 결과 UI, 저장/로드, 장소/NPC 기초, 장소별 정체성, 학교 밖 성장, 마일스톤, 도감, 하루 이벤트, NPC 스케줄 goal이 완료되었거나 이 goal에서 최소 샘플 흐름으로 연결 가능해야 한다.
- 멀티플레이 직접 검증은 이번 goal의 필수 완료 조건은 아니지만, 개인 진행 상태와 공유 월드 시간/장소/NPC 상태가 분리될 수 있는 구조를 유지해야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI에서 새 게임을 시작하고, 실제 키보드 이동/상호작용/UI 선택으로 1일차→2일차→3일차를 진행해야 한다.
- 최소 1개 이상의 E2E는 학교 수업만 따르는 경로가 아니라 학교 밖 경로를 포함해야 한다.
- 테스트에서 CampaignProgress, TutorialStage, MilestoneProgress, EncyclopediaProgress, QuestLog, Inventory, StudentLifeProgress를 직접 세팅해서 성공 처리하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- dayId, routeId, campaignId, milestoneId, locationId, npcId, activityId, eventId별 `if/switch` 분기, enum 기반 엔티티 분기, 일차별 C# 클래스 생성은 금지한다.
- 캠페인 단계, 일차 목표, 대체 경로, 보상, 안내 문구, 다음 날 전이는 SO 데이터와 전략 배열로 표현한다.
- 보상/인벤토리/마일스톤/도감/진로 힌트는 저장/로드 후 중복 적용되면 안 된다.
- 성능·최적화 동시 설계 원칙을 따른다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 데이터 구조:
- `CampaignDefinition`
  - 캠페인 표시명, 설명, 일차/단계 배열, 활성 조건, 완료 조건, 보상 배열을 정의한다.
- `CampaignDayDefinition`
  - 1일차/2일차/3일차의 주제, 추천 목표, 대체 경로 목록, 표시 안내, 결과 요약 규칙을 정의한다.
- `CampaignRouteDefinition`
  - 학교 루트, 도움 행동 루트, 독학 루트, 알바 루트, 탐험 루트 같은 대체 경로를 SO로 정의한다. C# enum을 쓰지 않는다.
- `CampaignObjectiveBase[]`
  - 장소 방문, NPC 대화, 학교/학교 밖 활동 수행, 도감 해금, 마일스톤 진행, 하루 이벤트 선택, 진로 힌트 발견을 일반 조건으로 평가한다.
- `CampaignRewardBase[]`
  - 진로 힌트, 지식, 특성/스킬, 관계, 컨디션, 아이템/재화, 도감 항목, 마일스톤 해금을 일반 전략으로 지급한다.
- `CampaignProgress`
  - 현재 캠페인, 현재 일차/단계, 완료한 objective, 선택한 route, 보상 수령 여부, 마지막 진행 로그를 saveSlot + player identity 기준으로 저장한다.
- `CampaignHudPanel`
  - 오늘의 추천 방향, 가능한 대체 경로, 현재 진행도, 다음 안내를 보여준다.
- `StudentDayResultPanel` 연동
  - 하루 결과 UI에 오늘 캠페인 진행, 선택한 경로, 다음 날 안내를 표시한다.

초반 3일 기획:

1일차 — `Town에 적응하기`
- 목적:
  - 이동, 장소 방문, NPC 기본 대화, 하루 종료를 소개한다.
- 추천 경로:
  - 광장 안내 NPC와 대화
  - 학교 또는 도서관 방문
  - 아무 장소 2곳 방문
- 대체 조건:
  - 안내 NPC 대화 1회
  - 장소 방문 2곳
  - 아무 활동 1회
  - 도감 장소 항목 1개 해금
- 결과:
  - 마일스톤 `마을에 적응하기` 진행
  - 장소/NPC 도감 해금
  - 2일차에 학교 밖 성장 안내 또는 장소별 힌트 표시

2일차 — `나만의 성장 방식 찾기`
- 목적:
  - 학교 루트와 학교 밖 루트가 모두 가능함을 보여준다.
- 추천 경로:
  - 학교 수업 1회
  - 도서관 독학 1회
  - NPC 도움 행동 1회
  - 알바/탐험 중 하나
- 대체 조건:
  - 학교 활동 1회 또는 학교 밖 활동 1회
  - 스킬/특성/관계/컨디션/지식 중 하나 이상 변화
  - 마일스톤 진행도 증가
- 결과:
  - 하루 결과 UI에 선택한 성장 경로 표시
  - 도감/마일스톤/HUD에 다음 추천 행동 표시
  - 3일차에 선택 이벤트 또는 진로 힌트 노출

3일차 — `진로의 단서와 선택`
- 목적:
  - 하루 이벤트, 진로 힌트, 마일스톤, 도감이 연결되는 경험을 제공한다.
- 추천 경로:
  - 장소 기반 하루 이벤트 1회 선택
  - 진로 힌트 1개 발견
  - 마일스톤 하나 완료 또는 진행도 기준치 도달
- 대체 조건:
  - 도서관/일터/광장/골목 중 하나에서 이벤트 선택
  - 학교 또는 학교 밖 활동으로 진로 힌트 진행
  - 도감 항목 1개 이상 해금
- 결과:
  - 첫 진로 후보 또는 진로 힌트 표시
  - 다음 장기 목표/마일스톤 활성화
  - 캠페인 완료 또는 다음 캠페인 예고

자유 경로 요구:
- 각 일차는 최소 2개 이상의 완료 경로를 가져야 한다.
- 최소 1개 일차는 학교 수업 없이도 완료 가능해야 한다.
- 최소 1개 일차는 학교 루트와 학교 밖 루트가 같은 목표를 진행할 수 있어야 한다.
- 이벤트를 보류하거나 거절해도 캠페인이 하드락되면 안 된다.
- NPC가 스케줄상 부재해도 안내/도감/게시판/다른 NPC/다른 장소 같은 대체 경로가 있어야 한다.

UI 요구:
- 캠페인 HUD 또는 목표 패널을 실제 1차 UI로 구현한다.
- 문서 목업만으로 완료하지 않는다.
- UI에는 오늘의 방향, 가능한 대체 경로, 현재 진행도, 다음 안내가 표시되어야 한다.
- 하루 결과 UI에는 오늘 선택한 경로, 캠페인 진행 변화, 다음 날 안내가 표시되어야 한다.
- Modern UI Style2 공통 패널 규약을 따른다.
- 1920x1080 기준에서 목표, 경로, 진행도, 버튼 텍스트가 겹치지 않아야 한다.

성능/최적화 요구:
- 캠페인 objective 평가는 활동/대화/도감 해금/마일스톤 변경/하루 종료 같은 이벤트 트리거에서만 수행한다.
- 매 프레임 모든 objective를 재평가하지 않는다.
- campaign/day/route/objective lookup은 Registry 전체 반복 순회 대신 캐시를 사용한다.
- HUD는 dirty 상태일 때만 갱신한다.
- 캠페인/마일스톤/도감/퀘스트 항목 수가 늘어나는 상황을 고려해 UI와 lookup 구조를 설계한다.
- 최종 보고에는 성능 검증 범위와 후속 최적화 과제를 포함한다.

멀티플레이 확장 고려:
- 캠페인 진행은 기본적으로 플레이어별 개인 상태다.
- WorldTimeState, NPC 위치, 장소 활성 상태는 추후 서버 권한 공유 월드 상태로 승격 가능해야 한다.
- 멀티플레이에서는 서버 권한으로 캠페인 진행/보상 수령을 확정할 수 있어야 한다.
- Client 1의 캠페인 HUD/결과 UI가 Client 2에게 강제로 공유되면 안 된다.
- 같은 공유 월드 시간 안에서도 각 플레이어가 다른 경로로 캠페인을 진행할 수 있어야 한다.
- 이번 goal에서 멀티플레이 직접 검증이 범위 밖이면 최종 보고에 후속 검증 항목과 리스크를 명시한다.

필수 저장 대상:
- saveSlot
- player identity
- active campaign id
- current campaign day/stage
- completed objective ids
- selected route ids
- reward claimed ids
- today campaign result summary
- next day guide state

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-12-town-locations-npc-foundation-goal.md`
- `docs/superpowers/goals/2026-05-12-location-identity-gameplay-goal.md`
- `docs/superpowers/goals/2026-05-12-daily-choice-event-loop-goal.md`
- `docs/superpowers/goals/2026-05-12-open-ended-milestone-growth-goal.md`
- `docs/superpowers/goals/2026-05-12-encyclopedia-discovery-ui-goal.md`
- `docs/superpowers/goals/2026-05-12-npc-schedule-life-pattern-goal.md`
- `.claude/rules/game-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/World/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- CAMPAIGN-EDIT-001: CampaignDefinition/CampaignDayDefinition/CampaignRouteDefinition이 Registry 또는 지정 데이터 경로에서 로드된다.
- CAMPAIGN-EDIT-002: 각 캠페인 일차는 2개 이상의 대체 route/objective를 가진다.
- CAMPAIGN-EDIT-003: 최소 1개 일차는 학교 수업 없이 완료 가능하다.
- CAMPAIGN-EDIT-004: objective 평가는 엔티티 ID별 분기 없이 장소/NPC/활동/도감/마일스톤/이벤트/진로 힌트 상태를 평가한다.
- CAMPAIGN-EDIT-005: 캠페인 진행, 완료 objective, 선택 route, 보상 수령 상태가 저장/로드된다.
- CAMPAIGN-EDIT-006: 같은 objective/result/request id를 재적용해도 진행도와 보상이 중복 적용되지 않는다.
- CAMPAIGN-EDIT-007: 캠페인 HUD summary와 하루 결과 summary가 오늘 진행 변화와 다음 안내를 생성한다.
- CAMPAIGN-EDIT-008: campaign/day/route lookup 캐시가 UI 갱신마다 Registry 전체 순회를 요구하지 않는다.

필수 PlayMode E2E 시나리오:
- CAMPAIGN-E2E-001: 저장 슬롯 UI로 새 게임을 시작해 Town에 진입한다.
- CAMPAIGN-E2E-002: 1일차 캠페인 HUD 또는 목표 패널이 오늘 방향과 대체 경로를 표시한다.
- CAMPAIGN-E2E-003: 실제 이동과 상호작용으로 1일차 목표를 완료한다.
- CAMPAIGN-E2E-004: 실제 하루 종료/결과 UI/다음 날 버튼으로 2일차에 진입한다.
- CAMPAIGN-E2E-005: 2일차에는 학교 수업이 아닌 학교 밖 경로로 캠페인 목표를 진행한다.
- CAMPAIGN-E2E-006: 실제 하루 종료/결과 UI/다음 날 버튼으로 3일차에 진입한다.
- CAMPAIGN-E2E-007: 3일차에는 하루 이벤트 또는 진로 힌트/도감/마일스톤 중 하나 이상을 실제 플레이로 진행한다.
- CAMPAIGN-E2E-008: 하루 결과 UI에 오늘 선택한 경로, 캠페인 진행 변화, 다음 안내가 표시된다.
- CAMPAIGN-E2E-009: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 캠페인 일차/진행/보상 상태가 유지된다.
- CAMPAIGN-E2E-010: 같은 하루 결과나 objective를 재확인해도 중복 보상/중복 진행이 발생하지 않는다.
- CAMPAIGN-E2E-011: 기존 학생 하루 루프, 장소/NPC, 장소 정체성, 하루 이벤트, 도감, 마일스톤 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, CampaignHudPanel, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- 캠페인 진행은 도메인 상태만 보지 말고 HUD 텍스트, 하루 결과 UI, 도감/마일스톤/이벤트 UI 중 하나 이상으로 함께 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, CampaignProgress, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "1일차 HUD 미표시", "학교 밖 경로 완료 불가", "3일차 이벤트 미연결", "하루 결과 캠페인 요약 누락", "저장 후 캠페인 복원 실패", "중복 캠페인 보상 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 초반 3일 캠페인이 데이터 기반으로 정의되고 Registry에서 로드된다.
- 각 일차는 2개 이상의 대체 경로를 가진다.
- 최소 1개 일차는 학교 수업 없이 완료 가능하다.
- 실제 플레이 E2E가 1일차→2일차→3일차를 진행한다.
- HUD와 하루 결과 UI가 캠페인 진행, 선택 경로, 다음 안내를 보여준다.
- 저장/로드 후 캠페인 일차/진행/보상 상태가 유지된다.
- 같은 objective/result/request id가 중복 적용되지 않는다.
- 보상/인벤토리 트랜잭션 원칙을 위반하지 않는다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 3일 캠페인 구성, 실행한 테스트, 실제 플레이 검증 흐름, 자유 경로 검증 결과, UI 구현 범위, 성능 검증 범위, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

