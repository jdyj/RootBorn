# SPUM 캐릭터 통합 가능성 조사

작성일: 2026-05-14  
대상: ROOTBORN Unity 6000.3.13f1, SPUM 1.8.8 로컬 패키지(`C:\Users\jdyj\Downloads\SPUM`)

## 결론

SPUM은 ROOTBORN에 적용 가능하지만, 그대로 런타임에 붙이는 방식은 헌법 규칙과 충돌한다. 특히 SPUM의 저장/로드 흐름은 `Assets/SPUM/Resources`와 `Resources.Load*`를 전제로 하고, ROOTBORN의 Player는 이미 Addressables, `CharacterPartComposer`, `CharacterPartAnimator`, `ToolDefinition.CharacterPartAnimationClip`에 맞춰 분해되어 있다.

추천은 **Option B: SPUM 출력물을 Addressables로 이관하는 마이그레이션 레이어**다. 단, 1차 적용 범위는 Player 교체 PoC와 소수 NPC 외관 검증으로 제한하고, SPUM Manager/Editor UI 전체를 런타임 의존성으로 들이지 않는다.

## 조사 근거

- SPUM 로컬 README: `C:\Users\jdyj\Downloads\SPUM\README.txt`
  - 로컬 버전은 `Version 1.8.8`.
  - 설치 후 `Assets/Resources/SPUM/SPUM_Sprites` 확인, 저장 유닛은 `Assets/Resources/SPUM/SPUM_Units`에 생성된다고 안내한다.
  - NFT 판매는 별도 계약이 필요하다고 명시한다.
- Unity Asset Store 페이지: `2D Pixel Unit Maker - SPUM`, soonsoon, Standard Unity Asset Store EULA, Extension Asset, Single Entity, 페이지상 latest version은 1.8.6로 표시된다. 로컬 README 1.8.8과 마켓 표시가 다르므로, 실제 프로젝트 반입 시 `README.txt`와 Unity Package Manager import 이력을 함께 보관해야 한다.
- Unity Asset Store Terms and EULA(2024-12-04): Non-Restricted Asset은 substantial original content가 있는 Licensed Product에 embedded component로 통합/배포/수익화/수정 가능하다. 제한으로 raw asset 재배포, 무단 UGC 주목적 수익화, 디지털 가치/소유권 표현(NFT 등), AI/ML 학습 사용이 금지된다.

## SPUM 의존성 분석

| 항목 | 확인 결과 | ROOTBORN 영향 |
|---|---|---|
| `Resources.LoadAll` | `Sample/Script/PlayerManager.cs`가 `Resources.LoadAll<SPUM_Prefabs>("")`로 저장 유닛 전체를 로드한다. `Script/SaveLoadHandler/IPrefabFileHandler.cs`, `Core/Script/SPUM_UIManager.cs`, `Core/Script/SPUM_Manager.cs`, `Script/Editor/SPUM_ManagerEditor.cs`도 `Resources.LoadAll`을 사용한다. | ROOTBORN 신규 런타임 로딩 규칙과 충돌. 그대로 사용하면 Addressables 일원화 위반. |
| `Resources.Load` | `Core/Script/Data/SPUM_Prefabs.cs`, `Core/Script/SPUM_AnimationManager.cs`, `Sprite_SheetExporter(Beta)/Script/SPUM_Exporter.cs`, `Core/Script/SPUM_Manager.cs`가 AnimationClip/TextAsset/Sprite 로딩에 사용한다. | SPUM 애니메이션 클립 로딩을 Addressables 또는 직렬화 참조로 감싸야 한다. |
| Editor/파일 쓰기 | `PlayerManager.cs`와 `SPUM_Exporter.cs`는 스크린샷 PNG를 `File.WriteAllBytes`로 저장한다. `SPUM_ManagerEditor.cs`는 `File.WriteAllText(.../Index.json)`를 사용한다. `IPrefabFileHandler.cs`와 `SPUM_AssetHelper.cs`는 `Directory.CreateDirectory`, `AssetDatabase.Refresh/MoveAsset/DeleteAsset/ImportPackage`를 사용한다. | ROOTBORN `Assets/**/*.cs` 직접 쓰기 금지와 직접 충돌하는 C# 생성 경로는 발견하지 못했다. 다만 Editor 도구가 `Assets/SPUM/...` 에셋/JSON/패키지를 직접 쓰므로, 프로젝트 반입 시 SPUM Editor 도구 실행 범위를 별도 격리해야 한다. |
| Sprite PPU | `Core/Basic_Resources` UI/Ect는 PPU 8/16/32/100/125/350 혼재. 실제 유닛 파츠가 있는 `Resources/Addons/**/0_Unit/0_Sprite/**/*.png.meta`는 다수 `spritePixelsToUnits: 32`로 확인된다. | Pixelwood Player는 59x49 cell, PPU 49라 1캐릭터 높이를 약 1 world unit으로 맞춘다. SPUM 유닛 파츠는 PPU 32 기준이므로 타일 PPU 16과 Pixelwood Player PPU 49 사이에서 크기 보정이 필요하다. |
| 애니메이션 방식 | `SPUM_AnimationController.cs`는 0바이트다. 실제 동작은 `SPUM_Prefabs`와 `SPUM_AnimationManager`가 `AnimatorOverrideController`를 만들고, `IDLE/MOVE/ATTACK/DAMAGED/DEBUFF/DEATH/OTHER` 클립을 오버라이드한 뒤 Animator bool/trigger를 설정한다. | 자체 frame loop가 아니라 Unity Animator 기반. ROOTBORN `Animator` 파라미터(`MoveX`, `MoveY`, `Speed`)와 직접 호환되지 않는다. 어댑터가 필요하다. |
| SamplePlayer prefab | `Sample/Prefabs/SamplePlayer.prefab`은 root `SamplePlayer`에 `Transform`, `CapsuleCollider2D`, `Rigidbody2D`, `PlayerObj`가 붙고 tag는 `Player`다. `_prefabs`는 비어 있으며, 저장된 `SPUM_Prefabs` 유닛을 런타임에 자식으로 붙이는 구조다. | ROOTBORN Player prefab 교체용으로 즉시 사용하기 어렵다. Player root는 유지하고 SPUM visual child만 교체하는 편이 안전하다. |

## 현 Player 통합 마찰 지점

| 지점 | 현재 ROOTBORN | SPUM | 마찰/해결 |
|---|---|---|---|
| 이동 입력 | `PlayerController`가 New Input System의 `Keyboard.current`를 읽고 `Rigidbody2D.linearVelocity` 또는 transform 이동을 적용한다. | `PlayerObj`는 마우스 클릭 지점으로 이동하고, 직접 `transform.position += dir * speed * deltaTime`을 수행한다. | ROOTBORN 이동/네트워크/플레이어 대리 테스트를 유지하려면 `PlayerObj` 이동 로직은 사용하지 않는다. SPUM은 visual adapter만 사용한다. |
| 좌우 반전 | `PlayerController.ApplyFlipX`가 root `SpriteRenderer`, `Part_tool`, `CharacterPartComposer.SetFlipX`에 `flipX`를 적용한다. | `PlayerObj.DoMove`는 `_prefabs.transform.localScale = (-1,1,1)` 또는 `(1,1,1)`로 반전한다. | SPUM visual child에만 scale 반전을 적용하거나, SPUM 파츠 SpriteRenderer flipX 일괄 적용 어댑터가 필요하다. root scale은 물리/카메라/테스트 회귀 리스크가 크다. |
| 이동 애니메이션 | Animator 파라미터 `MoveX`, `MoveY`, `Speed`를 쓴다. layered part일 때 `CharacterPartAnimator.SetMotion/Tick`가 Addressables sub-sprite를 갱신한다. | `SPUM_Prefabs.PlayAnimation(PlayerState.MOVE, index)`가 `AnimatorOverrideController["MOVE"]`를 바꾸고 `1_Move` bool을 설정한다. | `MoveX/MoveY/Speed`를 SPUM에 직접 전달할 수 없다. `RootbornSpumCharacterView` 같은 어댑터가 motion/facing을 `PlayerState.IDLE/MOVE`로 변환해야 한다. |
| 공격/휘두르기 | `BeginAttack`은 장착 아이템의 `ToolSpritePrefix`와 `ToolDefinition.CharacterPartAnimationClip`을 사용하고, 마지막 프레임에서 `GatherInteractor.TriggerInteract()`를 호출한다. | `PlayerState.ATTACK` + index로 Animator trigger를 찾는다. 손 무기 슬롯은 SPUM prefab의 자식 SpriteRenderer/패키지 데이터에 종속된다. | 공격 타이밍은 ROOTBORN이 소유해야 한다. SPUM에는 시각효과만 위임하고, `TriggerInteract` 호출 시점은 기존 `_attackFrameDuration`/프레임 카운트 계약을 유지한다. |
| 도구 외관 | `Tool_StoneAxe`는 `_characterPartAnimationClip`이 연결되어 있다. `Tool_StonePickaxe`는 현재 YAML상 `_characterPartAnimationClip` 필드가 없다. held tool은 `sprites/player/tool/{prefix}-{dir}` Addressables sub-sprite로 표시한다. | SPUM에는 `Resources/Addons/**/6_Weapons/**` 무기 파츠와 패키지 기반 외관 조합이 있다. | 도구별 코드 분기 금지. `ToolDefinition` 또는 별도 `SpumAppearanceDefinition` SO가 `ToolDefinition -> SPUM weapon slot/clip index`를 데이터로 들고, 어댑터가 적용해야 한다. |
| NPC 확장 | `NpcDefinition` 기반 SO가 이미 존재한다. | SPUM 저장 프리팹은 `_code`와 prefab 파일 중심이다. | `if (npcId == "Spum_...")` 금지. `NpcDefinition`이 외관 정의 SO를 참조하고, SPUM은 외관 데이터의 한 구현으로 취급해야 한다. |
| Addressables | `ResourceManager`는 `LoadAsync`, `LoadByLabelAsync`, `LoadSubSpriteAsync`, `GetCachedSubSprite`를 제공한다. | SPUM 핵심 로더는 `Resources.Load*`다. | SPUM 생성 결과는 Addressables 그룹으로 등록하고, 런타임에서는 `Managers.Resource`만 사용한다. |

## ROOTBORN 헌법 충돌 지점과 해결안

1. **Resources.Load 금지**
   - 충돌: SPUM은 `Resources.LoadAll("")`, `Resources.Load<AnimationClip>`, `Resources.LoadAll<Sprite>`를 런타임/Editor 양쪽에서 사용한다.
   - 해결: SPUM 원본 Editor 도구는 격리된 제작 도구로만 허용하고, ROOTBORN 런타임에는 `Resources` 경유 코드를 넣지 않는다. 저장된 `SPUM_Prefabs`, AnimationClip, Sprite 파츠는 `Assets/Data/Characters/Spum` 또는 `Assets/Art/SPUMGenerated` 같은 관리 경로로 복사한 뒤 Addressables 그룹 `CharacterVisuals`로 등록한다.

2. **PPU 통일 1 world unit 원칙**
   - 충돌: Pixelwood Player는 PPU 49, SPUM 유닛 파츠는 주로 PPU 32다.
   - 해결: 런타임 scale 보정보다 import PPU를 먼저 검토한다. SPUM 파츠의 원래 애니메이션 피벗이 PPU 32에 맞춰져 있으므로, 1차 PoC는 SPUM visual child scale을 `32/49 ~= 0.653`로 두어 Pixelwood 높이 기준에 맞추고, 최종 채택 전 전용 importer로 PPU를 재조정했을 때 애니메이션 피벗이 깨지는지 PlayMode 스크린샷으로 비교한다.

3. **데이터-드리븐 절대 원칙**
   - 충돌: SPUM 저장 프리팹 `_code` 또는 prefab 이름을 코드에서 분기하면 헌법 위반이다.
   - 해결: `NpcDefinition` 또는 Player appearance save data가 `CharacterAppearanceDefinition`/`SpumAppearanceDefinition` SO를 참조한다. 도구 무기 슬롯도 `ToolDefinition` 또는 별도 SO 필드로 매핑한다. 시스템 코드는 appearance interface만 호출하고 entity ID를 알지 않는다.

4. **C# 스크립트 디스크 직접 쓰기 금지**
   - 확인: SPUM 코드에서 `.cs`를 생성/수정하는 직접 쓰기 경로는 발견하지 못했다. 파일 쓰기는 PNG screenshot, `Index.json`, 폴더 생성, AssetDatabase 작업 중심이다.
   - 해결: SPUM Editor 도구를 ROOTBORN 프로젝트에서 실행할 경우 생성 경로를 문서화하고, 신규 C# 생성은 계속 `script-update-or-create` 또는 Unity Editor 메뉴만 허용한다.

5. **테스트 디시플린**
   - 충돌: 캐릭터 교체는 Player 입력, 이동, 상호작용, 도구 표시, NPC 대화 여정에 영향을 준다.
   - 해결: 실패 테스트 먼저 작성해야 하며, PlayMode는 실제 키보드/마우스 입력, 이동, 상호작용, UI 클릭 경로로 검증한다. 내부 메서드 직접 호출만으로 완료 판정하지 않는다.

## 마이그레이션 옵션 비교

| 옵션 | 설명 | 작업량 | 회귀 리스크 | 헌법 준수도 | 판단 |
|---|---|---:|---:|---:|---|
| Option A | SPUM `Resources` 폴더를 그대로 유지하고 예외 1줄 추가 | 낮음 | 높음 | 낮음 | 비추천. 빠르지만 Addressables 원칙을 깨고 이후 NPC/도구/빌드 크기 관리가 어려워진다. |
| Option B | SPUM 출력물을 Addressables 그룹으로 자동 이관하는 Editor 마이그레이션 레이어 | 중간~높음 | 중간 | 높음 | 추천. 원본 SPUM 제작 흐름과 ROOTBORN 런타임 규칙을 분리할 수 있다. |
| Option C | Player만 SPUM 사용, NPC는 Pixelwood 유지 | 중간 | 중간~높음 | 중간 | 단기 PoC로는 가능하지만 스타일 혼재와 Player 전용 예외가 생긴다. 최종 구조로는 부족하다. |

## 추천 아키텍처

ROOTBORN Player root는 유지한다. `PlayerController`, `GatherInteractor`, `PlayerInventory`, `PlayerInteractionRouter`, `Rigidbody2D`는 계속 ROOTBORN 코드가 소유한다. SPUM은 root 아래 visual child로 붙고, `ICharacterVisualView` 같은 경계 뒤에서 `SetMotion`, `SetFacing`, `PlayAction`, `ApplyAppearance`, `ApplyEquippedToolVisual`만 받는다.

Addressables 이관은 다음 원칙을 따른다.

- SPUM 원본 패키지: 외부/제작 도구로 취급한다.
- ROOTBORN 반입 결과: prefab/clip/sprite/SO를 `Assets/Art/SPUMGenerated` 및 `Assets/Data/Characters` 아래에 둔다.
- 런타임 로딩: `Managers.Resource.LoadAsync` 또는 직렬화 참조만 허용한다.
- NPC 외관: `NpcDefinition`이 외관 SO를 참조한다.
- 도구 외관: `ToolDefinition` 또는 별도 visual mapping SO가 SPUM 무기 슬롯과 공격 clip index를 참조한다.

## 테스트 영향 분석

요구서에는 기존 EditMode 124개 / PlayMode 12개로 적혀 있으나, 현재 워크스페이스에서 `rg "\[(Test|UnityTest)\]"` 기준으로 EditMode 625개, PlayMode 128개 테스트 어트리뷰트가 검색된다. 워크트리가 이미 크게 변경되어 있어 절대 개수는 브랜치 상태에 따라 달라질 수 있다.

회귀 영향이 큰 기존 테스트 영역:

- `Assets/Tests/PlayMode/CharacterPartCompositionPlayModeTests.cs`
  - layered part sprite, right-facing flip, held tool renderer, screenshot evidence를 검증한다.
- `Assets/Tests/EditMode/Family/CharacterPartAnimationIntegrationSourceTests.cs`
  - `PlayerController`가 `CharacterPartAnimator.SetMotion/Tick`, 장착 도구 clip, `Part_tool`을 사용한다는 소스 계약을 검증한다.
- Town/EndToEnd PlayMode 테스트 다수
  - `PlayerController`, `GatherInteractor`, `PlayerInteractionRouter`, `StudentLifeProgressComponent` 존재와 실제 이동/상호작용을 전제로 한다.

신규 필요 테스트:

| 테스트 | 모드 | 목적 |
|---|---|---|
| `SpumResourcesUsageGateTests` | EditMode | ROOTBORN 런타임 코드에서 신규 `Resources.Load*` 사용 금지. SPUM 원본 외부 경로는 제외. |
| `SpumAddressablesMigrationTests` | EditMode | SPUM 출력 prefab/clip/sprite가 Addressables 그룹과 registry에 등록되는지 검증. |
| `SpumVisualAdapterMotionTests` | EditMode | `SetMotion`이 IDLE/MOVE Animator state와 flip/scale을 올바르게 갱신하는지 검증. |
| `SpumToolVisualMappingTests` | EditMode | `ToolDefinition` 또는 visual mapping SO가 도구별 무기 슬롯/공격 clip을 데이터로 제공하는지 검증. |
| `SPUM_PM_001_PlayerKeyboardMoveKeepsSpumVisualSynced` | PlayMode | 실제 키보드 입력으로 Player root가 움직이고 SPUM visual이 MOVE/flip 상태가 되는지 검증. |
| `SPUM_PM_002_PlayerMouseAttackTriggersGatherOnce` | PlayMode | 실제 좌클릭 공격으로 SPUM 공격 애니메이션이 재생되고 기존 `GatherInteractor.TriggerInteract` 타이밍이 1회 유지되는지 검증. |
| `SPUM_PM_003_EquippedToolChangesSpumWeaponSlot` | PlayMode | StoneAxe/StonePickaxe 장착 변경이 코드 ID 분기 없이 SPUM 무기 외관으로 반영되는지 검증. |
| `SPUM_PM_004_NpcDefinitionSpawnsSpumAppearance` | PlayMode | `NpcDefinition`의 외관 SO만 바꿔 SPUM NPC가 생성되고 대화/상호작용 경로가 유지되는지 검증. |

## 라이선스 검토

SPUM 로컬 README는 Unity Asset Store Standard License가 유효한 취득 경로이며, NFT sales에는 별도 계약이 필요하다고 명시한다. Unity Asset Store EULA는 Non-Restricted Asset을 원본 게임 콘텐츠와 결합해 Licensed Product에 embedded component로 배포/수익화/수정할 수 있게 한다. 따라서 ROOTBORN 일반 게임 출시에는 사용할 수 있는 것으로 판단된다.

주의사항:

- raw SPUM asset, source prefab, generated sprite sheet를 별도 asset pack처럼 재배포하지 않는다.
- NFT, tokenized ownership, 디지털 가치/소유권 표현 용도에는 사용하지 않는다.
- SPUM 에셋을 AI/ML 학습 데이터나 생성 모델 입력으로 사용하지 않는다.
- SPUM은 Asset Store상 Extension Asset으로 표시되므로, 협업자/외주가 직접 SPUM Editor 도구를 사용하는 경우 각자 라이선스 보유 여부를 확인해야 한다.
