# House Furniture Placement Visual Bugfix And Camera Control Goal

## Goal

House 씬의 가구 배치 기능을 실제 플레이 기준으로 점검하고, 현재 직접 플레이 중 발견된 책상/의자 배치 문제와 카메라 조작 불가 문제를 수정한다.

이번 goal은 새 가구 시스템을 전면 재작성하는 작업이 아니다. 이미 구현된 `FurnitureDefinition`, `TilePlacementSurface`, `FurniturePlacementService`, `FurniturePlacementRegistry`, `InteriorFurniturePlacementUi`, `InteriorPlacementPreviewOverlay`, House 씬 배치 흐름을 기준으로 사용자에게 보이는 문제를 재현하고 고친다.

자동 EditMode/PlayMode 테스트는 필수지만 충분 조건이 아니다. 이 goal은 실제 player-facing flow에서 UI 클릭, 타일 선택, 가구 배치, 카메라 조작, Game View 또는 Camera screenshot, runtime Tilemap 상태 검사를 모두 통과해야 완료된다.

## User-Reported Problems

### 1. Desk 선택 시 잘못된 크기와 부속 오브젝트가 붙는 문제

House 씬에서 가구 배치 UI를 열고 책상을 선택했을 때, 사용자가 기대하는 책상은 1x1 가구인데 실제로는 2x1처럼 보이며 오른쪽에 컴퓨터처럼 보이는 타일이 자동으로 붙는다.

확인할 내용:

- 현재 palette에서 사용자가 누르는 책상 버튼이 어떤 `FurnitureDefinition` stable id에 매핑되는지 확인한다.
- 해당 definition의 tile parts, footprint, local offsets가 실제 의도와 맞는지 확인한다.
- 파일명 뒤 `_wxh` 규칙이 적용되어야 하는 경우, `1x1` 책상이 1개 타일만 점유하는지 확인한다.
- `desk.large` 또는 기존 desk cluster 정의가 일반 책상 선택에 잘못 노출되고 있는지 확인한다.
- 기존 자동 생성용 Desk/Chair/Computer cluster 로직이 수동 배치 경로에 섞여 들어오지 않는지 확인한다.
- 사용자가 선택한 책상 하나가 의도치 않게 computer tile part를 포함하지 않도록 수정한다.

수정 방향:

- 책상이 실제로 1x1이어야 한다면 1x1 `FurnitureDefinition`을 별도로 만들거나, 현재 노출되는 책상 버튼을 올바른 1x1 definition으로 연결한다.
- 2x1 desk+computer 조합이 필요하다면 palette에서는 별도 이름, stable id, icon으로 구분한다.
- runtime code에서 특정 furniture id별 `if`, `switch`, enum 분기는 추가하지 않는다. 모든 차이는 ScriptableObject definition의 tile parts, footprint, category, display name으로 표현한다.

### 2. blackChair 배치 시 위치가 타일에 맞지 않거나 잔상이 보이는 문제

blackChair를 배치했을 때 타일 중심보다 살짝 아래로 내려온 듯 보이는지 확인한다. 실제로 정상 정렬이라면 코드나 에셋을 수정하지 않고, Game View screenshot과 Tilemap 상태로 정상임을 증명한다.

추가로, 첫 번째 의자를 배치했을 때는 뒤에 아무것도 보이지 않았는데 두 번째 의자를 다른 곳에 배치한 뒤 첫 번째 의자 뒤편에 다른 타일, 오브젝트, preview, overlay, placeholder가 보이는 현상을 확인한다.

확인할 내용:

- blackChair의 sprite pivot, pixels per unit, tile anchor, Tilemap orientation/anchor가 48x48 grid 기준과 맞는지 확인한다.
- blackChair definition의 footprint와 tile part local cell이 1x1인지 확인한다.
- 첫 번째 배치 후 두 번째 배치 시 기존 preview/availability overlay가 지워지지 않고 남는지 확인한다.
- object tilemap, occupancy tilemap, preview tilemap, debug/sample tilemap 중 어느 레이어에서 잔상이 보이는지 분리해서 확인한다.
- `ModernOfficeChairSampleTilemap` 같은 sample/debug tilemap 또는 placeholder object가 House 씬에 남아 보이는지 확인한다.
- 같은 의자를 2회 배치한 뒤 첫 번째 의자 주변의 실제 tile names, sprite names, occupied cells, overlay cells를 로그로 확인한다.

수정 방향:

- 정렬 문제라면 asset import, sprite pivot, tile anchor, definition local offset 중 원인을 찾아 한 곳에서만 보정한다.
- 잔상 문제라면 preview tilemap clear, placed furniture tile clear, overlay clear, stale sample/debug object 제거 중 실제 원인을 수정한다.
- 정상 배치였고 사용자가 착시로 볼 수 있는 상태라면 Game View screenshot과 tilemap state로 정상임을 증명하고 코드 변경은 하지 않는다.

### 3. House 배치 중 카메라 조절 불가 문제

현재 House 씬에서 가구를 배치할 때 카메라 조절이 되지 않아 사용자가 원하는 타일을 보기 어렵다. 가구 배치 중에도 사용자가 배치 영역을 쉽게 볼 수 있도록 카메라 제어를 모듈화한다.

필수 기능:

- 확대/축소
- 상하좌우 이동
- House 배치 중 UI와 충돌하지 않는 입력 방식
- 카메라가 House 배치 가능 타일 전체를 볼 수 있는 최소 zoom-out 범위
- 카메라가 맵 밖으로 과도하게 벗어나지 않는 bounds 제한

권장 입력:

- 마우스 휠 또는 `+/-`: zoom in/out
- `WASD` 또는 방향키: camera pan
- 가운데 버튼 또는 우클릭 드래그 pan은 가능하면 지원
- 필요 시 전체 보기 버튼 또는 단축키를 추가해 모든 배치 가능 타일이 보이는 orthographic size로 전환한다.

설계 제약:

- 기존 `CameraFollow`와 충돌하지 않도록 배치 모드 카메라 제어를 별도 컴포넌트로 분리한다.
- 배치 모드 진입 시 player follow를 일시적으로 끄거나, placement camera controller가 우선권을 갖도록 명확한 ownership을 둔다.
- 배치 모드 종료 시 기존 카메라 추적 상태를 복원한다.
- `Update()` polling은 최소화한다. 불가피한 카메라 이동 입력만 `Update()`에서 처리하고, 비싼 scene scan이나 반복 `GetComponent`는 넣지 않는다.
- House 전용 하드코딩 대신 bounds/provider를 통해 다른 Tilemap surface에도 재사용 가능하게 만든다.

## Likely Code Areas

우선 다음 파일과 테스트를 조사한다.

- `Assets/Scripts/UI/Interiors/InteriorFurniturePlacementUi.cs`
- `Assets/Scripts/UI/Interiors/InteriorPlacementPreviewOverlay.cs`
- `Assets/Scripts/UI/Interiors/InteriorFurnitureCatalog.cs`
- `Assets/Scripts/Game/Placement/FurnitureDefinition.cs`
- `Assets/Scripts/Game/Placement/FurniturePlacementService.cs`
- `Assets/Scripts/Game/Placement/FurniturePlacementRegistry.cs`
- `Assets/Scripts/Game/Placement/TilePlacementSurface.cs`
- `Assets/Scripts/Game/Player/CameraFollow.cs`
- `Assets/Scenes/House.unity`
- `Assets/Data/Interiors/Furniture/`
- `Assets/Data/Registry/GameDataRegistry.asset`

관련 테스트:

- `Assets/Tests/PlayMode/Interiors/HouseInteriorPlacementUiPlayModeTests.cs`
- `Assets/Tests/PlayMode/Interiors/HouseInteriorFurniturePlacementPlayModeTests.cs`
- `Assets/Tests/PlayMode/Interiors/FurniturePlacementMouseFlowPlayModeTests.cs`
- `Assets/Tests/PlayMode/Interiors/HouseInteriorPlacementAvailabilityPlayModeTests.cs`
- `Assets/Tests/EditMode/Placement/*`

## Required Workflow

TDD로 진행한다.

1. 직접 플레이 문제가 재현되는 자동화 테스트를 먼저 추가한다.
2. 실패를 확인한다.
3. 최소 수정으로 통과시킨다.
4. 실제 PlayMode에서 사용자가 하는 경로 그대로 재현한다.
5. Game View 또는 Camera screenshot을 캡처한다.
6. runtime Tilemap, Renderer, UI 상태를 검사한다.
7. 저장되어야 하는 씬 또는 asset 상태가 있다면 EditMode에서 별도로 확인한다.

## Required Tests

### EditMode

- 1x1 desk definition은 tile part 1개와 footprint 1개만 가진다.
- `_wxh` 이름 규칙을 사용하는 경우 `1x1`, `2x2`, `2x1`이 정확한 footprint로 해석된다.
- blackChair definition은 1x1 footprint와 의도한 tile part만 가진다.
- furniture catalog에서 일반 desk 버튼이 desk+computer cluster definition으로 잘못 연결되지 않는다.
- placement camera controller는 min/max zoom과 bounds clamp를 계산한다.
- camera controller는 기존 `CameraFollow` 상태를 저장하고 배치 모드 종료 시 복원한다.

### PlayMode

- House 씬에서 실제 placement UI를 열고 desk 버튼을 클릭했을 때, 1x1 책상은 한 타일만 배치된다.
- desk 배치 후 object tilemap에 computer tile이 자동으로 생기지 않는다.
- blackChair를 두 번 서로 다른 위치에 배치해도 첫 번째 의자 뒤에 preview, debug, stale tile이 남지 않는다.
- blackChair의 renderer/tile position이 선택한 cell과 일치한다.
- 배치 모드에서 zoom in/out이 동작한다.
- 배치 모드에서 camera pan이 동작한다.
- 전체 보기 또는 최대 축소 상태에서 House 배치 가능 타일 전체가 보인다.
- 배치 모드 종료 후 기존 player camera follow 상태가 복원된다.

## Mandatory Direct Visual Verification

자동 테스트만으로 완료 처리하지 않는다. 반드시 실제 PlayMode에서 다음을 확인한다.

1. House 씬 진입
2. 가구 배치 UI 열기
3. 책상 선택
4. 유효한 타일 클릭
5. Game View 또는 Camera screenshot 캡처
6. 책상이 1x1로만 보이는지 확인
7. object tilemap에 desk 외 computer/extra tile이 생기지 않았는지 로그 확인
8. blackChair 선택
9. 첫 번째 의자 배치
10. 두 번째 의자 배치
11. 첫 번째 의자 뒤편에 stale tile, overlay, sample/debug tilemap, placeholder가 보이지 않는지 screenshot과 tilemap 로그로 확인
12. 카메라 zoom, pan, 전체 보기 조작
13. 사용자가 원하는 배치 가능 타일을 볼 수 있는지 확인

검사 로그에는 최소 다음 정보를 포함한다.

- selected furniture id
- selected asset/tile name
- surface id
- clicked cell position
- object tilemap name
- placed tile names
- sprite names
- occupied cells
- preview overlay valid/invalid count
- stale sample/debug tilemap 존재 여부
- camera orthographic size
- camera position
- camera bounds clamp 결과

## Saved State Verification

이 goal의 수정이 scene, prefab, ScriptableObject, registry asset, tile asset에 반영되어야 하는 경우 runtime 상태와 saved 상태를 구분해서 검증한다.

- runtime에서 정상으로 보이는 것만으로 완료 처리하지 않는다.
- `Assets/Scenes/House.unity` 또는 `Assets/Data/Interiors/Furniture/` asset이 변경되었다면 저장 후 다시 열거나 asset serialization을 검사한다.
- saved `FurnitureDefinition`의 stable id, tile parts, footprint, display name, category, registered asset reference를 확인한다.
- saved scene에 sample/debug tilemap 또는 placeholder object가 남아 있으면 완료 처리하지 않는다.

## Constraints

- `Assets/**/*.cs` 파일은 외부 shell로 직접 쓰지 않는다. 반드시 Unity MCP `script-update-or-create`를 사용한다.
- furniture id별 `if`, `switch`, enum 분기 추가 금지.
- 가구별 차이는 ScriptableObject definition과 registry 데이터로 표현한다.
- 기존 House 자동 생성 로직과 수동 배치 로직을 섞지 않는다.
- Modern UI Style2 패널 규칙을 깨지 않는다.
- 신규 goal/quest/campaign/hint UI를 만들지 않는다.
- 시각 작업 완료 보고 전 Game View 또는 Camera screenshot이 필요하다.
- 저장 씬/asset 상태가 영향을 받는 경우, runtime 상태와 saved EditMode 상태를 구분해서 검증한다.

## Done Criteria

완료 조건은 다음 전부를 만족해야 한다.

- 일반 책상 선택 시 의도한 크기대로 배치된다.
- 1x1 책상은 computer/extra tile을 자동으로 붙이지 않는다.
- 2x1 또는 desk+computer 가구가 남아야 한다면 별도 furniture로 명확히 분리되어 있다.
- blackChair가 선택 cell 기준으로 정상 정렬되거나, 정상임이 screenshot과 tilemap state로 증명된다.
- blackChair를 2회 이상 배치해도 첫 번째 배치 뒤편에 stale overlay, debug tile, sample tile, placeholder가 보이지 않는다.
- House 배치 모드에서 zoom in/out이 가능하다.
- House 배치 모드에서 상하좌우 pan이 가능하다.
- 전체 배치 가능 타일을 볼 수 있는 zoom-out 또는 전체 보기 기능이 있다.
- 배치 모드 카메라 제어가 기존 `CameraFollow`와 충돌하지 않는다.
- 관련 EditMode 테스트가 통과한다.
- 관련 PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- Unity Console에 관련 Error/Exception이 없다.
- 최종 보고에 Game View 또는 Camera screenshot 확인 결과와 Tilemap 상태 로그가 포함된다.
