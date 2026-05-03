# 게임 디자인 문서 규약 (GDD/ADR/TR/Manifest)

> **한국어 요약**: `/gs-*` 파이프라인 산출물 참조 규약. 에이전트가 산출물을 읽고 쓸 때의 위치·포맷.

## 경로 매핑

| 산출물 | 경로 | 템플릿 |
|---|---|---|
| GDD | `design/gdd/{system}.md` | `.claude/docs/templates/game-design-document.md` |
| ADR | `docs/architecture/adr-NNNN-{slug}.md` | `.claude/docs/templates/architecture-decision-record.md` |
| TR Registry | `docs/architecture/tr-registry.yaml` | `.claude/docs/templates/architecture-traceability.md` |
| Systems Index | `design/systems-index.md` | `.claude/docs/templates/systems-index.md` |
| Epic | `production/epics/EPIC-NNN-{slug}.md` | (외부 원본 없음 — inline 작성) |
| Story | `production/stories/story-NNN-{slug}.md` | (외부 원본 없음 — inline 작성) |
| Evidence | `production/qa/evidence/{slug}-evidence.md` | `.claude/docs/templates/test-plan.md` 참조 |
| Session State | `production/session-state/active.md` | (gitignored) |

## TR-ID 네이밍

- 형식: `TR-NNNN` (4자리 제로패딩)
- 할당: `tr-registry.yaml` 순차 증가
- Story 는 `tr_id:` 필드로 1개 TR-ID 참조

## ADR Status

- `Proposed` → Story 는 BLOCKED 상태
- `Accepted` → Story 구현 가능
- `Superseded` → 후속 ADR 링크 필수

## 일관성 규칙

- systems-index.md 의 수치 상수는 Immutable — GDD 에서 재정의 금지 (참조만)
- GDD 간 수치 충돌 발견 시 `/gs-consistency-check` 가 블로킹
- Epic/Story 템플릿은 외부 레포에 없으므로 프로젝트에서 자체 정의 (첫 사용 시 `.claude/docs/templates/epic.md`, `story.md` 추가 후 갱신)
