# Furniture Placement Save Load Goal

## Goal

플레이어가 배치한 furniture object를 저장하고, 씬 재진입 또는 게임 로드 후 같은 위치에 복원한다.

## Required Outcomes

- placed furniture instance는 furniture id, surface id, anchor cell, facing direction, optional state를 저장한다.
- 저장 데이터는 tile asset 직접 참조가 아니라 stable id 기반으로 복원한다.
- 로드 시 furniture catalog에서 definition을 찾아 object tilemap에 재구성한다.
- 누락된 definition은 저장을 깨지 않고 경고와 함께 skip 또는 placeholder 정책을 따른다.
- 저장/로드는 중복 배치나 tile 잔상을 만들지 않는다.

## Direct Play Verification Requirement

완료 판정은 실제 저장/로드 흐름으로 확인한다.

1. PlayMode에서 furniture를 2개 이상 배치한다.
2. 저장을 실행한다.
3. 씬을 나갔다가 다시 들어오거나 로드를 실행한다.
4. Game View에서 같은 위치와 같은 sprite로 복원됐는지 확인한다.
5. Tilemap inventory에서 중복 tile이 없고 object tilemap의 tile 수가 기대값과 같은지 확인한다.

## Tests

- EditMode: save DTO 직렬화/역직렬화
- EditMode: missing definition 처리
- PlayMode: 배치 후 저장, 씬 재진입, 복원 확인
- CI: `Scripts/ci/check-no-entity-id-branching.sh`

## Done Criteria

- 배치한 furniture가 저장된다.
- 로드 후 같은 위치에 복원된다.
- 중복 복원이나 tile 잔상이 없다.
- 직접 PlayMode 저장/로드와 화면 캡처로 검증했다.
- 관련 테스트와 CI가 통과한다.
