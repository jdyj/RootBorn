# Modern Interiors Shadowless House Placement Goal

## Goal

`C:\Users\jdyj\Downloads\moderninteriors-win`의 Modern Interiors 에셋 중 `1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48` 폴더만 전수 조사하고, 현재 ROOTBORN House 씬의 furniture placement 시스템에 적용 가능한 데이터 기반 가구 카탈로그로 전환한다.

이 goal의 핵심은 Modern Interiors 전체를 한 번에 섞는 것이 아니라, 중복과 그림자 변형 혼입을 피하기 위해 shadowless single PNG만 House 배치 후보로 제한하는 것이다.

## Source Scope

이번 goal에서 조사하고 사용할 폴더는 하나뿐이다.

```text
C:\Users\jdyj\Downloads\moderninteriors-win\1_Interiors\48x48\Theme_Sorter_Shadowless_Singles_48x48
```

다음 폴더는 이번 goal에서 사용하지 않는다.

```text
C:\Users\jdyj\Downloads\moderninteriors-win\1_Interiors\48x48\Theme_Sorter_Singles_48x48
C:\Users\jdyj\Downloads\moderninteriors-win\1_Interiors\48x48\Theme_Sorter_Black_Shadow_Singles_48x48
C:\Users\jdyj\Downloads\moderninteriors-win\1_Interiors\48x48\Theme_Sorter_48x48
C:\Users\jdyj\Downloads\moderninteriors-win\1_Interiors\48x48\Theme_Sorter_Shadowless_48x48
C:\Users\jdyj\Downloads\moderninteriors-win\1_Interiors\48x48\Theme_Sorter_Black_Shadow_48x48
```

제외 이유:

- 같은 파츠의 shadow, shadowless, black-shadow 변형이 동시에 들어오면 `FurnitureDefinition` 중복 등록 위험이 크다.
- House 배치 시스템은 플레이어가 하나의 가구 오브젝트를 선택하고 Tilemap에 배치하는 구조이므로, 첫 적용은 대표 sprite가 명확한 shadowless single PNG로 제한한다.
- 그림자 표현은 별도 shadow layer, sorting order, Tilemap 레이어 정책이 정해진 뒤 별도 goal로 확장한다.

## Current Project Context

현재 프로젝트에는 House 배치에 사용할 기반 시스템이 이미 있다.

- `Assets/Scripts/Game/Placement/FurnitureDefinition.cs`
- `Assets/Scripts/Game/Placement/FurniturePlacementService.cs`
- `Assets/Scripts/Game/Placement/FurniturePlacementRegistry.cs`
- `Assets/Scripts/Game/Placement/TilePlacementSurface.cs`
- `Assets/Scripts/UI/Interiors/InteriorFurnitureCatalog.cs`
- `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`
- `Assets/Scripts/Editor/Interiors/FurnitureSpriteAutoImporter.cs`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Scenes/House.unity`

따라서 새 배치 시스템을 만들지 말고, 기존 SO/registry 기반 furniture placement 흐름에 Modern Interiors shadowless singles를 연결한다.

## Required Inventory

`Theme_Sorter_Shadowless_Singles_48x48` 아래의 모든 하위 폴더를 조사한다.

각 하위 폴더마다 다음 정보를 남긴다.

- 폴더명
- PNG 개수
- 대표 파일명 3~5개
- 대표 이미지 크기
- House 배치 후보 여부
- 추천 카테고리
- 단일 타일, 다중 타일, 벽 장식, 바닥 장식, 상호작용 후보 여부

우선 적용 후보는 House와 직접 관련 있는 카테고리부터 시작한다.

- `2_Living_Room_Singles_Shadowless_48x48`
- `3_Bathroom_Singles_Shadowless_48x48`
- `4_Bedroom_Singles_Shadowless_48x48`
- `12_Kitchen_Singles_Shadowless_48x48`
- `20_Japanese_Interiors_Singles_Shadowless_48x48`
- `26_Condominium_Singles_Shadowless_48x48`

그 외 폴더는 카탈로그 후보로 조사하되, House 1차 배치 적용 대상과 별도 후보로 분리한다.

## Asset Import Direction

Unity 프로젝트 안에서 사용할 때는 에셋을 `Assets/moderninteriors-win` 경로 아래로 가져오는 것을 기준으로 한다. 기존 코드와 테스트가 이 경로를 참조하고 있으므로 다른 위치를 새 기준으로 만들지 않는다.

가구 PNG는 다음 방식으로 처리한다.

1. Shadowless single PNG를 Unity Sprite로 import한다.
2. Sprite pixels per unit은 48 기준으로 맞춘다.
3. 각 PNG에 대응하는 `Tile` asset을 생성한다.
4. 각 가구 후보에 대응하는 `FurnitureDefinition` SO를 생성한다.
5. 생성된 SO를 `Assets/Data/Interiors/Furniture/AutoImported/ModernInteriorsShadowless/...` 아래에 둔다.
6. 생성된 SO를 `Assets/Data/Registry/GameDataRegistry.asset`에 등록한다.

런타임 코드에서 특정 furniture id, 파일명, 폴더명별 `if/switch` 분기를 만들지 않는다. 분류 결과는 ScriptableObject 데이터와 에디터 import 규칙으로 표현한다.

## Furniture Definition Rules

각 `FurnitureDefinition`은 최소한 다음 데이터를 가진다.

- stable id
- display name
- category
- allowed surfaces
- tile parts
- footprint cells
- blocks movement
- placement preference
- optional unlock token

`allowed surfaces`에는 House 배치가 가능하도록 `house`를 포함한다. 기존 non-House 테스트 surface가 필요한 경우 `town-test` 같은 테스트 surface는 별도 테스트 목적에 한해 포함할 수 있다.

PNG 크기가 48의 배수이면 크기 기반으로 footprint를 추론한다. 파일명이나 이미지 크기로 footprint를 확정하기 어려운 경우 1x1 fallback으로 두되, fallback 목록을 별도로 보고한다.

## Category Policy

카테고리는 플레이어가 House UI에서 이해할 수 있는 단위로 정리한다.

권장 카테고리:

- LivingRoom
- Bedroom
- Bathroom
- Kitchen
- Japanese
- Condominium
- Decor
- WallDecor
- FloorDecor
- Utility
- Misc

카테고리명은 runtime 분기 조건이 아니라 UI 그룹과 SO 데이터 분류용으로만 사용한다.

## House Placement Flow

최종 사용자 흐름은 실제 House 씬에서 다음처럼 동작해야 한다.

1. 플레이어가 House 씬에 진입한다.
2. House furniture placement UI가 열린다.
3. Modern Interiors shadowless 카테고리 또는 해당 카테고리의 가구 버튼이 보인다.
4. 플레이어가 UI에서 가구를 클릭한다.
5. 배치 가능 위치가 overlay로 표시된다.
6. 플레이어가 실제 월드 위치를 마우스로 클릭한다.
7. 선택한 가구가 `HouseFurnitureObjectTilemap`에 보인다.
8. 점유 정보가 `HouseFurnitureOccupancyTilemap`에 기록된다.
9. 배치 후 overlay가 사라진다.
10. 기존 삭제, 이동, 저장, 로드 흐름과 충돌하지 않는다.

## Home Designs And Animated Objects

이번 goal에서는 `6_Home_Designs`와 `3_Animated_objects`를 구현 범위에 포함하지 않는다.

이유:

- 이번 goal은 중복 없는 shadowless single furniture catalog 적용이 목적이다.
- `6_Home_Designs`는 완성형 room template 레퍼런스이며, 개별 가구 배치 카탈로그와 다른 문제다.
- `3_Animated_objects`는 animation clip, prefab, state, interaction wiring이 필요하므로 별도 goal로 분리해야 한다.

필요하다면 최종 보고서에 후속 goal 후보로만 남긴다.

## Tests

TDD로 진행한다. 구현 전 실패 테스트를 먼저 작성한다.

### EditMode

- `Theme_Sorter_Shadowless_Singles_48x48`만 source scope로 인식하는지 검증한다.
- 제외 폴더가 import 대상에 들어가지 않는지 검증한다.
- 하위 폴더별 PNG count inventory가 생성되는지 검증한다.
- PNG 크기 기반 footprint 계산이 48px 단위로 동작하는지 검증한다.
- fallback footprint가 발생하면 목록으로 보고되는지 검증한다.
- 생성된 `FurnitureDefinition`이 stable id, display name, category, allowed surface, tile part, footprint를 가진다.
- 생성된 `FurnitureDefinition`이 `GameDataRegistry.asset`에 등록된다.
- runtime code에 furniture id별 분기나 folder name별 분기가 생기지 않는지 CI로 검증한다.

### PlayMode

- House 씬에서 Modern Interiors shadowless furniture가 player-facing palette에 보이는지 검증한다.
- 실제 UI 클릭으로 가구를 선택한다.
- 실제 월드 클릭으로 가구를 배치한다.
- 배치 후 `HouseFurnitureObjectTilemap`에 선택한 tile name과 sprite name이 기록되는지 확인한다.
- `HouseFurnitureOccupancyTilemap`에 footprint 점유 셀이 기록되는지 확인한다.
- overlay가 배치 후 사라지는지 확인한다.
- sample/debug tilemap이나 placeholder object가 Game View에 남지 않는지 확인한다.
- 저장/로드 후 같은 furniture id와 cell 위치가 복원되는지 확인한다.

## Direct Visual Verification Gate

이 goal은 자동 테스트만으로 완료 처리하지 않는다.

완료 전 반드시 다음을 수행한다.

1. PlayMode에서 실제 House 씬 진입
2. 실제 UI 클릭으로 Modern Interiors shadowless 가구 선택
3. 실제 월드 클릭으로 배치
4. Game View 또는 Camera screenshot 캡처
5. 런타임 Tilemap inventory 출력
6. 저장되어야 하는 scene, prefab, ScriptableObject, registry 상태를 EditMode에서 별도 확인
7. Unity Console에 관련 Error 또는 Exception이 없는지 확인

Tilemap inventory에는 다음을 포함한다.

- surface id
- object tilemap name
- occupancy tilemap name
- selected furniture id
- selected asset path
- tile name
- sprite name
- anchor cell
- occupied cells
- overlay valid count
- overlay invalid count
- stale sample/debug tilemap 존재 여부

## CI And Verification Commands

최소한 다음을 실행하고 결과를 보고한다.

```bash
Scripts/ci/check-no-entity-id-branching.sh
```

Unity EditMode/PlayMode 테스트는 변경 범위에 맞춰 실행한다.

권장 테스트 범위:

- `Rootborn.Tests.EditMode.Placement`
- `Rootborn.Tests.EditMode.Interiors`
- `Rootborn.Tests.PlayMode.Interiors`
- `Rootborn.Tests.PlayMode.Placement`

## Done Criteria

이 goal은 다음이 모두 만족될 때 완료된다.

- 조사 대상이 `Theme_Sorter_Shadowless_Singles_48x48` 하나로 제한되어 있다.
- shadow 있는 Singles, Black Shadow Singles, 통합 spritesheet가 import 대상에서 제외되어 있다.
- 하위 폴더별 inventory가 문서화되어 있다.
- House 1차 후보 카테고리의 PNG가 `FurnitureDefinition` SO로 생성되어 있다.
- 생성된 SO가 `GameDataRegistry.asset`에 등록되어 있다.
- House player-facing UI에서 새 가구를 선택할 수 있다.
- 실제 마우스 클릭으로 House Tilemap에 배치된다.
- Game View 또는 Camera screenshot으로 가시 결과를 확인했다.
- Tilemap inventory로 tile name, sprite name, cell, occupancy를 확인했다.
- 저장/로드 후 배치가 복원된다.
- 관련 EditMode 테스트가 통과한다.
- 관련 PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- Unity Console에 관련 Error/Exception이 없다.

## Explicit Non-Goals

- `Theme_Sorter_Singles_48x48` import
- `Theme_Sorter_Black_Shadow_Singles_48x48` import
- shadow layer 자동 생성
- `6_Home_Designs` 기반 씬 템플릿 생성
- `3_Animated_objects` 애니메이션 클립/프리팹 생성
- 새로운 placement system 재작성
- Objective Journal 또는 HUD 변경
- House 외 전체 Town/상점/학교 배치 확장
