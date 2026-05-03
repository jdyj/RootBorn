---
name: game-code-review
description: "Unity C# + 웹 TS 코드 리뷰 오케스트레이터. 헌법 준수/SOLID/성능 핫패스/보안/테스트 적정성을 팀으로 다각도 리뷰. PR 전, 배포 전, 기능 완성 후 품질 검증 시 사용. '리뷰해줘', '코드 검토', 'review' 트리거."
---

# Game Code Review Orchestrator

Unity C#과 웹 TypeScript 코드를 에이전트 팀으로 다각도 리뷰. 완성된 기능/모듈을 배포/PR 전에 점검.

## 실행 모드: 에이전트 팀

## 에이전트 구성

| 팀원 | agent_type | 역할 |
|------|-----------|------|
| code-reviewer  | code-reviewer  | 품질/SOLID/헌법 준수 리뷰 |
| qa-inspector   | qa-inspector   | 통합 정합성 교차 검증 |
| test-designer  | test-designer  | 테스트 커버리지 평가 + 누락 테스트 제안 |
| (리더)         | —              | 종합 + 우선순위화 |

> 소폭 변경일 때는 code-reviewer만 서브 에이전트로 호출 가능. 이 스킬은 종합 리뷰용.

## 워크플로우

### Phase 1: 준비
1. 리뷰 범위 결정 — 특정 디렉토리? 최근 수정 파일? 전체?
2. 변경 범위 추출 (git이 있으면 diff, 없으면 Glob + modified time)
3. `_workspace/` 생성, `review_scope.md`에 파일 목록 저장

### Phase 2: 팀 구성

```
TeamCreate(
  team_name: "code-review-team",
  members: [
    { name: "code-reviewer", agent_type: "code-reviewer", model: "opus",
      prompt: "헌법·코딩규약 로드 후 범위 내 코드 리뷰. P0/P1/P2 severity 구분, 파일:라인 인용 필수." },
    { name: "qa-inspector", agent_type: "qa-inspector", model: "opus",
      prompt: "이 범위에 포함된 경계면(API↔훅, Script↔Scene 등) 교차 검증." },
    { name: "test-designer", agent_type: "test-designer", model: "opus",
      prompt: "범위 내 코드의 테스트 커버리지 평가. 빠진 중요 케이스 목록 제시." }
  ]
)
```

### Phase 3: 병렬 리뷰

3명이 독립적으로 같은 범위를 리뷰:
- code-reviewer → `_workspace/review_quality.md`
- qa-inspector → `_workspace/review_qa.md`
- test-designer → `_workspace/review_coverage.md`

서로 흥미로운 발견은 SendMessage로 공유:
- code-reviewer가 성능 핫패스 발견 → test-designer에게 "이 경로 성능 테스트 필요" 전달
- qa-inspector가 경계면 이슈 발견 → code-reviewer에게 교차 참조 제안

### Phase 4: 종합

리더가 3개 보고서를 Read하고 통합:

```markdown
# 코드 리뷰 종합 보고서

## 요약
- 범위: {파일 수}
- P0(즉시): {n}건
- P1(권장): {n}건
- P2(선택): {n}건
- 테스트 누락: {n}건
- 경계면 이슈: {n}건

## P0 (즉시 수정 필요)
### 1. {제목}
- 파일: {path}:{line}
- 발견자: code-reviewer / qa-inspector / test-designer
- 내용: ...
- 수정: ...

## P1 (권장)
...

## P2 (선택)
...

## 테스트 커버리지
- 누락된 중요 케이스: ...

## 긍정적 발견
- ...
```

### Phase 5: 후속 조치 제안

종합 보고서 마지막에 다음 단계 제안:
- P0 있음 → `dev-iteration-loop` 또는 `unity-game-dev` 재호출로 수정
- 테스트 누락 → `unity-automated-test` 호출하여 추가
- 경계면 이슈 → 해당 에이전트에게 직접 SendMessage

### Phase 6: 정리
- 팀 해체
- `_workspace/` 보존

## 데이터 흐름

```
범위 선정
    ↓
┌────────────┬────────────┬────────────┐
code-reviewer  qa-inspector  test-designer
    ↓             ↓             ↓
review_quality  review_qa     review_coverage
    └─────────────┴─────────────┘
              ↓ Read + 통합
        리더 → review_final.md
              ↓
        후속 조치 제안
```

## 에러 핸들링

| 상황 | 전략 |
|------|------|
| 범위가 너무 큼 (500+ 파일) | 위험 영역(보안/성능 핫패스) 우선, 나머지는 샘플링 + 명시 |
| 헌법과 기존 코드 모순 | feedback-analyzer에게 알림 → 헌법 갱신 가능성 평가 |
| 특정 파일 리뷰 불가 (바이너리 등) | 스킵, 보고서에 명시 |

## 테스트 시나리오

### 정상 흐름
1. 사용자: "방금 만든 적 AI 리뷰해줘"
2. Phase 1: 최근 수정 파일 (`Assets/Scripts/Enemy/*.cs`, `Assets/Tests/**/Enemy*`) 수집
3. Phase 2: 3명 팀
4. Phase 3: 병렬 리뷰
   - code-reviewer: P1 "복잡한 switch 분기 → Strategy 패턴 제안", P2 "매직 넘버"
   - qa-inspector: "EnemyStats SO에 정의된 필드가 EnemyController에서 사용되지 않음 (dead field)"
   - test-designer: "시야 범위 경계값 테스트 누락"
5. 종합 보고서 생성
6. 제안: dev-iteration-loop 호출하여 P1 수정 + 누락 테스트 추가

### 에러 흐름
1. 리뷰 중 code-reviewer가 헌법에 없는 새 안티패턴 발견
2. feedback-analyzer에게 알림 전달
3. 종합 보고서에 "헌법 갱신 제안" 섹션 추가
