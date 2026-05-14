# Career Interest Selection Loop Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "플레이어가 진로 후보 중 하나를 관심 진로로 지정하고, 지정한 관심 진로에 따라 추천 활동, 관련 장소/NPC, 하루 결과 UI, 저장/로드 상태가 바뀌며, 이후에도 다른 관심 진로로 변경할 수 있는 진로 관심 고정 루프"를 설계/구현/검증해줘.

핵심 의도:
- 진로 후보는 단순 도감 항목이 아니라 플레이어가 방향을 고르는 실제 의사결정 지점이어야 한다.
- 관심 진로는 최종 직업 확정이 아니다. 플레이어가 당분간 더 깊게 탐색하고 싶은 방향을 고정하는 상태다.
- 관심 진로를 고정하면 관련 장소, NPC, 활동, 마일스톤, 하루 이벤트, 도감/후보 UI가 플레이어에게 다음 행동을 더 명확히 안내해야 한다.
- 관심 진로와 연결된 퀘스트/마일스톤/도감/진로 후보 진행도는 모두 같은 저장 슬롯에 함께 저장되어야 하며, 새로 로드했을 때 중간 진행 상태가 그대로 이어져야 한다.
- 플레이어는 학교 루트만이 아니라 입학, 아르바이트, 마을 도움 행동, 탐험, 관계 이벤트 같은 학교 밖 경로로도 관심 진로를 선택/진행할 수 있어야 한다.
- 관심 진로는 변경 가능해야 하지만, 변경 기록과 UI 안내가 있어야 한다. 첫 구현에서는 하루 1회 변경 제한 또는 변경 확인 UI 중 하나를 구현한다.

전제:
- 진로 후보 심화 루프(`2026-05-13-career-candidate-deepening-loop-goal.md`) 또는 그에 준하는 후보/힌트/진행 데이터가 존재한다고 가정한다.
- 후보 상태가 최소 `Revealed` 이상인 후보만 관심 진로로 지정할 수 있다. 단, 데이터로 예외 조건을 정의할 수 있게 한다.
- 멀티플레이 직접 검증은 이번 goal의 필수 완료 조건은 아니어도, 상태 구조는 player identity 기준 개인 상태로 분리되어야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI에서 시작해 실제 키보드 이동, 실제 NPC/장소/활동 상호작용, 실제 UI 버튼 선택, 진로 후보 UI 열기, 관심 진로 지정, 하루 종료, 결과 UI 확인, 저장/로드 재진입을 모두 거쳐야 한다.
- 최소 1개 이상의 PlayMode E2E는 학교 수업이 아닌 경로(입학, 아르바이트, 마을 도움 행동, 탐험, 관계 이벤트 중 하나)로 후보를 열거나 진행한 뒤 관심 진로를 지정해야 한다.
- 테스트에서 CareerInterestProgress, CareerCandidateProgress, StudentLifeProgress, MilestoneProgress, QuestLog, Inventory, SaveData 상태를 직접 세팅해서 성공 처리하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입, UI 없이 도메인 API 직접 호출로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.
- 최종 보고 전에 "실제 플레이 방식 상호작용 검증 체크리스트"를 반드시 작성하고, 각 항목을 PASS/FAIL로 표시한다.

실제 플레이 방식 상호작용 검증 체크리스트:
- 저장 슬롯 UI에서 새 게임 또는 기존 슬롯을 실제 UI 경로로 시작했는가?
- 플레이어 이동이 Transform 직접 세팅이 아니라 키보드/입력 시스템 경로로 수행됐는가?
- NPC, 장소, 활동, 이벤트, 진로 후보 UI가 실제 Prompt/Input/Button 경로로 열리고 수행됐는가?
- 관심 진로 지정이 도메인 메서드 직접 호출이 아니라 실제 후보 UI 버튼/확인 UI를 통해 발생했는가?
- 관심 진로 지정 후 HUD, 후보 UI, 하루 결과 UI 중 최소 2개 이상에서 선택 결과가 보이는가?
- 하루 종료가 내부 `EndDay()` 직접 호출이 아니라 실제 하루 종료 지점/상호작용으로 실행됐는가?
- 저장 슬롯 UI 재진입 후 같은 슬롯을 로드했을 때 관심 진로 상태가 유지되는가?
- 저장 슬롯 UI 재진입 후 같은 슬롯을 로드했을 때 관심 진로와 연결된 퀘스트 진행도, 마일스톤 진행도, 도감 발견 상태, 하루 이벤트 진행 상태가 함께 유지되는가?
- 퀘스트를 일부만 진행한 상태로 저장/로드했을 때 수락 상태, 목표 진행 수치, 완료 가능 상태, 보상 수령 여부가 정확히 복원되는가?
- 관심 진로 변경 제한 또는 변경 확인 UI가 실제 플레이 경로에서 검증됐는가?
- 같은 관심 진로 지정/변경 요청을 반복해도 중복 보상, 중복 로그, 중복 UI 이벤트가 발생하지 않는가?
- 체크리스트 항목 중 하나라도 FAIL이면 완료로 보고하지 않고 실패 단계와 남은 작업을 보고했는가?

헌법 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`를 외부 디스크 쓰기로 직접 수정하지 않는다. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- careerId, candidateId, interestId, routeId, activityId, locationId, npcId, milestoneId별 `if/switch` 분기, enum 기반 엔티티 분기, 관심 진로별 C# 클래스 생성은 금지한다.
- 관심 진로 선택 조건, 변경 조건, 추천 활동, UI 표시 규칙, 보상, 변경 제한은 SO 데이터와 전략 배열로 표현한다.
- 보상/인벤토리/마일스톤/도감/진로 힌트는 저장/로드 후 중복 적용되면 안 된다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 성능/최적화 동시 설계 원칙을 따른다.
- 기존 dirty 변경을 되돌리지 않는다.

권장 데이터 구조:
- `CareerInterestDefinition`
  - 관심 진로로 지정 가능한 후보, 표시명, 설명, 관심 진로 태그, 관련 장소/NPC/활동, 추천 마일스톤, 관련 도감 항목, UI 우선순위, 변경 제한 정책을 정의한다.
- `CareerInterestUnlockRequirementBase[]`
  - 후보 상태, 힌트 수, 이해도, 특성/기술, 관계, 장소 발견, 이벤트 결과, 마일스톤 상태를 일반 조건으로 평가한다.
- `CareerInterestSelectionRuleBase[]`
  - 관심 진로 지정 가능 여부, 하루 변경 제한, 변경 확인 필요 여부, 변경 비용, 변경 기록 정책을 데이터 전략으로 평가한다.
- `CareerInterestRecommendationBase[]`
  - 현재 관심 진로 기준으로 추천 장소, NPC, 활동, 다음 행동, 오늘의 목표 후보를 생성한다.
- `CareerInterestRewardBase[]`
  - 관심 진로 최초 지정, 특정 단계 도달, 하루 결과 반영에 따른 도감/마일스톤/힌트/소량 성장 보상을 일반 전략으로 지급한다.
- `CareerInterestProgress`
  - saveSlot + player identity 기준으로 현재 관심 진로, 이전 관심 진로 목록, 마지막 변경 날짜, 변경 횟수, 지정/변경 로그, 보상 수령 여부를 저장한다.
- `CareerInterestPanel`
  - 후보 UI 안에서 관심 진로 지정/변경/해제 상태를 보여준다.
- `CareerInterestHudWidget`
  - 현재 관심 진로와 추천 다음 행동 1~3개를 HUD 또는 기존 목표 UI에 표시한다.
- `StudentDayResultPanel` 연동
  - 오늘 관심 진로와 관련된 행동, 진행 변화, 추천 다음 행동을 결과 UI에 표시한다.
- `QuestLog`/`QuestProgress` 연동
  - 관심 진로 지정 또는 추천 활동 수행으로 발생한 퀘스트 수락 상태, 목표 진행도, 완료 가능 상태, 보상 수령 여부를 저장 슬롯에 함께 저장한다.
  - 재로드 후 퀘스트 UI와 실제 진행 가능 상태가 동일하게 복원되어야 한다.

관심 진로 상태:
- `None`
  - 아직 관심 진로가 없다. 후보 UI는 선택 가능한 후보와 조건 미충족 후보를 구분해서 보여준다.
- `Selected`
  - 현재 관심 진로가 있다. HUD/후보 UI/결과 UI에서 관련 추천이 표시된다.
- `ChangePending`
  - 변경 확인 UI가 열린 상태다. 확인 전에는 실제 상태가 바뀌지 않는다.
- `ChangedToday`
  - 오늘 이미 변경했다. 하루 1회 제한 정책을 쓰는 경우 추가 변경 버튼은 비활성화되고 이유가 표시된다.
- `LockedByCondition`
  - 후보는 보이지만 현재 조건상 관심 진로로 지정할 수 없다. 부족 조건을 UI로 보여준다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯 로드 후 Town에 진입한다.
2. 플레이어가 실제 이동으로 학교, 도서관, 공방, 상점, 광장, 골목 중 하나에 접근한다.
3. 실제 Prompt/Input/Button 경로로 활동, NPC 대화, 이벤트, 탐험 중 하나를 수행한다.
4. 수행 결과로 진로 후보가 `Revealed` 이상 상태가 된다.
5. 플레이어가 실제 입력 또는 UI 버튼으로 진로 후보 UI를 연다.
6. 후보 목록에서 관심 진로로 지정 가능한 후보와 지정 불가 후보가 구분되어 보인다.
7. 플레이어가 후보 상세에서 "관심 진로로 지정" 버튼을 누른다.
8. 필요하면 확인 UI를 거쳐 관심 진로 지정이 완료된다.
9. HUD 또는 목표 UI에 현재 관심 진로와 추천 다음 행동이 표시된다.
10. 플레이어가 관심 진로와 관련된 장소/NPC/활동 중 하나를 실제 이동과 상호작용으로 수행한다.
11. 하루 종료 지점에 실제 이동해서 하루를 종료한다.
12. 하루 결과 UI에 관심 진로 관련 행동, 진행 변화, 다음 추천 행동이 표시된다.
13. 관심 진로와 연결된 퀘스트가 있다면 퀘스트 UI에서 수락 상태, 목표 진행도, 완료 가능 상태를 확인한다.
14. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 현재 관심 진로, 변경 기록, 추천 상태, 퀘스트 진행도, 마일스톤 진행도, 도감 발견 상태가 유지된다.
15. 재로드 후 퀘스트 UI를 다시 열어 수락 상태, 목표 진행도, 완료 가능 상태, 보상 수령 여부가 저장 전과 일치하는지 확인한다.
16. 같은 관심 진로를 다시 지정하거나 같은 하루 결과를 재확인해도 중복 보상/중복 로그/퀘스트 중복 진행이 발생하지 않는다.
17. 다른 후보가 조건을 만족하면 관심 진로 변경 UI를 통해 변경 가능 여부와 제한 사유를 확인한다.

학교 밖 경로 검증 요구:
- 최소 하나의 E2E는 학교 수업 없이 학교 밖 경로로 후보를 열거나 진행한 뒤 관심 진로를 지정해야 한다.
- 학교 밖 경로 예시는 다음 중 하나 이상이어야 한다.
  - 도서관 입학/자습으로 학습형 후보를 열고 관심 진로 지정
  - 공방/공장 아르바이트로 기술·작업형 후보를 열고 관심 진로 지정
  - 상점/광장 NPC 도움 행동으로 관계·서비스형 후보를 열고 관심 진로 지정
  - 골목/마을 외곽 탐험으로 탐험·정보형 후보를 열고 관심 진로 지정
- 학교 수업 루트만으로 모든 관심 진로 검증을 끝내면 실패다.

UI 요구:
- 후보 UI 안에 현재 관심 진로 배지, 지정 버튼, 변경 버튼, 지정 불가 사유, 변경 제한 사유가 보여야 한다.
- 관심 진로가 없을 때와 있을 때의 빈 상태/선택 상태가 명확해야 한다.
- 관심 진로 지정 후 HUD 또는 목표 UI에 현재 관심 진로와 추천 다음 행동이 표시되어야 한다.
- 하루 결과 UI에는 오늘 관심 진로와 관련된 행동, 진행 변화, 추천 다음 행동이 표시되어야 한다.
- Modern UI Style2 공통 패널 규약을 따른다.
- 1920x1080 기준에서 후보명, 조건, 추천 행동, 버튼 텍스트가 겹치거나 잘리지 않아야 한다.
- UI는 문서 목업만 만들고 끝내면 안 된다. PlayMode에서 실제로 열리고 클릭 가능해야 한다.

성능/최적화 요구:
- 후보/관심 진로/recommendation lookup은 매 UI 갱신마다 Registry 전체 순회하지 않고 캐시 또는 인덱스를 사용한다.
- 추천 행동 계산은 매 프레임이 아니라 관심 진로 변경, 하루 결과, 장소 발견, 후보 진행, 마일스톤 변경 같은 이벤트 시점에만 수행한다.
- HUD/후보 UI는 dirty 상태일 때만 갱신한다.
- 후보 수가 늘어날 것을 고려해 UI 목록은 페이지/가상화/재사용 가능한 항목 구조를 우선한다.
- 같은 버튼 클릭으로 중복 요청이 들어와도 요청 id 또는 상태 체크로 멱등 처리한다.
- 최종 보고에는 성능 검증 범위와 남은 최적화 과제를 포함한다.

멀티플레이 확장 고려:
- 관심 진로 진행은 플레이어별 개인 상태로 저장한다.
- CareerInterestDefinition, 추천 규칙, 조건 데이터는 공유 데이터다.
- Client 1의 관심 진로 UI/상태가 Client 2에게 노출되면 안 된다.
- 공유 월드 시간, NPC 위치, 장소 상태는 개인 관심 진로와 분리한다.
- 멀티플레이에서 직접 검증하지 못하면 최종 보고에 미검증 범위와 추가 NET 테스트 후보를 명시한다.

필수 저장 데이터:
- saveSlot
- player identity
- current career interest id
- previous career interest ids
- last changed day
- change count for current day
- selection/change history log
- interest reward claimed flags
- last recommendation source/version
- today interest-related action summary
- accepted quest ids
- quest objective progress values
- quest completion-ready flags
- quest reward claimed flags
- milestone progress values
- encyclopedia discovered entry ids
- daily event state related to career interest

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-13-career-candidate-deepening-loop-goal.md`
- `docs/superpowers/goals/2026-05-12-open-ended-milestone-growth-goal.md`
- `docs/superpowers/goals/2026-05-12-encyclopedia-discovery-ui-goal.md`
- `docs/superpowers/goals/2026-05-12-daily-choice-event-loop-goal.md`
- `docs/superpowers/goals/2026-05-12-location-identity-gameplay-goal.md`
- `.claude/rules/game-design.md`
- `.claude/rules/testing-discipline.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Modern/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Careers/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- CAREER-INTEREST-EDIT-001: CareerInterestDefinition과 조건/추천/보상 전략이 Registry 또는 지정 데이터 경로에서 로드된다.
- CAREER-INTEREST-EDIT-002: `Revealed` 이상 후보만 기본적으로 관심 진로 지정 가능하다.
- CAREER-INTEREST-EDIT-003: 지정 조건은 후보/특성/기술/관계/장소/이벤트/마일스톤 상태를 ID 분기 없이 전략 배열로 평가한다.
- CAREER-INTEREST-EDIT-004: 관심 진로 최초 지정 시 현재 관심 진로, 변경 기록, 보상 수령 여부가 저장된다.
- CAREER-INTEREST-EDIT-005: 같은 관심 진로를 반복 지정해도 중복 보상/중복 로그가 발생하지 않는다.
- CAREER-INTEREST-EDIT-006: 하루 1회 변경 제한 또는 변경 확인 정책이 데이터 기반으로 동작한다.
- CAREER-INTEREST-EDIT-007: 저장/로드 후 현재 관심 진로, 변경 기록, 보상 수령 여부, 추천 상태가 복원된다.
- CAREER-INTEREST-EDIT-008: 관심 진로 기준 추천 장소/NPC/활동이 생성되고, 조건 미충족 추천은 제외되거나 잠금 사유와 함께 표시된다.
- CAREER-INTEREST-EDIT-009: 관심 진로 UI summary가 선택 가능/선택됨/변경 제한/조건 미충족 상태를 생성한다.
- CAREER-INTEREST-EDIT-010: 추천 계산과 UI summary 생성이 Registry 전체 순회를 매 호출 요구하지 않는 구조인지 검증한다.
- CAREER-INTEREST-EDIT-011: 관심 진로와 연결된 퀘스트 수락 상태, 목표 진행 수치, 완료 가능 상태, 보상 수령 여부가 저장/로드 후 복원된다.
- CAREER-INTEREST-EDIT-012: 퀘스트를 일부만 진행한 상태로 저장/로드해도 목표 진행 수치가 초기화되거나 중복 증가하지 않는다.
- CAREER-INTEREST-EDIT-013: 퀘스트 완료 후 보상 수령 상태가 저장/로드 후 유지되며, 재호출해도 보상이 중복 지급되지 않는다.

필수 PlayMode E2E 시나리오:
- CAREER-INTEREST-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- CAREER-INTEREST-E2E-002: 플레이어가 실제 이동으로 학교 밖 활동 또는 장소에 접근한다.
- CAREER-INTEREST-E2E-003: 실제 Prompt/Input/Button 경로로 활동/이벤트/탐험/NPC 대화를 수행한다.
- CAREER-INTEREST-E2E-004: 학교 밖 경로 결과로 관심 진로 지정 가능한 후보가 UI에 표시된다.
- CAREER-INTEREST-E2E-005: 실제 입력 또는 UI 버튼으로 진로 후보 UI를 연다.
- CAREER-INTEREST-E2E-006: 후보 상세에서 실제 버튼으로 관심 진로를 지정한다.
- CAREER-INTEREST-E2E-007: 지정 후 HUD 또는 목표 UI에 현재 관심 진로와 추천 다음 행동이 표시된다.
- CAREER-INTEREST-E2E-008: 관심 진로와 관련된 추천 활동 하나를 실제 이동/상호작용으로 수행한다.
- CAREER-INTEREST-E2E-009: 하루 종료 결과 UI에 관심 진로 관련 행동과 진행 변화가 표시된다.
- CAREER-INTEREST-E2E-010: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 관심 진로 상태가 유지된다.
- CAREER-INTEREST-E2E-011: 재로드 후 퀘스트 UI를 실제 버튼으로 열어 관심 진로 관련 퀘스트의 수락 상태, 목표 진행도, 완료 가능 상태가 유지되는지 확인한다.
- CAREER-INTEREST-E2E-012: 퀘스트를 일부만 진행한 상태로 저장/로드한 뒤 이어서 플레이해도 목표 진행도가 저장 전 값에서 이어진다.
- CAREER-INTEREST-E2E-013: 퀘스트 완료 및 보상 수령 후 저장/로드해도 보상 수령 상태가 유지되고, 재수령/중복 지급이 발생하지 않는다.
- CAREER-INTEREST-E2E-014: 같은 관심 진로 지정 또는 같은 하루 결과 재확인으로 중복 보상/중복 로그/퀘스트 중복 진행이 발생하지 않는다.
- CAREER-INTEREST-E2E-015: 변경 가능 후보가 있을 때 실제 UI 경로로 변경 확인 또는 변경 제한 사유를 확인한다.
- CAREER-INTEREST-E2E-016: 기존 학생 하루 루프, 진로 후보 심화, 마일스톤, 도감, 하루 이벤트 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, CareerCandidatePanel, CareerInterestPanel, CareerInterestHudWidget, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- 관심 진로 지정은 도메인 상태만 보지 말고 후보 UI 버튼 상태, HUD/목표 UI 텍스트, 하루 결과 UI 텍스트 중 최소 2개 이상으로 함께 확인한다.
- 퀘스트 저장/로드 검증은 도메인 상태만 보지 말고 퀘스트 UI의 수락 상태, 목표 진행 텍스트, 완료 가능 버튼, 보상 수령 버튼 상태 중 최소 2개 이상으로 함께 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, CareerInterestProgress, CareerCandidateProgress, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "관심 진로 버튼 미노출", "학교 밖 후보 미진행", "HUD 추천 미표시", "결과 UI 관심 진로 요약 누락", "저장 후 관심 진로 복원 실패", "저장 후 퀘스트 진행도 복원 실패", "퀘스트 보상 중복 수령 발생", "실제 플레이 상호작용 우회 발생"처럼 어느 단계가 깨졌는지 알 수 있게 작성한다.

완료 조건:
- 관심 진로 정의/조건/추천/보상/변경 정책이 데이터 기반으로 정의되고 Registry에서 로드된다.
- 최소 하나의 관심 진로를 학교 수업 없이 학교 밖 경로로 지정할 수 있다.
- 플레이어가 실제 UI로 관심 진로를 지정하고, HUD 또는 목표 UI에 추천 행동을 확인할 수 있다.
- 관심 진로 관련 활동 수행 후 하루 결과 UI에 관련 요약과 다음 추천 행동이 표시된다.
- 저장/로드 후 관심 진로와 변경 기록이 유지된다.
- 저장/로드 후 관심 진로와 연결된 퀘스트 수락 상태, 목표 진행도, 완료 가능 상태, 보상 수령 여부가 유지된다.
- 같은 지정/변경/하루 결과를 반복해도 중복 보상/중복 로그가 발생하지 않는다.
- 같은 퀘스트 진행/완료/보상 수령을 저장/로드 전후에 반복해도 목표 진행 중복 증가나 보상 중복 지급이 발생하지 않는다.
- 관심 진로 변경 제한 또는 변경 확인 UI가 실제 플레이 경로로 검증된다.
- 최종 보고에 실제 플레이 방식 상호작용 검증 체크리스트 PASS/FAIL이 포함된다.
- 체크리스트 항목 중 FAIL이 있으면 완료로 보고하지 않는다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정 SO 에셋, 실행한 테스트, 실제 플레이 검증 흐름, 학교 밖 경로 검증 결과, UI 구현 범위, 성능 검증 범위, 멀티플레이 확장 리스크를 포함한다.
```
