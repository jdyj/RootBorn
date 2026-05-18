# Multi Tile Furniture Object Goal

## Goal

여러 개의 tile이 합쳐져야 하나의 가구가 되는 object 배치를 지원한다. 플레이어는 개별 tile을 고르는 것이 아니라 `Desk_Large`, `Partition_Long`, `ComputerDesk_Set` 같은 하나의 furniture object를 선택해야 한다.

## Required Outcomes

- `FurnitureDefinition`이 anchor cell, footprint, tile parts를 가진다.
- 각 tile part는 local cell offset과 tile reference를 가진다.
- 배치 가능 여부는 footprint 전체가 비어 있고 surface 규칙을 통과할 때만 true가 된다.
- 배치 성공 시 모든 tile part가 object tilemap에 한 번에 적용된다.
- 실패 시 map occupancy와 tilemap이 부분 변경되지 않는다.

## Direct Play Verification Requirement

완료 판정은 실제 화면 확인을 포함한다.

1. PlayMode에서 multi-tile furniture를 palette에서 선택한다.
2. 유효한 칸에 배치한다.
3. Game View 또는 Camera screenshot에서 하나의 가구처럼 이어져 보이는지 확인한다.
4. Tilemap inventory에서 object tilemap에 기대한 tile part 개수와 이름이 모두 들어갔는지 확인한다.
5. footprint 중 일부만 막힌 위치에 배치 시도했을 때 빨간 preview와 실패 메시지가 나오는지 확인한다.

## Tests

- EditMode: footprint 전체 검사, 부분 실패 원자성, tile part local offset 적용
- PlayMode: multi-tile object UI 선택 후 실제 배치
- PlayMode: blocked cell 포함 시 배치 거부
- CI: `Scripts/ci/check-no-entity-id-branching.sh`

## Done Criteria

- 예시 multi-tile furniture 하나 이상이 구현되어 있다.
- 개별 tile 선택 없이 object 하나로 배치된다.
- 직접 PlayMode 화면에서 하나의 가구처럼 보이는 것을 확인했다.
- 관련 EditMode/PlayMode 테스트와 CI가 통과한다.
