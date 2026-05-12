# Student Trait Class Practice Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 학생 생활 루프를 확장해, "수업 대화 선택 + 직업/전공 실습"으로 플레이어의 성향과 스킬을 발견하고 성장시키는 1차 시스템을 설계/구현/검증해줘.

핵심 목표:
1. 성향은 정답/오답 선택이 아니라 플레이어가 어떤 방식으로 문제를 바라보는지 드러내는 누적 신호여야 한다.
2. 학교 수업은 짧은 대화형 이벤트로 표현한다.
3. 수업 이벤트에는 3~4개의 선택지가 있고, 선택지마다 서로 다른 성향/스킬 변화가 적용된다.
4. 실습 활동은 개발 실습, 디자인 실습, 발표 실습, 서비스 실습처럼 진로/직업 방향을 체험하는 활동이어야 한다.
5. 대화형 수업은 성향을 "발견"하는 역할, 실습은 성향/스킬을 "검증하고 성장"시키는 역할로 나눈다.
6. 첫 playable slice는 "오늘의 수업 -> 선택지 선택 -> 성향 변화 -> 실습 선택 -> 스킬/진로 힌트 변화 -> 활동 결과 표시"까지로 제한한다.
7. 학교는 유일한 성장 경로가 아니어야 한다. 같은 구조로 방과 후 활동, 알바, 독학, 취미, 멘토링도 확장 가능해야 한다.
8. 멀티플레이어에서는 수업 선택, 실습 결과, 성향/스킬/진로 힌트가 서버 권한 기준으로 처리되고 플레이어별 상태가 섞이지 않아야 한다.

절대 규칙:
- AGENTS.md와 .claude/rules/*를 따른다.
- Assets/**/*.cs는 외부 디스크 직접 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로를 사용한다.
- TDD로 진행한다. 먼저 실패 테스트를 작성하고 실패를 확인한 뒤 최소 구현/수정을 한다.
- 수업, 선택지, 실습, 성향, 스킬, 진로 힌트, 보상은 모두 ScriptableObject 데이터 기반이어야 한다.
- classId/activityId/practiceId/traitId/skillId/careerId 같은 문자열/enum별 if/switch 분기 로직을 만들지 않는다.
- 직업별 C# 클래스, 수업별 C# 클래스, 성향별 C# 분기 구현은 금지한다.
- 선택지 보상/해금/진로 힌트는 중복 적용되지 않아야 한다.
- 멀티플레이어 상태 변경은 서버 권한 기준으로 처리한다.
- 개인별 성향/스킬/진로/활동 진행도는 플레이어별로 분리한다.
- 기존 사용자 변경, dirty 파일, 생성된 에셋을 되돌리지 않는다.

먼저 확인할 문맥:
- docs/superpowers/specs/2026-05-08-student-career-life-design.md
- docs/superpowers/goals/2026-05-08-student-career-life-multiplayer-goal.md
- Assets/Scripts/Game/StudentLife/StudentLifeCore.cs
- Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs
- Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs
- Assets/Scripts/Network/StudentLife/StudentLifeNetworkStateBroadcaster.cs
- Assets/Tests/EditMode/StudentLife/
- Assets/Tests/PlayMode/TownConcept/TownStudentLifeInteractionTests.cs
- Assets/Scripts/Game/Common/GameDataRegistry.cs
- Assets/Scenes/Town.unity

기획 방향:
1. 수업 대화 이벤트 예시:
   - 오늘의 수업: 컴퓨터 시간
   - 상황: "팀 프로젝트에서 어떤 역할을 맡고 싶은가?"
   - 선택지 A: 자료를 정리하고 일정표를 만든다 -> 성실함 +2, 책임감 +1
   - 선택지 B: 아이디어를 내고 발표 방향을 잡는다 -> 창의성 +2, 사교성 +1
   - 선택지 C: 조용히 구현/문제풀이를 맡는다 -> 집중력 +2, 논리 +1
   - 선택지 D: 친구들이 어려워하는 부분을 도와준다 -> 사교성 +2, 공감 +1
2. 실습 활동 예시:
   - 개발 실습: 간단한 로직 퍼즐, 버그 찾기, 순서 맞추기, 기능 카드 조합
   - 디자인 실습: 색 조합, 포스터 레이아웃, UI 배치 선택
   - 발표 실습: 설명 방식 선택, 키워드 정리, 청중 반응 대응
   - 서비스 실습: 손님 응대 선택, 문제 상황 해결
   - 요리 실습: 재료 순서, 시간 맞추기
   - 운동 실습: 타이밍 입력 또는 체력 활동 선택
3. 성향 변화는 좋은 선택/나쁜 선택으로 표현하지 않는다.
   - 모든 선택은 다른 경향을 드러내야 한다.
   - 예: 계획형, 탐구형, 협업형, 창작형, 실행형, 돌봄형 등
4. 실습은 처음부터 복잡한 미니게임으로 만들지 않는다.
   - 1차는 "실습 카드 선택 -> 결과 적용"으로 충분하다.
   - 후속으로 각 실습을 미니게임화할 수 있게 데이터 구조만 열어 둔다.

권장 데이터 모델:
- ClassEventDefinition: 하루 수업/학교 이벤트 정의
- ClassChoiceDefinition: 수업 선택지 정의
- PracticeActivityDefinition: 실습 활동 정의
- PracticeStepDefinition: 실습 내부 단계 또는 카드 정의
- TraitDefinition: 성향 정의, 기존 StudentLife TraitDefinition 재사용 우선
- SkillDefinition: 스킬 정의, 기존 StudentLife SkillDefinition 재사용 우선
- CareerDefinition: 진로 힌트 정의, 기존 StudentLife CareerDefinition 재사용 우선
- ClassChoiceEffectBase 또는 기존 LifeActivityEffectBase 재사용
- PracticeResultEffectBase 또는 기존 LifeActivityEffectBase 재사용
- StudentLifeProgress: 기존 개인 진행도 재사용
- StudentLifeNetworkStateBroadcaster: 기존 서버 권한 브로드캐스트 패턴 재사용

구현 방향:
1. 기존 StudentLife 순수 도메인을 먼저 재사용한다.
2. 수업 이벤트와 실습 활동이 기존 LifeActivityRunner와 같은 효과 적용 경로를 쓰게 한다.
3. 수업 선택지는 내부적으로 데이터 기반 effect 배열을 실행한다.
4. 실습 선택지도 데이터 기반 effect 배열을 실행한다.
5. 선택/실습 요청에는 request id를 포함해 중복 적용을 막는다.
6. Town 씬에는 1차로 "ClassEventBoard" 또는 "SchoolClassEvent" 오브젝트를 자동 설치한다.
7. Town 씬에는 1차로 "PracticeBoard" 또는 "PracticeActivity" 오브젝트를 자동 설치한다.
8. 상호작용 결과를 테스트 가능한 컴포넌트 상태로 노출한다.
   - 마지막 선택지 id
   - 마지막 실습 id
   - 마지막 결과 kind
   - 변경된 성향/스킬/진로 힌트

1차 playable slice 요구:
1. Town 씬 로드 후 수업 이벤트 오브젝트가 존재한다.
2. 수업 이벤트는 최소 4개 선택지를 가진다.
3. 각 선택지는 서로 다른 성향/스킬 조합을 올린다.
4. 선택지를 실행하면 StudentLifeProgress가 변경된다.
5. Town 씬 로드 후 실습 오브젝트가 존재한다.
6. 실습은 최소 개발 실습과 디자인 실습 2개를 제공한다.
7. 개발 실습은 개발/논리/집중 계열 스킬 또는 성향을 올린다.
8. 디자인 실습은 창의성/디자인/표현 계열 스킬 또는 성향을 올린다.
9. 수업 선택과 실습 결과가 진로 힌트 해금에 연결된다.
10. 같은 request id로 같은 선택/실습을 반복해도 중복 적용되지 않는다.

필수 테스트:
- LIFE-CLASS-001: ClassEventDefinition은 4개 선택지를 데이터로 가진다.
- LIFE-CLASS-002: 수업 선택지 A/B/C/D는 서로 다른 성향/스킬 변화를 적용한다.
- LIFE-CLASS-003: 수업 선택은 정답/오답 상태가 아니라 성향 변화로만 기록된다.
- LIFE-CLASS-004: 같은 class choice request id를 반복 실행해도 성향/스킬이 중복 적용되지 않는다.
- LIFE-CLASS-005: 수업 선택 결과가 조건을 만족하면 진로 힌트를 연다.
- LIFE-PRACTICE-001: 개발 실습과 디자인 실습 정의가 데이터 기반으로 존재한다.
- LIFE-PRACTICE-002: 개발 실습은 개발/논리/집중 계열 진행도를 올린다.
- LIFE-PRACTICE-003: 디자인 실습은 창의성/디자인/표현 계열 진행도를 올린다.
- LIFE-PRACTICE-004: 같은 practice request id를 반복 실행해도 중복 적용되지 않는다.
- LIFE-PRACTICE-005: 실습 결과가 진로 힌트 해금에 연결된다.
- LIFE-PRACTICE-PM-001: Town 씬 로드 후 수업 이벤트 상호작용 오브젝트가 존재한다.
- LIFE-PRACTICE-PM-002: Town 씬에서 수업 선택지를 실행하면 플레이어 StudentLifeProgress가 변경된다.
- LIFE-PRACTICE-PM-003: Town 씬에서 개발 실습을 실행하면 관련 성향/스킬이 오른다.
- LIFE-PRACTICE-PM-004: Town 씬에서 디자인 실습을 실행하면 관련 성향/스킬이 오른다.
- LIFE-PRACTICE-PM-005: Town 씬에서 수업 선택과 실습 실행 후 진로 힌트가 열린다.
- LIFE-PRACTICE-NET-001: 멀티플레이어 host/client 구성에서 수업 선택 요청은 서버가 검증하고 확정한다.
- LIFE-PRACTICE-NET-002: Player 1의 수업 선택/실습 결과가 Player 2의 개인 성향/스킬을 변경하지 않는다.
- LIFE-PRACTICE-NET-003: 중복 요청, 재전송, 재접속 후에도 선택/실습 결과가 중복 적용되지 않는다.
- LIFE-PRACTICE-NET-004: 수업/실습 결과 브로드캐스트는 상태 변경 단위로만 발생하고 중복 폭증하지 않는다.
- LIFE-PRACTICE-CI-001: Scripts/ci/check-no-entity-id-branching.sh가 통과한다.

작업 순서:
1. git status로 dirty 상태를 확인하고 기존 변경을 되돌리지 않는다.
2. 기존 StudentLife 구현과 테스트를 읽고 재사용 가능한 경계를 요약한다.
3. 실패 테스트를 먼저 추가한다.
4. 실패를 확인한다.
5. 수업 이벤트/선택지/실습 정의와 실행 로직을 최소 구현한다.
6. Town 씬 런타임 자동 설치에 수업/실습 상호작용 오브젝트를 추가한다.
7. EditMode 테스트를 실행한다.
8. TownConcept PlayMode 테스트를 실행한다.
9. 멀티플레이어 브로드캐스트/서버 권한 테스트를 실행한다.
10. Scripts/ci/check-no-entity-id-branching.sh를 실행한다.
11. 변경 파일, 테스트 결과, 미구현 범위, 남은 리스크를 보고한다.

완료 조건:
- 수업 대화 선택형 성향 발견 루프가 데이터 기반으로 구현되어 있다.
- 개발/디자인 실습형 스킬 성장 루프가 데이터 기반으로 구현되어 있다.
- Town 씬에서 수업 선택과 실습 상호작용이 PlayMode 테스트로 검증된다.
- 성향/스킬/진로 힌트가 플레이어별 StudentLifeProgress에 반영된다.
- 중복 요청이 중복 성장을 만들지 않는다.
- 멀티플레이어 서버 권한/플레이어별 상태 분리 테스트가 통과한다.
- entity ID 분기 금지 CI가 통과한다.
- 최종 보고에는 실행한 테스트 명령, 통과/실패 결과, 커밋 여부, 남은 리스크를 포함한다.
```

## Design Notes

추천 방향은 **대화형 수업 + 실습형 활동**의 혼합이다.

- 대화형 수업은 제작 비용이 낮고 매일 다른 상황을 많이 만들기 좋다.
- 실습형 활동은 직업/전공 체험감을 만들고, 스킬과 진로 힌트에 연결하기 좋다.
- 선택지는 정답/오답이 아니라 서로 다른 성향을 드러내야 한다.
- 1차 실습은 복잡한 미니게임이 아니라 카드 선택형으로 시작하고, 후속 단계에서 미니게임으로 확장한다.

