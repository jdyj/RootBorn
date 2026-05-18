# Furniture Rotation Direction Goal

## Goal

Furniture object의 방향과 회전을 지원한다. 의자, 책상, 파티션처럼 방향별 sprite 또는 tile 조합이 다른 가구를 플레이어가 회전해서 배치할 수 있어야 한다.

## Required Outcomes

- `FurnitureDefinition`은 방향별 tile parts를 가질 수 있다.
- footprint는 방향에 따라 회전 또는 별도 정의를 사용할 수 있다.
- preview는 현재 방향의 footprint와 tile parts를 기준으로 표시한다.
- `R` 키 또는 UI 버튼으로 방향을 변경한다.
- 배치 후 저장/로드와 instance data에 방향이 유지된다.

## Direct Play Verification Requirement

완료 판정은 직접 조작으로 확인한다.

1. PlayMode에서 방향 variant가 있는 furniture를 선택한다.
2. `R` 키 또는 UI 버튼으로 방향을 바꾼다.
3. Game View에서 preview 모양과 footprint가 방향에 맞게 바뀌는지 확인한다.
4. 배치 후 object tilemap의 tile 이름과 위치가 방향별 기대값인지 확인한다.
5. 막힌 방향에서는 red preview가 나오고, 다른 방향에서는 green preview가 나오는 케이스를 확인한다.

## Tests

- EditMode: footprint rotation 또는 direction-specific footprint 검증
- EditMode: direction tile parts 선택 검증
- PlayMode: UI/입력으로 방향 변경 후 배치
- CI: `Scripts/ci/check-no-entity-id-branching.sh`

## Done Criteria

- 방향별 sprite/tile variant가 적용된다.
- preview와 실제 배치가 같은 방향 데이터를 사용한다.
- 직접 PlayMode 조작과 화면 캡처로 방향 변경을 확인했다.
- 관련 테스트와 CI가 통과한다.
