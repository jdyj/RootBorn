# LDtk Visual Follow-up Goal Prompts 1, 2, 4, 5

이 문서는 다음 세션이나 별도 에이전트에게 그대로 전달할 수 있는 goal 프롬프트입니다.

공통 절대 조건:

- `AGENTS.md`의 Direct Visual Play Verification Gate를 반드시 따른다.
- 타일맵, 스프라이트, 씬, 카메라, UI, 문 이동처럼 눈에 보이는 작업은 EditMode/PlayMode 테스트만으로 완료 처리하지 않는다.
- 실제 PlayMode에서 플레이어 입력, 이동, 트리거, 씬 전환, Game View 또는 Camera screenshot 확인까지 수행한다.
- 저장 씬/프리팹/에셋에 반영되어야 하는 변경은 런타임 상태와 별도로 저장된 EditMode 상태를 검사한다.
- `Assets/**/*.cs`는 외부 파일 쓰기로 수정하지 않는다. C# 변경은 Unity MCP `script-update-or-create` 또는 Unity Editor 경유로만 수행한다.
- 신규 구현/버그 수정은 실패 테스트 먼저 작성한 뒤 구현한다.
- 완료 보고에는 실행한 테스트, 직접 확인한 PlayMode 경로, screenshot 확인 결과, 남은 리스크를 적는다.

---

## Goal 1 Prompt - Hospital Missing Script Warning Cleanup

### Goal

`Town -> HospitalInterior`로 실제 입장할 때 발생하는 missing script/runtime script 경고의 정확한 원인을 찾고 제거한다.

### Context

현재 Hospital 입장 과정에서 다음 류의 경고가 발생할 수 있다.

- `The referenced script (Unknown) on this Behaviour is missing!`
- `GameObject (named 'Player') references runtime script in scene file. Fixing!`

이 경고는 단순히 로그를 무시하거나 테스트에서 필터링하면 안 된다. 저장된 씬, 프리팹, MonoBehaviour 스크립트 참조를 확인해서 원인을 제거해야 한다.

### Required Work

1. `Assets/Scenes/HospitalInterior.unity`의 저장 씬 상태를 검사한다.
2. `Player` 또는 관련 오브젝트에 missing MonoBehaviour가 있는지 확인한다.
3. runtime-only script serialization, nested MonoBehaviour class, 깨진 `.meta` GUID, 삭제된 script reference 가능성을 모두 확인한다.
4. 원인이 되는 script reference를 올바른 `.cs` asset reference로 복구하거나 불필요한 컴포넌트라면 제거한다.
5. Hospital 입장 동작, LDtk tilemap 렌더링, Player spawn이 유지되는지 확인한다.

### Required Tests

추가 또는 보강할 테스트:

- Hospital 저장 씬에 missing MonoBehaviour가 없는지 검사하는 EditMode 테스트
- Hospital 입장 PlayMode 테스트에서 경고 로그가 발생하지 않는지 검사

반드시 실행:

- `TownDoorPortalPlayModeTests.TOWN_DOOR_PM_003_PlayerWalksIntoHospitalDoorAndSeesHospitalInteriorLdtkLevel`
- `InteriorReturnDoorPortalPlayModeTests.TOWN_DOOR_PM_007_PlayerWalksIntoHospitalThenReturnsToTownThroughInteriorLdtkDoorWhenPresent`

### Required Visual Verification

1. PlayMode 진입
2. 실제 플레이어 이동 입력으로 Town의 Hospital door까지 이동
3. trigger로 HospitalInterior 입장
4. Game View 또는 Camera screenshot 확인
5. Console warning/error 확인

### Completion Criteria

- Hospital 입장 시 missing script/runtime script 경고가 없다.
- `HospitalInterior`가 LDtk와 동일한 구조로 정상 렌더링된다.
- Player가 정상 spawn되고 카메라가 정상 framing된다.
- 저장 씬의 MonoBehaviour reference가 깨져 있지 않다.

---

## Goal 2 Prompt - Town Return Screen Visual Cleanup

### Goal

`HouseInterior -> Town`, `ShopInterior -> Town` 복귀 화면에서 디버그 UI, 어색한 카메라 framing, 빈 배경 노출, 잘못된 HUD 표시를 정리한다.

### Context

Interior에서 Town으로 돌아왔을 때 플레이어-facing 화면에 다음 문제가 보이면 실패다.

- `Quest Chains`, `World Log`, `Interest`, `Career` 같은 디버그/테스트 버튼 노출
- 카메라가 플레이어를 즉시 따라오지 않아 빈 공간이나 solid color가 크게 보임
- Town tilemap이 화면에서 비정상적으로 밀림
- 정상 gameplay HUD와 debug/test UI가 구분되지 않음

### Required Work

1. House return door와 Shop return door의 실제 복귀 flow를 PlayMode에서 재현한다.
2. Town 복귀 직후 Player spawn, Camera position, CameraFollow target, orthographic size를 검사한다.
3. 정상 gameplay HUD는 유지하고 debug/test UI는 일반 플레이 화면에서 숨긴다.
4. 복귀 직후 카메라가 Player 중심으로 snap 또는 안정적으로 framing되도록 한다.
5. `Town` runtime TilemapRenderer 상태와 UI Canvas/Image 활성 상태를 검사한다.

### Required Tests

추가 또는 보강할 테스트:

- Town 복귀 후 debug/test UI가 inactive 또는 missing인지 검사
- Town 복귀 후 Main Camera와 Player의 거리/프레이밍 검사
- Town 복귀 후 Player 위치 아래에 실제 Town ground tile이 있는지 검사

반드시 실행:

- `InteriorReturnDoorPortalPlayModeTests.TOWN_DOOR_PM_005_PlayerWalksIntoHouseThenReturnsToTownThroughInteriorLdtkDoor`
- `InteriorReturnDoorPortalPlayModeTests.TOWN_DOOR_PM_006_PlayerWalksIntoShopThenReturnsToTownThroughInteriorLdtkDoor`

### Required Visual Verification

1. PlayMode에서 Town 시작
2. 실제 이동 입력으로 House door 입장
3. HouseInterior return door로 Town 복귀
4. Game View screenshot 확인
5. 같은 방식으로 ShopInterior return door 복귀 확인

### Completion Criteria

- Town 복귀 화면에 debug/test UI가 보이지 않는다.
- Player와 Town tilemap이 자연스럽게 framing된다.
- 카메라가 복귀 직후 Player 근처에 위치한다.
- House와 Shop 양쪽 복귀 flow가 모두 실제 PlayMode 입력 경로로 통과한다.

---

## Goal 4 Prompt - Strengthen Interior/Town Visual Regression Tests

### Goal

Town, HouseInterior, ShopInterior, HospitalInterior LDtk 시각 결과가 다시 깨지지 않도록 PlayMode visual regression test와 runtime state assertion을 강화한다.

### Context

이전 문제는 tile asset 데이터만 맞고 실제 Game View가 깨진 상태를 완료로 판단한 것이다. 앞으로는 저장 씬 상태, runtime tilemap 상태, 실제 Game View를 모두 확인해야 한다.

### Required Work

1. 다음 씬의 LDtk Tilemap layer 목록, tile count, sorting order를 수집한다.
   - `Assets/Scenes/Town.unity`
   - `Assets/Scenes/HouseInterior.unity`
   - `Assets/Scenes/ShopInterior.unity`
   - `Assets/Scenes/HospitalInterior.unity`
2. PlayMode door entry test에서 각 Interior 입장 후 다음을 검사한다.
   - expected LDtk root object 존재
   - expected Tilemap layer 존재
   - 각 Tilemap의 used tile count가 0보다 크거나 기대 count와 일치
   - `TilemapRenderer.enabled == true`
   - `TilemapRenderer.sortingOrder`가 LDtk layer order와 일치
   - Main Camera orthographic size가 Interior에서 `6.5` 근처
3. Town return test에서 다음을 검사한다.
   - debug/test UI 비노출
   - stale sample/debug tilemap 비노출
   - Player가 실제 Town tile 위에 있음
   - Camera가 Player를 정상 framing
4. screenshot output 이름과 저장 위치를 통일한다.
5. 런타임 상태 검사와 저장 EditMode 씬/에셋 검사를 분리한다.

### Required Tests

반드시 보강 또는 추가:

- HouseInterior 입장 visual regression assertion
- ShopInterior 입장 visual regression assertion
- HospitalInterior 입장 visual regression assertion
- Town return visual regression assertion

반드시 실행:

- `TownDoorPortalPlayModeTests.TOWN_DOOR_PM_002_PlayerWalksIntoHouseDoorAndSeesHouseInteriorLdtkLevel`
- `TownDoorPortalPlayModeTests.TOWN_DOOR_PM_003_PlayerWalksIntoHospitalDoorAndSeesHospitalInteriorLdtkLevel`
- `TownDoorPortalPlayModeTests.TOWN_DOOR_PM_004_PlayerWalksIntoShopDoorAndSeesShopInteriorLdtkLevel`
- `InteriorReturnDoorPortalPlayModeTests.TOWN_DOOR_PM_005_PlayerWalksIntoHouseThenReturnsToTownThroughInteriorLdtkDoor`
- `InteriorReturnDoorPortalPlayModeTests.TOWN_DOOR_PM_006_PlayerWalksIntoShopThenReturnsToTownThroughInteriorLdtkDoor`

### Required Visual Verification

각 checkpoint마다 Game View 또는 Camera screenshot을 확인한다.

- Town -> HouseInterior
- Town -> ShopInterior
- Town -> HospitalInterior
- HouseInterior -> Town
- ShopInterior -> Town

### Completion Criteria

- 핵심 LDtk layer가 missing, empty, hidden, sorting mismatch일 때 테스트가 실패한다.
- Interior camera zoom이 `6.5`에서 벗어나면 테스트가 실패한다.
- stale/debug placeholder tilemap 또는 debug UI가 보이면 테스트가 실패한다.
- 테스트 결과뿐 아니라 실제 screenshot 확인 결과를 완료 보고에 포함한다.

---

## Goal 5 Prompt - Build LDtk Import Validator

### Goal

LDtk와 Unity Game View가 달라지는 원인을 PlayMode 디버깅 전에 잡을 수 있도록 LDtk import validator를 만든다.

### Context

반복된 문제:

- LDtk의 ground tile은 흰색 보도블록인데 Unity에서는 다른 tile로 보임
- 투명해야 할 겹침 부분에 색이 들어간 것처럼 보임
- layer order가 LDtk와 Unity에서 다름
- generated Tile asset의 sprite rect가 원본 tileset pixel 좌표와 맞지 않음
- stale/generated duplicate tile asset이 남아 실제 렌더링에 섞임

validator는 이런 문제를 사람이 눈으로 발견하기 전에 EditMode에서 실패시켜야 한다.

### Required Work

1. Editor 전용 LDtk import validator를 만든다.
2. validator는 Town과 Interior LDtk import 결과를 모두 검사한다.
3. 다음 항목을 검사한다.
   - LDtk tileset path resolution
   - generated Tile asset sprite rect와 원본 texture pixel coordinate 일치
   - texture import setting
   - Pixels Per Unit `32`
   - Filter Mode `Point`
   - Mipmap disabled
   - pixel art에 맞는 무손실 또는 uncompressed import
   - duplicate/stale generated tile asset 존재 여부
   - LDtk layer order와 Unity TilemapRenderer sorting order 일치
4. failure message는 원인 category를 명확히 포함한다.
   - `rect`
   - `PPU`
   - `path`
   - `sorting`
   - `stale asset`
5. validator test는 broken import가 어떤 종류로 깨졌는지 사람이 바로 알 수 있게 메시지를 작성한다.

### Required Tests

추가할 EditMode 테스트 예시:

- `LDtk_IMPORT_EDIT_001_TownGeneratedTilesMatchOriginalTilesetRects`
- `LDtk_IMPORT_EDIT_002_InteriorGeneratedTilesMatchOriginalTilesetRects`
- `LDtk_IMPORT_EDIT_003_LDtkTilesetTexturesUsePixelPerfectImportSettings`
- `LDtk_IMPORT_EDIT_004_TownAndInteriorLayerSortingMatchesLDtkOrder`
- `LDtk_IMPORT_EDIT_005_NoStaleOrDuplicateGeneratedLDtkTilesAreVisibleToImport`

기존 테스트도 반드시 유지 및 실행:

- Town LDtk tile rect 관련 EditMode 테스트
- Interior LDtk tile rect 관련 EditMode 테스트
- Town/Interior door PlayMode visual tests

### Required Visual Verification

validator 자체는 EditMode 도구지만, 완료 전에는 반드시 실제 PlayMode Game View로 검증한다.

1. Town 시작 화면 확인
2. Town -> HouseInterior 확인
3. Town -> ShopInterior 확인
4. Town -> HospitalInterior 확인
5. 각 scene의 Tilemap/Renderer runtime state와 screenshot을 확인

### Completion Criteria

- LDtk import가 깨졌을 때 validator failure message가 원인 category와 asset path를 알려준다.
- Town과 Interior generated tile rect, texture import setting, layer sorting, stale asset 상태가 자동 검증된다.
- validator 통과 후 실제 PlayMode Game View에서도 LDtk와 동일한 시각 결과가 확인된다.

---

## Recommended Execution Order

1. Goal 1 - Hospital missing script warning 제거
2. Goal 2 - Interior에서 Town 복귀 화면 정리
3. Goal 4 - PlayMode visual regression test 강화
4. Goal 5 - LDtk import validator 구축

Goal 1과 Goal 2는 사용자-facing warning/visual 문제를 먼저 제거한다. Goal 4는 같은 문제가 다시 발생하지 않도록 실제 PlayMode 회귀 테스트를 강화한다. Goal 5는 import 단계에서 LDtk/Unity 불일치 원인을 조기 차단한다.

