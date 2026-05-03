# 커밋 메시지 규칙

> 적용 대상: 모든 커밋 (AI 에이전트 생성 포함). 제목·본문 모두 **한글**.

## 형식

```
[PREFIX] 제목 — 핵심 변경 한 줄 요약

본문 (선택, 한글):
- 무엇을 / 왜 변경했는지
- 영향 범위 (테스트 결과, 호환성, 회귀)
- 관련 ADR/스토리/이슈 ID

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

## Prefix 카탈로그

| Prefix | 용도 |
|---|---|
| `[FEATURE]` | 신규 기능 추가 |
| `[BUGFIX]` | 버그 수정 |
| `[REFACTOR]` | 동작 변경 없는 구조 개선 |
| `[UI]` | UI/UX 변경 (Inspector·Scene 포함) |
| `[ASSET]` | .asset / Prefab / 텍스처 / 오디오 등 자산 (Pixelwood 스프라이트 매핑 포함) |
| `[BALANCE]` | 수치 튜닝, 성장 시간, 도구 효율, 상태 감쇠율 등 |
| `[TEST]` | 테스트 추가/수정 (코드 동작 변경 X) |
| `[DOCS]` | 문서·헌법·규칙·ADR·CHANGELOG |
| `[CHORE]` | 빌드 설정·gitignore·CI·도구 |
| `[NETWORK]` | NGO 동기화·dedicated server 부트·세션 |
| `[META]` | 세대·가문·후계자·조상 등 메타 시스템 |

복수 prefix 가능: `[ASSET][BALANCE]` (예: 작물 성장 시간 + 스프라이트 동시 변경).

## 제목 규칙

- 한글 명령형 또는 명사형. 50자 이내.
- 변경의 **결과**를 표현 ("Wheat 성장 시간 60→45초 리밸런스" ○, "Wheat 수정" ✗).
- 파일·함수명·기호는 영어 그대로 ("FinalStateHash 에 Star opt-in segment 추가" ○).

## 본문 규칙

- 한글 산문 또는 불릿. 120자/줄 권장.
- "왜" 가 비명시적이면 한 줄 추가.
- 숫자·메트릭 포함 시 단위 명시 (DPS, 시드, ★, ms 등).
- AI 에이전트 리뷰 결과 반영 시 그 출처 명시 (예: "code-reviewer APPROVE — MEDIUM 1건 주석 처리").

## 예시

### 좋은 예

```
[FEATURE] 도구 해금 — 지식 트리거 SO 평가기 추가

- KnowledgeProgress가 액션 버스(OnGather/OnHit)를 받아 KnowledgeNode._triggers
  배열을 모두 평가, 전부 만족 시 unlocksTools/unlocksRecipes를 인벤토리에 추가.
- 트리거 평가는 SO 다형 (KnowledgeTriggerBase). 신규 트리거 = 신규 SO 클래스 1개.
- 시스템 코드는 특정 knowledgeId에 분기하지 않음 (CI 게이트 통과).

테스트: 4 EditMode 신규 (Trigger_HitGroundWithRock 임계 10회, 기타 트리거 누락 시
미해금, 멀티 트리거 AND 평가, GameDataRegistry 일관성). 통과.

리뷰: code-reviewer APPROVE.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

### 나쁜 예

```
fix: knowledge bug                       ← 영어 + 모호함
[FEATURE] update files                   ← 무엇을 추가했는지 불명
[패치] 도구 좀 손봤음                       ← prefix 외래 + 비정형
```

## 다중 변경 분리 원칙

한 커밋에 다른 카테고리 변경이 섞이면 분리 권장:
- 데이터 수치 + 시뮬 코드 = 두 커밋
- 새 기능 + 무관한 docs = 두 커밋

예외: 한 변경이 본질적으로 묶여야 의미 있는 경우 (예: 신규 클래스 + 그 클래스에 대한 테스트).

## 강제 절차

- AI 에이전트가 커밋할 때 본 규칙 위반 시 사용자가 즉시 지적할 수 있도록 prefix 와 한글 본문은 **타협 없는 규약**.
- 과거 커밋(이 규칙 도입 이전)은 retro-fix 하지 않음. 신규 커밋부터 적용.
