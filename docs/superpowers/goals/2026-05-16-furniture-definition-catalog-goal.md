# Furniture Definition Catalog Goal

## Goal

현재 runtime Resources 기반 furniture catalog를 ScriptableObject 데이터 카탈로그로 전환한다. 모든 furniture는 데이터로 정의되고, 런타임 코드는 특정 furniture id나 asset 이름으로 분기하지 않는다.

## Required Outcomes

- `FurnitureDefinition` ScriptableObject 타입을 만든다.
- SO 인스턴스는 `Assets/Data/Interiors/Furniture/` 또는 기존 데이터 규칙에 맞는 경로에 둔다.
- 각 definition은 id, display name, category, allowed surfaces, footprint, direction variants, tile parts, blocks movement, optional unlock data를 가진다.
- `GameDataRegistry` 또는 프로젝트 규칙에 맞는 registry에서 furniture catalog를 로딩한다.
- Resources 직접 로딩은 임시 fallback이 아니면 제거한다.

## Direct Play Verification Requirement

완료 판정은 에셋 데이터와 실제 플레이를 같이 확인한다.

1. Unity Editor에서 furniture SO asset들이 존재하는지 확인한다.
2. PlayMode에서 SO catalog 기반 palette가 뜨는지 확인한다.
3. 각 category에서 최소 1개 furniture를 직접 선택하고 배치한다.
4. Game View 또는 Camera screenshot에서 선택한 furniture가 실제로 보이는지 확인한다.
5. Tilemap inventory에서 SO id와 배치 tile이 일치하는지 로그로 확인한다.

## Tests

- EditMode: furniture SO asset wiring 검사
- EditMode: registry에 모든 furniture가 등록됐는지 검사
- EditMode: Resources 의존 제거 또는 제한 검사
- PlayMode: SO catalog에서 palette 생성 후 실제 배치
- CI: `Scripts/ci/check-no-entity-id-branching.sh`

## Done Criteria

- furniture catalog가 SO/registry 기반이다.
- 런타임 코드가 furniture id별 분기를 하지 않는다.
- 직접 PlayMode에서 SO 기반 palette와 배치를 확인했다.
- 관련 테스트와 CI가 통과한다.
