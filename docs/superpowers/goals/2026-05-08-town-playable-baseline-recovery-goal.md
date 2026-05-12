# Goal: Town 씬 플레이 가능 상태 복구 및 타일 배치 가이드 구축

현재 ROOTBORN은 기본 진입 경로가 Farm에서 Town으로 전환된 상태지만, `Assets/Scenes/Town.unity`에 실제 플레이 가능한 기반이 부족하다. Town에는 타일이 깔려 있지 않고, 플레이어도 없으며, Tab 키 UI/인벤토리/상태/퀘스트 등 기존 Farm에서 동작하던 기본 조작이 Town에서 정상 작동하지 않는 것으로 보인다.

## 목표

Town 씬을 Farm과 동일한 기본 플레이 가능 수준으로 복구한다.

완료 시점에는 다음이 모두 가능해야 한다.

1. `Town.unity`를 실행하면 빈 화면이 아니라 실제 바닥/길/도시 생활 공간이 보인다.
2. 플레이어 캐릭터가 Town 씬에 존재하고 이동/카메라/HUD가 정상 동작한다.
3. Tab 키 및 기존 기본 UI 패널 흐름이 Farm에서처럼 Town에서도 작동한다.
4. Town 씬에 타일맵 또는 배치 가능한 타일 기반이 마련되어 사용자가 Unity Editor에서 직접 타일을 쉽게 배치할 수 있다.
5. 타일 설치/배치 방법이 문서화되어, 사용자가 같은 방식으로 Town 공간을 확장할 수 있다.
6. Farm 전용 하드코딩을 Town으로 단순 복사하지 않고, 기본 플레이 씬이 Town이어도 기존 시스템이 재사용 가능하게 정리한다.

## 반드시 지킬 규칙

- `Assets/**/*.cs` 파일은 디스크 직접 쓰기 금지. C# 변경은 반드시 Unity MCP `script-update-or-create` 사용.
- 신규/수정 코드는 TDD로 진행한다. 실패 테스트를 먼저 만들고 구현한다.
- `.unity`, prefab, scene YAML은 직접 손으로 편집하지 않는다. Unity MCP scene/gameobject/tilemap/editor API 또는 Unity Editor API를 사용한다.
- 게임 엔티티별 ID 분기, crop/tool/resource ID hardcoding, enum 기반 엔티티 분기는 금지한다.
- 기존 Farm/Crop 시스템을 Town 기본 경로에 다시 노출하지 않는다. 필요한 경우 legacy로 격리한다.
- 보상/인벤토리 트랜잭션 원칙과 기존 회귀 테스트를 깨지 않는다.

## 먼저 확인할 파일과 문맥

- `docs/superpowers/specs/2026-05-08-town-concept-conversion-design.md`
- `docs/superpowers/plans/2026-05-08-town-concept-conversion.md`
- `docs/art/town-concept-audit.md`
- `Assets/Scenes/Town.unity`
- `Assets/Scenes/Farm.unity`
- `Assets/Tests/PlayMode/TownConcept/TownSceneBootTests.cs`
- `Assets/Tests/PlayMode/TownConcept/TownStyle2UiSmokeTests.cs`
- `Assets/Tests/EditMode/TownConcept/TownDefaultFlowSourceAuditTests.cs`
- `Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs`
- `Assets/Scripts/Game/Bootstrap/*`
- `Assets/Scripts/UI/MainMenu/*`
- existing player/camera/input/HUD installers used by Farm

## 작업 순서

1. 현재 Town 씬 상태를 Unity MCP로 감사한다.
   - root GameObject 목록
   - Camera/EventSystem/Canvas 존재 여부
   - Player 또는 player spawn point 존재 여부
   - Grid/Tilemap 존재 여부
   - UI installer/HUD installer 존재 여부
   - Farm 씬과 비교해 빠진 핵심 오브젝트 목록

2. 실패 테스트를 먼저 추가한다.
   - Town 씬 로드 시 root object만 있는 수준이 아니라 visible Renderer 또는 Canvas가 있어야 한다.
   - Town 씬에 player 또는 player spawn path가 있어야 한다.
   - Town 씬에 Grid/Tilemap 기반이 있어야 한다.
   - Town에서 Tab 또는 기본 UI 패널 경로가 동작해야 한다.
   - 가능하면 Farm에서 보장되던 기본 입력/UI smoke와 동일한 기대값을 Town PlayMode 테스트로 만든다.

3. Town 씬을 복구한다.
   - Main Camera
   - EventSystem
   - Canvas/HUD/Style2 Modern UI path
   - Player 또는 player prefab instance/spawn point
   - Grid + Tilemap layers
   - 최소 도시 생활 공간: 집/거리/상점/커뮤니티 단서 중 2개 이상
   - 빈 배경이 아니라 플레이 가능한 바닥 타일을 배치

4. 타일 설치 방식을 정리한다.
   - 현재 프로젝트에서 사용할 타일 에셋 후보를 확인한다.
     - `Assets/Data/Tiles/GroundTile.asset`
     - `Assets/AddressableAssetsData/AssetGroups/Tiles.asset`
     - `Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Town/`
     - `Assets/Modern_Farm_v1.2/...`
   - 사용자가 Unity Editor에서 직접 배치할 수 있도록 Tile Palette 또는 Tilemap 기반 가이드를 만든다.
   - 필요한 경우 `docs/art/town-tilemap-placement-guide.md`를 작성한다.
   - 가이드에는 최소한 다음을 포함한다.
     - 어떤 씬을 열어야 하는지
     - Grid/Tilemap 계층 구조
     - 어떤 Tile asset을 선택해야 하는지
     - Tile Palette를 만드는 방법
     - 바닥/길/장식/충돌 Tilemap을 어떻게 나눌지
     - 사용자가 직접 타일을 추가 배치한 뒤 테스트하는 방법

5. Farm과 동일한 기본 작동을 Town에서 보장한다.
   - Tab UI
   - Inventory/Status/Quest/HUD
   - player movement
   - camera follow/framing
   - scene boot/default flow
   - save slot/main menu에서 Town 진입
   - 기존 Farm-only installer가 Town에서 빠져서 생기는 문제를 town-aware installer 또는 공통 bootstrap으로 해결

6. 검증한다.
   - TownConcept EditMode 테스트 실행
   - TownConcept PlayMode 테스트 실행
   - 관련 Farm 회귀 테스트 중 기본 흐름 테스트 실행
   - `Scripts/ci/check-no-entity-id-branching.sh` 실행
   - Unity MCP screenshot으로 Town Game View를 캡처해 비어 있지 않고 player/UI/tile이 보이는지 확인
   - `docs/art/town-concept-audit.md` 또는 새 가이드 문서에 검증 결과를 기록

## 완료 조건

- `Town.unity`가 빈 씬처럼 보이지 않는다.
- Town에 타일 기반 바닥이 있다.
- Town에 플레이어가 있고 기본 이동이 된다.
- Tab 및 기본 UI가 Farm과 동일하게 동작한다.
- 사용자가 직접 타일을 배치할 수 있는 문서가 존재한다.
- PlayMode smoke 테스트가 Town의 player/UI/tile/visual root를 검증한다.
- 기존 Farm 기본 흐름에서 옮겨온 기능이 Town에서 회귀 없이 동작한다.
- 모든 변경은 테스트와 함께 커밋 가능한 상태이며, 커밋 메시지는 한글 본문과 허용 prefix를 사용한다.
