# House Interior Placement UI Goal

Unity 2D 픽셀 게임 ROOTBORN 프로젝트에서 House 씬의 자동 실내 생성 시스템을 완성한다.

## Current State

- House 씬에 2D Tilemap 기반 실내 자동 생성이 있다.
- `Assets/Modern_Town/1_Room_Builder_Office_48x48` 타일을 사용한다.
- `InteriorGenerationProfile`, `InteriorTileSetDefinition`, `InteriorTilemapApplier`, `InteriorObjectInteraction` 계열 코드가 있다.
- 책상, 의자, 컴퓨터, 소파, 화분 등은 Tilemap에 배치되고, 컴퓨터 같은 상호작용 대상은 별도 Collider/Interactor로 연결된다.
- 특정 가구 배치 요청 구조인 `InteriorFurniturePlacementRequest`와 `InteriorPlacementPreference`가 추가되어 있다.
- 아직 실제 Unity UI 패널은 구성되지 않았다.

## Goal

House 씬에서 개발자가 가구 배치 조건을 직접 조절하고, 랜덤 시드 기반으로 실내를 재생성하며, 결과를 Tilemap에 즉시 적용할 수 있는 `Interior Furniture Placement UI`를 처음부터 끝까지 구현한다.

## Required Outcomes

1. House 씬에서 열리는 개발용 UI 패널 구현
2. 가구 선택 UI 구현
3. 가구별 배치 요청 설정 UI 구현
4. 수량, 방향, 배치 선호도, 필수 여부를 조절 가능하게 구현
5. Seed 입력과 Regenerate 버튼 구현
6. Generate 결과를 기존 Tilemap Layer에 즉시 적용
7. 실패 시 UI에 실패 이유 표시
8. 생성 성공 시 배치 요약 표시
9. 기존 House 자동 생성, Tilemap 적용, 컴퓨터 상호작용 기능을 깨지 않음
10. EditMode/PlayMode 테스트 추가 및 통과

## UI Structure

### Left Panel: Furniture Palette

- Desk
- Sofa
- Plant
- Shelf
- OfficeProp
- Computer는 단독 배치보다 Desk cluster 옵션으로 취급한다.
- Chair도 단독 배치보다 Desk cluster 결과로 우선 취급한다.

### Right Panel: Selected Furniture Settings

- Count
- Facing Direction: Auto, North, East, South, West
- Placement Preference: Any, Avoid Corridor, Near Wall, Near Window
- Required toggle
- Interactive toggle은 우선 표시만 하고, 실제 기능은 Computer 중심으로 유지한다.

### Bottom Bar

- Seed input
- Regenerate button
- Apply button
- Status text: Success / Failed / Validation message

### Center View

- 별도 미니맵을 새로 만들지 않는다.
- 기존 House Tilemap 결과를 직접 보면서 확인하는 방식으로 한다.

## Implementation Direction

- UI는 런타임 개발용 패널로 만든다.
- 파일 위치는 UI 책임에 맞게 `Assets/Scripts/UI/Interiors/` 아래에 둔다.
- C# 파일 생성/수정은 반드시 Unity MCP `script-update-or-create`를 사용한다.
- Modern UI Style2 규칙을 지킨다.
- 패널 배경은 `ModernUiTileImage + ModernUiRecipes.CommonPanel`을 사용한다.
- 단순 `Image.sprite` 직접 배경 사용을 피한다.
- House 씬에 자동 설치용 RuntimeInstaller를 붙이거나 기존 House 부트스트랩 흐름에 연결한다.
- 기존 `ObjectiveJournalPanel`, HUD 규칙과 충돌하지 않게 개발용 실내 편집 패널로 분리한다.

## Data And Runtime Flow

- `InteriorGenerationProfile`의 placement requests를 UI에서 편집 가능한 런타임 요청 배열로 변환한다.
- UI 조작은 원본 asset을 직접 영구 수정하지 않는다.
- Regenerate 시 다음 순서로 처리한다.

1. 현재 UI 상태를 `InteriorFurniturePlacementRequest` 배열로 만든다.
2. 현재 seed로 `InteriorGenerator.Generate`를 호출한다.
3. 실패하면 Tilemap을 변경하지 않고 상태 메시지를 표시한다.
4. 성공하면 `InteriorTilemapApplier`로 Tilemap Layer를 갱신한다.
5. 상호작용 Interactor도 다시 생성/정리한다.

## Test Requirements

- TDD로 진행한다.
- 먼저 실패 테스트를 작성한다.

### EditMode Tests

- UI 모델이 선택 가구, 수량, 방향, 선호도, 필수 여부를 placement request로 변환하는지 검증
- Required 요청 실패 시 실패 상태가 유지되는지 검증
- Optional 요청 실패 시 생성 자체는 유지되는지 검증

### PlayMode Tests

- House 씬에서 패널이 생성되는지 검증
- Seed 변경 후 Regenerate 시 Tilemap이 갱신되는지 검증
- 컴퓨터 상호작용 interactor가 유지되는지 검증
- 실패한 필수 배치 요청이 Tilemap을 망가뜨리지 않는지 검증

## Constraints

- `Assets/**/*.cs` 직접 디스크 쓰기 금지.
- 기존 사용자 변경사항을 되돌리지 않는다.
- 기존 House 씬/Tilemap 구조를 먼저 조사한 뒤 거기에 맞춰 연결한다.
- 대규모 리팩터링은 하지 말고, UI 패널과 배치 요청 적용 경로에 집중한다.
- 하드코딩된 개별 엔티티 ID 분기 방식은 피한다.
- 생성 실패 디버깅이 가능하도록 실패 메시지를 사람이 읽을 수 있게 남긴다.

## Done Criteria

- House 씬에서 UI 패널이 보인다.
- Desk, Sofa, Plant 등의 수량과 배치 규칙을 UI에서 바꿀 수 있다.
- Regenerate를 누르면 기존 House Tilemap이 새 설정과 seed에 맞게 갱신된다.
- 컴퓨터 타일과 상호작용 collider/interactor가 계속 정상 작동한다.
- EditMode Interiors 테스트가 통과한다.
- PlayMode Interiors 또는 House 관련 테스트가 통과한다.
- 최종 보고에 변경 파일, 테스트 결과, 남은 한계를 짧게 정리한다.
