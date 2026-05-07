# Town Concept Conversion Design

Date: 2026-05-08
Status: Draft approved for planning

## Goal

ROOTBORN의 현재 farm/farmer 중심 컨셉을 전면 폐기하고, 도시에서 살아가는 사람의 생활 시뮬레이션으로 전환한다. 핵심 판타지는 농장 경영이나 작물 재배가 아니라, 집, 거리, 상점, 직장 또는 알바, 이웃, 소비, 관계, 평판, 일정, 생활 성장이다.

이번 전환은 단순한 씬 이름 변경이 아니다. 사용자-facing 기획, UI 톤, 기본 씬, 데이터 의미론, 테스트 카탈로그를 모두 town life 기준으로 재정렬한다. 기존 시스템은 가능한 만큼 재사용하지만, 플레이어에게 보이는 의미는 farm이 아니라 city/town 생활이어야 한다.

## Non-Goals

- Urban garden, rooftop farming, 실내 텃밭을 1차 핵심 루프로 남기지 않는다.
- 기존 farm 루프를 보존하기 위해 town을 장식처럼 덧씌우지 않는다.
- Style2 UI 교체만으로 town 전환을 완료 처리하지 않는다.
- crop/tool/resource 기존 C# 타입명을 즉시 모두 rename하는 것을 1차 완료 조건으로 삼지 않는다. 사용자-facing 의미와 데이터 매핑을 먼저 전환하고, technical rename은 별도 phase에서 처리한다.

## Current Context

이미 프로젝트에는 modern 전환 작업의 결과물이 일부 존재한다.

| Area | Current evidence | Town conversion role |
| --- | --- | --- |
| Modern farm spec | `docs/superpowers/specs/2026-05-06-modern-farm-art-conversion-design.md` | superseded by town direction |
| Modern UI reconstruction | `docs/art/modern-ui-reconstruction-manifest.json`, `docs/art/modern-ui-reconstruction-audit.md` | Style1 recipe 기반을 Style2로 1:1 매핑 |
| Style sheets | `Assets/modernuserinterface-win/16x16/Modern_UI_Style_1.png`, `Modern_UI_Style_2.png` | Style2를 1차 UI 기준으로 사용 |
| Town scene | `Assets/Scenes/Town.unity` | 기본 플레이 씬 후보. 현 상태 audit 필요 |
| Existing farm scene | `Assets/Scenes/Farm.unity` | legacy 또는 migration source |
| Modern society code | `Assets/Scripts/Game/ModernSociety/` | town life 도메인 후보 |
| Farm code/data | `Assets/Scripts/Game/Crops/`, `Assets/Scripts/Game/Farming/`, `Assets/Data/Crops/`, `Assets/Data/Tools/Effects/Effect_*Crop.asset` | 제거, 격리, 또는 town 의미로 치환할 대상 |

## Concept Pillars

### 1. City Resident, Not Farmer

플레이어는 농부가 아니라 도시 생활자다. 기본 활동은 출근, 알바, 동네 심부름, 상점 이용, 이웃과 대화, 자기계발, 생활 물품 관리, 휴식, 지역 해금으로 구성한다.

### 2. Time And Energy As Primary Pressure

농작물 성장 시간 대신 하루 일정, 시간대, 체력, 집중도, 돈, 평판이 선택의 비용이 된다. 기존 time/save/quest/inventory 구조는 유지하되, 활동의 의미를 생활 루틴으로 바꾼다.

### 3. Neighborhood Progression

플레이어 성장은 밭 확장이 아니라 생활권 확장으로 표현한다. 예: 집 정리, 편의점/카페/사무실/시장 해금, 이웃 신뢰도 상승, 새로운 일거리와 서비스 발견.

### 4. Data-Driven Town Entities

도시 활동과 엔티티는 기존 헌법처럼 ScriptableObject로 정의한다. 도시 활동, 생활 도구, 관계 이벤트, 지식 해금, 상태 변화는 SO 정의와 전략 배열로 구성하고, 엔티티 ID별 C# 분기를 만들지 않는다.

## Domain Mapping

1차 전환에서는 기존 타입과 저장 구조를 무리하게 전부 rename하지 않는다. 대신 사용자-facing 표시명, 데이터 인스턴스, 씬 배치, 퀘스트 문맥을 town 의미로 바꾼다.

| Existing farm concept | Town concept | 1차 처리 |
| --- | --- | --- |
| Farm scene | Town scene / district scene | `Town.unity`를 기본 후보로 audit하고 Boot flow 연결 계획 수립 |
| Crop | Activity progress, 생활 과제, 취미 진행, 업무 처리 | crop UI/용어 제거. 기존 crop 시스템은 legacy 또는 후속 삭제 |
| Tool | 생활 도구, 전자기기, 업무 도구, 교통카드, 취미 도구 | `ToolDefinition` 구조는 재사용 가능하나 데이터 의미를 town 도구로 치환 |
| Resource | 돈, 시간, 체력, 집중도, 평판, 생활 물품 | 기존 resource node는 거리/상점/생활 오브젝트로 재해석하거나 제거 |
| Recipe | 준비, 구매, 조합, 업무 처리, 서비스 이용 | 제작 UI를 도시 생활 action/result 구조로 재명명 |
| Quest | 이웃 부탁, 직장 업무, 도시 이벤트, 관계 이벤트 | 보상 원자성 원칙 유지 |
| Knowledge | 도시 정보, 관계 정보, 생활 팁, 직무 스킬 | `KnowledgeNode` 구조 유지 가능 |
| Status | 컨디션, 스트레스, 피로, 집중, 사회성 | 상태 UI를 town life summary로 변경 |
| Inventory | 가방, 생활 물품, 소지품 | 기존 인벤토리 원자성/수용량 유지 |

## Style2 UI Conversion

Style2는 town concept의 기본 UI 톤이다. 기존 Style1 recipe는 row/column 기준으로 Style2에 1:1 대응한다고 보고 치환한다.

### Required mapping behavior

- `sprites/ui/modern/16/style-1` 참조를 1차 범위에서 `sprites/ui/modern/16/style-2`로 전환한다.
- `ModernUI_16_Style1_r{row}_c{column}` 참조는 같은 row/column의 `ModernUI_16_Style2_r{row}_c{column}`로 매핑한다.
- Style2 sheet가 Style1보다 작은 영역은 참조 row/column이 Style2 grid 안에 존재하는지 테스트한다.
- `docs/art/modern-ui-reconstruction-manifest.json`은 기존 Style1 manifest의 보존본 또는 migration 기록을 남기고, Style2 manifest를 별도로 생성하거나 명확히 version 필드를 둔다.
- UI는 16x16 sliced sprite 조각 조합을 유지한다. 단일 16x16 sprite를 큰 panel로 확대하지 않는다.

### First-scope UI

- HUD summary
- Inventory panel
- Status panel
- Quest panel
- Settings or menu panel if currently reachable

Save slot, dialogue, main menu는 1차 범위에서 audit 대상으로 포함하되, 전면 치환은 후속 phase로 분리할 수 있다.

## Scene Direction

Town의 첫 화면은 도시 생활자 컨셉을 즉시 보여야 한다.

1차 공간 후보:

- apartment or small home
- street or sidewalk
- shop or service counter
- neighbor/community interaction spot
- work or errand board

Farm 배치물, crop plot, field, watering/harvest affordance는 기본 플레이 경로에서 제거한다. 기존 `Farm.unity`는 migration reference로 남기되, 사용자-facing 기본 진입은 `Town.unity`로 옮긴다.

## Data And Registry

모든 신규 town data는 ScriptableObject로 만든다.

권장 신규 또는 재사용 데이터:

- `ModernActivityDefinition`: 도시 활동 정의
- `ModernRoutineFlagRule`: 일정/조건 rule
- `ModernSocietyStats`: 돈, 체력, 집중, 평판 등 town stats
- town tool definitions: phone, transit card, work badge, notebook, umbrella 등
- town quest definitions: neighbor request, job shift, delivery errand, community event
- town knowledge nodes: district info, relationship tip, job skill, store unlock

모든 SO 인스턴스는 `Assets/Data/Registry/GameDataRegistry.asset` 또는 대응 registry에 등록한다. `Resources.Load` 신규 사용으로 데이터를 찾지 않는다.

## Phases

### Phase 1: Audit And Decision Lock

- farm residue audit 작성
- Style1 UI recipe usage audit 작성
- `Town.unity` 현 상태 audit
- 기존 `ModernSociety` 코드와 테스트 현황 audit
- town domain mapping table 확정

완료 조건:

- farm residue가 user-facing, code symbol, data asset, scene object, test name 별로 분류된다.
- Style2로 즉시 1:1 치환 가능한 UI recipe와 보류 항목이 분리된다.

### Phase 2: Style2 UI Baseline

- Style2 sheet slicing/address 검증 테스트 작성
- Style1 recipe를 Style2로 migration
- inventory/status/quest/HUD 1차 화면에 Style2 적용
- screenshot audit 작성

완료 조건:

- 1차 UI가 Style2 sub-sprite를 사용한다.
- Style1 sheet 참조가 1차 UI runtime path에서 제거된다.
- screenshot에서 panel/text bounds가 깨지지 않는다.

### Phase 3: Town Scene Baseline

- `Town.unity`를 기본 플레이 후보로 정리
- apartment/street/shop/community spot 중 최소 2개 이상 시각 단서를 배치
- Boot or play flow가 town scene으로 진입하도록 계획 및 구현
- farm plot/crop affordance를 기본 경로에서 제거

완료 조건:

- PlayMode에서 town scene에 진입 가능하다.
- 첫 화면에서 town concept이 보인다.

### Phase 4: Town Data And Loop

- 도시 생활 활동 SO를 구성
- town resource/status/quest 표시를 UI에 연결
- crop growth 중심 루프를 city routine loop로 대체
- inventory/reward transaction 원칙 유지

완료 조건:

- 하루 또는 한 세션의 기본 loop가 town activity로 구성된다.
- 퀘스트/보상/인벤토리 회귀가 유지된다.

### Phase 5: Farm Legacy Isolation

- 기본 사용자-facing path에서 farm/crop/farmer 용어 제거
- 남은 farm code/data는 legacy로 문서화하거나 삭제 계획 수립
- technical rename 계획 작성

완료 조건:

- 사용자가 기본 경로에서 farm game으로 인식할 요소가 없다.
- 남은 farm residue는 의도된 legacy이며 후속 ticket으로 추적된다.

## Scenario Catalog

### TOWN-CONCEPT-001

시작 화면, HUD, 인벤토리, 퀘스트, 상태창에서 farm/farmer/crop 중심 표현이 제거된다. 남은 farm 용어는 legacy 또는 technical debt로 문서화된다.

### TOWN-SCENE-001

기본 플레이 씬은 도시 생활 공간을 표현한다. 아파트/집, 거리, 상점, 이웃/업무 공간 중 최소 2개 이상의 town 단서가 보인다.

### TOWN-LOOP-001

농사 루프 대신 도시 생활 루프가 정의된다. 일정 확인, 활동 선택, 자원/시간 소비, 보상/관계/평판 변화, 새 의뢰/장소/아이템 해금의 흐름을 갖는다.

### TOWN-DATA-001

도시형 엔티티는 ScriptableObject로 정의되고 registry에 등록된다. 엔티티 ID별 C# 분기가 추가되지 않는다.

### TOWN-UI-STYLE2-001

기존 Style1 UI recipe가 Style2로 1:1 치환된다. panel/button/slot/tab/scrollbar/window가 Style2 조각으로 구성된다.

### TOWN-VISUAL-001

Game View 또는 PlayMode screenshot에서 town concept이 첫 화면에 드러난다. UI는 Style2를 사용하고, text bounds와 panel bounds가 정상이다.

### TOWN-REGRESSION-001

퀘스트 보상 원자성, 인벤토리 수용량/중복 처리, bootstrap, movement, network smoke 흐름은 깨지지 않는다.

## Testing Strategy

### EditMode

- Style2 sheet slicing 검증
- Style1 recipe와 Style2 recipe row/column 1:1 대응 검증
- 모든 Style2 manifest sub-sprite 존재 검증
- oversized single-sprite panel 금지 source audit
- 신규 runtime `Resources.Load`, `GameObject.Find`, `FindObjectOfType` 금지 source audit
- town user-facing localization/label에서 farm 핵심 용어 제거 검증
- 도시형 SO registry 등록 검증
- entity ID branching CI gate 유지

### PlayMode

- Boot to Town scene 진입
- Style2 HUD 표시
- Inventory/Status/Quest 또는 1차 town panel open/close
- town scene screenshot non-empty 검증
- text bounds, panel bounds, Style2 sprite usage 검증

### Regression

- Quest reward claim atomic/idempotent tests
- Inventory capacity and duplicate handling tests
- movement/bootstrap/network smoke tests
- `Scripts/ci/check-no-entity-id-branching.sh`

## Risks

| Risk | Mitigation |
| --- | --- |
| Style2 sheet가 Style1보다 작아 일부 row/column이 없음 | migration test로 누락 좌표를 먼저 검출하고, 대체 sprite를 manifest에 명시 |
| 기존 코드명이 Farm/Crop에 깊게 묶여 있음 | 1차는 사용자-facing 의미와 데이터 wiring을 우선 전환하고, technical rename phase를 분리 |
| 기존 구현 중 town 관련 코드가 부분적으로만 존재함 | audit에서 재사용 가능/폐기/보류를 분리 |
| Farm 기능 삭제가 회귀를 유발함 | legacy isolation phase를 두고 테스트 통과 전 삭제하지 않음 |
| UI 전환만 완료되고 게임 루프는 farm으로 남음 | TOWN-LOOP-001과 TOWN-CONCEPT-001을 completion gate로 둠 |

## Approval Gate

이 spec이 승인되면 다음 단계는 `docs/superpowers/plans/2026-05-08-town-concept-conversion.md` 구현 계획을 기준으로 Phase 1부터 TDD로 실행하는 것이다. 구현 계획 전에는 code edit, scene/prefab 변경, importer 변경을 하지 않는다.
