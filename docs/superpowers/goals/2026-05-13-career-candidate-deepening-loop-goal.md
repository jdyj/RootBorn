# Career Candidate Deepening Loop Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "플레이어가 학교, 독학, 알바, 도움 행동, 탐험, 관계 이벤트 등 여러 경로로 진로 힌트를 모으면 학습형/기술·작업형/관계·서비스형/탐험·정보형 같은 진로 후보가 점점 해금되고, 각 후보의 관련 장소·활동·필요 성향·스킬·관계·마일스톤을 UI에서 확인하며, 실제 플레이와 저장/로드 후에도 후보 진행 상태가 유지되는 진로 후보 심화 루프"를 설계/구현/검증해줘.

핵심 원칙:
- 진로 후보는 확정 직업이 아니라 플레이어가 성장 방향을 탐색하는 후보군이다.
- 학교 수업은 진로 힌트를 얻는 경로 중 하나일 뿐 유일한 정답이 아니다.
- 플레이어는 여러 진로 후보를 동시에 열어둘 수 있어야 한다.
- 미해금 후보도 완전히 숨기기보다 `???`, 희미한 설명, 일부 힌트로 탐색 욕구를 줘야 한다.
- 진로 후보 진행은 도감, 마일스톤, 장소 활동, 하루 이벤트, 관계/컨디션 루프와 연결되어야 한다.

전제:
- 자유 경로 성장 기반, 장소/NPC 기초, 장소별 정체성, 학교 밖 성장 루트, 마일스톤, 도감, 하루 이벤트, 초반 3일 캠페인 중 최소 관련 기반이 존재하거나 이 goal에서 샘플 루트로 연결 가능해야 한다.
- 멀티플레이 직접 검증은 이번 goal의 필수 완료 조건은 아니지만, 진로 후보 진행은 플레이어별 개인 상태로 분리될 수 있어야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI에서 시작해 실제 키보드 이동, 실제 NPC/장소/활동 상호작용, 실제 UI 버튼 선택, 하루 종료, 결과 UI, 진로 후보 UI 확인, 저장/로드 재진입을 모두 거쳐야 한다.
- 최소 1개 이상의 PlayMode E2E는 학교 수업이 아닌 경로(독학, 알바, 도움 행동, 탐험, 관계 이벤트 중 하나)로 진로 힌트 또는 후보 진행도를 올려야 한다.
- 가능하면 같은 진로 후보 또는 같은 후보군을 학교 루트와 학교 밖 루트 두 방식으로 진행할 수 있음을 검증한다.
- 테스트에서 CareerCandidateProgress, CareerHintProgress, StudentLifeProgress, MilestoneProgress, EncyclopediaProgress, QuestLog, Inventory 상태를 직접 세팅해서 성공 처리하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.
- 최종 보고 전에 "실제 플레이 방식 상호작용 검증 체크리스트"를 반드시 작성하고, 각 항목을 PASS/FAIL로 표시한다.

실제 플레이 방식 상호작용 검증 체크리스트:
- 저장 슬롯 UI에서 새 게임 또는 기존 슬롯을 실제 UI 경로로 시작했는가?
- 플레이어 이동을 Transform 직접 세팅이 아니라 키보드/입력 시스템 경로로 수행했는가?
- NPC, 장소, 활동, 이벤트, 도감/진로 UI는 실제 Prompt/Input/Button 경로로 열거나 수행했는가?
- 진로 힌트 획득은 내부 API 직접 호출이 아니라 실제 활동/이벤트/퀘스트/탐험 결과로 발생했는가?
- 하루 종료는 내부 `EndDay()` 직접 호출이 아니라 실제 하루 종료 지점 상호작용으로 실행했는가?
- 결과 UI와 진로 후보 UI의 화면 텍스트/버튼/해금 상태를 실제 PlayMode에서 확인했는가?
- 저장 슬롯 UI 재진입 후 진로 후보 진행 상태가 유지되는지 확인했는가?
- 같은 힌트/결과를 재확인해도 중복 진행/중복 보상이 발생하지 않는지 확인했는가?
- 위 항목 중 하나라도 FAIL이면 완료로 보고하지 말고 실패 단계와 남은 작업을 보고했는가?

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- careerId, candidateId, hintId, routeId, activityId, locationId, npcId, milestoneId별 `if/switch` 분기, enum 기반 엔티티 분기, 후보별 C# 클래스 생성은 금지한다.
- 진로 후보, 힌트, 요구 조건, 진행도 계산, 추천 행동, UI 표시 규칙, 보상은 SO 데이터와 전략 배열로 표현한다.
- 보상/인벤토리/마일스톤/도감/진로 힌트는 저장/로드 후 중복 적용되면 안 된다.
- 성능·최적화 동시 설계 원칙을 따른다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

초기 진로 후보:

1. `학습형 루트`
   - 관련 장소: 학교, 도서관, 집
   - 주요 성장: 기초 학습 스킬, 성실함, 집중
   - 대표 행동: 수업, 독학, 어려운 책 읽기, 교사/사서 대화
   - 해금 힌트: 학습형 진로 후보, 연구/교육/기획 계열

2. `기술·작업형 루트`
   - 관련 장소: 작업장, 공장, 상점 창고
   - 주요 성장: 책임감, 체력, 작업 스킬, 돈
   - 대표 행동: 알바, 작업 돕기, 잔업 선택, 도구/자원 관련 퀘스트
   - 해금 힌트: 기술직, 생산직, 장인, 엔지니어 계열

3. `관계·서비스형 루트`
   - 관련 장소: 광장, 상점, NPC 주변
   - 주요 성장: 사교성, 관계, 평판
   - 대표 행동: NPC 도움 행동, 친구 부탁, 상점 주인 대화, 주민 의뢰
   - 해금 힌트: 서비스, 상담, 영업, 커뮤니티 리더 계열

4. `탐험·정보형 루트`
   - 관련 장소: 골목, 외곽, 도서관, 게시판
   - 주요 성장: 호기심, 지식, 관찰력, 진로 힌트
   - 대표 행동: 탐험 발견, 수상한 단서 조사, 정보 수집 이벤트
   - 해금 힌트: 조사, 기자, 연구, 탐험/정보 계열

권장 데이터 구조:
- `CareerCandidateDefinition`
  - 후보 표시명, 설명, 해금 전 표시명/힌트, 관련 장소, 관련 활동, 관련 NPC, 관련 마일스톤, 요구 조건 배열, 추천 행동 배열, 후보 보상 배열을 정의한다.
- `CareerHintDefinition`
  - 어떤 행동/이벤트/퀘스트/도감 항목이 어떤 후보에 힌트를 주는지 정의한다.
- `CareerCandidateRouteDefinition`
  - 학교, 독학, 알바, 도움 행동, 탐험, 관계 이벤트 같은 진행 경로를 SO로 정의한다. C# enum을 쓰지 않는다.
- `CareerCandidateRequirementBase[]`
  - 스킬, 특성, 관계, 컨디션, 마일스톤, 도감, 활동 로그, 퀘스트 상태, 이벤트 선택 결과를 일반 조건으로 평가한다.
- `CareerHintSourceBase[]`
  - 활동 결과, 하루 이벤트, 장소 발견, NPC 대화, 퀘스트 완료, 도감 해금, 마일스톤 진행을 일반 힌트 소스로 평가한다.
- `CareerCandidateRewardBase[]`
  - 지식, 도감 항목, 마일스톤 해금, 진로 관련 대사/이벤트 노출, 소량 특성/스킬 보상을 일반 전략으로 지급한다.
- `CareerCandidateProgress`
  - 후보별 해금 여부, 힌트 수, 이해도, 적합도, 마지막 힌트 로그, 보상 수령 여부를 saveSlot + player identity 기준으로 저장한다.
- `CareerCandidatePanel`
  - 후보 목록, 미해금/부분 해금/해금 상태, 힌트 수, 관련 장소/활동/필요 조건, 추천 다음 행동을 표시한다.
- `StudentDayResultPanel` 연동
  - 오늘 얻은 진로 힌트, 후보 해금/진행도 변화, 다음 추천 장소/활동을 표시한다.
- `Encyclopedia` 연동
  - 진로 힌트 도감 카테고리와 후보 상세 정보를 연결한다.

진로 후보 상태:
- `Locked`
  - 후보 이름은 `???`로 표시한다.
  - 일부 방향성 힌트만 표시할 수 있다.
- `Hinted`
  - 후보 이름 또는 일부 설명이 공개된다.
  - 힌트 수와 관련 장소 일부가 보인다.
- `Revealed`
  - 후보 설명, 관련 장소/활동, 필요 성향/스킬/관계가 보인다.
- `Deepening`
  - 후보별 마일스톤/추천 행동이 열리고 이해도/적합도가 오른다.
- `ReadyForChoice`
  - 이번 goal에서는 최종 직업 확정까지 가지 않아도 된다. 후속 진로 선택 goal로 넘긴다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 플레이어가 실제 이동으로 학교, 도서관, 작업장, 광장, 골목 중 하나에 접근한다.
3. 실제 상호작용 입력으로 활동, NPC 대화, 하루 이벤트, 탐험, 퀘스트 중 하나를 수행한다.
4. 수행 결과로 진로 힌트가 발생한다.
5. HUD/토스트/하루 결과 UI 중 하나에 진로 힌트 획득 피드백이 표시된다.
6. 플레이어가 실제 입력 또는 UI 버튼으로 진로 후보 UI를 연다.
7. 후보 목록에서 미해금 `???`, 부분 해금, 해금 후보 상태를 확인한다.
8. 후보 상세에서 관련 장소, 관련 활동, 필요한 성향/스킬/관계, 추천 다음 행동을 확인한다.
9. 플레이어가 실제 이동으로 하루 종료 지점에 접근해 하루를 종료한다.
10. 하루 결과 UI에 오늘 얻은 진로 힌트와 후보 진행 변화가 표시된다.
11. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 후보 해금/힌트/진행 상태가 유지된다.
12. 같은 힌트 소스나 같은 하루 결과를 재확인해도 후보 진행도/보상이 중복 적용되지 않는다.

자유 경로 검증 요구:
- 최소 하나의 진로 후보는 학교 수업 없이 학교 밖 경로로 힌트가 올라야 한다.
- 최소 하나의 진로 후보는 2개 이상의 경로로 힌트를 얻을 수 있어야 한다.
  - 예: `학습형 루트`는 학교 수업과 도서관 독학 양쪽에서 힌트 획득 가능.
  - 예: `기술·작업형 루트`는 알바와 작업장 도움 행동 양쪽에서 힌트 획득 가능.
  - 예: `관계·서비스형 루트`는 NPC 도움 행동과 상점/광장 이벤트 양쪽에서 힌트 획득 가능.
  - 예: `탐험·정보형 루트`는 골목 탐험과 도서관 정보 조사 양쪽에서 힌트 획득 가능.
- 학교 루트만으로 모든 후보가 열리는 구조를 만들지 않는다.
- 학교 밖 루트가 보조 보상만 주고 후보 진행에 참여하지 않는 구조는 실패다.

UI 요구:
- 진로 후보 UI는 실제 1차 UI로 구현한다. 문서 목업만으로 완료하지 않는다.
- 후보 목록, 후보 상세, 미해금 `???`, 힌트 수/진행도, 관련 장소/활동, 추천 다음 행동을 표시한다.
- 하루 결과 UI에는 오늘 얻은 진로 힌트와 후보 상태 변화가 표시되어야 한다.
- 도감 시스템이 있다면 진로 힌트 도감 카테고리와 연결한다.
- Modern UI Style2 공통 패널 규약을 따른다.
- 1920x1080 기준에서 후보명, 설명, 조건, 추천 행동, 버튼 텍스트가 겹치지 않아야 한다.

성능/최적화 요구:
- 후보/힌트/route lookup은 Registry 전체 반복 순회 대신 캐시를 사용한다.
- 진로 후보 진행도 평가는 활동 완료, 이벤트 선택, 도감 해금, 마일스톤 진행, 하루 종료 같은 트리거에서만 수행한다.
- 매 프레임 모든 후보 조건을 재평가하지 않는다.
- 후보 UI는 dirty 상태일 때만 갱신한다.
- 후보 수와 힌트 수가 늘어나는 상황을 고려해 UI 목록은 재사용/페이징/캐싱 구조를 우선한다.
- 최종 보고에는 성능 검증 범위와 후속 최적화 과제를 포함한다.

멀티플레이 확장 고려:
- 진로 후보 진행은 플레이어별 개인 상태다.
- 진로 후보 정의, 힌트 정의, 관련 장소/활동 데이터는 공유 데이터다.
- 멀티플레이에서는 서버 권한으로 힌트 획득/보상 수령을 확정할 수 있어야 한다.
- Client 1의 진로 후보 UI/진행 상태가 Client 2에게 섞이면 안 된다.
- 공유 월드 시간/NPC 위치/장소 상태와 개인 진로 후보 진행 상태를 분리한다.
- 이번 goal에서 멀티플레이 직접 검증이 범위 밖이면 최종 보고에 후속 검증 항목과 리스크를 명시한다.

필수 저장 대상:
- saveSlot
- player identity
- 후보별 state
- 후보별 hint count
- 후보별 이해도/적합도
- 획득한 hint id 목록
- 마지막 hint source id
- 후보별 보상 수령 여부
- 오늘 진로 힌트 결과 요약

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-12-open-ended-milestone-growth-goal.md`
- `docs/superpowers/goals/2026-05-12-encyclopedia-discovery-ui-goal.md`
- `docs/superpowers/goals/2026-05-12-location-identity-gameplay-goal.md`
- `docs/superpowers/goals/2026-05-12-daily-choice-event-loop-goal.md`
- `docs/superpowers/goals/2026-05-12-first-three-days-campaign-goal.md`
- `.claude/rules/game-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Modern/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Careers/`
- `Assets/Data/Knowledge/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- CAREER-CANDIDATE-EDIT-001: CareerCandidateDefinition/CareerHintDefinition/route 정의가 Registry 또는 지정 데이터 경로에서 로드된다.
- CAREER-CANDIDATE-EDIT-002: 최소 4개 후보(학습형, 기술·작업형, 관계·서비스형, 탐험·정보형)가 데이터로 존재한다.
- CAREER-CANDIDATE-EDIT-003: 후보 요구 조건과 힌트 소스는 엔티티 ID별 분기 없이 전략 배열로 평가된다.
- CAREER-CANDIDATE-EDIT-004: 최소 하나의 후보는 2개 이상의 route로 힌트 획득 가능하다.
- CAREER-CANDIDATE-EDIT-005: 학교 밖 활동 로그 또는 이벤트 결과만으로도 최소 하나의 후보 힌트가 증가한다.
- CAREER-CANDIDATE-EDIT-006: 후보 진행 상태, 힌트 목록, 보상 수령 여부가 saveSlot + player identity 기준으로 저장/로드된다.
- CAREER-CANDIDATE-EDIT-007: 같은 hint source/request id를 재적용해도 진행도/보상이 중복 적용되지 않는다.
- CAREER-CANDIDATE-EDIT-008: 후보 UI summary가 미해금/부분 해금/해금 상태와 추천 다음 행동을 생성한다.
- CAREER-CANDIDATE-EDIT-009: 후보/힌트 lookup 캐시가 UI 갱신마다 Registry 전체 순회를 요구하지 않는다.

필수 PlayMode E2E 시나리오:
- CAREER-CANDIDATE-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- CAREER-CANDIDATE-E2E-002: 플레이어가 실제 이동으로 학교 밖 활동 또는 장소에 접근한다.
- CAREER-CANDIDATE-E2E-003: 실제 Prompt/Input/Button 경로로 활동/이벤트/탐험/대화를 수행한다.
- CAREER-CANDIDATE-E2E-004: 학교 밖 경로 결과로 진로 힌트가 UI와 도메인 상태에 반영된다.
- CAREER-CANDIDATE-E2E-005: 실제 입력 또는 UI 버튼으로 진로 후보 UI를 연다.
- CAREER-CANDIDATE-E2E-006: 후보 UI에서 미해금 `???`, 부분 해금, 해금 후보 상태를 확인한다.
- CAREER-CANDIDATE-E2E-007: 후보 상세에서 관련 장소/활동/필요 조건/추천 다음 행동을 확인한다.
- CAREER-CANDIDATE-E2E-008: 하루 종료 결과 UI에 오늘 얻은 진로 힌트와 후보 진행 변화가 표시된다.
- CAREER-CANDIDATE-E2E-009: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 후보 진행 상태가 유지된다.
- CAREER-CANDIDATE-E2E-010: 같은 힌트 소스나 하루 결과를 재확인해도 중복 진행/보상이 발생하지 않는다.
- CAREER-CANDIDATE-E2E-011: 가능하면 학교 루트와 학교 밖 루트가 같은 후보 또는 다른 후보를 각각 진행하는지 확인한다.
- CAREER-CANDIDATE-E2E-012: 기존 학생 하루 루프, 학교 밖 성장, 도감, 마일스톤, 하루 이벤트 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, CareerCandidatePanel, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- 진로 후보 진행은 도메인 상태만 보지 말고 후보 UI 텍스트, 하루 결과 UI, 도감 UI, HUD/토스트 중 하나 이상으로 함께 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, CareerCandidateProgress, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "학교 밖 힌트 미반영", "진로 후보 UI 미표시", "후보 상태 저장 실패", "중복 진로 힌트 발생", "실제 플레이 상호작용 우회 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 진로 후보/힌트/route가 데이터 기반으로 정의되고 Registry에서 로드된다.
- 최소 4개 후보가 존재한다.
- 최소 하나의 후보가 학교 수업 없이 학교 밖 경로로 진행된다.
- 최소 하나의 후보가 2개 이상의 대체 경로로 진행 가능하다.
- 실제 플레이 E2E가 진로 힌트 획득, 후보 UI 확인, 하루 결과 UI 확인, 저장/로드 복원을 검증한다.
- 최종 보고에 실제 플레이 방식 상호작용 검증 체크리스트 PASS/FAIL이 포함된다.
- 체크리스트 항목 중 FAIL이 있으면 완료로 보고하지 않는다.
- 후보 진행/보상은 저장/로드 후 중복 적용되지 않는다.
- 후보 UI는 미해금/부분 해금/해금 상태와 추천 다음 행동을 보여준다.
- SO 데이터와 전략 배열 기반이며 엔티티 ID별 분기가 없다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 후보/힌트 목록, 실행한 테스트, 실제 플레이 검증 흐름, 자유 경로 검증 결과, UI 구현 범위, 성능 검증 범위, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

