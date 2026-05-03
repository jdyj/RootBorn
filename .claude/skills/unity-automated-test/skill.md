---
name: unity-automated-test
description: "Unity Test Framework 자동화 테스트 오케스트레이터. EditMode/PlayMode 테스트 케이스 작성→실행→결과 분석을 에이전트 팀으로 조율. 테스트 추가/실행/회귀 검증이 필요할 때 사용. '테스트 돌려줘', 'unit test', 'playmode test' 트리거."
---

# Unity Automated Test Orchestrator

Unity Test Framework 기반 자동화 테스트 작성·실행·분석을 통합 조율.

## 실행 모드: 에이전트 팀

## 에이전트 구성

| 팀원 | agent_type | 역할 |
|------|-----------|------|
| test-designer         | test-designer         | 테스트 케이스 작성 |
| unity-test-runner     | unity-test-runner     | CLI 실행 + JUnit XML 파싱 |
| test-result-analyzer  | test-result-analyzer  | 실패 패턴 분석, 근본 원인 가설 |
| (리더)                | —                     | 종합 + 보고 |

## 워크플로우

### Phase 1: 준비
1. 입력 파악 — 새 테스트 작성 vs 기존 실행 only
2. `_workspace/` 준비, `.claude/rules/unity-cli.md` 로드
3. `Assets/Tests/` 구조 확인 (asmdef 존재 여부)

### Phase 2: 팀 구성

```
TeamCreate(
  team_name: "unity-test-team",
  members: [
    { name: "test-designer", agent_type: "test-designer", model: "opus",
      prompt: "unity-test-framework 스킬 로드 후 대상 코드의 테스트 작성. Assembly Definition 확인." },
    { name: "unity-test-runner", agent_type: "unity-test-runner", model: "opus",
      prompt: "unity-cli-reference 스킬 로드. CLI batchmode로 테스트 실행. Editor 락 회피." },
    { name: "test-result-analyzer", agent_type: "test-result-analyzer", model: "opus",
      prompt: "JUnit XML 파싱 후 근본 원인 추정. 패턴 묶음 분석." }
  ]
)
```

### Phase 3: 작성 (선택)
신규 기능 테스트가 필요하면:
- test-designer가 EditMode 우선, PlayMode는 필요 시
- 작성 완료 후 unity-test-runner에게 SendMessage

### Phase 4: 실행
- unity-test-runner가 EditMode → PlayMode 순서로 실행
- 각 결과 XML을 `Logs/test-*.xml`에 저장
- 완료 후 test-result-analyzer에게 SendMessage

### Phase 5: 분석 및 보고
1. test-result-analyzer가 `_workspace/test_analysis.md` 생성
2. 실패가 있으면:
   - 근본 원인 가설 + 제안 수정
   - dev-iteration-loop 호출 또는 bug-fixer 직접 호출을 리더가 결정
3. 전체 통과 시 `_workspace/test_summary.md`에 통과 요약

### Phase 6: 정리
- 팀 해체
- `_workspace/`, `Logs/test-*.xml` 보존

## 데이터 흐름

```
[신규 테스트 필요 시] test-designer → Assets/Tests/*.cs
                                           ↓
unity-test-runner ─CLI→ Unity batchmode ─→ Logs/test-{platform}.xml
                                           ↓ SendMessage
                                    test-result-analyzer → _workspace/test_analysis.md
                                           ↓
                                     [실패 시 bug-fixer or dev-iteration-loop 호출]
```

## 에러 핸들링

| 상황 | 전략 |
|------|------|
| Unity Editor 실행 중 | 사용자에게 종료 요청, 대기 |
| 컴파일 에러로 테스트 실행 불가 | 빌드 로그에서 에러 추출, csharp-developer 호출 |
| XML 누락 | 로그 확인 후 재시도 1회 |
| 플레이키 의심 테스트 | 3회 반복 실행으로 확증 |
| 타임아웃 | PlayMode 15분, EditMode 5분 초과 시 중단 + 부분 결과 보고 |

## 테스트 시나리오

### 정상 흐름
1. 사용자: "전투 로직 테스트 실행"
2. Phase 2: 3명 팀 생성
3. Phase 3 생략 (기존 테스트)
4. Phase 4: EditMode 42개, PlayMode 8개 실행 → 모두 통과
5. Phase 5: test_summary.md 통과 기록
6. 정리

### 에러 흐름
1. PlayMode 테스트 3개 실패
2. test-result-analyzer가 공통 원인 = `PlayerController._rigidbody` 미할당으로 추정
3. 리더가 bug-fixer 호출 (별도 팀 구성) 또는 dev-iteration-loop 전환 제안
4. 사용자에게 보고
