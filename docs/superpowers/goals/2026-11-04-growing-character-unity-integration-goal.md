# Growing Character Unity Integration Goal

아래 프롬프트를 새 goal 세션에 그대로 사용한다. 아트 팩이 사용자 승인을 받은 뒤에 실행한다.

```text
ROOTBORN Unity 프로젝트에 승인된 성장 캐릭터 아트 팩을 에셋과 프리팹으로 연결해, 플레이어가 실제 게임에서 3단계 성장과 옷 갈아입기를 쓸 수 있게 해줘.

입력:
- 아트 팩: output/assetpacks/character-parts-v1-20261104/ (README.md, spec.json, layer-order.json)
- 설계: docs/superpowers/specs/2026-11-04-growing-character-parts-design.md
- 작업 기록: .superpowers/sdd/2026-11-04-growing-character-parts/progress.md 의 Ruling 줄. 설계와 달라진 결정이 여기 있으며, 충돌하면 이쪽이 최신이다.
- 먼저 README로 팩이 조각형(부위별)인지 프레임형(방향·프레임별 시트 겹침)인지 확인하고, 실제 구조에 맞춰 구현한다.

핵심 요구:
- 놀이터, 학교, 졸업 후 3단계 × 아래·왼쪽·오른쪽·위 방향으로 걷기와 대기가 동작한다.
- 베이스 위에 머리카락, 옷을 겹치고, 옷을 바꿔도 몸의 위치와 크기가 변하지 않는다.
- 옷은 첫 세트에서 단계 전용이다. 현재 단계에 맞지 않는 옷은 입힐 수 없고, 저장에 남아 있으면 안전하게 기본 옷으로 대체한다.
- 성장 단계와 착용 상태가 세이브 슬롯에 저장되고 로드 후 복원된다. 멀티플레이에서 다른 플레이어에게도 같은 외형이 보인다.
- 신규 Player 교체 후에도 이동, 상호작용, 도구, NPC 대화, 카메라가 그대로 동작한다.

에셋화:
- 임포트는 Editor 메뉴 1개로 재실행할 수 있어야 한다. 예: `Rootborn/Characters/Import Growing Character Pack`.
- PNG는 Assets/Art/Characters/Growing/ 아래에 둔다. TextureImporter는 PPU와 슬라이스 값을 spec.json에서 읽고, Point 필터, 무압축, Mipmap 꺼짐, 원본 크기 이상의 maxTextureSize, Read/Write 꺼짐으로 설정한다.
- 데이터는 ScriptableObject로 만든다. 예: GrowthStageDefinition, CharacterBaseDefinition, OutfitDefinition, HairDefinition. Assets/Data/Characters/Growing/ 에 두고 GameDataRegistry에 등록한다.
- 기존 CharacterAppearanceDefinition과 ICharacterVisualView를 확장한다. PlayerCharacterVisualAdapter, NpcCharacterVisualAdapter, PlayerController는 입력과 이동 소유권을 유지한다.
- Addressables 그룹과 주소를 등록하고, 주소 규약은 rules/path-based/assets-addressables.md를 따른다.

프리팹화:
- Assets/Prefabs/Characters/GrowingCharacterVisual.prefab: 레이어별 SpriteRenderer를 layer-order.json 순서로 가진 시각 전용 자식 프리팹. 콜라이더와 Rigidbody를 넣지 않는다.
- Assets/Prefabs/Player.prefab: 시각 자식만 교체한다. NetworkObject와 기존 컴포넌트 참조를 유지한다.
- 프리팹은 Unity API로 만들고 YAML을 직접 수정하지 않는다. 저장 후 다시 열어 직렬화 참조에 null이 없는지 검증한다.

최적화 (아래를 측정하고 결과를 보고한다):
- 단계·옷별 아틀라스로 묶어 현재 착용분만 Addressables로 로드하고, 옷을 바꾸면 이전 핸들을 Release한다. 같은 주소를 중복 로드하지 않는다.
- 전체 옷을 PreLoad 라벨에 넣지 않는다.
- 프레임 전환은 방향이나 프레임 인덱스가 바뀔 때만 sprite를 교체한다. Update에서 GetComponent, Find, 문자열 조립, LINQ, 할당을 하지 않는다. sprite 조회는 (레이어, 방향, 프레임) 배열 인덱스로 한다.
- 같은 캐릭터의 레이어가 같은 재질과 아틀라스를 쓰게 해서 배칭을 유지한다. 플레이어 4명 + NPC 5명 기준 드로우콜과 SetPass를 Frame Debugger 또는 렌더링 통계로 전후 비교한다.
- 메모리: 아틀라스 합계 크기와 런타임 Texture 메모리를 표로 기록한다. 예산을 넘으면 먼저 사용자에게 알린다.
- 레이어 수가 많아 드로우콜이 예산을 넘으면, 착용 변경 때 한 장으로 합성해 캐시하는 방식을 비교 후 제안한다. 근거 없이 먼저 도입하지 않는다.

절대 규칙:
- AGENTS.md와 .claude/rules를 따른다. 사용자 미커밋 변경을 되돌리거나 일괄 커밋하지 않는다.
- Assets/**/*.cs 신규 파일은 디스크에 직접 쓰지 않는다. Unity MCP script-update-or-create 또는 Editor 메뉴로 만든다.
- 실패 테스트를 먼저 쓴다. 옷, 단계, 머리카락 ID별 if, switch, enum, 전용 클래스를 금지한다.
- 신규 Resources.Load를 쓰지 않는다. BootstrapAsync 완료 전에 동기 조회하지 않는다.
- 작업 시작 시 .mcp.json의 포트와 UserSettings/AI-Game-Developer-Config.json의 host가 같은지 확인한다. Unity를 다루는 에이전트는 하나만 둔다.
- Assets/AddressableAssetsData/link.xml이 테스트 실행 중 삭제되는 알려진 문제가 있다. 삭제되면 복원하고 보고한다.
- 사용자 세이브를 쓰지 않고 격리된 QA 세이브를 쓴다.

필수 테스트:
- EditMode: 임포트 재실행이 멱등한지, TextureImporter 값, sprite 이름과 rect, SO 참조와 Registry 등록, Addressables 주소, 단계 전용 옷 검증, 저장/로드, 잘못된 세이브 복구, 키 없는 ID 처리.
- PlayMode: 실제 입력(WASD)으로 4방향 이동 시 올바른 방향과 프레임, 옷 교체 전후 몸 좌표 불변, Addressables 핸들 해제, 씬 전환 후 외형 유지.
- 플레이어 흐름: MainMenu → 슬롯 선택 → 캐릭터 선택 → Town → 이동 → 옷 변경 → 저장 → 재시작 → 복원. 메서드 직접 호출로 대체하지 않는다.
- 시각 검증: PlayMode Game View 스크린샷으로 3단계와 4방향을 직접 확인한다. 런타임 상태와 저장된 프리팹/에셋 상태를 구분해 각각 검증한다.

완료 보고:
- 변경 파일과 재생성 절차, 영향 받은 씬과 프리팹.
- 테스트 결과. 전체 EditMode는 기준선(2026-11-04 시점 2,150건 중 68건 실패)과 비교해 새 실패를 이름으로 적는다.
- 드로우콜, 메모리 측정값, 남은 성능 위험.
- 검증하지 못한 항목과 그 이유.
```
