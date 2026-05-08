# Student Career Life Design

Date: 2026-05-08
Status: Draft for review

## Goal

ROOTBORN의 town life 전환 위에, 플레이어가 학생으로 시작해 도시 생활 속 활동을 수행하며 성향과 스킬을 발견하고 다양한 진로/직업 후보를 열어 가는 성장 구조를 설계한다.

이 설계의 1차 목표는 직업 시스템 전체 구현이 아니다. 첫 playable slice는 “등교 -> 공부 -> 성향/스킬 변화 -> 진로 힌트”가 실제 플레이와 테스트로 검증 가능한 구조가 되도록 범위를 제한한다. 학교는 첫 성장 경로이지만 유일한 경로가 아니며, 같은 데이터 구조로 알바, 독학, 취미, 동네 활동, 멘토링, 조기 취업 같은 학교 밖 경로를 확장할 수 있어야 한다.

멀티플레이어는 후속 고려 사항이 아니다. 활동 실행, 성향 변화, 스킬 진행, 진로 힌트, 보상/해금 상태는 서버 권한으로 확정되고 플레이어별로 분리되어야 한다.

## Non-Goals

- 직업별 C# 클래스나 과목별 C# 클래스를 만들지 않는다.
- `studentId`, `careerId`, `activityId`, `traitId`, `skillId`별 `if`/`switch` 분기를 만들지 않는다.
- 학교를 필수 단일 루트로 고정하지 않는다.
- 졸업, 취업, 전체 직업 성장, 성인 생활 전체 루프를 1차 구현 범위에 넣지 않는다.
- 기존 `ModernSociety` 코드를 즉시 대규모 rename하지 않는다. 먼저 데이터 모델과 테스트 경계를 확정한다.

## Current Context

현재 프로젝트에는 town life 전환의 기반이 일부 존재한다.

| Area | Evidence | Reuse Direction |
| --- | --- | --- |
| Town concept | `docs/superpowers/specs/2026-05-08-town-concept-conversion-design.md` | 도시 생활, 시간/에너지 압박, 데이터 기반 활동 원칙을 계승 |
| Modern activity seed | `Assets/Scripts/Game/ModernSociety/ModernActivityDefinition.cs` | 1차 구조는 존재하지만 enum과 고정 delta 중심이라 전략 배열형으로 승격 필요 |
| Modern stats seed | `Assets/Scripts/Game/ModernSociety/ModernSocietyStats.cs` | 에너지, 돈, 불안, 지식, 관계의 초기 능력치를 town life stats로 재사용 가능 |
| Routine rules | `Assets/Scripts/Game/ModernSociety/ModernRoutineFlagRule.cs` | 활동 이력 기반 story flag 해금 패턴을 진로 힌트/생활 이벤트 조건으로 확장 가능 |
| Quest network pattern | `Assets/Scripts/Network/Quests/QuestNetworkStateBroadcaster.cs` | 서버 권한, saveSlot 포함, 중복 억제, 선형 브로드캐스트 카운터 패턴 재사용 |
| Save service | `Assets/Scripts/Game/Save/SaveService.cs` | saveSlot 단위 JSON 저장 구조 재사용 |
| Registry | `Assets/Scripts/Game/Common/GameDataRegistry.cs` | 신규 학생/활동/성향/스킬/진로 SO 배열 등록 필요 |

## Design Approaches

### Recommended: Activity-First Life Progression

모든 학교/비학교 콘텐츠를 공통 `LifeActivityDefinition`으로 정의하고, 활동 결과를 `ActivityEffectBase[]` 전략으로 적용한다. 학생 수업, 공부, 동아리, 알바, 독학, 취미는 모두 같은 실행 파이프라인을 사용한다.

장점은 확장성이 가장 높고 헌법의 데이터-드리븐 원칙과 맞는다. 학교를 첫 콘텐츠 팩으로 만들 수 있으면서, 학교 밖 콘텐츠를 같은 구조로 추가할 수 있다. 단점은 초기 설계가 조금 더 엄격해야 한다.

### Alternative: School-First Progression

`SchoolSubjectDefinition`, `ClassScheduleDefinition`, `StudySessionDefinition`을 먼저 만들고 학생 루프를 촘촘하게 구성한다. 초반 체감은 빠르지만, 알바/독학/취미/직업 경로를 붙일 때 학교 전용 모델을 다시 일반화해야 한다.

### Alternative: Career-First Progression

직업 후보와 해금 조건을 먼저 만들고 학생 활동은 직업 조건을 채우는 튜토리얼로 둔다. 장기 목표가 명확해지는 장점이 있지만, 사용자가 원한 “성향을 찾아나가는 느낌”보다 직업 최적화 게임처럼 보일 위험이 크다.

## Core Loop

1차 playable slice의 루프는 하루 단위로 동작한다.

1. 플레이어는 학생 상태로 하루를 시작한다.
2. 등교 활동을 선택하거나 학교/교실/NPC와 상호작용한다.
3. 공부 활동을 실행한다.
4. 서버 또는 싱글플레이 권한자가 비용과 조건을 검증한다.
5. 활동 효과가 시간, 체력, 집중도, 스트레스, 성향, 스킬에 적용된다.
6. 활동 로그가 기록된다.
7. 성향/스킬 조건을 만족하면 진로 힌트 또는 다음 활동 후보가 열린다.
8. 하루 종료 시 활동 이력 기반 routine rule이 story flag, life event, career hint를 갱신한다.

학교 밖 활동은 같은 루프를 사용한다. 예를 들어 알바는 돈과 책임감, 피로를 바꾸고, 독학은 집중도와 기술 스킬을 바꾸며, 동네 활동은 관계와 평판을 바꾼다.

## Data Model

### `LifeActivityDefinition`

도시 생활 활동의 공통 정의다. 기존 `ModernActivityDefinition`의 후속 모델로 둔다.

필드 후보:

- display name/localization key
- activity category: school, work, hobby, errand, social, rest, self-study
- location requirement
- time cost
- energy/focus/stress cost
- optional required item/tool
- `ActivityRequirementBase[]`
- `ActivityEffectBase[]`
- optional group activity policy
- optional quest/story/life event tags

category는 UI 필터와 콘텐츠 분류용으로만 사용한다. C# 분기 조건으로 사용하지 않는다.

### `ActivityRequirementBase`

활동 실행 가능 여부를 검증하는 SO 전략이다.

예시:

- `StatusThresholdRequirement`
- `ScheduleWindowRequirement`
- `LocationRequirement`
- `RequiredItemRequirement`
- `TraitThresholdRequirement`
- `SkillThresholdRequirement`
- `StoryFlagRequirement`
- `RelationshipRequirement`

### `ActivityEffectBase`

활동 결과를 적용하는 SO 전략이다.

예시:

- `StatusDeltaEffect`
- `TraitDeltaEffect`
- `SkillProgressEffect`
- `MoneyDeltaEffect`
- `RelationshipDeltaEffect`
- `StoryFlagSetEffect`
- `CareerHintUnlockEffect`
- `QuestEventEmitEffect`
- `ActivityLogEffect`

### `TraitDefinition`

성향 정의다. 성향은 고정 직업 선택지가 아니라 플레이 경향의 누적 신호다.

초기 후보:

- 성실함
- 창의성
- 사교성
- 집중력
- 체력
- 호기심
- 책임감
- 독립성

성향 변화는 `TraitDeltaEffect`가 처리한다. 성향별 C# 분기 대신 `CareerUnlockRequirementBase`가 데이터로 조건을 표현한다.

### `SkillDefinition`

학습/생활/직무 스킬이다.

초기 후보:

- 기초 학습
- 언어
- 수학/논리
- 디지털 활용
- 예술 표현
- 서비스 응대
- 운동
- 자기관리

### `CareerDefinition`

진로 또는 직업 후보 정의다. 직업은 플레이어가 누르는 고정 클래스가 아니라 조건을 만족하며 힌트, 체험, 제안, 면접, 채용 단계로 열리는 데이터다.

필드 후보:

- display name/localization key
- career category
- `CareerUnlockRequirementBase[]`
- related traits
- related skills
- preview activities
- starting quest or event
- rewards/unlocks

초기 후보는 학생 루프와 가까운 것부터 둔다.

- 동아리 리더
- 학업 우수 학생
- 동네 알바생
- 독학 개발자 지망
- 디자인/창작 지망
- 서비스직 지망
- 교사/멘토 지망
- 프리랜서 지망

### Save Data

개인 성장과 공유 월드 상태를 분리한다.

`PlayerLifeProgressSaveData`:

- saveSlot
- player identity
- performed activity keys
- trait values
- skill progress
- unlocked career hints
- claimed personal rewards
- personal activity log

`SharedLifeWorldStateSaveData`:

- saveSlot
- school schedule state
- shared event flags
- shared NPC/classroom availability
- group activity records

## Multiplayer Authority

학생 활동 실행은 서버 권한으로 처리한다.

1. 클라이언트가 활동 요청을 보낸다.
2. 서버가 player identity, saveSlot, activity definition, 위치, 시간, 상태 비용, 선행 조건을 검증한다.
3. 서버가 활동 결과를 확정한다.
4. 서버가 개인 상태와 공유 상태를 분리해 갱신한다.
5. 서버가 필요한 클라이언트에만 결과를 브로드캐스트한다.
6. 클라이언트 UI는 서버 확정 결과를 기준으로 갱신한다.

권장 런타임 경계:

- `LifeActivityRunner`: 싱글플레이와 서버에서 공통으로 사용하는 활동 검증/효과 적용 서비스
- `LifeActivityRequest`: 클라이언트 요청 DTO
- `LifeActivityResult`: 서버 확정 결과 DTO
- `PlayerLifeProgress`: 플레이어별 개인 성장 상태
- `SharedLifeWorldState`: saveSlot별 공유 학교/도시 상태
- `LifeActivityNetworkStateBroadcaster`: 퀘스트 브로드캐스터와 같은 중복 억제/카운터 패턴

중복 억제 키에는 최소한 saveSlot, player identity, activity definition, request id, result kind, activity sequence를 포함한다. 같은 요청이 재전송되어도 성향, 스킬, 보상, 진로 힌트가 중복 적용되지 않아야 한다.

## Shared Versus Personal State

멀티플레이어에서 가장 중요한 경계는 개인 상태와 공유 상태의 분리다.

개인 상태:

- 성향
- 스킬
- 진로 힌트
- 개인 활동 로그
- 개인 보상 수령 상태
- 개인 퀘스트/생활 이벤트 진행도

공유 상태:

- 오늘의 학교 시간표
- 교실 또는 학교 시설 활성 상태
- 공용 NPC의 위치와 상호작용 가능 상태
- 그룹 활동의 공용 완료 기록
- saveSlot 단위 도시/학교 이벤트 플래그

같은 수업을 여러 플레이어가 함께 들어도 결과는 섞이지 않는다. 예를 들어 수업 참여라는 공유 기록은 하나일 수 있지만, Player 1은 집중력이 올라가고 Player 2는 사교성이 올라가는 식의 개인 결과가 가능해야 한다.

## First Playable Slice

1차 구현 단위는 다음으로 제한한다.

1. 학생 시작 데이터가 registry에 등록된다.
2. Town 또는 학교 테스트 씬에서 등교 활동이 가능하다.
3. 공부 활동이 가능하다.
4. 공부 활동은 시간/체력/집중도 같은 상태 비용을 소비한다.
5. 공부 활동은 성향 또는 스킬을 데이터 기반 효과로 증가시킨다.
6. 일정 기준을 넘으면 진로 힌트 1개가 열린다.
7. 개인 진행도는 saveSlot과 player identity로 분리된다.
8. host/client 테스트에서 서버가 활동 결과를 확정하고 요청 플레이어에게 반영한다.

권장 1차 데이터:

- `Activity_AttendSchool`
- `Activity_StudyBasics`
- `Trait_Diligence`
- `Trait_Curiosity`
- `Skill_BasicStudy`
- `CareerHint_StudyPath`
- `Status_Focus`
- `Status_Energy`
- `Status_Stress`

## Content Expansion Axes

### School

- 과목별 수업
- 시험과 수행평가
- 동아리
- 친구/선생님 관계
- 진로 상담
- 학교 행사
- 지각/결석/조퇴

### Non-School

- 아르바이트
- 독학
- 자격증 준비
- 취미 창작
- 운동
- 동네 심부름
- 멘토 찾기
- 조기 취업
- 검정고시 또는 대체 교육

### Career

- 회사원
- 개발자
- 디자이너
- 요리사
- 교사
- 연구자
- 간호/돌봄
- 경찰/공공직
- 예술가
- 창업가
- 서비스직
- 프리랜서

## Testing Strategy

### EditMode

- `LIFE-STUDENT-001`: 학생 시작 데이터가 registry에 등록되어 있고 null 참조가 없다.
- `LIFE-STUDENT-002`: 등교 활동 실행 시 시간 또는 일정 상태가 갱신된다.
- `LIFE-STUDENT-003`: 공부 활동 실행 시 집중도/체력/성향/스킬 중 정의된 효과가 데이터 기반으로 적용된다.
- `LIFE-STUDENT-004`: 같은 activity id에 대한 C# `if`/`switch` 분기 없이 `ActivityEffectBase[]`로 결과가 적용된다.
- `LIFE-STUDENT-005`: 성향 누적값에 따라 최소 1개 이상의 진로 힌트가 열린다.
- `LIFE-STUDENT-006`: 학교 활동과 비학교 활동이 같은 activity runner에서 처리된다.
- `LIFE-STUDENT-007`: 활동 보상 또는 해금은 중복 지급되지 않고 멱등성을 유지한다.
- `LIFE-STUDENT-008`: 저장/로드 후 학생 활동 진행도, 성향, 스킬, 열린 진로 힌트가 유지된다.
- `LIFE-STUDENT-009`: saveSlot A의 성향/진로 진행도가 saveSlot B에 섞이지 않는다.
- `LIFE-STUDENT-010`: entity ID 분기 금지 CI가 통과한다.

### PlayMode

- Town 또는 학교 테스트 씬에서 플레이어가 등교 상호작용을 수행한다.
- 공부 활동 실행 후 HUD 또는 활동 로그 UI가 서버 확정 결과를 표시한다.
- 하루 종료 또는 활동 로그에서 성향/스킬 변화와 진로 힌트를 확인할 수 있다.
- scene load, object binding, network spawn은 임의 대기 시간이 아니라 명확한 완료 조건을 기다린다.

### Multiplayer

- `LIFE-STUDENT-NET-001`: host/client 구성에서 클라이언트가 등교 또는 공부 활동을 요청하면 서버가 조건을 검증하고 결과를 확정한다.
- `LIFE-STUDENT-NET-002`: 서버 확정 결과가 요청 플레이어의 클라이언트 UI와 서버 저장 상태에 반영된다.
- `LIFE-STUDENT-NET-003`: Player 1의 성향/스킬/진로 힌트 변화가 Player 2의 개인 성장 상태를 변경하지 않는다.
- `LIFE-STUDENT-NET-004`: 같은 수업 또는 그룹 활동에 여러 플레이어가 참여해도 공유 상태와 개인 상태가 분리된다.
- `LIFE-STUDENT-NET-005`: 같은 활동 요청이 중복 전송되어도 성향/스킬/보상/해금이 중복 적용되지 않는다.
- `LIFE-STUDENT-NET-006`: 서버가 비용 부족, 위치 불일치, 선행 조건 부족 때문에 활동 요청을 거절하면 개인 성장 상태와 보상 상태가 변경되지 않는다.
- `LIFE-STUDENT-NET-007`: 저장/로드 후 같은 saveSlot, 같은 player identity의 학생 활동 진행도와 진로 힌트가 복원된다.
- `LIFE-STUDENT-NET-008`: saveSlot A와 saveSlot B 사이에 개인 성장 상태와 공유 학교 상태가 섞이지 않는다.
- `LIFE-STUDENT-NET-009`: 재접속 또는 클라이언트 재동기화 후 서버 확정 상태와 클라이언트 표시 상태가 일치한다.
- `LIFE-STUDENT-NET-010`: 활동 결과 브로드캐스트가 상태 변경 단위로만 발생하고, 중복 요청이나 무효 요청이 불필요한 추가 브로드캐스트를 만들지 않는다.
- `LIFE-STUDENT-NET-011`: 플레이어 수 증가 시 네트워크 메시지/상태 갱신 횟수가 중복 폭증하지 않는지 성능 회귀 카운터로 검증한다.
- `LIFE-STUDENT-NET-012`: 기존 NPC/Dialogue/Quest 멀티플레이 테스트와 충돌하지 않고 함께 통과한다.

## Verification Gates

- 신규 C#을 작성한다면 반드시 Unity MCP `script-update-or-create`를 사용한다.
- `Assets/**/*.cs`는 외부 디스크 직접 쓰기로 만들거나 수정하지 않는다.
- 신규 데이터는 `Assets/Data/{Traits,Status,Knowledge,Generations}` 기존 폴더 또는 새 `Assets/Data/LifeActivities`, `Assets/Data/Careers`, `Assets/Data/Skills`, `Assets/Data/School`에 둔다.
- 모든 SO는 `Assets/Data/Registry/GameDataRegistry.asset` 또는 대응 registry에 등록한다.
- `Scripts/ci/check-no-entity-id-branching.sh`를 실행한다.
- 멀티플레이어 테스트를 실행하지 못하면 완료로 보고하지 않는다. 실행 불가 사유와 남은 리스크를 명시한다.

## Risks

| Risk | Mitigation |
| --- | --- |
| 기존 `ModernActivityKind` enum이 활동 확장을 제한함 | enum은 UI 분류용 legacy로만 보고, 신규 실행 로직은 SO 전략 배열을 사용 |
| 학교 루프가 단일 필수 경로처럼 굳어짐 | 학교와 비학교 활동을 같은 `LifeActivityDefinition`으로 모델링 |
| 개인 성장 상태가 멀티플레이에서 섞임 | saveSlot + player identity를 저장/동기화 키로 강제 |
| 그룹 활동 결과와 개인 효과가 혼재됨 | 공유 상태와 개인 상태 저장 클래스를 분리 |
| 브로드캐스트가 플레이어 수에 따라 폭증함 | 퀘스트 브로드캐스터처럼 중복 억제 키와 카운터 기반 회귀 테스트 도입 |
| 직업이 하드코딩 선택지가 됨 | `CareerDefinition` + `CareerUnlockRequirementBase[]`로만 해금 |

## Approval Gate

이 spec이 승인되면 다음 단계는 `docs/superpowers/plans/2026-05-08-student-career-life-implementation-plan.md`를 작성하는 것이다. 구현 계획 전에는 code edit, scene/prefab 변경, importer 변경을 하지 않는다.
