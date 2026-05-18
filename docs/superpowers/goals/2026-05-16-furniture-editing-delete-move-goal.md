# Furniture Editing Delete Move Goal

## Goal

이미 배치한 furniture object를 선택해서 삭제하거나 다른 위치로 이동할 수 있게 한다. 배치 실험 중 잘못 놓은 가구를 되돌릴 수 있어야 한다.

## Required Outcomes

- object tilemap에 놓인 furniture instance를 선택할 수 있다.
- 선택된 instance의 footprint와 tile parts를 식별할 수 있다.
- 삭제 시 tilemap과 occupancy를 모두 정리한다.
- 이동 시 기존 위치를 비우고 새 위치가 유효할 때만 이동한다.
- 이동 preview는 배치 preview와 같은 green/red 규칙을 따른다.

## Direct Play Verification Requirement

완료 판정은 직접 조작으로 확인한다.

1. PlayMode에서 chair 또는 multi-tile furniture를 배치한다.
2. 놓인 furniture를 직접 클릭해서 선택한다.
3. 삭제 버튼 또는 지정 입력으로 삭제한다.
4. Game View에서 가구가 사라졌는지 확인한다.
5. 다시 배치 후 이동 모드로 다른 유효 칸에 옮긴다.
6. Tilemap inventory에서 기존 cell은 비고 새 cell에만 tile이 있는지 확인한다.

## Tests

- EditMode: instance registry 삭제/이동 원자성
- PlayMode: 실제 UI 선택, 삭제, 이동 경로
- PlayMode: 막힌 위치 이동 실패 시 기존 위치 유지
- CI: `Scripts/ci/check-no-entity-id-branching.sh`

## Done Criteria

- 배치한 furniture를 삭제할 수 있다.
- 배치한 furniture를 이동할 수 있다.
- 실패한 이동이 기존 가구를 잃어버리지 않는다.
- 직접 PlayMode 조작과 화면 캡처로 삭제/이동을 확인했다.
- 관련 테스트와 CI가 통과한다.
