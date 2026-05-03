---
name: dev-iteration-loop
description: "★핵심★ Unity 개발 반복 루프 오케스트레이터. 개발→빌드→실제 테스트→결과 분석→수정→재빌드를 자동 반복. 빌드 통과 + 전체 테스트 통과까지 iteration-coordinator가 팀을 지휘. 기능 구현 후 안정화, 회귀 수정, TDD 사이클, '빌드 될 때까지 고쳐줘'·'테스트 통과시켜줘'·'이거 돌아갈 때까지' 류 요청에서 반드시 사용. 최대 반복 횟수 기본 5회."
---

# Dev Iteration Loop — 개발 반복 자동화 오케스트레이터

Unity 2D 프로젝트의 **개발→빌드→실제 테스트→결과 도출→수정** 루프를 자동화한다. 하네스의 핵심 스킬이며, 실제 동작 가능한 산출물이 나올 때까지 팀이 반복한다.

## 실행 모드: 에이전트 팀

## 에이전트 구성

| 팀원 | agent_type | 역할 |
|------|-----------|------|
| iteration-coordinator | iteration-coordinator | **리더**. 루프 상태 관리, 종료/상승 판단 |
| unity-architect       | unity-architect       | 설계 결함 발견 시 재설계 |
| csharp-developer      | csharp-developer      | 초기 구현 + 변경 |
| unity-builder         | unity-builder         | CLI 빌드 실행 |
| unity-test-runner     | unity-test-runner     | EditMode/PlayMode 실행 |
| test-result-analyzer  | test-result-analyzer  | 실패 패턴 → 근본 원인 가설 |
| bug-fixer             | bug-fixer             | 실제 코드 수정 적용 |
| test-designer         | test-designer         | 회귀 테스트 작성 |
| qa-inspector          | qa-inspector          | 증분 경계면 검증 |

## 루프 구조

```
┌──────────────────────────────────────────────────────┐
│ iteration-N                                          │
│                                                      │
│  ① 개발/수정     csharp-developer or bug-fixer        │
│         │                                            │
│  ② 빌드         unity-builder                        │
│         │                                            │
│         ├── 실패 → test-result-analyzer → ①로 복귀    │
│         │                                            │
│         └── 성공                                     │
│  ③ 테스트 실행   unity-test-runner (EditMode → PlayMode)│
│         │                                            │
│  ④ 증분 QA       qa-inspector (경계면 검증 병렬)       │
│         │                                            │
│  ⑤ 결과 분석     test-result-analyzer                 │
│         │                                            │
│         ├── 전체 통과 + QA 통과 → 종료                 │
│         │                                            │
│         ├── 개선 중 (실패 수 감소) → iteration-(N+1)    │
│         │                                            │
│         └── 정체/악화 → iteration-coordinator 판단:    │
│               ├── 설계 결함 → unity-architect 호출     │
│               └── 루프 한도 초과 → 사용자에게 보고      │
└──────────────────────────────────────────────────────┘
```

## 워크플로우

### Phase 1: 준비

1. 사용자 입력 파악 — 신규 기능 요청? 기존 버그 수정? 테스트 통과 목표?
2. `.claude/constitution.md`, `.claude/rules/*.md` 로드
3. `_workspace/` 생성, 초기 `_workspace/loop_state.json` 작성:
   ```json
   {
     "iteration": 0,
     "max_iterations": 5,
     "goal": "{사용자 목표 한 줄}",
     "status": "starting"
   }
   ```
4. 기존 코드 상태 스냅샷 — git이 있으면 현재 커밋, 없으면 건드리기 전 파일 해시

### Phase 2: 팀 구성

```
TeamCreate(
  team_name: "dev-iteration-team",
  leader: "iteration-coordinator",
  members: [
    { name: "iteration-coordinator", agent_type: "iteration-coordinator", model: "opus",
      prompt: "루프 상태 관리. 각 iteration 종료 시 loop_state.json 갱신. 최대 5회." },
    { name: "unity-architect",  agent_type: "unity-architect",  model: "opus",
      prompt: "초기 설계 + 설계 결함 escalation 수신 시 재설계." },
    { name: "csharp-developer", agent_type: "csharp-developer", model: "opus",
      prompt: "설계 구현 + bug-fixer 부재 시 직접 수정." },
    { name: "unity-builder",    agent_type: "unity-builder",    model: "opus",
      prompt: "unity-cli-reference 로드. 매 iteration 끝에 빌드 실행." },
    { name: "unity-test-runner",agent_type: "unity-test-runner",model: "opus",
      prompt: "빌드 성공 시 EditMode → PlayMode 실행. XML 결과 수집." },
    { name: "test-result-analyzer", agent_type: "test-result-analyzer", model: "opus",
      prompt: "실패 패턴 분석 → 근본 원인 가설 → bug-fixer에게 전달." },
    { name: "bug-fixer",        agent_type: "bug-fixer",        model: "opus",
      prompt: "가설 검증 후 최소 침습적 수정. 회귀 테스트는 test-designer에게 요청." },
    { name: "test-designer",    agent_type: "test-designer",    model: "opus",
      prompt: "회귀 테스트 작성. bug-fixer 수정 시 쌍으로 동작." },
    { name: "qa-inspector",     agent_type: "qa-inspector",     model: "opus",
      prompt: "빌드 성공 후 테스트와 병렬로 경계면 증분 검증." }
  ]
)
```

### Phase 3: 루프 실행

iteration-coordinator가 매 iteration마다 작업을 등록:

```
# Iteration N 시작
TaskCreate([
  { title: "iter-N: 개발/수정",  assignee: "<csharp-developer or bug-fixer>" },
  { title: "iter-N: 빌드",       assignee: "unity-builder",       depends_on: ["iter-N: 개발/수정"] },
  { title: "iter-N: 테스트",     assignee: "unity-test-runner",   depends_on: ["iter-N: 빌드"] },
  { title: "iter-N: QA",         assignee: "qa-inspector",        depends_on: ["iter-N: 빌드"] },
  { title: "iter-N: 분석",       assignee: "test-result-analyzer",depends_on: ["iter-N: 테스트"] }
])
```

**단계별 동작:**

#### ① 개발/수정
- iteration 0: `unity-architect` → `csharp-developer` (초기 구현)
- iteration ≥1: `bug-fixer`가 직전 iteration의 근본 원인 가설을 받아 수정

#### ② 빌드 (unity-builder)
- `_workspace/iteration-N/build_report.md`
- 실패 시: 컴파일 에러를 test-result-analyzer에게 전달 (테스트 단계 건너뛰고 바로 ⑤로)

#### ③ 테스트 (unity-test-runner)
- EditMode → PlayMode 순서
- 결과: `Logs/test-editmode.xml`, `Logs/test-playmode.xml`
- `_workspace/iteration-N/test_results.md`

#### ④ QA (qa-inspector) — 테스트와 병렬
- 이번 iteration에서 수정된 파일의 경계면 교차 비교
- `_workspace/iteration-N/qa_report.md`

#### ⑤ 분석 (test-result-analyzer)
- 빌드 실패 + 테스트 실패 + QA 이슈 종합
- `_workspace/iteration-N/analysis.md` 생성
- iteration-coordinator에게 요약 SendMessage

#### iteration-coordinator 판단
`loop_state.json` 갱신 후 다음 중 하나:

| 상황 | 조치 |
|------|------|
| 빌드 성공 + 전체 테스트 통과 + QA 통과 | **종료 (성공)** |
| 개선 중 (실패 수 감소) | 다음 iteration 진행 |
| 정체 (2회 연속 같은 실패) | test-result-analyzer에게 재분석 요청 |
| 설계 결함 추정 (2회 연속 수정 실패) | unity-architect 호출, 재설계 후 재구현 |
| max_iterations 도달 | **종료 (중단)** + 사용자 보고 |
| 팀원 연속 실패 | 에스컬레이션 (사용자 보고) |

### Phase 4: 회귀 테스트 추가
루프 종료 시 bug-fixer가 수정한 버그마다 test-designer에게 회귀 테스트 작성 요청. 한 번 더 unity-test-runner 실행하여 회귀 테스트가 통과하는지 확인.

### Phase 5: 정리
1. `_workspace/loop_final_report.md` 생성:
   - 총 iteration 수
   - 최종 상태 (성공/중단/에스컬레이션)
   - iteration별 실패 수 추이 그래프(텍스트)
   - 해결된 이슈 목록
   - 남은 이슈 목록 (있다면)
2. 팀 해체
3. `_workspace/iteration-*` 보존

## 상태 파일 스키마: `_workspace/loop_state.json`

```json
{
  "iteration": 3,
  "max_iterations": 5,
  "goal": "PlayerController 기본 구현 + 테스트 전체 통과",
  "last_build": { "status": "success", "warnings": 2 },
  "last_test": { "total": 42, "passed": 40, "failed": 2, "platform": "PlayMode" },
  "last_qa": { "issues": 0 },
  "failure_trend": [8, 5, 3, 2],
  "escalations": [],
  "status": "in_progress"
}
```

`failure_trend`는 iteration마다 (빌드 에러 + 테스트 실패 + QA 이슈) 합을 누적. 단조 감소 → 진전 중, 정체/증가 → 개입 필요.

## 데이터 흐름

```
[사용자 목표]
    ↓
iteration-coordinator ─ TaskCreate ─→ 개발/수정 에이전트
                                         ↓
                                    unity-builder ─→ Builds/, build_report.md
                                         ↓
                    ┌───────── 성공 ─────┴────── 실패 ─────┐
                    ↓                                      │
        unity-test-runner ─→ Logs/test-*.xml               │
                    ↓                                      │
        qa-inspector (병렬)                                │
                    ↓                                      │
        test-result-analyzer ←────────────────────────────┘
                    ↓
        iteration-coordinator (판단)
                    ↓
         [계속 | 에스컬레이션 | 종료]
```

## 에러 핸들링

| 상황 | 전략 |
|------|------|
| 매 iteration 빌드 실패 | 3회 연속 실패 시 csharp-developer가 아닌 architect 호출 |
| 동일 테스트 반복 실패 | test-result-analyzer에게 재분석, 가설 교체 |
| 팀원 무응답 | 리더가 SendMessage로 상태 확인, 재시작 또는 재할당 |
| 수정 후 새 실패 등장 | 회귀로 간주, 원인 추적 (직전 수정 롤백 고려) |
| 사용자 목표 불명 | iteration 0에서 목표 재확인 요청 후 시작 |

## 종료 조건 (명확히)

**성공 종료:**
- 빌드 exit code 0 + 경고 수 허용치 이하
- EditMode 테스트 100% 통과
- PlayMode 테스트 100% 통과
- qa-inspector 이슈 0건 (P0)

**실패 종료 (에스컬레이션):**
- `max_iterations` 도달했는데 위 조건 미충족
- 설계 결함이 근본 원인인데 재설계 이후에도 실패
- 팀원 과반 실패

## 테스트 시나리오

### 정상 흐름 (3 iteration)
1. 목표: "적 AI 기본 구현 + 테스트 통과"
2. iter-0: architect 설계 → csharp-developer 구현 → 빌드 성공 → 테스트 5/8 통과 → 분석 "순찰 경로 초기화 오류"
3. iter-1: bug-fixer 수정 → 빌드 성공 → 테스트 7/8 통과 → 분석 "시야 각도 off-by-one"
4. iter-2: bug-fixer 수정 → 빌드 성공 → 테스트 8/8 통과 → QA 통과 → 회귀 테스트 3개 추가 → 재실행 통과
5. 종료: loop_final_report.md 작성
6. failure_trend: [3, 1, 0]

### 에러 흐름 (에스컬레이션)
1. 목표: "플레이어 점프 구현"
2. iter-0~2: 수정해도 같은 PlayMode 테스트 3개 실패 반복 (failure_trend: [3, 3, 3])
3. iteration-coordinator 판단 → 정체 감지 → test-result-analyzer에게 재분석 요청
4. 재분석: "Rigidbody2D의 Gravity Scale이 0이어서 점프 후 낙하 안 됨 → 설계 결함"
5. unity-architect 호출 → PlayerStats SO에 gravityScale 추가 → 재구현
6. iter-3: 빌드 성공 → 테스트 통과 → 종료
