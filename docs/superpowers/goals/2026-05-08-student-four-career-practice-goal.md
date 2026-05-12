# Student Four Career Practice Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 학생 생활 루프의 진로 실습 콘텐츠를 구현해줘. 개발자/디자이너 같은 메타 직업 예시가 아니라, 실제 게임 안에서 반복 플레이와 미니 실습으로 표현하기 좋은 4개 직업군을 1차 후보로 사용한다.

1차 직업군:
1. 요리사
2. 인테리어/공간 디자이너
3. 군인
4. 응급구조사/간호 계열

핵심 목표:
1. 플레이어는 학생으로 시작하고, 학교 또는 진로 체험 시간에 4개 직업 실습 중 하나를 선택할 수 있다.
2. 실습은 직업을 바로 확정하는 선택이 아니라 "진로 힌트"와 성향/스킬을 발견하는 활동이어야 한다.
3. 요리사 실습은 재료 선택, 순서, 타이밍, 주문 대응 같은 플레이로 표현한다.
4. 인테리어 실습은 방 꾸미기, 가구 배치, 색 조합, 예산 안에서 공간 완성 같은 플레이로 표현한다.
5. 군인 실습은 전투가 아니라 체력 훈련, 규율, 반응속도, 팀 지시 수행 같은 플레이로 표현한다.
6. 응급구조사/간호 실습은 증상 판단, 응급 처치 순서, 환자 응대, 침착한 선택 같은 플레이로 표현한다.
7. 각 실습은 서로 다른 성향/스킬 조합을 올려야 한다.
8. 선택지는 정답/오답이 아니라 플레이어의 경향을 드러내야 한다.
9. 같은 실습을 반복해도 request id 기준 중복 적용이 발생하지 않아야 한다.
10. 멀티플레이어에서는 실습 결과가 서버 권한으로 확정되고, Player 1의 성향/스킬/진로 힌트가 Player 2에게 섞이면 안 된다.

절대 규칙:
- AGENTS.md와 .claude/rules/*를 따른다.
- Assets/**/*.cs는 외부 디스크 직접 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로를 사용한다.
- TDD로 진행한다. 먼저 실패 테스트를 작성하고 실패를 확인한 뒤 최소 구현/수정을 한다.
- 직업, 실습, 실습 단계, 성향, 스킬, 진로 힌트, 보상은 모두 ScriptableObject 데이터 기반이어야 한다.
- careerId/practiceId/traitId/skillId/stepId 같은 문자열/enum별 if/switch 분기 로직을 만들지 않는다.
- 직업별 C# 클래스, 실습별 C# 클래스, 성향별 C# 분기 구현은 금지한다.
- 선택/실습 결과는 중복 적용되지 않아야 한다.
- 멀티플레이어 상태 변경은 서버 권한 기준으로 처리한다.
- 기존 사용자 변경, dirty 파일, 생성된 에셋을 되돌리지 않는다.

먼저 확인할 문맥:
- docs/superpowers/specs/2026-05-08-student-career-life-design.md
- docs/superpowers/goals/2026-05-08-student-career-life-multiplayer-goal.md
- docs/superpowers/goals/2026-05-08-student-trait-class-practice-goal.md
- Assets/Scripts/Game/StudentLife/StudentLifeCore.cs
- Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs
- Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs
- Assets/Scripts/Network/StudentLife/StudentLifeNetworkStateBroadcaster.cs
- Assets/Tests/EditMode/StudentLife/
- Assets/Tests/PlayMode/TownConcept/TownStudentLifeInteractionTests.cs
- Assets/Scripts/Game/Common/GameDataRegistry.cs
- Assets/Scenes/Town.unity

권장 설계:
1. 기존 StudentLife의 LifeActivityDefinition, LifeActivityEffectBase, StudentLifeProgress, LifeActivityRunner를 최대한 재사용한다.
2. 새 데이터 모델이 필요하면 PracticeActivityDefinition 또는 CareerPracticeDefinition을 추가한다.
3. 실습별 전용 C# 타입을 만들지 말고, 실습 정의 + 단계 정의 + 효과 배열로 표현한다.
4. 1차 구현은 복잡한 미니게임이 아니라 "실습 카드/선택지 실행"으로 충분하다.
5. 후속 확장을 위해 각 실습은 단계 배열을 가질 수 있어야 한다.
   - 예: 조리 실습 = 재료 선택 -> 조리 순서 -> 손님 응대
   - 예: 인테리어 실습 = 공간 목적 확인 -> 가구 배치 -> 색감 선택
   - 예: 군인 실습 = 체력 훈련 -> 지시 수행 -> 팀워크 선택
   - 예: 응급 실습 = 증상 확인 -> 처치 순서 -> 환자 안정

4개 실습의 1차 효과 방향:
1. 요리사 실습
   - 주요 성향: 집중력, 성실함, 서비스 감각
   - 주요 스킬: 조리, 순서 판단, 타이밍
   - 진로 힌트: 음식과 서비스에 재능이 보인다
2. 인테리어/공간 디자이너 실습
   - 주요 성향: 창의성, 계획성, 미적 감각
   - 주요 스킬: 공간 배치, 색 조합, 예산 감각
   - 진로 힌트: 공간을 꾸미고 개선하는 감각이 보인다
3. 군인 실습
   - 주요 성향: 체력, 책임감, 규율, 침착함
   - 주요 스킬: 훈련 수행, 반응속도, 팀워크
   - 진로 힌트: 규율과 체력 기반 진로에 적성이 보인다
4. 응급구조사/간호 실습
   - 주요 성향: 침착함, 공감, 책임감, 관찰력
   - 주요 스킬: 응급 처치, 증상 판단, 돌봄
   - 진로 힌트: 돌봄과 응급 대응 계열에 관심이 생긴다

Town 씬 요구:
1. Town 씬 로드 후 "CareerPracticeBoard" 또는 유사한 진로 실습 오브젝트가 존재해야 한다.
2. 진로 실습 오브젝트는 4개 실습 정의를 노출해야 한다.
3. 플레이어가 실습 하나를 실행하면 StudentLifeProgress가 변경되어야 한다.
4. 각 실습은 다른 성향/스킬/진로 힌트를 변경해야 한다.
5. 실습 결과는 테스트 가능한 컴포넌트 상태로 노출되어야 한다.
   - 마지막 실습 id
   - 마지막 request id
   - 마지막 result kind
   - 열린 진로 힌트

필수 테스트:
- LIFE-CAREER-PRACTICE-001: 4개 진로 실습 정의가 데이터 기반으로 존재한다.
- LIFE-CAREER-PRACTICE-002: 요리사 실습은 조리/순서/타이밍 계열 성향 또는 스킬을 올린다.
- LIFE-CAREER-PRACTICE-003: 인테리어 실습은 공간/창의/계획 계열 성향 또는 스킬을 올린다.
- LIFE-CAREER-PRACTICE-004: 군인 실습은 체력/규율/책임감 계열 성향 또는 스킬을 올린다.
- LIFE-CAREER-PRACTICE-005: 응급구조사/간호 실습은 침착/공감/관찰 계열 성향 또는 스킬을 올린다.
- LIFE-CAREER-PRACTICE-006: 각 실습은 서로 다른 CareerDefinition 진로 힌트를 연다.
- LIFE-CAREER-PRACTICE-007: 같은 request id로 같은 실습을 반복 실행해도 중복 적용되지 않는다.
- LIFE-CAREER-PRACTICE-008: 실습 실행 로직에 careerId/practiceId별 if/switch 분기가 없다.
- LIFE-CAREER-PRACTICE-PM-001: Town 씬 로드 후 진로 실습 오브젝트가 존재한다.
- LIFE-CAREER-PRACTICE-PM-002: Town 씬에서 요리사 실습을 실행하면 플레이어 진행도가 변경된다.
- LIFE-CAREER-PRACTICE-PM-003: Town 씬에서 인테리어 실습을 실행하면 플레이어 진행도가 변경된다.
- LIFE-CAREER-PRACTICE-PM-004: Town 씬에서 군인 실습을 실행하면 플레이어 진행도가 변경된다.
- LIFE-CAREER-PRACTICE-PM-005: Town 씬에서 응급구조사/간호 실습을 실행하면 플레이어 진행도가 변경된다.
- LIFE-CAREER-PRACTICE-PM-006: Town 씬에서 4개 실습이 서로 다른 진로 힌트를 연다.
- LIFE-CAREER-PRACTICE-NET-001: host/client 구성에서 실습 요청은 서버 권한으로 검증/확정된다.
- LIFE-CAREER-PRACTICE-NET-002: Player 1의 실습 결과가 Player 2의 개인 성향/스킬/진로 힌트를 변경하지 않는다.
- LIFE-CAREER-PRACTICE-NET-003: 중복 실습 요청은 중복 브로드캐스트나 중복 성장을 만들지 않는다.
- LIFE-CAREER-PRACTICE-CI-001: Scripts/ci/check-no-entity-id-branching.sh가 통과한다.

작업 순서:
1. git status로 dirty 상태를 확인하고 기존 변경을 되돌리지 않는다.
2. 기존 StudentLife 구현과 Town 씬 자동 설치 코드를 읽고 재사용 가능한 경계를 요약한다.
3. 실패 테스트를 먼저 추가한다.
4. 실패를 확인한다.
5. 4개 실습 정의/실행 구조를 최소 구현한다.
6. Town 씬 런타임 자동 설치에 진로 실습 오브젝트를 추가한다.
7. EditMode 테스트를 실행한다.
8. TownConcept PlayMode 테스트를 실행한다.
9. 멀티플레이어/브로드캐스트 테스트를 실행한다.
10. Scripts/ci/check-no-entity-id-branching.sh를 실행한다.
11. 변경 파일, 테스트 결과, 미구현 범위, 남은 리스크를 보고한다.

완료 조건:
- 요리사, 인테리어/공간 디자이너, 군인, 응급구조사/간호 4개 실습이 데이터 기반으로 존재한다.
- 각 실습은 서로 다른 성향/스킬/진로 힌트에 연결된다.
- Town 씬에서 4개 실습 상호작용이 PlayMode 테스트로 검증된다.
- 중복 request id가 중복 성장을 만들지 않는다.
- 멀티플레이어 서버 권한/플레이어별 상태 분리 테스트가 통과한다.
- entity ID 분기 금지 CI가 통과한다.
- 최종 보고에는 실행한 테스트 명령, 통과/실패 결과, 커밋 여부, 남은 리스크를 포함한다.
```

## Design Notes

이 goal은 개발자/디자이너 같은 예시 직업을 배제하고, 게임 안에서 실습 행동으로 표현하기 쉬운 4개 직업군을 1차 콘텐츠로 고정한다.

- 요리사: 순서와 타이밍 중심
- 인테리어/공간 디자이너: 배치와 조합 중심
- 군인: 체력, 규율, 팀워크 중심
- 응급구조사/간호: 판단, 순서, 공감 중심

1차 구현은 복잡한 미니게임이 아니라 데이터 기반 실습 카드 실행으로 시작한다. 후속 goal에서 각 실습을 별도 미니게임으로 확장한다.

