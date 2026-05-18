# Integrated Vertical Slice Foundation Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "New Game -> SPUM 캐릭터 확정 -> Town 진입 -> 실제 이동/상호작용으로 하루 생활 진행 -> 단서/퀘스트/진로 힌트 중 최소 1개 획득 -> Objective Journal 또는 관련 UI에서 다음 목표 확인 -> House/생활 보상 후보가 다음 동기로 연결됨 -> 저장/로드 후 진행 상태가 유지되는 통합 세로 슬라이스 기반"을 설계/구현/검증해줘.

이번 goal의 목적:
- 개별 기능을 더 많이 추가하는 것이 아니라, 이미 개발된 StudentLife, Town, Quest, DiscoveryClues, Career, Objective Journal, Save/Load, House/Interiors 기반을 하나의 실제 플레이 흐름으로 연결한다.
- 플레이어가 "왜 다음 날을 진행해야 하는지", "어디로 가야 하는지", "무엇이 성장했는지", "다음 보상이 무엇인지"를 실제 UI와 플레이 흐름에서 확인할 수 있게 한다.
- 통합 세로 슬라이스는 최종 콘텐츠가 아니라 앞으로 모든 기능을 붙일 기준 플레이 경로다.

범위 원칙:
- 우선 1일차 또는 1~2일차 수준의 짧은 세로 슬라이스를 완성한다.
- 새 시스템을 무리하게 많이 만들지 말고 기존 구현을 최대한 연결한다.
- 핵심은 "기능 존재"가 아니라 "실제 유저가 처음부터 플레이해서 흐름을 이해하고 저장/로드 후 이어갈 수 있음"이다.
- House는 완전한 확장/시공 루프를 반드시 끝내지 않아도 된다. 다만 진로/생활 진행의 다음 보상 후보 또는 방문 동기로 연결되어야 한다.
- 멀티플레이 직접 검증은 이번 goal의 필수 완료 조건이 아니지만, 개인 상태와 공유 상태 경계는 유지해야 한다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 shell, echo, cat, apply_patch 등 외부 디스크 직접 쓰기로 수정하지 않는다. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 ScriptableObject 데이터 기반이어야 한다.
- activityId, questId, clueId, careerId, locationId, npcId, houseStageId, routeId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성을 금지한다.
- 보상, 인벤토리, 퀘스트, 진로 힌트, 발견 단서, House 관련 보상 후보는 저장/로드 후 중복 적용되면 안 된다.
- Objective/goal/quest/campaign/hint 성격의 UI는 `ObjectiveJournalPanel`이 소유한다. 상시 HUD는 추적 목표 1개만 표시한다.
- visible gameplay/UI/scene/prefab/tilemap/placement/camera 변경은 실제 PlayMode player-facing flow와 Game View 또는 Camera screenshot 검증 없이는 완료로 보고하지 않는다.
- TDD로 진행한다. 실패 테스트를 먼저 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경을 되돌리지 않는다.

먼저 전수조사할 영역:
- `docs/superpowers/specs/2026-05-08-student-career-life-design.md`
- `docs/superpowers/specs/2026-05-13-objective-journal-ui-design.md`
- `docs/superpowers/specs/2026-05-17-house-upgrade-and-interior-construction-design.md`
- `docs/superpowers/specs/2026-05-17-house-upgrade-next-slice-design.md`
- `docs/superpowers/audits/`
- `production/qa/evidence/`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/DiscoveryClues/`
- `Assets/Scripts/Game/WorldState/`
- `Assets/Scripts/Game/Housing/`
- `Assets/Scripts/Game/Interiors/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Scripts/UI/DiscoveryClues/`
- `Assets/Scripts/UI/Objectives/`
- `Assets/Scripts/UI/Housing/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Data/StudentLife/`
- `Assets/Data/Quests/`
- `Assets/Data/DiscoveryClues/`
- `Assets/Data/Careers/`
- `Assets/Data/Housing/`
- `Assets/Tests/EditMode/`
- `Assets/Tests/PlayMode/EndToEnd/`

권장 플레이 흐름:
1. MainMenu에서 새 게임을 시작한다.
2. SPUM 캐릭터 생성/확정을 실제 UI 버튼으로 완료한다.
3. Town에 진입한다.
4. 플레이어가 실제 입력으로 이동해 첫 상호작용 지점에 접근한다.
5. NPC 대화, 장소 방문, 생활 활동, 탐험, 게시판, 단서 중 하나를 실제 prompt/input/button 경로로 수행한다.
6. 그 결과로 StudentLife 성장, 퀘스트 진행, DiscoveryClue, CareerHint, WorldState, Encyclopedia, Milestone 중 최소 2개 도메인에 변화가 발생한다.
7. Objective Journal 또는 현재 목표 UI에서 다음 행동이 표시된다.
8. 하루 종료 또는 결과 UI를 실제 상호작용으로 확인한다.
9. 다음 목표가 House 방문, House 확장 후보, 인테리어 진로 힌트, 또는 다른 Town 생활 행동 중 하나로 이어진다.
10. 저장 후 MainMenu로 돌아가 같은 슬롯을 로드한다.
11. Town 재진입 후 성장/퀘스트/단서/진로/목표 표시가 유지되는지 확인한다.
12. 같은 결과 또는 보상 요청을 반복해도 중복 보상/중복 진행이 발생하지 않는지 확인한다.

최소 기능 요구:
- 새 게임부터 Town 진입까지 실제 UI 경로가 안정적으로 동작한다.
- 첫날 플레이에서 최소 2개 이상 시스템이 연결된다.
  - 예: 생활 활동 + 진로 힌트
  - 예: NPC 대화 + 퀘스트 진행
  - 예: 단서 발견 + Objective Journal 목표 갱신
  - 예: 장소 방문 + WorldState/Encyclopedia 변화
- Objective Journal 또는 추적 HUD가 "다음에 무엇을 하면 되는지"를 표시한다.
- 하루 결과 UI 또는 결과 요약이 오늘 변화와 다음 동기를 표시한다.
- 저장/로드 후 진행 상태와 UI 표시가 유지된다.
- House/Interior는 최소한 다음 목표 또는 보상 후보로 연결된다.

비목표:
- 전체 3일 캠페인 완성은 필수가 아니다.
- 모든 진로 후보/모든 NPC/모든 장소를 연결하지 않는다.
- House 직접 시공 전체 완성은 필수가 아니다.
- 멀티플레이 Host+Client 직접 검증은 이번 goal에서 필수가 아니다.
- 새 대형 UI 프레임워크를 만들지 않는다.
- 특정 엔티티 ID를 기준으로 코드를 분기하지 않는다.

권장 데이터 모델/연결 방식:
- 기존 `LifeActivityDefinition`, `QuestDefinition`, `DiscoveryClueDefinition`, `CareerCandidateDefinition`, `CareerInterestDefinition`, `WorldStateDefinition`, `CampaignDefinition`, `MilestoneDefinition`, `HouseUpgradeStageDefinition`을 우선 재사용한다.
- 새 데이터가 필요하면 `Assets/Data/{StudentLife|Quests|DiscoveryClues|Careers|WorldState|Housing}` 아래 ScriptableObject로 추가하고 `GameDataRegistry.asset`에 등록한다.
- 통합 슬라이스 전용 "경로"가 필요하면 C# 하드코딩이 아니라 SO 조건/효과/요약 전략 배열로 표현한다.
- UI는 각 도메인 UI를 새로 난립시키지 말고 Objective Journal, 하루 결과 UI, 기존 패널에 연결한다.

필수 저장 데이터 확인:
- saveSlot
- player identity
- StudentLife progress
- QuestLog state
- DiscoveryClue/ClueInterpretation progress
- Career hint/candidate/interest progress 중 해당되는 상태
- WorldState/Encyclopedia/Milestone 중 해당되는 상태
- Objective tracking state 또는 next objective summary
- House 관련 보상 후보/해금/방문 유도 상태가 있다면 해당 상태

필수 EditMode 테스트:
- VERTICAL-EDIT-001: 통합 슬라이스에 필요한 기본 SO 데이터가 Registry에서 로드되고 null 참조가 없다.
- VERTICAL-EDIT-002: 첫날 경로가 특정 엔티티 ID 분기 없이 조건/효과 전략 배열로 2개 이상 도메인 변화를 만든다.
- VERTICAL-EDIT-003: Objective Journal 또는 목표 요약 모델이 현재 진행 상태에서 다음 행동을 생성한다.
- VERTICAL-EDIT-004: 하루 결과 요약이 오늘 변화와 다음 동기를 생성한다.
- VERTICAL-EDIT-005: 저장/로드 후 StudentLife, Quest, Clue/Career/WorldState 중 연결된 상태가 복원된다.
- VERTICAL-EDIT-006: 같은 결과/보상을 반복 적용해도 중복 성장, 중복 보상, 중복 퀘스트 진행이 발생하지 않는다.
- VERTICAL-EDIT-007: House/Interior 연결이 있다면 House 보상 후보 또는 방문 동기가 데이터 기반으로 생성된다.
- VERTICAL-EDIT-008: 통합 슬라이스 런타임 코드가 entity id별 `if/switch` 분기를 포함하지 않는다.

필수 PlayMode E2E:
- VERTICAL-E2E-001: MainMenu -> New Game -> SPUM Confirm -> Town 진입을 실제 UI 버튼 경로로 수행한다.
- VERTICAL-E2E-002: 플레이어가 실제 이동 입력으로 첫 상호작용 지점에 접근한다.
- VERTICAL-E2E-003: 실제 Prompt/Input/Button 경로로 NPC/장소/생활 활동/단서/게시판 중 하나를 수행한다.
- VERTICAL-E2E-004: 수행 결과로 최소 2개 도메인 상태가 변한다.
- VERTICAL-E2E-005: Objective Journal 또는 추적 HUD에 다음 목표가 표시된다.
- VERTICAL-E2E-006: 하루 결과 UI 또는 요약 UI가 오늘 변화와 다음 동기를 표시한다.
- VERTICAL-E2E-007: House/Interior 또는 다음 생활 목표가 후속 동기로 표시된다.
- VERTICAL-E2E-008: 저장 후 MainMenu로 돌아가 같은 슬롯을 로드하면 진행 상태와 UI 표시가 유지된다.
- VERTICAL-E2E-009: 같은 결과/보상/목표 갱신을 반복해도 중복 지급 또는 중복 진행이 발생하지 않는다.
- VERTICAL-E2E-010: Game View 또는 Camera screenshot으로 실제 플레이 화면에서 목표 UI/결과 UI/후속 동기가 보이는지 확인한다.

실제 플레이 검증 체크리스트:
- PASS/FAIL로 최종 보고에 포함한다.
- 새 게임을 실제 UI 클릭으로 시작했는가?
- SPUM 캐릭터 확정을 실제 UI 클릭으로 수행했는가?
- Town 진입 후 플레이어 이동을 Transform 직접 세팅이 아니라 입력 경로로 수행했는가?
- 상호작용을 내부 메서드 직접 호출이 아니라 Prompt/Input/Button 경로로 수행했는가?
- 최소 2개 도메인 상태 변화가 UI와 런타임 상태에서 확인되었는가?
- Objective Journal 또는 추적 HUD가 다음 행동을 표시했는가?
- 하루 결과 또는 요약 UI가 오늘 변화와 다음 동기를 표시했는가?
- House/Interior 또는 다음 생활 목표가 후속 동기로 연결되었는가?
- 저장/로드 후 동일 상태가 복원되었는가?
- 반복 수행으로 중복 보상/중복 진행이 발생하지 않았는가?
- Game View 또는 Camera screenshot을 캡처/검사했는가?
- Unity Console에 관련 Error/Exception이 없는가?

성능/최적화 요구:
- Objective/Journal/summary 갱신은 매 프레임 전체 Registry 스캔으로 처리하지 않는다.
- 상태 변경, UI open, day result, save/load 같은 이벤트 지점에서 dirty 갱신한다.
- 대량 데이터가 늘어날 것을 고려해 lookup cache 또는 index를 사용한다.
- UI 목록은 이후 가상화/페이징이 가능하도록 item model과 view 생성을 분리한다.
- 최종 보고에 성능 검증 범위와 남은 최적화 과제를 포함한다.

완료 조건:
- 통합 세로 슬라이스가 실제 player-facing PlayMode 흐름으로 검증된다.
- 관련 EditMode 테스트와 PlayMode E2E 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- Game View 또는 Camera screenshot 검증 결과가 보고된다.
- 저장/로드 후 진행 상태와 UI 표시가 유지된다.
- 중복 보상/중복 진행이 방지된다.
- Objective Journal/추적 HUD/결과 UI 중 최소 2개 UI 표면에서 다음 행동과 진행 변화가 보인다.
- House/Interior 또는 다음 생활 목표가 후속 동기로 연결된다.
- 최종 보고에는 수정 파일, 생성/수정 SO asset, 실행 테스트, 실제 플레이 검증 체크리스트 PASS/FAIL, screenshot/검증 증거, 성능 리스크, 멀티플레이 확장 리스크를 포함한다.
```

