# SPUM Character Integration Technical Design

작성일: 2026-05-14

## 결정 요약

SPUM은 ROOTBORN 런타임 타입이 아니라 Addressables로 이관된 visual asset과 ScriptableObject appearance data 뒤의 구현체로 취급한다. Player root, `PlayerController`, `GatherInteractor`, `PlayerInventory`, `Rigidbody2D`는 유지하고, SPUM은 root 하위 visual child가 `ICharacterVisualView` 경계를 통해 모션, 방향, 액션, 장착 도구 외관만 반영한다.

추천안은 Option B다. SPUM 원본 패키지는 제작 도구로만 사용하고, 저장된 prefab/clip/sprite/part metadata를 ROOTBORN 관리 경로로 복사한 뒤 Addressables `CharacterVisuals` 그룹과 SO catalog에 등록한다. ROOTBORN 런타임에는 SPUM 원본 `Resources.Load*`, `PlayerObj` 이동 로직, SPUM Manager UI를 들이지 않는다.

## 컴포넌트 경계

### `ICharacterVisualView`

`PlayerController`와 NPC runtime은 구체 visual 시스템을 알지 않는다. 필요한 최소 계약은 다음이다.

```csharp
public interface ICharacterVisualView
{
    void ApplyAppearance(CharacterAppearanceDefinition appearance);
    void SetMotion(Vector2 input, Vector2 facing);
    void SetFlipX(bool flipX);
    void PlayAction(CharacterVisualAction action);
    void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping);
    void Tick(float deltaTime);
}
```

이 경계가 `PlayerController`가 SPUM 타입을 직접 알지 않게 하는 핵심이다. `PlayerController`는 기존 `Animator`, `SpriteRenderer`, `CharacterPartAnimator` 직접 호출을 장기적으로 visual view 호출로 이동한다. 단기 전환 기간에는 `PixelwoodCharacterVisualView`가 기존 `CharacterPartComposer`/`CharacterPartAnimator`를 감싸고, `SpumCharacterVisualView`가 SPUM AnimatorOverrideController와 visual child를 감싼다.

### `SpumCharacterVisualView`

책임은 SPUM visual child 내부에 한정한다.

- `SpumAppearanceDefinition`의 prefab reference와 part snapshot을 적용한다.
- ROOTBORN motion/facing을 SPUM `PlayerState.IDLE`, `MOVE`, `ATTACK`과 clip index로 변환한다.
- root transform, collider, rigidbody, camera target은 수정하지 않는다.
- 좌우 반전은 visual child localScale 또는 child SpriteRenderer flipX에만 적용한다.
- PPU/scale 보정은 `SpumAppearanceDefinition.VisualScale`과 `VisualRootOffset`을 사용한다.

### 기존 Pixelwood layered character

기존 `CharacterPartComposer`/`CharacterPartAnimator`는 제거하지 않는다. `PixelwoodCharacterVisualView` adapter를 추가해 SPUM과 같은 `ICharacterVisualView`를 구현한다. 이렇게 하면 SaveSlot, Player, NPC가 Pixelwood와 SPUM을 같은 data boundary로 다루고, rollback도 기존 visual view로 전환하는 수준에서 끝난다.

### Player/NPC adapter 분리

visual view는 공통이고, host object adapter는 분리한다.

- `PlayerCharacterVisualAdapter`: `PlayerController`와 inventory/equipment 변경 이벤트를 받아 visual view에 전달한다.
- `NpcCharacterVisualAdapter`: `NpcDefinition` 또는 spawn context의 appearance SO를 받아 idle/facing/dialogue 상태를 전달한다.

Player와 NPC의 입력/상호작용 소유권이 다르므로 adapter를 합치지 않는다. 단, 둘 다 같은 `ICharacterVisualView`, `CharacterAppearanceDefinition`, `ToolVisualMappingDefinition`을 사용한다.

## 데이터 모델

모든 외관 연결은 SO 데이터와 save snapshot으로 표현한다. NPC ID, tool ID, SPUM prefab 이름, SPUM `_code`에 대한 C# 분기는 금지한다.

| 타입 | 경로 | 책임 |
|---|---|---|
| `CharacterAppearanceDefinition` | `Assets/Scripts/Game/Characters/` / `Assets/Data/Characters/` | Pixelwood/SPUM 공통 appearance root. `VisualKind`, default scale, fallback policy, concrete appearance reference를 가진다. |
| `SpumAppearanceDefinition` | `Assets/Scripts/Game/Characters/Spum/` / `Assets/Data/Characters/Spum/Appearances/` | SPUM prefab Addressables reference, part catalog, selected part snapshot, animator clip set, visual scale/offset. |
| `SpumPartCatalogDefinition` | `Assets/Data/Characters/Spum/Catalogs/` | UI와 runtime이 사용할 category/part 목록의 단일 출처. Addressables label을 직접 훑지 않는다. |
| `SpumPartDefinition` | `Assets/Data/Characters/Spum/Parts/` | category id, stable part id, display key, preview sprite, runtime sprite/prefab/clip reference, default 여부. |
| `SpumCharacterCreatorPresetDefinition` | `Assets/Data/Characters/Spum/Presets/` | New Game 기본값과 random seed 기반 후보 pool. |
| `ToolVisualMappingDefinition` | `Assets/Data/Tools/VisualMappings/` | `ToolDefinition` 참조, SPUM weapon slot part, attack clip index/action key, held-tool preview policy. |

`NpcDefinition`에는 `CharacterAppearanceDefinition _appearance` 필드를 추가한다. 기존 `Texture2D _worldTexture` 경로는 Pixelwood/fallback 표시로 유지하되, SPUM NPC 생성은 `NpcDefinition.Appearance`가 있는 경우 visual adapter가 처리한다.

Player save metadata는 기존 `CharacterAppearance`를 바로 확장하지 않고, 새 `CharacterAppearanceSnapshot` 값을 둔다.

```text
SaveSlotMetadata
  CharacterAppearanceSnapshot AppearanceSnapshot
    schemaVersion: 1
    visualKind: "spum" | "pixelwood"
    appearanceDefinitionId
    catalogId
    selectedParts: [{ categoryId, partId }]
    toolPreviewPartIds: optional, preview only
```

catalog 업데이트로 part id가 누락되면 `SpumPartCatalogDefinition`이 같은 category의 default part를 반환한다. default도 없으면 category를 생략하고 warning을 남기며 저장 데이터는 즉시 덮어쓰지 않는다. 다음 저장 시 resolved snapshot을 기록한다.

## Addressables 이관 흐름

Editor 도구 이름은 `SpumAddressablesMigrationWindow`와 `SpumAddressablesMigrationService`로 둔다. 이 도구는 사용자 승인 후 Unity Editor에서만 실행한다.

1. 사용자가 SPUM 원본 출력 경로와 대상 catalog id를 선택한다.
2. 도구가 SPUM saved prefab, AnimationClip, Sprite/texture part, preview sprite 후보를 읽는다.
3. 대상 경로에 에셋을 복사한다.
   - `Assets/Art/SPUMGenerated/{catalogId}/Prefabs/`
   - `Assets/Art/SPUMGenerated/{catalogId}/Sprites/`
   - `Assets/Art/SPUMGenerated/{catalogId}/Animations/`
   - `Assets/Data/Characters/Spum/{Catalogs,Parts,Appearances,Presets}/`
4. Addressables group `CharacterVisuals`를 만들거나 찾고, 복사된 prefab/clip/sprite를 등록한다.
5. `SpumPartCatalogDefinition`과 `SpumAppearanceDefinition`을 생성/갱신한다.
6. 검증기가 ROOTBORN runtime assembly에서 신규 `Resources.Load*` 사용이 없는지 확인한다.

런타임은 `Managers.Resource.LoadAsync` 또는 직렬화된 Addressables reference만 사용한다. Addressables label은 asset preload와 검증 용도이고, UI category/part 목록의 출처는 `SpumPartCatalogDefinition`이다. label 스캔만으로 UI를 만들면 표시 순서, default, fallback, localization key가 불안정해지기 때문이다.

## Runtime Flow

### Player

1. SaveSlot New Game에서 `CharacterAppearanceSnapshot`을 생성한다.
2. Town 진입 bootstrap이 snapshot을 읽고 `CharacterAppearanceDefinition`을 registry에서 찾는다.
3. Player root는 기존 prefab과 component를 유지한다.
4. `PlayerCharacterVisualAdapter`가 visual child를 생성하거나 기존 child를 찾는다.
5. `SpumCharacterVisualView.ApplyAppearance`가 Addressables prefab/part를 적용한다.
6. 매 frame `PlayerController`의 input/facing/attack/equipment 상태가 adapter를 통해 visual view에 전달된다.
7. 공격 상호작용 타이밍은 기존 `_attackFrameDuration`과 `GatherInteractor.TriggerInteract()`가 계속 소유한다.

### NPC

1. NPC spawn 시스템은 `NpcDefinition.Appearance`를 읽는다.
2. `NpcCharacterVisualAdapter`가 NPC root 아래 visual child를 만든다.
3. 대화/상호작용 collider와 prompt는 기존 NPC root가 유지한다.
4. visual view는 idle/facing/dialogue reaction 정도만 표시한다.

## 공격/도구 Mapping

`ToolDefinition`에는 직접 SPUM 필드를 계속 늘리지 않고, `ToolVisualMappingDefinition` registry를 둔다. mapping은 `ToolDefinition` 참조와 SPUM visual payload를 가진다.

- `WeaponSlotCategoryId`: 예: `weapon`.
- `WeaponPartId`: catalog의 stable part id.
- `AttackActionKey`: 예: `attack.chop`.
- `SpumAttackClipIndex`: SPUM `PlayerState.ATTACK` index.
- `PixelwoodClip`: 기존 `CharacterPartAnimationClipDefinition` fallback.

Player가 장착 도구를 바꾸면 adapter는 현재 `ToolDefinition`으로 mapping registry를 조회한다. 없으면 무기 visual을 비우고 기본 attack visual을 사용한다. StoneAxe/StonePickaxe도 같은 데이터 조회를 타며 tool id switch 문은 만들지 않는다.

공격 타이밍은 ROOTBORN이 소유한다. SPUM은 `PlayAction(Attack)`을 받아 시각 애니메이션만 재생하고, 채집 판정은 기존 `PlayerController.BeginAttack`과 `GatherInteractor.TriggerInteract()` 경로가 유지된다.

## PPU/Scale 정책

| 옵션 | 장점 | 리스크 | 판단 |
|---|---|---|---|
| SPUM import PPU를 49로 재조정 | 월드 단위가 Pixelwood와 맞고 child scale이 단순해진다. | Animator pivot, sliced sprite rect, SPUM 패키지 업데이트 때 재import 차이가 생긴다. | 최종 후보지만 PoC 1차는 아니다. |
| visual child scale을 `32/49 ~= 0.653`로 보정 | 원본 SPUM pivot/import를 건드리지 않고 빠르게 비교 가능하다. | collider/root 크기와 visual bounds 차이를 SO로 관리해야 한다. UI preview와 world scale을 별도로 맞춰야 한다. | 1차 PoC 추천. |
| 캐릭터별 visual bounds 기준 scale을 SO에 저장 | NPC/Player별 체형 차이와 preview 크기를 정밀하게 맞출 수 있다. | 튜닝 데이터가 늘고 잘못된 bounds가 있으면 일관성이 깨진다. | 2차 보정으로 사용. |

기본 정책은 원본 PPU 유지 + visual child scale `0.653` + `SpumAppearanceDefinition.VisualBounds` 기록이다. importer PPU 49는 별도 브랜치에서 screenshot 비교 후 채택 여부를 결정한다.

## MainMenu/SaveSlot Character Creator Flow

UI 위치는 Option UI-B를 추천한다. `NewGameButton` 클릭 후 별도 `SpumCharacterCreatorPanel`을 표시하고, 완료 시 저장 슬롯을 생성한 뒤 Town으로 진입한다.

선택 이유:

- 기존 `SaveSlotSelectPanel`의 슬롯 카드 책임과 character creator 책임을 분리한다.
- SPUM 전용 category/grid/preview/preload가 커질 수 있어 `SaveSlotSelectPanel` 내부 step으로 넣으면 파일과 UI가 비대해진다.
- MainMenu 별도 Character 메뉴는 저장 슬롯 metadata와 분리되어 초보 흐름이 늘어난다.

기존 Pixelwood layered 선택 UI와 SPUM UI는 공통 panel로 억지 통합하지 않는다. 공통화는 `ICharacterCreatorPanel` 수준의 save snapshot contract와 preview adapter만 둔다. SPUM UI는 `SpumPartCatalogDefinition` 기반 category tab/grid를 제공한다.

preview는 실제 Player prefab이 아니라 UI 전용 preview root를 쓴다. 실제 Player prefab은 collider, input, camera, network gate가 섞여 있어 UI 로딩과 테스트가 무거워진다. preview root는 `SpumCharacterVisualView`만 붙인 isolated hierarchy로 만들고, runtime visual과 같은 appearance adapter를 사용해 일치성을 검증한다.

## SPUM Part Catalog/Save Schema

category id는 SPUM 원본 폴더명에 직접 종속하지 않고 ROOTBORN stable id로 정규화한다.

- `body`
- `skin`
- `eyes`
- `hair`
- `outfit`
- `accessory`
- `back`
- `helmet`
- `weapon-preview`

`weapon-preview`는 게임 장착 도구와 분리한다. character creator에서는 무기 미리보기 토글만 제공하고, 실제 게임 시작 장착 도구는 inventory/starting tool 데이터가 소유한다.

저장 snapshot에는 selected category/part pair만 들어간다. Sprite, AnimationClip, prefab reference는 저장하지 않는다. runtime은 catalog id와 part id로 SO를 다시 해석한다.

## 테스트 전략

실패 테스트를 먼저 작성한다.

- EditMode: `SpumResourcesUsageGateTests`가 ROOTBORN runtime source에서 신규 `Resources.Load*`를 차단한다.
- EditMode: `SpumAddressablesMigrationTests`가 migrated prefab/clip/sprite와 `CharacterVisuals` group 등록을 검증한다.
- EditMode: `SpumAppearanceDefinitionTests`가 catalog default/fallback과 missing part 대체를 검증한다.
- EditMode: `SpumToolVisualMappingTests`가 StoneAxe/StonePickaxe mapping을 SO 데이터로만 검증한다.
- EditMode: `SpumVisualAdapterMotionTests`가 motion/facing/action을 SPUM state로 변환하는지 검증한다.
- EditMode: `SpumCharacterCreatorPanelStyleTests`가 `ModernUiTileImage + ModernUiRecipes.CommonPanel` 사용을 검증한다.
- PlayMode: 실제 키보드 입력으로 Player 이동, MOVE/flip 반영을 검증한다.
- PlayMode: 실제 좌클릭 공격으로 SPUM ATTACK과 `GatherInteractor` 1회 호출을 검증한다.
- PlayMode: 실제 UI 클릭으로 part 변경, random, save slot 생성, Town 진입, Player appearance 유지를 검증한다.
- PlayMode: save/load 후 SPUM appearance snapshot이 유지되는지 검증한다.

## 성능 정책

SPUM은 다중 SpriteRenderer와 AnimatorOverrideController를 사용할 수 있으므로 다음 예산을 구현 전부터 둔다.

- creator panel 표시 전 `CharacterVisuals` catalog metadata와 preview sprite만 preload한다.
- full prefab/animation clip은 preview root 1개와 실제 Player/NPC 생성 시점에 제한적으로 load한다.
- part grid는 paging 또는 virtualization을 적용한다. category별 40개 이상이면 모든 cell을 한 번에 Instantiate하지 않는다.
- random 생성은 catalog 배열을 한 번 cache한 뒤 allocation 없는 selection path로 만든다.
- Town 다수 NPC 적용 전 renderer count, Animator count, Addressables memory를 PlayMode performance probe로 기록한다.

## 선택하지 않은 대안

- SPUM `Resources` 유지: 헌법의 Addressables 규칙과 신규 runtime `Resources.Load*` 금지에 정면으로 충돌한다.
- Player만 SPUM 적용: 단기 PoC는 가능하지만 Player/NPC appearance boundary가 갈라져 이후 분기와 중복 테스트가 늘어난다.
- `SaveSlotSelectPanel` 안에 creator step 직접 삽입: 빠르지만 현재 panel이 이미 slot card, metadata 생성, preview까지 소유하고 있어 SPUM category/grid/preload까지 넣으면 책임이 과해진다.
- Addressables label을 UI catalog로 직접 사용: 표시 순서, default, localization, fallback rule을 asset label만으로 안정적으로 표현하기 어렵다.

## 남은 불확실성

- SPUM 로컬 README는 v1.8.8이고 Asset Store 표시는 v1.8.6으로 달라 실제 반입 시 라이선스/버전 근거를 다시 보관해야 한다.
- 실제 SPUM category 폴더와 weapon slot 구조는 migration tool 작성 시 샘플 세트를 기준으로 다시 확정해야 한다.
- PPU 49 reimport가 pivot을 깨는지는 screenshot/PlayMode 비교 전에는 확정할 수 없다.
- StonePickaxe의 기존 `_characterPartAnimationClip` 누락은 SPUM mapping 구현 전 별도 데이터 보정이 필요하다.

