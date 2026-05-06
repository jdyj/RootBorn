# Save Slots And Seeded World Design

## Goal

ROOTBORN의 시작 흐름에 3개 고정 세이브 슬롯을 추가하고, 각 슬롯의 캐릭터 외형과 월드/타일 seed를 저장한다. `Farm`은 슬롯에 저장된 seed와 ScriptableObject 생성 규칙을 읽어 같은 seed에서는 같은 타일맵/자연물 배치를 만들고, 다른 seed에서는 다른 배치를 만든다.

완료 기준은 다음과 같다.

- `MainMenu`에서 1인 시작을 누르면 3개 세이브 슬롯 선택 UI가 열린다.
- 빈 슬롯은 새 게임 생성 흐름으로 진입하고, 기존 슬롯은 로드/삭제할 수 있다.
- 슬롯 카드에는 비어 있음/존재함 상태, 캐릭터 외형 프리뷰, world seed, tile seed가 표시된다.
- 캐릭터 외형은 전역 `PlayerPrefs`가 아니라 슬롯 메타데이터에 저장된다.
- `SaveService`는 슬롯 목록 조회, 생성, 메타데이터 저장/로드, 삭제를 제공한다.
- `-saveSlot` CLI 흐름은 기존처럼 직접 슬롯명을 지정할 수 있고 UI 슬롯 규약과 충돌하지 않는다.
- `FarmAutoFiller`는 고정 seed 자원 배치 대신 현재 슬롯의 seed 기반 생성 결과를 적용한다.
- 타일 선택은 단순 랜덤이 아니라 SO에 정의된 패턴/변형/인접 규칙을 해석하는 일반 알고리즘으로 처리한다.
- 기존 `FarmFlowTests`, `WorldMapFlowTests`, `WorldSceneFlowTests`, `FarmingScenarioTests` 계열이 깨지지 않는다.

## Current State

현재 저장소에는 다음 기반이 있다.

- `Assets/Scripts/Game/Save/SaveService.cs`
  - 슬롯별 디렉터리를 만들고 JSON 파일을 읽고 쓰는 최소 기능만 있다.
  - 슬롯 목록, 3개 제한, 삭제, 메타데이터 모델은 없다.
- `Assets/Scripts/Game/Player/CharacterCustomization.cs`
  - `BodyIndex`, `EyesIndex`, `HairstyleIndex`, `OutfitIndex`, `AccessoryIndex`를 가진다.
  - 현재 `PlayerPrefs`의 단일 전역 키에 저장한다.
- `Assets/Scripts/UI/CharacterCreator.cs`
  - 캐릭터 외형을 선택하고 `Farm`을 로드한다.
  - 현재 슬롯 개념을 모른다.
- `Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs`
  - 1인 시작 시 `CharacterCreator`를 바로 보여준다.
- `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs`
  - `Farm`에 타일맵, 자원 노드, 플레이어, HUD를 자동 생성한다.
  - 자원 배치는 고정 seed `20260504`를 사용한다.
- `Assets/Data/Resources/Resource_Tree.asset`, `Assets/Data/Resources/Resource_Rock.asset`
  - 자연물은 이미 `ResourceNodeDefinition` SO 데이터로 표현된다.
- `Assets/Data/Tiles/GroundTile.asset`
  - 현재 최소 ground tile만 존재한다.

## Recommended Approach

기존 시작 흐름에 슬롯 선택 단계를 끼워 넣는다.

```text
ModeSelectPanel.OnSingle()
  -> SaveSlotSelectPanel.Show()
      -> existing slot: set active save slot, load Farm
      -> empty slot: set pending save slot, show CharacterCreator
          -> CharacterCreator.OnStart(): save metadata, load Farm
```

이 방식은 기존 `MainMenu -> CharacterCreator -> Farm` 흐름을 가장 적게 흔든다. 호스트/클라이언트 흐름은 이번 작업에서 재설계하지 않는다. 서버/CLI는 `-saveSlot`으로 지정한 슬롯명을 계속 사용하되, UI 슬롯은 `slot-0`, `slot-1`, `slot-2` 고정 ID를 사용한다.

## Save Slot Model

### Runtime DTO

`SaveSlotMetadata`는 JSON 저장용 DTO다.

- `slotId`: `slot-0`, `slot-1`, `slot-2` 또는 CLI에서 직접 지정한 슬롯명
- `displayName`: UI 표시명
- `createdAtUtcTicks`
- `updatedAtUtcTicks`
- `worldSeed`
- `tileSeed`
- `character`: `CharacterCustomization`

`SaveSlotSummary`는 UI 목록용 읽기 모델이다.

- `slotId`
- `exists`
- `metadata`

### Service Responsibilities

`SaveService`는 기존 `WriteJson`/`ReadJson` 호환을 유지하면서 슬롯 관리 API를 추가한다.

- `MaxUiSlots = 3`
- `GetUiSlotIds()`: `slot-0..slot-2`
- `ListUiSlots()`
- `LoadMetadata(slotId)`
- `SaveMetadata(metadata)`
- `CreateMetadata(slotId, character, worldSeed, tileSeed)`
- `DeleteSlot(slotId)`

파일 배치는 다음과 같다.

```text
Application.persistentDataPath/
  saves/
    slot-0/
      metadata.json
    slot-1/
      metadata.json
    slot-2/
      metadata.json
```

슬롯 ID는 파일 경로 안전성을 위해 알파벳, 숫자, `_`, `-`만 허용한다. UI에서 만드는 슬롯은 고정 ID라 추가 검증 실패 가능성이 낮다.

## Active Save Slot Flow

현재 실행 중인 슬롯은 `ActiveSaveContext` 같은 작은 정적 컨텍스트로 보관한다.

- `ActiveSaveContext.SlotId`
- `ActiveSaveContext.Metadata`
- `ActiveSaveContext.Set(metadata)`
- `ActiveSaveContext.Clear()`

`GameBootstrap.Config.SaveSlot`이 있으면 부팅 시 해당 슬롯명을 기본 슬롯으로 사용한다. 메타데이터가 없으면 deterministic fallback metadata를 생성한다. 이 fallback은 CLI/서버가 UI를 거치지 않아도 `FarmAutoFiller`가 seed를 얻을 수 있게 하기 위한 것이다.

## UI Design

### SaveSlotSelectPanel

신규 UI 컴포넌트 `SaveSlotSelectPanel`은 런타임 생성 UI로 시작한다. 기존 `CharacterCreator`가 런타임 UI를 직접 만드는 패턴과 맞춘다.

구성:

- 전체 화면 root
- 제목: 세이브 선택
- 3개 슬롯 카드
- 각 카드:
  - 빈 슬롯: `빈 슬롯`, `새 게임`, 생성될 seed 미리보기
  - 기존 슬롯: 캐릭터 프리뷰, `worldSeed`, `tileSeed`, `로드`, `삭제`

버튼 동작:

- 빈 슬롯 카드 클릭 또는 `새 게임`
  - 새로운 `worldSeed`, `tileSeed`를 생성한다.
  - pending metadata를 `ActiveSaveContext`에 넣는다.
  - `CharacterCreator.ShowForSlot(pendingMetadata)`를 호출한다.
- 기존 슬롯 `로드`
  - metadata를 `ActiveSaveContext`에 넣는다.
  - `Farm` 씬을 로드한다.
- 기존 슬롯 `삭제`
  - 슬롯 디렉터리를 삭제한다.
  - UI를 갱신한다.

### Character Preview

`SaveSlotSelectPanel`과 `CharacterCreator`는 `CharacterCustomization`의 프리뷰 sprite 선택 로직을 공유한다. 단기 구현에서는 `CharacterPieceCatalog.GetIdleSprite()`를 직접 재사용하고, 중복이 커지면 후속 리팩터링으로 `CharacterPreviewBuilder`를 분리한다.

## Seeded World Generation Data

월드 생성 규칙은 모두 ScriptableObject로 표현한다. 특정 타일 이름이나 자원 ID별 C# 분기는 만들지 않는다.

### TileVariantSetDefinition

같은 의미를 갖는 타일 후보 묶음이다.

- `variants`: `TileBase`와 `weight` 배열
- `Pick(seed, x, y)`: 주어진 좌표와 seed에서 weighted deterministic 선택

### TilePatternDefinition

패턴이 깨지면 안 되는 타일셋의 배치 규칙이다.

- `width`
- `height`
- `cells`: 각 패턴 좌표가 참조하는 `TileVariantSetDefinition`
- `repeatMode`: 초기 구현은 반복 패턴만 지원

타일 선택은 `x % width`, `y % height`로 패턴 좌표를 구한 뒤 해당 cell의 variant set에서 고른다. 이렇게 하면 2x2, 3x3, autotile atlas처럼 상대 배치가 중요한 타일도 패턴 좌표가 유지된다.

### TerrainGenerationDefinition

농장 전체 생성 설정이다.

- `width`
- `height`
- `basePattern`
- `patchPatterns`: seed에 따라 덧씌우는 선택적 패치
- `reservedAreas`: 플레이어 시작점, 포털, 농사 튜토리얼 영역 등 생성 금지 영역
- `naturalPropSpawns`

### NaturalPropSpawnDefinition

자연물 배치 규칙이다.

- `resource`: `ResourceNodeDefinition`
- `targetCount`
- `clusterRadius`
- `minDistanceFromReservedArea`
- `minDistanceBetweenProps`
- `spawnableArea`
- `maxAttempts`

배치 결과는 `ResourceNodeDefinition` 참조와 cell 좌표만 가진다. 런타임 GameObject 생성은 `FarmAutoFiller` 또는 별도 applier가 담당한다.

## Generator Architecture

생성기는 순수 로직과 Unity 적용 계층을 분리한다.

### Pure Logic

`SeededWorldGenerator.Generate(TerrainGenerationDefinition definition, int worldSeed, int tileSeed)`는 `GeneratedWorld`를 반환한다.

`GeneratedWorld`:

- `width`
- `height`
- `tiles`: cell 좌표와 `TileBase`
- `props`: cell 좌표와 `ResourceNodeDefinition`

이 계층은 EditMode 테스트에서 scene 없이 검증할 수 있다.

### Unity Applier

`FarmAutoFiller`는 다음 순서로 동작한다.

1. `ActiveSaveContext.Metadata`를 확인한다.
2. 없으면 `GameBootstrap.Config.SaveSlot` 기반 fallback metadata를 만든다.
3. `GameDataRegistry` 또는 serialized field에서 `TerrainGenerationDefinition`을 얻는다.
4. `SeededWorldGenerator`로 결과를 만든다.
5. Tilemap에 타일을 적용한다.
6. `[Resources]` 아래에 자연물 GameObject를 생성한다.
7. 기존 `FarmGrid`, `Player`, HUD 보강 로직은 유지한다.

## Data Registration

신규 SO 클래스는 `Assets/Scripts/Game/WorldGeneration/` 아래에 둔다. 인스턴스는 `Assets/Data/Tiles/` 또는 `Assets/Data/WorldGeneration/` 아래에 둔다.

`GameDataRegistry`에는 다음 참조를 추가한다.

- `TerrainGenerationDefinition defaultFarmTerrainGeneration`

기존 `Resources.Load("GameDataRegistry")` fallback과 `DataManager.Registry` 흐름은 유지한다. 신규 생성 데이터도 registry를 통해 접근한다.

## Testing Strategy

### EditMode

`Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs`

- `ListUiSlots_ReturnsThreeSlots`
- `SaveAndLoadMetadata_PreservesSeedsAndCharacter`
- `DeleteSlot_RemovesMetadata`
- `CreateFourthUiSlot_IsRejected`

`Assets/Tests/EditMode/WorldGeneration/SeededWorldGeneratorTests.cs`

- `Generate_SameSeeds_ProducesSameTilesAndProps`
- `Generate_DifferentSeeds_ProducesDifferentTilesOrProps`
- `Generate_RespectsTilePatternCoordinates`
- `Generate_DoesNotPlacePropsInsideReservedArea`
- `Generate_DoesNotPlacePropsInsideStartSafeRadius`

### PlayMode

기존 테스트가 깨지지 않아야 한다.

- `FarmFlowTests`
- `WorldMapFlowTests`
- `WorldSceneFlowTests`
- `FarmingScenarioTests`

추가 후보:

- `FarmScene_UsesActiveSaveSeeds_ForGeneratedResources`
- `SaveSlotSelectPanel_DisplaysThreeSlots`

## Error Handling

- 슬롯 metadata JSON이 없거나 파싱 실패하면 해당 슬롯은 빈 슬롯으로 취급한다.
- CLI 슬롯 metadata가 없으면 slot name hash에서 deterministic fallback seed를 만든다.
- `TerrainGenerationDefinition`이 없으면 기존 단일 ground tile fill fallback을 유지한다. 이 fallback은 로그 경고를 남기며 기존 테스트 안정성을 위한 임시 경로다.
- 자연물 배치가 `maxAttempts` 내 목표 수를 채우지 못하면 가능한 만큼만 배치하고 경고 로그를 남긴다.

## Non-Goals

- 클라우드 세이브
- 슬롯별 전체 게임 진행 저장
- 멀티플레이어 세이브 동기화
- 타일셋 자동 분석 또는 자동 slicing
- 모든 biome/season 생성
- 룰타일 패키지 의존 추가

## Risks

- 현재 `CharacterCreator.cs`와 일부 UI 문자열이 깨진 인코딩으로 보인다. 이번 작업에서 기능 경계만 수정하고 대규모 UI 텍스트 정리는 별도 작업으로 둔다.
- `GameDataRegistry` 수정은 기존 데이터 생성 도구와 registry asset wiring을 함께 갱신해야 한다.
- `FarmAutoFiller`가 많은 책임을 갖고 있어 generator 적용을 추가하면 더 커질 수 있다. 순수 생성 로직은 별도 파일로 분리하고, filler는 scene 적용만 담당하도록 제한한다.
- 기존 테스트가 `ResourceNode` 수 20개 같은 고정 값을 기대하므로, 기본 generation definition은 초기 target count를 기존 12 tree + 8 rock과 맞춘다.

## Approval

이 설계는 사용자의 승인 응답 `진행해`를 받아 문서화한다.
