# Unified Furniture Placement System Goal

## Goal

ROOTBORN의 현재 House 전용 furniture placement 기능을 공용 tile/furniture placement system으로 확장한다. 최종 목표는 House뿐 아니라 Town, 상점, 학교, 플레이어 방, 사무실 등 여러 Tilemap surface에서 같은 방식으로 furniture object를 선택, 미리보기, 배치, 삭제, 이동, 회전, 저장/로드할 수 있게 하는 것이다.

이 작업은 단순히 tile asset을 Tilemap에 찍는 것이 아니라, 플레이어가 선택하는 단위를 `FurnitureDefinition` object로 관리하고, 렌더링만 Tilemap에 적용하는 구조로 완성해야 한다.

## Background And Lessons Learned

이전 chair 배치 작업에서 자동 테스트는 통과했지만 실제 화면에서는 잘못 보이는 문제가 있었다. 원인은 다음과 같았다.

- House 씬에 `ModernOfficeChairSampleTilemap` 샘플 전시용 Tilemap이 남아 있어 실제 배치가 아닌 chair들이 화면에 계속 보였다.
- 수동 배치 UI인데 기본 오피스 가구가 자동 생성되어 책상, 컴퓨터, 기본 chair가 이미 깔려 있었다.
- 배치 가능/불가능 overlay가 배치 후에도 남아 실제 furniture sprite를 덮었다.
- 테스트는 tile asset 이름만 확인했고, 실제 Game View/Camera 화면을 검증하지 않았다.

따라서 이 goal의 완료 판정은 자동 테스트만으로 하지 않는다. 모든 단계는 실제 PlayMode 조작, Game View 또는 Camera screenshot, Tilemap inventory 로그까지 확인해야 한다.

## Scope

### 1. Shared Tile Placement Surface

House 전용 이름에 묶인 배치 구조를 공용 `TilePlacementSurface`로 분리한다.

Surface는 최소한 다음 데이터를 제공해야 한다.

- ground tilemap
- object/furniture tilemap
- collision 또는 occupancy source
- preview/availability tilemap
- bounds
- placement rules
- surface id

House는 이 공용 시스템을 사용하는 첫 번째 surface가 된다. 이후 다른 씬은 surface만 제공하면 같은 placement UI와 placement logic을 사용할 수 있어야 한다.

### 2. FurnitureDefinition Object Model

Furniture는 개별 tile이 아니라 object definition으로 관리한다.

Definition은 최소한 다음 정보를 가진다.

- stable id
- display name
- category
- allowed surfaces
- footprint
- tile parts
- direction variants
- blocks movement
- placement preference/rules
- optional unlock data

런타임 코드는 특정 furniture id로 분기하지 않는다. 데이터 기반으로 처리한다.

### 3. Multi-Tile Furniture

책상, 긴 파티션, 컴퓨터 책상 세트처럼 여러 tile이 합쳐져야 하나의 가구가 되는 object를 지원한다.

요구사항:

- 플레이어는 `tile_r21_c00` 같은 개별 tile을 고르지 않는다.
- 플레이어는 `Desk_Large` 같은 furniture object 하나를 고른다.
- 내부적으로 anchor cell과 local tile offsets를 사용해 여러 tile을 배치한다.
- footprint 전체가 유효해야만 배치된다.
- 배치 실패 시 일부 tile만 남거나 occupancy만 바뀌는 일이 없어야 한다.

### 4. Delete And Move

이미 배치한 furniture instance를 선택해서 삭제하거나 이동할 수 있어야 한다.

요구사항:

- placed furniture instance registry를 유지한다.
- click 또는 UI selection으로 instance를 선택한다.
- 삭제 시 object tilemap과 occupancy를 모두 정리한다.
- 이동 시 기존 위치는 새 위치가 유효할 때만 비운다.
- 실패한 이동은 기존 furniture를 잃지 않는다.

### 5. Save And Load

플레이어가 배치한 furniture object를 저장하고 복원한다.

저장 데이터는 tile asset 직접 참조가 아니라 다음과 같은 stable data를 가져야 한다.

- furniture id
- surface id
- anchor cell
- direction
- optional state

로드 시 catalog에서 definition을 찾아 tilemap과 occupancy를 재구성한다. 중복 배치, tile 잔상, missing definition crash가 없어야 한다.

### 6. Rotation And Direction

방향이 있는 furniture를 회전해서 배치할 수 있어야 한다.

요구사항:

- `R` 키 또는 UI 버튼으로 방향 변경
- direction-specific footprint 또는 rotated footprint 지원
- direction-specific tile parts 지원
- preview와 실제 배치가 같은 direction data를 사용
- 저장/로드 후 direction 유지

### 7. ScriptableObject Catalog

현재 Resources 기반 임시 catalog를 ScriptableObject/registry 기반 catalog로 전환한다.

요구사항:

- `FurnitureDefinition` SO 타입을 만든다.
- SO 인스턴스는 프로젝트 데이터 규칙에 맞는 `Assets/Data/...` 경로에 둔다.
- registry에서 furniture catalog를 로딩한다.
- runtime code가 furniture id별 if/switch 분기를 하지 않는다.
- `Scripts/ci/check-no-entity-id-branching.sh`를 통과해야 한다.

## Required User Flow

최종 사용 흐름은 다음과 같아야 한다.

1. 플레이어가 placement UI를 연다.
2. furniture category를 선택한다.
3. furniture object를 선택한다.
4. 배치 가능한 칸은 green, 불가능한 칸은 red로 표시된다.
5. 방향 변경이 가능한 furniture는 방향을 바꿀 수 있다.
6. 유효한 칸을 클릭하면 furniture object가 배치된다.
7. 배치 후 overlay는 사라지거나 가구를 덮지 않는 상태가 된다.
8. 배치한 furniture를 선택해서 삭제하거나 이동할 수 있다.
9. 저장 후 씬 재진입 또는 로드 시 같은 위치에 복원된다.
10. 같은 시스템이 House 외 surface에서도 동작한다.

## Mandatory Direct Play Verification

이 goal은 자동 테스트만으로 완료할 수 없다. 완료 전 반드시 다음을 실제 PlayMode에서 확인한다.

### Basic Placement Verification

1. PlayMode 진입
2. palette에서 furniture 선택
3. 실제 UI 클릭 또는 사용자의 입력 경로와 동일한 방식으로 배치
4. Game View 또는 Camera screenshot 캡처
5. 선택한 furniture가 화면에서 실제로 보이는지 확인
6. Tilemap inventory 로그로 다음 값 확인
   - surface id
   - object tilemap name
   - placed furniture id
   - tile names
   - sprite names
   - occupied cells
   - overlay valid/invalid count
   - decoration/default tile under furniture

### Negative Visual Verification

다음이 없는지 확인한다.

- sample/debug tilemap이 Game View에 남아 있음
- default generated furniture가 수동 배치 결과처럼 보임
- availability overlay가 배치된 furniture를 덮음
- tile asset 이름은 맞지만 sprite가 다른 것처럼 보임
- object tilemap에는 1개인데 다른 tilemap에 잔상이 남아 있음

### Multi-Tile Verification

1. multi-tile furniture 선택
2. 유효한 위치에 배치
3. Game View에서 하나의 furniture처럼 이어져 보이는지 확인
4. Tilemap inventory에서 tile part 수와 이름이 기대값과 일치하는지 확인
5. 일부 footprint가 막힌 위치에서는 배치가 거부되는지 확인

### Edit Verification

1. 배치한 furniture 선택
2. 삭제
3. Game View에서 사라지는지 확인
4. 다시 배치 후 이동
5. 기존 cell은 비고 새 cell에만 tile이 있는지 Tilemap inventory로 확인

### Save Load Verification

1. furniture 2개 이상 배치
2. 저장
3. 씬 재진입 또는 로드
4. Game View에서 같은 furniture가 같은 위치에 복원되는지 확인
5. Tilemap inventory에서 중복 tile이 없는지 확인

## Testing Requirements

TDD로 진행한다.

### EditMode Tests

- placement surface model
- furniture definition validation
- single tile furniture placement
- multi-tile footprint validation
- partial placement failure atomicity
- delete/move instance registry
- save DTO serialization/deserialization
- direction variant resolution
- SO catalog and registry wiring
- no runtime Resources dependency where registry should be used

### PlayMode Tests

- House surface initialization
- non-House test surface initialization
- UI palette creation from catalog
- actual furniture selection and placement
- overlay clears after placement
- sample/debug tilemap absence
- multi-tile furniture placement
- blocked footprint rejection
- delete and move through user flow
- save/load through scene flow
- direction change through user input

### CI

Run at minimum:

```bash
Scripts/ci/check-no-entity-id-branching.sh
```

Also run the relevant Unity EditMode and PlayMode test namespaces before reporting completion.

## Implementation Constraints

- Do not write `Assets/**/*.cs` directly from shell. Use Unity MCP `script-update-or-create`.
- Do not introduce furniture id based branching in runtime code.
- Do not break Modern UI Style2 panel rules.
- Do not create new always-on objective/quest HUD surfaces.
- Do not treat a passing asset-name test as visual verification.
- Do not leave debug/sample Tilemaps in playable scenes.
- Do not claim completion without fresh verification output.

## Done Criteria

This goal is complete only when all of the following are true.

- Shared placement surface works in House and at least one non-House/test surface.
- Furniture is selected and placed as object definitions, not raw individual tile choices.
- Multi-tile furniture works with atomic placement.
- Delete and move work through real user flow.
- Save and load restore placed furniture without duplicates.
- Direction/rotation variants work through user input.
- Furniture catalog is SO/registry based and data-driven.
- Direct PlayMode verification was performed and documented in the final report.
- Game View or Camera screenshots confirmed the visible result.
- Tilemap inventory logs confirmed actual tilemap state.
- EditMode tests pass.
- PlayMode tests pass.
- `check-no-entity-id-branching.sh` passes.
- Unity Console has no relevant errors after verification.
