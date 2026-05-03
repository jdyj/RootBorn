---
name: unity-game-dev
description: "Unity 2D 게임 기능 개발 오케스트레이터. 설계→구현→Scene 조립→테스트 작성→리뷰를 에이전트 팀으로 조율. 신규 게임 시스템(플레이어 컨트롤러, 적 AI, 아이템, UI, 스테이지 등)을 만들 때 반드시 사용. '유니티 기능 추가', 'Unity feature', '새 시스템 만들어줘' 류 요청에서 트리거."
---

# Unity Game Dev Orchestrator

Unity 2D 게임의 신규 기능/시스템을 설계부터 테스트까지 일괄 구현하는 오케스트레이터. 에이전트 팀 모드로 실행한다.

## 실행 모드: 에이전트 팀

## 에이전트 구성

| 팀원 | agent_type | 역할 | 주요 산출물 |
|------|-----------|------|-----------|
| unity-architect | unity-architect | 설계 결정, 클래스 구조 | `_workspace/01_architect_design.md` |
| csharp-developer | csharp-developer | C# 스크립트 구현 | `Assets/Scripts/**/*.cs` + `02_csharp_changes.md` |
| scene-builder | scene-builder | Prefab/Scene 구성 절차 또는 Editor 스크립트 | `Assets/Editor/SceneBuilders/*.cs`, `03_scene_steps.md` |
| test-designer | test-designer | 테스트 케이스 작성 | `Assets/Tests/**/*.cs` |
| qa-inspector | qa-inspector | 경계면 정합성 검증 | `05_qa_report.md` |
| code-reviewer | code-reviewer | 품질 리뷰 | `06_review.md` |
| (리더) | — | 조율 + 종합 | `final_report.md` |

## 워크플로우

### Phase 1: 준비
1. 사용자 요구사항 분석 — 무엇을 만드는지, 기존 시스템과의 접점
2. `.claude/constitution.md`, `.claude/rules/coding-standards.md` 로드
3. `_workspace/` 생성, 입력을 `_workspace/00_input/request.md`에 저장
4. 기존 `Assets/Scripts/` 구조 간단 파악 (Glob)

### Phase 2: 팀 구성

```
TeamCreate(
  team_name: "unity-game-dev-team",
  members: [
    { name: "unity-architect",   agent_type: "unity-architect",   model: "opus",
      prompt: "헌법·코딩규약 로드 후 요구사항 설계. _workspace/01_architect_design.md 작성." },
    { name: "csharp-developer",  agent_type: "csharp-developer",  model: "opus",
      prompt: "architect의 설계 문서 수신 후 Assets/Scripts/ 하위에 구현. _workspace/02_csharp_changes.md 요약." },
    { name: "scene-builder",     agent_type: "scene-builder",     model: "opus",
      prompt: "architect 설계에서 Scene/Prefab 부분 수행. 절차서 또는 Editor 자동화 스크립트 작성." },
    { name: "test-designer",     agent_type: "test-designer",     model: "opus",
      prompt: "구현 완료 후 EditMode/PlayMode 테스트 작성. unity-test-framework 스킬 참조." },
    { name: "qa-inspector",      agent_type: "qa-inspector",      model: "opus",
      prompt: "각 모듈 완성 직후 즉시 경계면 검증 수행. 양쪽 동시 읽기 원칙." },
    { name: "code-reviewer",     agent_type: "code-reviewer",     model: "opus",
      prompt: "전 단계 완료 후 헌법 기준 코드 리뷰. P0/P1/P2 severity." }
  ]
)
```

### Phase 3: 작업 등록

```
TaskCreate([
  { title: "설계 문서 작성",    assignee: "unity-architect" },
  { title: "C# 스크립트 구현",  assignee: "csharp-developer", depends_on: ["설계 문서 작성"] },
  { title: "Scene/Prefab 구성", assignee: "scene-builder",    depends_on: ["C# 스크립트 구현"] },
  { title: "테스트 작성",       assignee: "test-designer",    depends_on: ["C# 스크립트 구현"] },
  { title: "QA 증분 검증",      assignee: "qa-inspector",     depends_on: ["C# 스크립트 구현"] },
  { title: "코드 리뷰",         assignee: "code-reviewer",    depends_on: ["테스트 작성","QA 증분 검증"] }
])
```

### Phase 4: 실행 (자체 조율)

- architect가 설계 완료 → csharp-developer에게 SendMessage + scene-builder에게 Prefab 힌트
- csharp-developer 완료 시 test-designer와 qa-inspector 동시 트리거
- qa-inspector는 경계면 이슈 발견 즉시 해당 팀원에게 SendMessage
- test-designer 완료 → (필요 시) unity-test-runner 호출은 dev-iteration-loop에서 수행. 이 스킬은 테스트 작성까지만 담당
- code-reviewer는 모든 작업 완료 후 최종 리뷰

### Phase 5: 통합 및 정리
1. 모든 팀원 작업 완료 대기 (TaskGet)
2. 각 산출물 Read로 수집
3. `_workspace/final_report.md` 생성
   - 만든 파일 목록
   - 테스트 파일 목록
   - QA 통과 여부
   - 리뷰 P0/P1 항목
   - 다음 단계 제안 (`dev-iteration-loop` 또는 `unity-build-deploy` 호출)
4. 팀 정리 (TeamDelete)
5. `_workspace/` 보존

## 데이터 흐름

```
사용자 요구사항
    ↓
unity-architect → 01_architect_design.md
    ↓ SendMessage
csharp-developer → Assets/Scripts/ + 02_csharp_changes.md
    ↓ SendMessage (병렬 트리거)
    ├→ scene-builder  → Assets/Editor/ + 03_scene_steps.md
    ├→ test-designer  → Assets/Tests/
    └→ qa-inspector   → 05_qa_report.md
                          ↓ (이슈 발견 시)
                          ← csharp-developer 피드백 루프
code-reviewer → 06_review.md
    ↓
리더 → final_report.md
```

## 에러 핸들링

| 상황 | 전략 |
|------|------|
| architect 설계 모호 | 2~3 옵션 제시, 사용자 선택 대기 |
| 구현 실패 | 1회 재시도 후 architect에게 설계 재검토 요청 |
| QA 경계면 이슈 | 즉시 해당 에이전트에게 SendMessage, 수정 후 재검증 |
| 리뷰 P0 다수 | 팀 해체 전 csharp-developer에게 수정 요청 |
| 팀원 과반 실패 | 사용자에게 중단 보고 |

## 테스트 시나리오

### 정상 흐름
1. 사용자: "플레이어가 좌우 이동하는 기본 컨트롤러 만들어줘"
2. Phase 1: 요구 분석 → `_workspace/00_input/request.md` 저장
3. Phase 2: 6명 팀 생성
4. Phase 3: 6개 작업 등록
5. Phase 4: 
   - architect가 PlayerController, PlayerStats(SO), InputReader 설계
   - csharp-developer가 스크립트 3개 작성
   - scene-builder가 Prefab 생성 절차 또는 Editor 메뉴 스크립트 제공
   - test-designer가 이동 테스트(PlayMode) + 입력 파싱 테스트(EditMode) 작성
   - qa-inspector가 SerializeField 참조 연결 필요 항목 보고
   - code-reviewer가 P0 0건, P1 1건(경고) 보고
6. Phase 5: final_report.md 생성, 팀 정리
7. 결과: `Assets/Scripts/Player/*.cs`, `Assets/Tests/`, 테스트 작성 완료

### 에러 흐름
1. csharp-developer가 GetComponent in Update를 실수로 도입
2. code-reviewer가 P0로 보고
3. csharp-developer에게 SendMessage로 수정 요청
4. 수정 후 재리뷰
5. 통과 시 final_report에 수정 이력 기록
