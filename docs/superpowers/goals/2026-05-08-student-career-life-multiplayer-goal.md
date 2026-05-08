# Student Career Life Multiplayer Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트의 town life 전환 방향을 이어서, "학생으로 시작해 도시 생활 속에서 성향을 발견하고 다양한 진로/직업으로 성장하는 확장형 생활 시뮬레이션"의 기획 설계와 1차 구현 범위를 정리해줘.

핵심 목표:
1. 플레이어는 초반에 학생으로 시작한다.
2. 학생 생활은 등교, 수업, 공부, 과제, 동아리, 친구/선생님 관계, 방과 후 활동을 통해 성향을 발견하는 초반 루프가 된다.
3. 학교는 필수 단일 루트가 아니라 여러 성장 경로 중 하나여야 한다.
4. 플레이어는 꼭 학교를 끝까지 다니지 않아도 되고, 알바, 독학, 취미, 동네 활동, 조기 취업, 자격/기술 습득 같은 다른 경로로 성장할 수 있어야 한다.
5. 직업은 학생 이후 선택하는 고정 클래스가 아니라, 성향/스킬/경험/관계/도시 평판에 의해 열리는 데이터 기반 진로 후보여야 한다.
6. 콘텐츠가 많이 확장될 수 있도록 활동, 성향, 스킬, 직업, 이벤트, 퀘스트를 모두 ScriptableObject 데이터 기반으로 설계한다.
7. 첫 구현 범위는 "학생이 학교에 등교하고 공부하며 성향을 찾아나가는 느낌"이 실제 플레이 흐름으로 보이는 수준까지로 제한한다.
8. 멀티플레이어 환경에서도 학생 활동, 성향 변화, 진로 힌트, 보상/해금 상태가 서버 권한 기준으로 일관되게 동기화되어야 한다.
9. 멀티플레이어 테스트는 선택 사항이 아니라 필수 완료 조건이다.

절대 규칙:
- AGENTS.md와 .claude/rules/*를 따른다.
- Assets/**/*.cs는 외부 디스크 직접 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로를 사용한다.
- TDD로 진행한다. 먼저 실패 테스트를 작성하고 실패를 확인한 뒤 최소 구현/수정을 한다.
- 학생, 직업, 활동, 성향, 스킬, 학교 과목, 동아리, 알바, 이벤트, 퀘스트, 보상은 모두 ScriptableObject 데이터 기반이어야 한다.
- studentId/jobId/activityId/traitId/skillId 같은 문자열/enum별 if/switch 분기 로직을 만들지 않는다.
- 직업별 C# 클래스, 과목별 C# 클래스, 성향별 C# 분기 구현은 금지한다.
- 모든 보상/인벤토리/해금은 지급 전에 수용 가능 여부와 중복 수령 여부를 검증하고, 실패 시 상태를 변경하지 않는다.
- 멀티플레이어 상태 변경은 서버 권한을 기준으로 처리한다.
- 클라이언트별 UI 표시 상태와 서버 저장 상태를 혼동하지 않는다.
- 개인별 성향/스킬/진로/활동 진행도는 플레이어별로 분리되어야 한다. 한 플레이어의 공부 결과가 다른 플레이어의 개인 성장 상태를 오염시키면 안 된다.
- 공유 월드 상태와 개인 성장 상태를 명확히 분리한다. 예: 학교 수업 시간표, 교실 이벤트, 공용 NPC 상태는 공유 가능하지만 개인 성향/스킬/진로 힌트는 플레이어별 상태여야 한다.
- 기존 사용자 변경, dirty 파일, 생성된 에셋을 되돌리지 않는다.

먼저 확인할 문맥:
- docs/superpowers/specs/2026-05-08-town-concept-conversion-design.md
- docs/superpowers/goals/2026-05-08-town-playable-baseline-recovery-goal.md
- docs/superpowers/goals/2026-05-08-npc-dialogue-quest-playflow-goal.md
- Assets/Scripts/Game/ModernSociety/
- Assets/Scripts/Game/Quests/
- Assets/Scripts/Game/Dialogue/
- Assets/Scripts/Game/Time/
- Assets/Scripts/Game/Inventory/
- Assets/Scripts/Network/
- Assets/Data/Registry/GameDataRegistry.asset
- Assets/Data/Knowledge/
- Assets/Data/Traits/
- Assets/Data/Generations/
- Assets/Data/Status/
- Assets/Scenes/Town.unity
- Assets/Tests/EditMode/
- Assets/Tests/PlayMode/

기획 설계 요구:
1. "학생 시작 -> 성향 발견 -> 진로 후보 개방 -> 직업/생활 경로 선택"의 장기 구조를 설계한다.
2. 학교 활동과 비학교 활동을 같은 추상 구조로 다룰 수 있게 한다.
   - 예: SchoolClassActivity, StudyActivity, ClubActivity, PartTimeJobActivity, HobbyActivity, ErrandActivity 등이 모두 공통 ActivityDefinition 또는 그에 준하는 데이터 구조로 표현된다.
3. 성향은 고정 직업 선택지가 아니라 플레이 성향을 드러내는 누적 신호여야 한다.
   - 예: 성실함, 창의성, 사교성, 집중력, 체력, 호기심, 책임감, 독립성
4. 직업은 단일 선택지가 아니라 해금 조건과 성장 경로를 가진 데이터여야 한다.
   - 예: 회사원, 개발자, 디자이너, 요리사, 교사, 연구자, 간호사, 경찰, 예술가, 창업가, 배달/서비스직, 프리랜서 등
5. 학교를 다니는 루트와 학교 밖 루트가 모두 가능해야 한다.
   - 학교 루트: 수업/시험/동아리/친구/진학
   - 비학교 루트: 알바/독학/자격/멘토/현장 경험/창작/지역 평판
6. 콘텐츠 확장 예시를 충분히 제안한다.
   - 수업 콘텐츠
   - 동아리 콘텐츠
   - 알바 콘텐츠
   - 인간관계 콘텐츠
   - 동네 이벤트
   - 진로 상담
   - 자격/기술 습득
   - 스트레스/피로/컨디션 관리
   - 돈/시간/평판/집중도 관리
   - 졸업, 중퇴, 검정고시, 조기 취업 같은 분기
7. 1차 playable slice는 너무 넓히지 않는다.
   - 등교한다.
   - 학교 또는 교실 오브젝트/NPC와 상호작용한다.
   - 공부 활동을 실행한다.
   - 시간/체력/집중도 같은 상태가 변한다.
   - 성향 또는 스킬 진행도가 오른다.
   - 하루 종료 또는 활동 로그에서 플레이어가 어떤 성향을 보였는지 확인할 수 있다.
   - 이 결과로 최소 1개 이상의 진로 힌트 또는 다음 활동 후보가 열린다.

멀티플레이어 설계 요구:
1. 학생 활동 실행은 서버 권한으로 처리한다.
2. 클라이언트는 활동 요청을 보내고, 서버가 조건 검증 후 결과를 확정한다.
3. 활동 비용 검증은 서버에서 수행한다.
   - 시간
   - 체력
   - 집중도
   - 필요 아이템
   - 위치 또는 상호작용 가능 거리
   - 선행 활동/성향/스킬 조건
4. 활동 결과는 개인 상태와 공유 상태로 분리한다.
   - 개인 상태: 성향, 스킬, 진로 힌트, 개인 활동 로그, 개인 보상 수령 상태
   - 공유 상태: 학교 시간표, 공용 이벤트, 교실/NPC 활성 상태, 파티 또는 그룹 활동 결과
5. 멀티플레이어에서 같은 수업을 함께 듣는 상황을 지원할 수 있어야 한다.
   - 같은 활동에 참여해도 개인별 결과 수치는 각 플레이어 조건에 따라 다를 수 있다.
   - 그룹 활동의 공유 결과와 개인별 보상/성향 변화가 섞이면 안 된다.
6. 중복 요청과 재전송을 방어한다.
   - 같은 활동 요청이 반복되어도 보상/성향/스킬이 중복 적용되지 않는다.
   - 네트워크 지연 또는 재접속 후에도 서버 확정 상태가 기준이 된다.
7. saveSlot과 player identity를 함께 고려한다.
   - saveSlot A의 Player 1 성장 상태가 saveSlot B 또는 Player 2에 섞이면 안 된다.
   - host/client 전환 또는 재접속 후 개인 성장 상태가 유지되어야 한다.
8. 브로드캐스트는 상태 변경 단위로 최소화한다.
   - 활동 결과가 없는 반복 요청은 불필요한 브로드캐스트를 만들지 않는다.
   - 플레이어 수 증가 시 메시지 수가 중복 폭증하지 않도록 검증한다.
9. 클라이언트 UI는 서버 확정 결과를 기준으로 갱신한다.
   - 예측 UI를 쓰더라도 서버 거절 시 롤백 또는 명확한 실패 표시가 가능해야 한다.

권장 데이터 모델 후보:
- LifeActivityDefinition: 도시 생활 활동 정의
- ActivityEffectBase: 활동 결과 전략
- TraitDefinition: 성향 정의
- TraitDeltaEffect: 성향 변화 전략
- SkillDefinition: 스킬/능력 정의
- SkillProgressEffect: 스킬 진행 전략
- CareerDefinition: 직업/진로 정의
- CareerUnlockRequirementBase: 진로 해금 조건 전략
- SchoolSubjectDefinition: 학교 과목 정의
- ScheduleSlotDefinition: 시간표/하루 일정 정의
- LifeEventDefinition: 생활 이벤트 정의
- RelationshipDefinition 또는 NPC 관계 데이터
- MultiplayerActivityRequest: 클라이언트의 활동 요청 데이터
- MultiplayerActivityResult: 서버 확정 활동 결과 데이터
- PlayerLifeProgressSaveData: 플레이어별 성향/스킬/진로/활동 로그 저장 데이터
- SharedLifeWorldStateSaveData: saveSlot별 공유 학교/도시 상태 저장 데이터
- GameDataRegistry 등록 확장

1차 테스트 시나리오:
- LIFE-STUDENT-001: 학생 시작 데이터가 GameDataRegistry에 등록되어 있고 null 참조가 없다.
- LIFE-STUDENT-002: 등교 활동을 실행하면 시간 또는 일정 상태가 갱신된다.
- LIFE-STUDENT-003: 공부 활동을 실행하면 집중도/체력/성향/스킬 중 정의된 효과가 데이터 기반으로 적용된다.
- LIFE-STUDENT-004: 같은 activityId에 대한 C# if/switch 분기 없이 ActivityEffectBase 전략 배열로 결과가 적용된다.
- LIFE-STUDENT-005: 성향 누적값에 따라 최소 1개 이상의 진로 힌트가 열린다.
- LIFE-STUDENT-006: 학교 활동이 필수 고정 루트가 아니며, 비학교 활동 정의도 같은 구조로 등록 가능하다.
- LIFE-STUDENT-007: 활동 보상 또는 해금은 중복 지급되지 않고 멱등성을 유지한다.
- LIFE-STUDENT-008: 저장/로드 후 학생 활동 진행도, 성향, 스킬, 열린 진로 힌트가 유지된다.
- LIFE-STUDENT-009: saveSlot A의 성향/진로 진행도가 saveSlot B에 섞이지 않는다.
- LIFE-STUDENT-010: entity ID 분기 금지 CI가 통과한다.

필수 멀티플레이어 테스트 시나리오:
- LIFE-STUDENT-NET-001: host/client 구성에서 클라이언트가 등교 또는 공부 활동을 요청하면 서버가 조건을 검증하고 결과를 확정한다.
- LIFE-STUDENT-NET-002: 서버가 확정한 활동 결과가 요청한 플레이어의 클라이언트 UI와 서버 저장 상태에 반영된다.
- LIFE-STUDENT-NET-003: Player 1의 성향/스킬/진로 힌트 변화가 Player 2의 개인 성장 상태를 변경하지 않는다.
- LIFE-STUDENT-NET-004: 같은 수업 또는 그룹 활동에 여러 플레이어가 참여해도 공유 상태와 개인 상태가 분리된다.
- LIFE-STUDENT-NET-005: 같은 활동 요청이 중복 전송되어도 성향/스킬/보상/해금이 중복 적용되지 않는다.
- LIFE-STUDENT-NET-006: 서버가 비용 부족, 위치 불일치, 선행 조건 부족 때문에 활동 요청을 거절하면 개인 성장 상태와 보상 상태가 변경되지 않는다.
- LIFE-STUDENT-NET-007: 저장/로드 후 같은 saveSlot, 같은 player identity의 학생 활동 진행도와 진로 힌트가 복원된다.
- LIFE-STUDENT-NET-008: saveSlot A와 saveSlot B 사이에 개인 성장 상태와 공유 학교 상태가 섞이지 않는다.
- LIFE-STUDENT-NET-009: 재접속 또는 클라이언트 재동기화 후 서버 확정 상태와 클라이언트 표시 상태가 일치한다.
- LIFE-STUDENT-NET-010: 활동 결과 브로드캐스트가 상태 변경 단위로만 발생하고, 중복 요청이나 무효 요청이 불필요한 추가 브로드캐스트를 만들지 않는다.
- LIFE-STUDENT-NET-011: 플레이어 수 증가 시 네트워크 메시지/상태 갱신 횟수가 중복 폭증하지 않는지 성능 회귀 카운터로 검증한다.
- LIFE-STUDENT-NET-012: 기존 NPC/Dialogue/Quest 멀티플레이 테스트와 충돌하지 않고 함께 통과한다.

테스트 작성 기준:
- 가능하면 기존 테스트 구조를 확장한다.
- 순수 데이터 모델, 활동 효과, 저장 데이터, 조건 검증은 EditMode 테스트로 먼저 검증한다.
- 실제 씬 상호작용과 UI 표시, 등교/공부 플레이 흐름은 PlayMode 테스트로 검증한다.
- 멀티플레이어 서버 권한, 브로드캐스트, 재접속, 클라이언트별 상태 분리는 PlayMode 또는 NGO 테스트 유틸리티로 검증한다.
- 성능 검증은 가능한 한 결정적 카운터를 사용한다.
  - 예: 활동 결과당 브로드캐스트 횟수
  - 중복 요청 처리 횟수
  - 관련 NetworkVariable/RPC 호출 횟수
  - 플레이어 수 N 대비 메시지 증가량
- 테스트 이름에는 위 시나리오 ID를 포함한다.
- 테스트가 임의 대기 시간에 의존하지 않도록 씬 로드/오브젝트 생성/네트워크 스폰/바인딩 완료 조건을 명확히 기다린다.

작업 순서:
1. git status로 dirty 상태를 확인하고 기존 변경을 되돌리지 않는다.
2. 기존 town concept, quest, time, status, trait, knowledge, registry, network 구조를 읽고 재사용 가능한 부분을 요약한다.
3. 먼저 기획 설계를 작성한다.
   - 장기 구조
   - 1차 playable slice
   - 데이터 모델
   - 콘텐츠 확장 축
   - 멀티플레이어 권한/동기화 구조
   - 테스트 시나리오
4. 구현 전 docs/superpowers/specs/YYYY-MM-DD-student-career-life-design.md 문서로 설계를 저장한다.
5. 설계 문서에 placeholder, TODO, 모호한 요구, AGENTS.md 위반 가능성이 없는지 자체 리뷰한다.
6. 이후 구현이 필요하면 별도 implementation plan을 작성한다.
7. 구현 단계에서는 실패 테스트를 먼저 만들고, 최소 구현으로 통과시킨다.
8. 관련 EditMode/PlayMode 테스트와 멀티플레이어 테스트를 실행한다.
9. Scripts/ci/check-no-entity-id-branching.sh를 실행한다.

완료 조건:
- 학생 시작형 도시 생활 성장 구조가 설계 문서로 정리되어 있다.
- 학교 활동과 비학교 활동이 모두 확장 가능한 데이터 기반 구조로 표현된다.
- 직업은 하드코딩된 선택지가 아니라 성향/스킬/경험/관계/평판 조건으로 열리는 데이터로 설계된다.
- 1차 playable slice가 "등교 -> 공부 -> 성향/스킬 변화 -> 진로 힌트"로 명확히 제한되어 있다.
- 콘텐츠 확장 후보가 충분히 정리되어 있다.
- 멀티플레이어 서버 권한, 클라이언트별 개인 성장 상태 분리, 공유 학교 상태 분리, 브로드캐스트 최소화 방침이 설계되어 있다.
- 테스트 시나리오가 LIFE-STUDENT-* 및 LIFE-STUDENT-NET-* 형태로 정리되어 있다.
- 구현을 진행했다면 관련 EditMode/PlayMode/Network 테스트와 entity ID 분기 금지 CI 결과를 보고한다.
- 멀티플레이어 테스트를 실행하지 못했다면 완료로 보고하지 않고, 왜 못 했는지와 남은 검증 리스크를 명시한다.
```

## Brainstorming Notes

이 goal은 직업 시스템 전체 구현이 아니라, 학생 시작형 성장 구조의 방향을 고정하고 1차 playable slice를 좁히는 데 초점을 둔다. 멀티플레이어는 후속 고려가 아니라 설계 단계부터 포함한다.

핵심 설계 판단:

- 학교는 첫 경로이지만 유일한 경로가 아니다.
- 활동은 학교/비학교를 가리지 않고 공통 데이터 모델로 확장한다.
- 성향과 스킬은 직업 선택의 하드코딩 분기가 아니라 진로 후보를 여는 누적 신호다.
- 서버는 활동 결과의 최종 권한을 가진다.
- 개인 성장 상태와 공유 월드 상태를 분리하지 않으면 멀티플레이어에서 저장/동기화 오염이 생기므로 초기 설계부터 분리한다.
