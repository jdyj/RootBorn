# SPUM Character Creator UI Spec

작성일: 2026-05-14

## 목적

New Game 시작 전에 플레이어가 SPUM 파츠 기반 외관을 선택하고, 선택 결과를 저장 슬롯 metadata에 기록한 뒤 Town/Farm 진입 시 Player visual에 같은 외관을 적용한다. 이 UI는 목표/퀘스트/캠페인 UI가 아니므로 `ObjectiveJournalPanel`에 넣지 않는다.

## 선택한 흐름

추천 흐름은 Option UI-B다.

1. MainMenu에서 Save Slots를 연다.
2. 빈 슬롯의 `New Game` 버튼을 클릭한다.
3. `SpumCharacterCreatorPanel`이 modal flow처럼 열린다.
4. 플레이어가 category tab과 part grid, 이전/다음 버튼, random 버튼으로 외관을 선택한다.
5. Confirm을 누르면 `CharacterAppearanceSnapshot`을 포함한 save metadata를 생성한다.
6. SaveSlot flow가 Town/Farm을 로드한다.
7. Player runtime adapter가 snapshot을 해석해 같은 SPUM visual을 적용한다.

Cancel은 슬롯 카드 화면으로 돌아간다. Confirm 전에는 save metadata를 쓰지 않는다.

## 화면 구조

기준 해상도는 1920x1080이고 Canvas Scaler는 `ScaleWithScreenSize`, reference resolution `1920x1080`, match `0.5`를 따른다.

```text
SpumCharacterCreatorPanel
  Header band
    Title: Character
    Back button
    Random button
    Confirm button
  Body
    Left: category rail
      Body/Skin
      Eyes
      Hair
      Outfit
      Accessory
      Back
      Helmet
      Weapon Preview
    Center: preview stage
      UI-only preview root
      rotate/facing controls
      tool preview toggle
    Right: part grid
      category title
      paged grid cells
      previous/next page buttons
  Footer
    selected part chips
    validation/status line
```

패널 배경과 주요 구획은 `ModernUiTileImage + ModernUiRecipes.CommonPanel`을 사용한다. 단일 `Image.sprite`를 패널 배경으로 직접 넣지 않는다. 카드 안에 카드를 중첩하지 않고, part grid cell은 repeated item card로만 사용한다.

## 입력/클릭 흐름

- Category tab click: active category를 바꾸고 grid를 해당 category part 목록으로 갱신한다.
- Part cell click: `CharacterAppearanceSnapshot.selectedParts[categoryId]`를 갱신하고 preview에 즉시 반영한다.
- Previous/Next part button: 현재 category 안에서 stable sort order 기준으로 이전/다음 part를 선택한다.
- Previous/Next page button: grid page만 바꾸며 선택값은 바꾸지 않는다.
- Random button: `SpumCharacterCreatorPresetDefinition`의 allowed pool에서 category별 part를 선택한다. 같은 seed를 쓰면 같은 결과를 낼 수 있어야 한다.
- Facing control: preview facing만 바꾸며 저장 snapshot에는 넣지 않는다.
- Weapon preview toggle/cell: preview visual만 바꾼다. 실제 시작 장착 도구는 inventory/starting tool 데이터가 소유한다.
- Back/Cancel: 변경 중인 draft snapshot을 버리고 SaveSlot 화면으로 돌아간다.
- Confirm: draft snapshot을 resolve/fallback 처리한 뒤 save metadata를 생성한다.

긴 설명 문구는 넣지 않는다. 조작 가능한 tab, button, grid, preview 상태로 기능을 표현한다.

## Category와 Part 목록

UI는 Addressables label을 직접 읽지 않고 `SpumPartCatalogDefinition`을 읽는다. catalog는 다음 정보를 제공한다.

- category id
- localized display key
- sort order
- default part
- random 포함 여부
- preview sprite reference
- runtime visual reference

초기 category는 다음을 지원한다.

- `body`
- `skin`
- `eyes`
- `hair`
- `outfit`
- `accessory`
- `back`
- `helmet`
- `weapon-preview`

실제 SPUM 패키지에서 category가 더 세분화되면 migration tool이 ROOTBORN category로 매핑한다. 코드에서 SPUM 폴더명 또는 part id별 분기를 만들지 않는다.

## Preview 갱신 흐름

preview는 실제 Player prefab을 쓰지 않는다. `SpumCharacterCreatorPreview`가 UI 전용 root를 만들고 `ICharacterVisualView` 구현을 붙인다.

1. panel open 시 catalog metadata와 preview sprites를 preload한다.
2. default preset으로 draft snapshot을 만든다.
3. preview root가 `ApplyAppearance(snapshot)`을 호출한다.
4. part cell click 또는 random 후에는 변경된 category만 반영하되, 구현이 단순할 때는 snapshot 전체 재적용을 허용한다.
5. full runtime prefab/clip은 preview root 1개에만 load한다.
6. panel close 시 preview root와 temporary handles를 release한다.

preview stage는 world Player와 같은 `VisualScale`을 사용하고 UI 안에서는 camera/RectTransform framing으로 크기만 맞춘다. 이렇게 해야 UI에서 맞춘 외관이 Town에서 다른 크기로 보이는 문제를 줄일 수 있다.

## Save/Load 흐름

Confirm 시 저장하는 값:

```text
CharacterAppearanceSnapshot
  schemaVersion = 1
  visualKind = "spum"
  appearanceDefinitionId
  catalogId
  selectedParts[]
    categoryId
    partId
```

저장하지 않는 값:

- Sprite reference
- prefab reference
- AnimationClip reference
- Addressables handle
- preview-only facing
- preview-only weapon selection

Load 시:

1. `SaveSlotMetadata.AppearanceSnapshot`을 읽는다.
2. registry에서 `appearanceDefinitionId`와 `catalogId`를 찾는다.
3. 누락 part id는 같은 category default로 대체한다.
4. default가 없으면 category를 비우고 warning을 남긴다.
5. resolved snapshot을 visual adapter에 전달한다.

catalog가 업데이트되어도 기존 save가 깨지지 않도록 original snapshot은 load 과정에서 즉시 파괴하지 않는다. 다음 save 시점에 resolved snapshot을 기록한다.

## Style2 적용 지점

- Root modal panel: `ModernUiTileImage + ModernUiRecipes.CommonPanel`
- Category rail background: common panel
- Preview stage background: common panel
- Part grid background: common panel
- Grid cell selected/hover state: Style2 button/slot recipe를 별도 정의하되, panel 안에 panel을 중첩하지 않는다.

`SpumCharacterCreatorPanelStyleTests`는 panel hierarchy를 검사해 `ModernUiTileImage`가 필요한 곳에 있는지, common panel recipe가 적용되는지, root panel background에 단일 `Image.sprite`가 직접 설정되지 않았는지 확인한다.

## Loading과 성능

- panel open 전에 catalog SO와 preview sprites만 preload한다.
- category grid는 visible cell 수만 만든다. category part가 많으면 page 단위로 재사용한다.
- preview root는 1개만 유지한다.
- random은 catalog cache에서 선택하고 매 클릭마다 Addressables label scan을 하지 않는다.
- Confirm 전에는 Town Player prefab이나 NPC prefab을 instantiate하지 않는다.
- Addressables full prefab/clip load는 preview root와 Town Player/NPC 생성 시점으로 제한한다.

## 필요한 PlayMode 테스트

- `SPUM_UI_PM_001_NewGameOpensCreator`: 실제 `NewGameButton` 클릭으로 creator panel이 열린다.
- `SPUM_UI_PM_002_ClickPartUpdatesPreview`: 실제 category tab과 part cell 클릭으로 preview selected part가 바뀐다.
- `SPUM_UI_PM_003_RandomCreatesResolvedAppearance`: random 클릭 후 모든 required category가 fallback 포함 resolved 상태가 된다.
- `SPUM_UI_PM_004_ConfirmCreatesSaveAndLoadsTown`: Confirm 클릭 후 save metadata에 SPUM snapshot이 있고 Town Player visual이 같은 snapshot을 사용한다.
- `SPUM_UI_PM_005_CancelDoesNotCreateSave`: Cancel 클릭 시 빈 슬롯 metadata가 생성되지 않는다.
- `SPUM_UI_PM_006_SaveLoadKeepsAppearance`: 저장 후 같은 슬롯 load 시 SPUM appearance가 유지된다.

모든 PlayMode 테스트는 UI button click, keyboard/mouse input, scene transition 관찰을 사용한다. panel 내부 메서드 직접 호출만으로 완료 판정하지 않는다.

## 선택하지 않은 UI 대안

- UI-A, `SaveSlotSelectPanel` 내부 step: 현재 panel이 슬롯 카드, save 생성, preview까지 소유한다. SPUM category/grid/preload를 넣으면 책임이 커지고 Style2 검증 범위도 흐려진다.
- UI-C, MainMenu 별도 Character 메뉴: 저장 슬롯 생성과 외관 선택이 분리되어 저장 대상이 모호해진다. 새 게임 첫 흐름에서는 불필요한 단계가 늘어난다.
- 기존 Pixelwood 선택 UI와 완전 공용화: 기존 UI는 간단한 next 버튼 중심이고 SPUM은 catalog/grid/preview/preload가 필요하다. 공통화는 snapshot contract와 preview adapter 수준으로 제한한다.

