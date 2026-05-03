---
name: feedback-constitution-update
description: "사용자 부정적 피드백 기반 헌법/규칙 자동 갱신 스킬. AI가 생성한 산출물이 잘못됐다는 피드백('잘못됐다', '틀렸다', '그렇게 하지 마', 'wrong', 'incorrect')을 받으면 원인을 분석하고 .claude/constitution.md 또는 .claude/rules/*.md를 갱신하여 다음 세션부터 같은 실수를 방지. .claude/hooks/detect-feedback.js 훅이 감지한 피드백이나 사용자 명시 요청에서 트리거."
---

# Feedback → Constitution Update

사용자 부정적 피드백을 받았을 때, 해당 피드백이 반복되지 않도록 프로젝트 헌법/규칙을 갱신하는 스킬.

## 실행 모드: 서브 에이전트 (2개)

> 두 에이전트가 순차 실행되며 통신이 결과 전달 중심이므로 에이전트 팀보다 서브 에이전트가 적합.

## 에이전트 구성

| 에이전트 | subagent_type | 역할 | 출력 |
|---------|--------------|------|------|
| feedback-analyzer     | feedback-analyzer     | 피드백 원인 분석 + 일반화된 교훈 추출 | `_workspace/feedback_analysis_{ts}.md` |
| constitution-updater  | constitution-updater  | 실제 `.claude/constitution.md` / `rules/*.md` 수정 + changelog 기록 | 수정된 파일 + `_workspace/constitution_update_{ts}.md` |

## 트리거 조건

이 스킬은 다음 경우에 호출된다:

1. **`UserPromptSubmit` 훅이 감지한 피드백 키워드** — `.claude/hooks/detect-feedback.js`가 시스템 리마인더를 출력하면 Claude가 이 스킬을 호출
2. **사용자가 명시적으로 요청** — "이거 헌법에 추가해줘", "규칙에 반영해줘", "다음엔 이렇게 하지 말아줘"
3. **에이전트가 반복 실수 인지** — 같은 종류의 수정 지시를 2회 이상 받았을 때

## 워크플로우

### Phase 1: 준비
1. 피드백 원문 확보 (사용자 메시지 또는 최근 대화 컨텍스트)
2. 관련 산출물 식별 — 어떤 파일/커밋/응답에 대한 피드백인지
3. 현재 헌법/규칙 파일 로드 (`.claude/constitution.md`, `.claude/rules/*.md`)
4. `_workspace/` 생성, 타임스탬프 결정 (`YYYY-MM-DD_HHMMSS`)

### Phase 2: 분석 (feedback-analyzer)

```
Agent(
  name: "feedback-analyzer",
  subagent_type: "feedback-analyzer",
  model: "opus",
  prompt: |
    사용자 피드백:
    ---
    {피드백 원문}
    ---

    관련 산출물:
    - 파일: {경로}
    - 에이전트: {에이전트 이름 또는 'unknown'}
    - 작업 맥락: {최근 대화 요약}

    현재 헌법: .claude/constitution.md
    현재 규칙: .claude/rules/ 하위 파일 목록

    임무:
    1. 피드백의 구체적 불만 추출
    2. 원인 분류 (규칙 부재 / 위반 / 충돌 / 오버피팅)
    3. 기존 규칙과 대조
    4. 일반화된 교훈 도출 (Why + How to apply)
    5. constitution-updater에게 전달할 변경 지시 작성

    출력: _workspace/feedback_analysis_{ts}.md
)
```

### Phase 3: 갱신 (constitution-updater)

분석 결과 파일을 입력으로 전달:

```
Agent(
  name: "constitution-updater",
  subagent_type: "constitution-updater",
  model: "opus",
  prompt: |
    입력: _workspace/feedback_analysis_{ts}.md

    임무:
    1. 분석 결과의 '변경 지시'를 따라 해당 파일 Edit
    2. 기존 규칙과의 중복/충돌 검사
    3. 일반화된 형태로 작성 (오버피팅 금지)
    4. .claude/rules/changelog.md에 이력 추가
    5. 변경 전 diff 요약을 _workspace/constitution_update_{ts}.md에 저장
    6. 사용자에게 승인 요청 (헌법 변경은 중요)
)
```

### Phase 4: 사용자 승인 및 적용

1. constitution-updater가 제안한 변경을 사용자에게 제시
2. 승인 시: 실제 파일 수정 확정 + changelog 기록
3. 거부/수정 시: 사용자 의견 반영 후 재시도 (최대 2회)

### Phase 5: 정리
- `_workspace/` 보존 (사후 감사)
- 변경 요약을 사용자에게 보고

## 데이터 흐름

```
[사용자 피드백 또는 훅 감지]
    ↓
feedback-analyzer → _workspace/feedback_analysis_{ts}.md
    ↓
constitution-updater → .claude/constitution.md 또는 rules/*.md (Edit)
                       → .claude/rules/changelog.md (append)
                       → _workspace/constitution_update_{ts}.md
    ↓
[사용자 승인]
    ↓
다음 세션부터 모든 에이전트가 갱신된 규칙 자동 로드
```

## 원칙: 오버피팅 회피

분석과 갱신 모두 **일반화된 원리 수준**에서 수행한다.

**오버피팅 수정 (금지):**
> "PlayerController.cs에서 _rigidbody 이름 쓰지 마"

**일반화된 수정 (권장):**
> **§ 네이밍**
>
> MonoBehaviour의 SerializeField 필드는 언더스코어 접두사를 사용한다 (`_moveSpeed`, `_rigidbody`).
>
> **Why:** 일반 지역 변수/프로퍼티와 시각적으로 구분되어, 에디터 직렬화 대상임을 코드 리뷰 시 즉시 알 수 있다.
>
> **How to apply:** 모든 신규 MonoBehaviour 작성 시, 그리고 기존 코드 수정 시.

## 에러 핸들링

| 상황 | 전략 |
|------|------|
| 피드백이 단순 질문 | 이 스킬 호출하지 말고 무시 |
| 관련 산출물 못 찾음 | 최근 대화 이력 기반으로 추정, 불확실성 명시 |
| 기존 규칙과 충돌 | 사용자에게 우선순위 결정 요청 |
| 헌법 파일 500줄 초과 | 세부 내용을 `rules/` 하위로 분리 후 포인터 남김 |
| 사용자 거부 | 변경 철회, 이력은 남기지 않음 |

## 테스트 시나리오

### 정상 흐름
1. 사용자: "왜 또 Update에서 GetComponent 불렀어? 이거 성능상 안 된다고 했잖아"
2. 훅이 "왜", "안 된다" 키워드 감지 → 시스템 리마인더 출력
3. Claude가 이 스킬 호출
4. feedback-analyzer:
   - 원인: "규칙 위반 + 규칙이 충분히 강조되지 않음"
   - 일반화: "Update/FixedUpdate 핫패스에서 GetComponent 금지 원칙을 csharp-developer 에이전트 정의에 추가"
5. constitution-updater:
   - `.claude/rules/coding-standards.md`의 "금지 패턴" 테이블 강조
   - `.claude/agents/csharp-developer.md`에 작업 원칙 추가 항목
   - changelog.md에 이력 기록
6. 사용자에게 diff 제시 → 승인
7. 다음 세션부터 csharp-developer가 로드 시 강화된 규칙 확인

### 에러 흐름
1. 사용자: "아니 그게 아니야"
2. 훅이 감지 → 스킬 호출
3. feedback-analyzer가 컨텍스트를 봐도 무엇에 대한 불만인지 불명확
4. 사용자에게 구체화 질문 — "어떤 파일/어떤 결정이 잘못됐나요?"
5. 답변 후 재분석
