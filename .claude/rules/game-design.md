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

## 자유 경로 성장 원칙

학생 생활은 학교 수업·등교 루트 하나만 정답인 선형 구조로 설계하지 않는다. 플레이어는 학교를 중간에 나가거나, 마을 활동, 관계, 알바, 탐험, 제작, 도움 행동, 독학, 휴식, 이벤트 선택 같은 다양한 경로로 특성·성향·스킬·진로 힌트·관계·컨디션을 성장시킬 수 있어야 한다.

### 금지

- 특정 성장이나 퀘스트 진행을 "학교 수업 완료" 하나에만 종속시키는 설계
- `if (routeId == "school")`, `if (activityId == "StudyOnly")`, `switch(locationId)`처럼 특정 활동/장소/퀘스트 ID별 C# 분기로 진행 경로를 제한하는 구현
- 플레이어가 정해진 순서대로만 이동/수업/퀘스트를 수행해야 진행되는 일직선 구조
- 학교 밖 활동을 단순 보조 보상으로만 두고 핵심 성장·진로·관계 진행에서 배제하는 설계

### 필수

- 동일한 핵심 성장 목표에는 가능한 한 2개 이상의 대체 경로를 제공한다.
  - 예: 성실함은 수업 참여, 도서관 독학, NPC 도움, 반복 작업으로 성장 가능.
  - 예: 사교성은 친구 대화, 마을 행사, 협동 퀘스트, 상점/주민 도움으로 성장 가능.
  - 예: 진로 힌트는 학교 활동뿐 아니라 현장 실습, 마을 의뢰, 독학, 관계 이벤트로도 해금 가능.
- 모든 경로는 ScriptableObject 데이터와 전략 배열로 표현한다.
- `TraitDefinition`, `SkillDefinition`, `CareerDefinition`, `LifeActivityDefinition`, `LifeChoiceDefinition`, `QuestDefinition`, `LocationDefinition`, `RelationshipDefinition`, `StatusDefinition` 같은 데이터가 성장 경로를 조합해야 한다.
- 퀘스트와 활동은 하나의 필수 순서가 아니라 조건 기반 선택지, 대체 목표, 복수 해결 방식을 지원해야 한다.
- 신규 코어 루프 테스트는 최소 1개 이상의 "학교 루트가 아닌 경로"로도 성장/퀘스트/진로 힌트가 진행되는지 실제 플레이 방식으로 검증해야 한다.

## 성능·최적화 동시 설계 원칙

신규 시스템은 기능 완성 후 사후 최적화 대상으로만 남기지 않는다. 기획, 데이터 모델, UI 목업, 테스트 단계에서부터 항목 수 증가, 저장 데이터 크기, UI 갱신 비용, Addressables 로딩 비용, 멀티플레이 확장 비용을 함께 고려한다.

### 금지

- 대량 목록 UI에서 매 프레임 전체 항목을 스캔하거나 재정렬하는 구조
- `Update()` 폴링으로 도감/퀘스트/인벤토리/마일스톤 상태를 계속 재계산하는 구조
- UI 갱신 때마다 전체 항목 GameObject를 Destroy/Instantiate 하는 구조
- 런타임에서 반복적인 `string +`, LINQ, boxing, Dictionary foreach 등으로 GC 할당을 폭증시키는 구조
- 같은 sprite/addressable/data asset을 화면 갱신마다 반복 로드하는 구조
- 테스트 데이터 5개에서는 통과하지만 실제 항목 수 100~500개 이상에서 UI가 버벅일 구조를 완료로 보고하는 것

### 필수

- 도감, 인벤토리, 퀘스트, 마일스톤, 관계 목록처럼 항목이 늘어나는 UI는 가상화, 페이징, 필터 캐싱, 지연 로딩, dirty 갱신 중 적절한 방식을 선택한다.
- SO 데이터는 런타임 조회를 위해 id/reference 기반 캐시 또는 인덱스를 구성하고, 매번 전체 Registry를 순회하지 않는다.
- UI는 상태 변경 이벤트, save/load 완료, 탭 전환, 검색어 변경 같은 명확한 트리거에서만 필요한 영역을 갱신한다.
- Addressables와 sprite는 중복 로드를 피하고, 필요한 화면/카테고리/항목만 로드한다.
- 신규 goal은 완료 조건에 성능 리스크 확인과 최소한의 성능 검증 또는 측정 계획을 포함한다.
- 최종 보고에는 성능 검증 범위, 남은 병목 가능성, 후속 최적화 과제를 명시한다.
