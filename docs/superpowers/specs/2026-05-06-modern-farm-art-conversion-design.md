# Modern Farm Art Conversion Design

Date: 2026-05-06
Status: Draft approved for planning

## Goal

ROOTBORN의 기존 Pixelwood Valley 기반 시각 체계를 LimeZu Modern 계열 기반의 modern farm 스타일로 전환한다. 전환 범위는 UI만이 아니라 인벤토리 아이콘, 캐릭터, 애니메이션, 농장 타일, 작물, 자원 오브젝트, 실내/실외 공간까지 포함한다.

게임의 농장 루프는 유지한다. 시각 표현은 현대식 농장, 온실, 실내 텃밭, 작업실, 창고, 집, 상점, 마을 시설을 조합한 modern farm으로 재해석한다.

## Asset Sources

표준 아트 소스는 LimeZu Modern 계열로 고정한다.

| Source | Local path | Observed PNG count | Role |
| --- | --- | ---: | --- |
| Modern Interiors | `Assets/moderninteriors-win` | 51,904 | 실내 공간, 가구, 캐릭터, animated objects, 기본 UI 보조 |
| Modern Farm | `Assets/Modern_Farm_v1.2` | 7,833 | 농장 타일, 작물, 동물, 농장 props, farmer generator, 농장 아이콘 |
| Modern User Interface | `Assets/modernuserinterface-win` | 912 | UI 프레임, 버튼, 슬롯, portrait/dialogue UI |
| Modern Office Revamped | `Assets/Modern_Office_Revamped_v1.2` | 1,047 | 현대 작업실/관리 UI 공간 보조 후보 |
| Modern Interiors RPG Maker Version | `Assets/Modern_Interiors_RPG_Maker_Version` | 494 | 호환 보조 후보. 주 source가 부족할 때만 사용 |

Modern Interiors 라이선스는 상업/비상업 사용과 수정을 허용하고, 재판매/재배포를 금지하며 `limezu.itch.io` 크레딧을 요구한다. Modern User Interface는 NFT minting 제외 조건과 크레딧 필수 조건이 있다. Modern Farm은 상업/비상업 사용과 수정을 허용하고 재판매/재배포를 금지하며 크레딧을 권장한다. 크레딧 표기는 릴리즈 문서와 게임 credits 화면에 반영한다.

Pixelwood Valley는 즉시 삭제하지 않는다. 전환 중에는 legacy fallback으로 격리하고, modern 매핑이 검증된 뒤 Addressables, ScriptableObject, scene, prefab 참조에서 단계적으로 제거한다.

## Current Integration Points

현재 Pixelwood 참조는 다음 경로에 걸쳐 있다.

| Area | Current files | Conversion target |
| --- | --- | --- |
| Sprite slicing | `Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs` | `ModernAssetSliceSetup` 또는 범용 `SpriteSheetSliceSetup` 추가 |
| Addressables | `Assets/Scripts/Editor/Tools/AddressablesSetup.cs` | Modern sprite address catalog로 교체 |
| UI addresses | `Assets/Scripts/Game/Common/GameDataRegistry.cs`의 `UISpriteAddresses` | Modern UI 주소와 sub-sprite 이름으로 갱신 |
| Runtime preload | `Assets/Scripts/Game/Managers/Managers.cs`, `ResourceManager.cs` | sheet/single sprite preload 유지, 주소만 modern화 |
| UI construction | `Assets/Scripts/UI/HUD/StatusHud.cs` | book/fantasy UI를 modern panels/dialogue/slots로 대체 |
| Item/tool icons | `Assets/Scripts/Editor/Tools/ItemIconWiring.cs` | Modern Farm/Icon sheet 기반 매핑으로 대체 |
| Default data generation | `Assets/Scripts/Editor/Tools/GenerateDefaultData.cs` | Pixelwood hard path 제거, modern catalog 사용 |
| Game data | `Assets/Data/**/*.asset`, `Assets/Resources/GameDataRegistry.asset` | SO sprite/icon 필드 modern sprite로 재와이어링 |
| Scenes/prefabs | `Assets/Scenes/Farm.unity`, `Assets/Prefabs/Player.prefab` | modern tilemap, props, player animation으로 전환 |

기존 조사에서 `GenerateDefaultData.cs`는 `Assets/Pixelwood Valley Icon Pack 1.0/...` 경로를 참조하지만 실제 import 위치는 `Assets/Pixelwood Valley/Pixelwood Valley Icon Pack 1.0/...`이다. modern 전환 시 이 경로 불일치는 legacy 정정이 아니라 Pixelwood 경로 제거로 해결한다.

## Inventory And Classification

에셋 수가 크기 때문에 수작업 전수조사는 하지 않는다. 먼저 자동 리포트를 생성한다.

리포트 산출물:

- `docs/art/modern-asset-inventory.md`: 사람용 요약
- `docs/art/modern-asset-inventory.json`: 도구용 원본 목록

각 PNG는 다음 필드로 분류한다.

- `path`
- `sourcePack`
- `topFolder`
- `width`
- `height`
- `gridCandidate`: `16x16`, `32x32`, `48x48`, `mixed`, `single`
- `role`: `ui`, `icon`, `tile`, `crop`, `character`, `animal`, `prop`, `animatedObject`, `portrait`, `palette`, `preview`, `legacyCandidate`
- `sliceMode`: `single`, `grid`, `explicitRects`, `nineSlice`, `ignore`
- `priority`: `core`, `support`, `optional`, `ignore`
- `notes`

분류는 파일명과 폴더명을 기반으로 자동 1차 판정하고, core 후보만 수동 검수한다. `preview`, `guide`, `palette`, `RPG_Maker` 전용 파일은 기본적으로 `ignore` 또는 `optional`로 둔다.

## Slice Rules

Unity importer 변경은 C# Editor tool로만 수행한다. `Assets/**/*.cs` 파일은 디스크 직접 쓰기를 금지하고, 신규 C# 작성이 필요하면 MCP `script-update-or-create` 또는 Unity Editor 경유만 사용한다.

기본 importer 정책:

- Texture Type: Sprite
- Filter Mode: Point
- Compression: Uncompressed
- Mip Maps: Off
- Wrap Mode: Clamp
- Alpha Is Transparency: true
- PPU: 16px tiles는 16, 32px character/icon sheet는 32, 48px character/object sheet는 48

slice 규칙:

- 16/32/48 정규 sheet는 grid slice
- UI frame과 panel은 9-slice border를 명시적으로 지정
- Portrait/dialogue frame은 single sprite 또는 explicit rect
- Character animation은 방향/동작별 sub-sprite 이름을 고정 규칙으로 생성
- Crop growth sheet는 `Crop_<cropKey>_Stage_<index>` 식으로 명명하되, crop-specific C# 분기는 만들지 않는다

## Modern Catalog

새 카탈로그는 아트 주소와 역할을 분리한다.

1. `ModernSpriteCatalog` ScriptableObject
   - UI single sprites
   - UI sheet sub-sprite names
   - icon sheet references
   - character animation sheet references
   - terrain/tile sprite references

2. `ModernAssetManifest` ScriptableObject
   - source pack별 root path
   - slice profile
   - Addressables address prefix
   - legacy fallback 허용 여부

3. Existing SO rewiring
   - `ItemDefinition.Icon`
   - `ToolDefinition.Icon`
   - `CropDefinition.GrowthStageSprites`
   - `ResourceNodeDefinition.Sprite`
   - `GameDataRegistry.GroundSprite`
   - `GameDataRegistry.PlayerSprite`

엔티티별 C# 분기, ID enum, `if (cropId == "...")`, `switch(toolId)`는 금지한다. 작물/도구/자원별 시각 차이는 ScriptableObject 필드와 catalog data로만 표현한다.

## Conversion Phases

### Phase 1: Audit And Catalog

Modern 에셋 전체 리포트를 생성한다. Pixelwood 참조 위치를 모두 조사하고, 각 참조를 modern catalog 항목으로 매핑한다.

완료 조건:

- modern asset inventory markdown/json 생성
- Pixelwood reference report 생성
- core asset 후보 목록 검수
- UI, icon, crop, resource, character, tile mapping table 작성

### Phase 2: UI Conversion

Fantasy Book UI와 Wood UI 의존을 Modern User Interface 기반으로 교체한다. `UISpriteAddresses`와 `AddressablesSetup` entry를 modern 주소로 바꾼다.

완료 조건:

- HUD panel, hint panel, button, slot, ribbon, dialogue/book UI가 modern asset을 사용
- `UISpriteAddressesTests`를 modern 주소 기준으로 갱신
- fallback color UI가 정상 동작
- Game View에서 주요 HUD가 깨지지 않음

### Phase 3: Icons And Data SO

Modern Farm/Modern UI icon sheet를 기준으로 item/tool/resource/status/knowledge icon을 분류하고 ScriptableObject에 와이어링한다.

완료 조건:

- `ItemDefinition`과 `ToolDefinition` icon이 modern sprite 참조
- icon mapping은 data table 기반
- `ItemIconWiring`은 Pixelwood 경로를 참조하지 않음
- 신규/기존 SO는 `GameDataRegistry`에 등록

### Phase 4: Farm World Visuals

Farm scene의 ground tile, crop sprites, tree/rock 같은 resource nodes를 Modern Farm 기반으로 교체한다. 농장 표현은 modern farm, greenhouse, planter, work shed, yard tiles 중심으로 잡는다.

완료 조건:

- `GroundTile.asset` modern tile 사용
- `Crop_Wheat` 등 crop growth sprites modern crop sheet 사용
- resource node sprites modern props/resources 사용
- Farm scene과 seeded world applier에서 visible sprite 누락 없음

### Phase 5: Character And Animation

Player Character sheet를 Modern Farm farmer 또는 Modern Interiors character generator 기반으로 교체한다. 기존 tool sprite prefix 구조는 유지하되, prefix와 sheet 주소를 modern data로 옮긴다.

완료 조건:

- Idle/Walk animation clip sprite references modern화
- tool animation sheet preload 동작
- player prefab SpriteRenderer/Animator가 modern sprites 사용
- 기존 movement/playmode scenario가 유지

### Phase 6: Legacy Pixelwood Removal

모든 core scene, prefab, SO, Addressables, runtime code에서 Pixelwood 참조를 제거한다. 필요한 경우 Pixelwood 에셋은 별도 legacy 폴더 또는 미사용 에셋으로만 남긴다.

완료 조건:

- runtime/editor code에서 `Pixelwood` direct path 참조 없음
- Addressables active group에서 Pixelwood sprite entry 없음
- `Assets/Data`와 scene/prefab YAML에서 Pixelwood sprite guid 참조 없음
- CI 테스트와 Unity smoke test 통과

## Testing

TDD 원칙을 따른다. 각 구현 phase는 실패 테스트를 먼저 추가하고 구현한다.

필수 테스트:

- `ModernAssetInventoryTests`: inventory 생성 결과가 core packs를 포함
- `ModernSpriteAddressTests`: catalog 주소와 Addressables entry 일치
- `ModernSpriteFileExistenceTests`: catalog가 가리키는 파일 존재
- `ModernIconMappingTests`: item/tool icon mapping 누락 없음
- `ModernCropSpriteWiringTests`: crop growth sprites stage count 유효
- `ModernResourceSpriteWiringTests`: resource node sprites 누락 없음
- `ModernFarmSceneVisualSmokeTests`: Farm scene sprite renderer 누락/빨간 fallback 검사
- 기존 `UISpriteAddressesTests` modern 기준 갱신

검증 명령:

- 영향 범위 EditMode 테스트
- Farm 관련 PlayMode smoke test
- `Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64`

## Risks

| Risk | Mitigation |
| --- | --- |
| Modern packs의 파일 수가 매우 커 수동 분류가 불가능 | inventory generator로 자동 분류 후 core 후보만 수동 검수 |
| 여러 pack의 같은 역할 sprite가 중복 | source priority를 `Modern Farm > Modern UI > Modern Interiors > optional packs`로 고정 |
| UI 9-slice border가 부정확 | preview scene 또는 isolated render로 panel/button/slot 확인 |
| Pixelwood guid가 SO/scene에 남음 | reference report와 YAML guid scan으로 phase별 제거 |
| Character frame 규칙이 기존 59x49 Pixelwood와 다름 | character animation adapter를 data-driven sheet profile로 분리 |
| 농장 기능이 art 교체 중 깨짐 | 작물/도구/자원 동작 테스트는 유지하고 sprite 참조만 변경 |

## Open Decisions

이미 결정된 사항:

- 전체 전환한다.
- modern과 farm을 모두 만족해야 한다.
- LimeZu Modern 계열 통합 접근을 사용한다.
- 추가 modern farm asset은 허용하며, `Assets/Modern_Farm_v1.2`가 프로젝트에 존재한다.

남은 결정은 구현 계획에서 core 후보 sprite를 실제로 선택할 때 처리한다. 디자인 관점에서는 pack priority와 phase 순서가 고정되어 있다.

## Approval Gate

이 spec이 승인되면 다음 단계는 `writing-plans`를 사용해 phase 1부터 구현 계획을 작성하는 것이다. 구현 계획 전에는 slice 실행, importer 변경, code edit, scene/prefab 변경을 하지 않는다.
