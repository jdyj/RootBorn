# Codex 위임 프롬프트 — ROOTBORN Style2 카탈로그 Gemini 기반 재생성

> 이 파일을 그대로 Codex(또는 다른 자율 에이전트)에 넘기면 작업을 단독으로 완료할 수 있도록 작성된 자급자족 작업 지시서.

## 너의 정체와 환경

- 너는 ROOTBORN(2D Unity 6 도시 생활 게임)의 코드베이스를 다루는 자율 코딩 에이전트다.
- 작업 디렉토리: `c:\Users\jdyj\farmer` (Windows, PowerShell 또는 git bash).
- Unity 버전: 6000.3.13f1. 어셈블리 `Rootborn.Game.Common`, `Rootborn.Tests.EditMode.TownConcept`.
- 헌법: `.claude/constitution.md`, `.claude/rules/*.md` 준수. 한국어 커밋. TDD 의무.
- 신규 `Assets/**/*.cs` 디스크 직접 쓰기 **금지**. `script-update-or-create` MCP 도구 또는 Unity Editor 메뉴를 경유. (`.claude/rules/unity-cli.md` 절대 원칙)

## 작업 목적 (Why)

`Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png`(34행 × 49열 = 1666 sprites) 카탈로그를 Gemini Nano Banana의 시각 분류 결과로 교체한다. 기존 카탈로그는 사람의 "사용 의도 별명"(예: `panel.base`, `ribbon.items`)으로 채워져 있어 시각 형태와 일치하지 않을 수 있었다. Gemini는 7배치로 나눠 콘텐츠가 있는 셀 509개를 정확한 시각 라벨(panel.cornerTopLeft, button.lock, item.giftBox 등)과 3-frame state group 64개로 분류했다. 빈 셀 1157개는 Addressables/C# 카탈로그에서 제외하여 런타임 효율을 높인다.

## 입력 파일 (이미 준비됨)

| 파일 | 용도 |
|---|---|
| `docs/art/modern-ui-style2-icon-catalog-gemini-lean.json` | **423 entries (콘텐츠 셀만)** — 새 C# 카탈로그의 단일 소스 |
| `docs/art/modern-ui-style2-icon-catalog-gemini-merged.json` | 1666 entries 완전판 (unknown 자동 채움 포함). 테스트가 1666 grid 검증을 요구하면 이걸 사용 |
| `docs/art/modern-ui-style2-icon-catalog-gemini-conflicts.md` | 기존 vs Gemini 충돌 504건 (Gemini 우선 정책 적용됨) |
| `docs/art/style2-gemini-batches/batch-*.json` | Gemini 7개 원본 배치 (참조용) |
| `docs/art/style2-gemini-batches/merge.py` | 재실행 가능 머지 스크립트 |
| `docs/art/modern-ui-style2-contact-sheet-4x.png` | 4배 확대 + grid overlay (시각 검증용) |
| `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png` | 원본 sprite atlas |

## 교체 대상 파일

| 파일 | 라인 수 | 역할 |
|---|---|---|
| `Assets/Scripts/Game/Common/ModernUiStyle2Icon.cs` | 514 | UI 전용 enum (gameplay entity id 아님) |
| `Assets/Scripts/Game/Common/ModernUiStyle2IconCatalog.cs` | 568 | enum → 좌표/sprite/state group 매핑 + `GetSpriteKey()` 리졸버 |
| `Assets/Tests/EditMode/TownConcept/ModernUiStyle2IconCatalogTests.cs` | 166 | 카탈로그 무결성 검증 (5 테스트) |
| `docs/art/modern-ui-style2-icon-catalog.json` (기존) | 1666 entries | 백업 후 `gemini-merged.json` 내용으로 교체 |
| `docs/art/modern-ui-style2-icon-catalog.md` (기존) | 308 KB | 백업 후 lean 기반으로 재생성 (테스트가 마크다운 일부 단언) |

## 의존 코드 (영향 분석)

`Grep ModernUiStyle2Icon\.[A-Za-z]+` 결과:
- **catalog 파일과 tests 파일 외에 enum 값을 직접 사용하는 곳 0건** — 다른 게임 코드는 enum 직접 참조 없음.
- 단, `ModernUiStyle2Sprites.cs`의 `CommonPanel` 좌표 (r2_c0~r4_c2) 가 `ModernUiTileImage`, `ModernUiRecipes.CommonPanel` 등 패널 생성 코드에서 광범위 참조됨. **CommonPanel 좌표 변경은 ROOTBORN UI 전반에 회귀 위험**.

따라서 enum/카탈로그 자체는 자유롭게 재생성해도 안전. CommonPanel 좌표 변경 여부는 별도 의사결정.

## 기존 테스트 단언 (충돌 분석)

`ModernUiStyle2IconCatalogTests.cs`의 5개 테스트:

1. `IconCatalogJson_CoversEveryStyle2CoordinateExactlyOnce` — `docs/art/modern-ui-style2-icon-catalog.json` 이 **1666 entries** 필수, 좌표 r0_c0~r33_c48 빠짐없음, confidence ∈ {confirmed, probable, unknown}.
2. `IconCatalogMarkdown_ProvidesHumanReviewTableAndUnknownPolicy` — 마크다운에 11컬럼 헤더 + `unknown.r{row}.c{column}` + `r3_c42`, `r3_c43`, `r3_c44` 문자열 포함.
3. `IconCatalog_GroupsR3C42ToR3C44AsPressedButtonFrames` — JSON 에서 r3_c42~c44가 `button.icon.heightFrame` stateGroupId + `frame0/1/2` role.
4. `IconCatalog_ExposesSemanticEnumAndResolverForKnownControls` — C# 카탈로그에서 `ButtonIconHeightFrame0/1/2`, `FurnitureChair`, `FurnitureBed` enum 으로 sprite key 조회 가능 + button/cursor/toggle/direction 카테고리 존재.
5. `CommonPanelCoordinates_RemainStyle2Baseline` — `ModernUiStyle2Sprites.CommonPanel.*Name` 이 r2_c0~r4_c2.

### Gemini 카탈로그와의 충돌

- **r3_c42~c44**: Gemini는 `button.chevronDown` 3-frame 으로 분류 (`button.chevronDown.state.r3`). 기존 테스트는 `button.icon.heightFrame` + `frame0/1/2`.
- **r1_c13/r1_c14**: Gemini는 unknown(빈 셀로 판단). 기존 테스트는 `FurnitureChair/Bed`.
- 카테고리 단언 `cursor`, `toggle` 은 lean 에 각 1개, 4개 있어 통과 가능.
- **CommonPanel r2_c0~r4_c2**: Gemini는 r0_c0~r2_c2 를 패널 모서리로 분류. 기존 코드는 r2~r4. 둘 중 어느 게 시각상 맞는지는 PNG 픽셀 확인 필요.

## 작업 단계 (이 순서대로)

### 1단계 — 시각 검증 (필수 선행)

`Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png` 또는 `docs/art/modern-ui-style2-contact-sheet-4x.png` 를 직접 시각 확인하고 다음 4개 좌표의 실제 sprite 모양을 결정하라:

- r0_c0~r2_c2 vs r2_c0~r4_c2 — 어느 9-slice 좌표가 시각상 ROOTBORN UI 의 공통 패널과 일치하는가? (현재 코드는 r2~r4 사용)
- r3_c42~c44 — `button.icon.heightFrame` (같은 높이 프레임 3개) 인가, `button.chevronDown` 3-frame 상태 그룹인가?
- r1_c13, r1_c14 — furniture chair/bed 이미지가 있는가, 아니면 비어있는가?

이 결과를 `docs/art/modern-ui-style2-icon-catalog-visual-audit.md` 에 1줄씩 기록하라 (시각 단서 + 결정).

### 2단계 — 결정 분기

- **CommonPanel 좌표가 r2_c0~r4_c2 가 맞다** → `ModernUiStyle2Sprites.cs` 보존, lean 카탈로그의 해당 좌표가 panel 카테고리인지 확인. 아니면 lean 의 그 좌표 entry 들의 semanticId 를 `panel.cornerTopLeft` 등으로 보정 (Gemini 가 빈 셀로 잘못 판단했을 가능성).
- **r3_c42~c44 가 button.icon.heightFrame 이 맞다** → lean 카탈로그 그 3개 entry 의 stateGroupId 를 `button.icon.heightFrame` + frame0/1/2 로 패치. 기존 테스트 3 보존.
- **r1_c13, r1_c14 가 furniture 가 맞다** → lean 카탈로그에 해당 entry 2개 추가 (`item.furnitureChair`, `item.furnitureBed` / category=`furniture` / probable). 기존 테스트 4 보존.

### 3단계 — JSON 카탈로그 교체

1. 기존 `docs/art/modern-ui-style2-icon-catalog.json` 백업: `cp $&{} modern-ui-style2-icon-catalog.json.backup-pre-gemini.json`.
2. 2단계 보정이 끝난 후, `modern-ui-style2-icon-catalog-gemini-merged.json`(1666 entries 완전판)을 기준으로 기존 카탈로그 교체. **테스트 1이 1666 grid 검증을 요구하므로 lean 이 아닌 merged 사용.**
3. 마크다운 `docs/art/modern-ui-style2-icon-catalog.md` 재생성 — 11컬럼 테이블 + `unknown.r{row}.c{column}` 정책 문단 포함 (테스트 2 통과 조건).

### 4단계 — C# enum + Catalog 재생성

코드 생성 스크립트를 작성하여 lean JSON 의 423 entries 를 기준으로 C# 파일을 생성:

`docs/art/style2-gemini-batches/gen_csharp.py` (신규 작성):

```python
"""Generate ModernUiStyle2Icon.cs and ModernUiStyle2IconCatalog.cs from lean JSON.

Reads docs/art/modern-ui-style2-icon-catalog-gemini-lean.json and emits:
- Assets/Scripts/Game/Common/ModernUiStyle2Icon.cs  (enum with all enumNames)
- Assets/Scripts/Game/Common/ModernUiStyle2IconCatalog.cs (Entry list + GetSpriteKey)

Requirements:
- enum 멤버 = lean entry 의 enumName 필드 그대로 (이미 PascalCase + 좌표 접미사로 unique)
- Catalog Entries 배열 = lean 의 423 entries 순서 (row, column 오름차순)
- EntryFor(...) 호출 형식은 기존 코드 그대로 유지 (icon, row, col, semanticId, category, confidence, stateGroupId, stateRole)
- 헤더 주석에 "Generated from lean JSON; do not edit by hand. Source: docs/art/modern-ui-style2-icon-catalog-gemini-lean.json" 포함
- 2단계에서 ButtonIconHeightFrame0/1/2, FurnitureChair, FurnitureBed 가 보존되도록 lean 에 보정 entry 추가됐는지 사전 확인
"""
```

스크립트 실행 후 두 .cs 파일이 갱신되어야 한다. **단, `Write` 또는 디스크 직접 쓰기 금지** — 반드시 `script-update-or-create` MCP 도구 경유. 헌법 `rules/unity-cli.md` 절대 원칙.

### 5단계 — 테스트 보정

`Assets/Tests/EditMode/TownConcept/ModernUiStyle2IconCatalogTests.cs` 의 5개 테스트가 새 카탈로그를 통과하도록:

- 테스트 1, 2 는 그대로 통과해야 함 (1666 grid, 마크다운).
- 테스트 3 (r3_c42~c44) — 2단계 결정에 따라:
  - 시각상 height-frame 이 맞으면: lean 보정으로 통과.
  - 시각상 chevron-down 이 맞으면: 테스트 단언 문자열을 `button.chevronDown.state` + normal/hover/pressed 로 갱신.
- 테스트 4 (enum 단언) — 마찬가지로 시각 검증 결과 반영. 단, **새 카테고리 단언 추가**: `panel`, `item`, `glyph`, `buttonMatrix`, `status`, `toggle`, `direction`, `cursor`, `ribbon`, `slot`, `furniture` 가 모두 lean 에 존재하므로 추가 검증으로 활용 가능.
- 테스트 5 (CommonPanel) — 2단계 결정에 따라:
  - 시각상 r2~r4 가 맞으면: 그대로 유지.
  - 시각상 r0~r2 가 맞으면: `ModernUiStyle2Sprites.cs` 의 CommonPanel 좌표를 r0_c0~r2_c2 로 변경 + 본 테스트의 좌표 단언도 갱신. **CommonPanel 좌표 변경 시 `Assets/Scripts/UI/Modern/ModernUiRecipes.cs` 등 호출처가 자동으로 반영되는지 확인** (sprite name 만 바뀌므로 호출 사이드는 변경 불필요).

### 6단계 — 회귀 검증

다음 순서로 실행:

1. Unity Editor 컴파일 통과 확인 (`tests-run` MCP 또는 Unity 콘솔 에러 0).
2. EditMode 테스트 실행:
   - `ModernUiStyle2IconCatalogTests` (5 테스트) — 전부 통과 필수.
   - `ModernUiStyle2RecipeTests`, `ModernUiStyle2RuntimeSourceAuditTests`, `ModernHudSpriteKeysTests` — sprite address 의존, 회귀 없어야 함.
3. `Scripts/ci/check-no-entity-id-branching.sh` — 통과.
4. PlayMode smoke (선택): `TownStyle2UiSmokeTests`, `TownStyle2SpriteExhaustivePlayModeTests` — 실제 sprite 로드 검증. 카탈로그 변경 자체로는 PlayMode 회귀가 없어야 하지만 CommonPanel 좌표 변경 시 필수.

### 7단계 — 커밋

`.claude/rules/commit-conventions.md` 따른 한글 커밋. prefix: `[REFACTOR][ASSET][DOCS]` (카탈로그 재생성 + JSON 갱신 + 마크다운 갱신).

예시:
```
[REFACTOR][ASSET][DOCS] Style2 카탈로그 Gemini 시각 분류로 재생성

- modern-ui-style2-icon-catalog.json 교체 (1666 entries, Gemini 우선 정책)
- ModernUiStyle2Icon/Catalog C# 재생성 (lean 423 entries, unknown 1243 제외)
- 시각 검증 후 ButtonIconHeightFrame*, FurnitureChair/Bed 보존
- CommonPanel 좌표 r2~r4 유지 (시각상 일치 확인)
- ModernUiStyle2IconCatalogTests 5/5 통과, 회귀 없음
- 충돌 리포트: docs/art/modern-ui-style2-icon-catalog-gemini-conflicts.md (504건, Gemini 우선)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
```

## 금지 사항

- 카탈로그 변경을 핑계로 게임플레이 entity id (cropId/toolId/activityId 등) 분기 코드 추가 — 헌법 위반.
- `Assets/**/*.cs` 를 `Write` 또는 외부 IO 로 직접 생성 — Unity MonoScript 인식 실패 위험 (`rules/unity-cli.md`).
- 충돌 리포트의 504건을 임의로 "기존 우선" 으로 되돌리기 — 정책은 Gemini 우선이 이미 확정.
- 테스트를 비활성화하거나 단순 skip 처리하여 통과로 만드는 것. 시각 검증 후 정당한 보정만 허용.

## 산출물 체크리스트

작업 완료 보고 시 다음을 명시:

- [ ] 시각 감사 문서 `docs/art/modern-ui-style2-icon-catalog-visual-audit.md` 작성
- [ ] `docs/art/modern-ui-style2-icon-catalog.json` 백업 + 교체
- [ ] `docs/art/modern-ui-style2-icon-catalog.md` 재생성
- [ ] `Assets/Scripts/Game/Common/ModernUiStyle2Icon.cs` 재생성 (423 enum members + Unknown* 제외)
- [ ] `Assets/Scripts/Game/Common/ModernUiStyle2IconCatalog.cs` 재생성
- [ ] `Assets/Tests/EditMode/TownConcept/ModernUiStyle2IconCatalogTests.cs` 보정
- [ ] (선택) `Assets/Scripts/Game/Common/ModernUiStyle2Sprites.cs` CommonPanel 좌표 보정
- [ ] EditMode 테스트 5/5 통과 로그
- [ ] PlayMode smoke 통과 로그 (CommonPanel 변경 시)
- [ ] `Scripts/ci/check-no-entity-id-branching.sh` 통과 로그
- [ ] 단일 한글 커밋 생성

## 의문이 생기면

- 시각 판단이 애매하면 **추측하지 말고 PNG 픽셀을 직접 다시 보고 결정**한다.
- 좌표/메카닉 변경이 사용자 의도와 다를 위험이 있으면 **변경하지 말고 보고서에 "TODO: 사용자 확인 필요" 로 남긴다**.
- 코드 생성이 막히면 (script-update-or-create 실패 등) 사용자에게 그 시점에 보고하라. 우회 디스크 쓰기 금지.

이 프롬프트는 그 자체로 완결되어 있다. 추가 컨텍스트 없이 작업을 시작하라.
