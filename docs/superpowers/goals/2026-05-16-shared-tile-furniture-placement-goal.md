# Shared Tile Furniture Placement Goal

## Goal

House에 묶여 있는 가구 배치 기능을 공용 `TilePlacementSurface` 기반 시스템으로 분리한다. 같은 팔레트와 배치 로직을 House뿐 아니라 Town, 상점, 학교, 플레이어 방, 사무실 같은 다른 Tilemap 씬에서도 사용할 수 있어야 한다.

## Required Outcomes

- `House*` 이름에 묶인 배치 참조를 공용 surface 참조로 분리한다.
- surface는 ground/object/collision/preview tilemap, bounds, 배치 가능성 규칙을 가진다.
- 기존 House 배치 기능은 공용 시스템을 사용하는 첫 번째 구현으로 유지한다.
- 다른 씬에서도 surface만 있으면 같은 furniture palette를 붙일 수 있는 구조를 만든다.
- 기존 Modern UI Style2 패널 규칙과 Objective Journal/HUD 소유권 규칙을 침범하지 않는다.

## Direct Play Verification Requirement

완료 판정은 자동 테스트만으로 하지 않는다. 반드시 Unity PlayMode에서 사용자가 하는 흐름으로 직접 확인한다.

1. House 씬에서 PlayMode 진입
2. furniture palette에서 chair 선택
3. 초록색 배치 가능 칸에 실제 클릭 또는 동일 입력 경로로 배치
4. Game View 또는 Camera screenshot에서 선택한 가구가 눈에 보이는지 확인
5. Tilemap inventory 로그로 배치 surface, object tilemap, tile 이름, sprite 이름, overlay count를 확인
6. sample/debug tilemap이 화면에 남아 있지 않은지 확인

## Tests

- EditMode: surface 모델과 배치 규칙 단위 테스트
- PlayMode: House surface가 공용 배치 시스템으로 초기화되는지 검증
- PlayMode: 실제 UI 버튼 선택 후 object tilemap에 정확한 furniture tile이 들어가는지 검증
- CI: `Scripts/ci/check-no-entity-id-branching.sh`

## Done Criteria

- House 전용 배치 코드가 공용 surface 추상화로 이동했다.
- House에서 기존 chair placement가 유지된다.
- 최소 하나의 non-House 테스트 surface에서 같은 배치 로직이 동작한다.
- 직접 PlayMode 조작과 Game View/Camera screenshot으로 실제 화면을 확인했다.
- EditMode/PlayMode 관련 테스트와 entity-id branching CI가 통과한다.
