# Modular Portability Audit Goal

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```md
ROOTBORN Unity 프로젝트에서 현재 구현된 게임 시스템들이 "현재 게임을 만들기 위한 임시 결합 구조"가 아니라, 다른 Unity 게임에도 접목 가능한 모듈형 구조로 설계/구현되어 있는지 전수조사해줘.

이번 goal은 구현 작업이 아니라 **모듈화·재사용성·이식성 감사(audit)** 작업이다. 코드 수정, 씬 수정, SO 수정은 하지 말고, 조사 결과와 개선 제안 문서를 작성하는 데 집중한다.

## 절대 규칙

- AGENTS.md와 `.claude/rules/`의 헌법급 원칙을 먼저 읽고 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. 이번 goal에서는 원칙적으로 코드 수정하지 않는다.
- 모든 게임 엔티티는 ScriptableObject 데이터 기반이어야 한다.
- 특정 activityId, cropId, toolId, questId, locationId, careerId 등 엔티티 ID별 `if`/`switch`/enum 분기가 있으면 모듈화 위반으로 기록한다.
- Objective/Quest/Campaign/Hint UI는 `ObjectiveJournalPanel` 소유권 규약을 따라야 한다.
- 감사 중 발견한 문제를 즉시 고치지 말고, 심각도·영향 범위·개선 goal 후보로 분리한다.

## 조사 목적

다음 질문에 답한다.

1. ROOTBORN의 각 시스템이 다른 Unity 2D 게임에 재사용 가능한 독립 모듈로 분리되어 있는가?
2. 게임 고유 콘텐츠와 범용 시스템 코드가 분리되어 있는가?
3. ScriptableObject 데이터만 추가해 새 콘텐츠/규칙/보상/조건/UI 항목을 확장할 수 있는가?
4. 특정 게임 콘셉트, 씬, 에셋, ID, 텍스트, 경로, 입력 흐름에 하드 결합된 부분은 어디인가?
5. 다른 게임에 접목하려면 어떤 모듈을 그대로 재사용할 수 있고, 어떤 모듈은 분리/추상화/재설계가 필요한가?
6. 모듈화 개선이 필요한 경우, 당장 해야 할 것과 나중에 해도 되는 것을 어떻게 나눌 수 있는가?

## 감사 범위

최소한 다음 영역을 전수조사한다.

- `Assets/Scripts/Game`
  - Common/Registry
  - Quests
  - StudentLife
  - WorldState
  - WorldGeneration
  - Save
  - Time
  - Inventory/Rewards/Knowledge/Traits/Status/Careers/Jobs/Relationships/Locations/Tools/Resources/Recipes 관련 코드
- `Assets/Scripts/UI`
  - Objective Journal
  - Quest/Campaign/Goal/Hint UI
  - WorldState UI
  - Modern UI Style2 공통 패널 사용 여부
- `Assets/Scripts/Network`
  - 서버 권한 처리
  - player별 상태와 shared world state 분리
  - saveSlot/player identity 결합도
- `Assets/Data`
  - ScriptableObject 데이터 경로와 Registry 등록 상태
  - 시스템 코드 없이 데이터만으로 확장 가능한지
- `Assets/Tests/EditMode`, `Assets/Tests/PlayMode`
  - 모듈 경계, 데이터 기반 확장, 실제 플레이 경로 검증이 테스트로 보호되는지
- `Scripts/ci`
  - entity ID branching 금지 게이트
  - scenario registry sync 등 모듈화 규칙을 강제하는 CI 존재 여부
- `docs/superpowers/goals`, `docs/superpowers/specs`
  - 최근 goal들이 모듈화 원칙과 일관되는지

## 판정 기준

각 시스템을 다음 등급으로 분류한다.

- **A: Portable Core**
  - ROOTBORN 고유 콘텐츠 없이도 다른 Unity 게임에서 재사용 가능
  - SO 데이터/전략 배열/인터페이스로 확장 가능
  - 씬·프리팹·특정 ID·특정 UI에 직접 결합되지 않음
- **B: Reusable With Adapter**
  - 핵심 로직은 재사용 가능하지만 입력, UI, 저장, 네트워크, Registry 어댑터가 필요
- **C: Game-Specific Module**
  - ROOTBORN 콘셉트나 데이터에는 적합하지만 다른 게임에 쓰려면 분리 작업이 큼
- **D: Hard-Coupled / Refactor Needed**
  - 특정 ID, 씬, 프리팹, 에셋, 텍스트, enum, switch, singleton, 직접 참조가 강함
  - 데이터만으로 확장할 수 없음
- **E: Unknown / Needs Runtime Verification**
  - 정적 조사만으로 판단 불가. PlayMode 또는 실제 사용자 흐름 검증 필요

## 중점 점검 항목

다음을 반드시 확인한다.

1. **데이터/로직 분리**
   - 콘텐츠 값, 보상, 조건, unlock, location, quest, activity, career, clue, hidden path 등이 SO 데이터로 빠져 있는가?
   - 시스템 코드가 특정 엔티티 ID를 알고 있는가?

2. **전략 배열 구조**
   - 조건/결과/보상/트리거/정책이 `Base[]` 전략 배열로 표현되는가?
   - 새 전략 추가와 새 데이터 추가의 경계가 명확한가?

3. **Registry 의존성**
   - `GameDataRegistry.asset` 또는 동등 Registry를 통해 로드되는가?
   - `Resources.Load`, 씬 직접 검색, 이름 기반 검색에 과도하게 의존하지 않는가?

4. **UI 모듈성**
   - UI가 도메인별 Canvas/좌표/크기를 직접 소유하지 않는가?
   - Objective/Quest/Campaign/Hint류 UI가 Objective Journal 규약을 따르는가?
   - Style2 공통 패널 규약을 지키는가?
   - 대량 목록 UI가 가상화/페이징/캐싱/dirty 갱신을 고려하는가?

5. **Network 모듈성**
   - 서버 권한, 클라이언트 표시, 저장 데이터, 브로드캐스트가 분리되어 있는가?
   - 개인 상태와 공유 상태가 명확히 분리되어 있는가?
   - 다른 게임의 네트워크 계층으로 바꿔 끼울 수 있는 경계가 있는가?

6. **Save/Load 모듈성**
   - saveSlot, player identity, shared state가 범용적으로 분리되어 있는가?
   - 특정 시스템 저장 포맷이 다른 시스템을 직접 침범하지 않는가?

7. **테스트 모듈성**
   - 데이터만 추가해서 새 콘텐츠가 동작하는 테스트가 있는가?
   - 실제 사용자 흐름을 우회하지 않는 PlayMode 테스트가 있는가?
   - 모듈 경계가 깨졌을 때 실패하는 테스트가 있는가?

8. **성능/확장성**
   - 대량 데이터에서 전체 스캔, 매 프레임 rebuild, 런타임 문자열 할당, Instantiate/Destroy 루프가 있는가?
   - 캐시/인덱스/이벤트 기반 dirty 갱신이 설계되어 있는가?

## 조사 방법

1. 관련 규칙과 문서를 먼저 읽는다.
2. `rg`로 다음 패턴을 검색한다.
   - `if (.*Id`
   - `switch`
   - `Resources.Load`
   - `FindObjectOfType`
   - `GameObject.Find`
   - `GetComponent` in `Update`
   - hardcoded scene names
   - hardcoded asset paths
   - hardcoded localization/display strings
   - direct quest/activity/location/career/tool/crop IDs
3. asmdef 경계와 namespace 경계를 확인한다.
4. 각 도메인별 SO 정의, SO 인스턴스, Registry 등록, 런타임 소비 경로를 추적한다.
5. 테스트가 실제 모듈 경계를 보호하는지 확인한다.
6. `Scripts/ci/check-no-entity-id-branching.sh`를 실행 가능한 환경이면 실행하고 결과를 기록한다.
7. 필요하면 Unity MCP로 asset/registry/test 정보를 조회하되, 코드/에셋 수정은 하지 않는다.

## 산출물

다음 문서를 작성한다.

`docs/superpowers/audits/YYYY-MM-DD-modular-portability-audit.md`

문서에는 반드시 다음 섹션을 포함한다.

1. **Executive Summary**
   - 전체 판정
   - 가장 재사용 가능한 모듈 TOP 5
   - 가장 결합도가 높은 위험 모듈 TOP 5

2. **Module Portability Matrix**
   - 모듈명
   - 등급 A/B/C/D/E
   - 재사용 가능 범위
   - 결합 지점
   - 필요한 adapter/refactor
   - 관련 파일

3. **Data-Driven Compliance**
   - SO 데이터화가 잘 된 영역
   - ID 분기/enum/switch/하드코딩 의심 지점
   - Registry 누락 또는 직접 로드 지점

4. **UI Ownership Audit**
   - Objective Journal 규약 준수 여부
   - HUD/Canvas 중복 여부
   - Style2 공통 패널 규약 준수 여부
   - 다른 게임에 UI만 이식할 때 필요한 분리 작업

5. **Network and Save Boundary Audit**
   - 서버 권한 경계
   - 개인/공유 상태 분리
   - saveSlot/player identity 의존성
   - 다른 네트워크 계층으로 옮길 때의 위험

6. **Testing and CI Coverage**
   - 모듈화 위반을 잡는 테스트/CI
   - 부족한 테스트
   - 실제 플레이 경로 검증 공백

7. **Performance and Scale Risks**
   - 대량 데이터/대량 UI/멀티플레이 확장 시 위험
   - 캐싱/가상화/dirty 갱신 필요 지점

8. **Recommended Follow-Up Goals**
   - 즉시 해야 할 refactor goal
   - 다음 기능 전에 해야 할 guardrail goal
   - 나중에 해도 되는 정리 goal
   - 각 goal은 제목, 목적, 영향 파일, 테스트 요구사항, 완료 조건을 포함한다.

9. **Final PASS/FAIL Checklist**
   - "다른 Unity 게임에 접목 가능한 모듈 구조인가?"에 대한 최종 판정
   - PASS/FAIL/NEEDS WORK로 항목별 기록

## 완료 조건

- 감사 문서가 작성되어 있다.
- 각 주요 시스템이 A/B/C/D/E 중 하나로 분류되어 있다.
- 파일 경로와 근거가 없는 추상적 평가는 하지 않는다.
- 최소 5개 이상의 구체적인 결합 위험과 최소 5개 이상의 재사용 가능한 모듈 후보를 기록한다.
- 후속 goal 후보가 우선순위별로 정리되어 있다.
- 최종 보고에는 작성 문서 경로, 실행한 검색/검증 명령, 실행하지 못한 검증, 남은 리스크를 포함한다.
```
