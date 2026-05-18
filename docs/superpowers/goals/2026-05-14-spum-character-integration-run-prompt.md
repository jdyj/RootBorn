# SPUM 캐릭터 통합 및 진입 전 커스터마이징 UI 작업 프롬프트

## Task: SPUM 캐릭터 ROOTBORN 통합 + 게임 진입 전 캐릭터 커스터마이징 UI 설계 구체화 및 구현 계획 작성

## 목표

ROOTBORN 프로젝트에 SPUM(Soonsoon Pixel Unit Maker) 캐릭터를 헌법 규칙을 지키면서 통합하고, 게임 진입 전 플레이어가 SPUM 파츠별 캐릭터 외관을 선택할 수 있는 커스터마이징 UI를 만들 수 있도록 실제 구현 전 상세 설계와 TDD 구현 계획을 작성한다.

조사 문서:

- `docs/research/spum-integration-feasibility.md`
- `docs/architecture/adr-0001-spum-character-integration.md`
- `production/epics/EPIC-001-spum-character-integration.md`

## 결론 전제

SPUM 통합은 가능하다. 단, SPUM 원본의 `Resources.Load*`/`Assets/Resources/SPUM/...` 런타임 의존을 그대로 쓰는 방식은 금지한다.

선택할 방향은 Option B:

- SPUM 출력 prefab/clip/sprite를 ROOTBORN 관리 경로로 이관
- Addressables 그룹으로 등록
- Player/NPC는 ScriptableObject 외관 데이터로 SPUM visual을 참조
- 기존 `PlayerController`, `GatherInteractor`, `PlayerInventory`, `Rigidbody2D`는 유지
- SPUM은 root 아래 visual child/adapter로만 사용
- MainMenu/SaveSlot/New Game 흐름에 SPUM 파츠 커스터마이징 UI를 추가
- 선택한 SPUM 파츠 조합은 저장 슬롯 metadata 또는 별도 appearance save data로 저장
- 게임 진입 후 Player와 NPC 외관은 같은 appearance data/adapter 경계를 사용

## 반드시 지킬 ROOTBORN 헌법

1. `Assets/**/*.cs` 직접 디스크 쓰기 금지
   - 신규/수정 C#은 반드시 Unity MCP `script-update-or-create` 사용.
2. 신규 런타임 `Resources.Load*` 금지
   - SPUM 원본 코드의 `Resources.Load*`는 ROOTBORN 런타임에 들이지 않는다.
3. 엔티티 ID별 C# 분기 금지
   - `if (npcId == ...)`, `switch(toolId)`, SPUM prefab 이름별 분기 금지.
4. 모든 Player/NPC/Tool 외관 연결은 ScriptableObject 데이터 기반.
5. TDD 필수
   - 실패 테스트 먼저, 구현 다음.
6. PlayMode 검증은 실제 입력/이동/상호작용/클릭 경로를 사용.
7. Unity Editor 실행 전 사용자 승인 필요. 이 계획 단계에서는 Editor 실행 금지.
8. Modern UI Style2 공통 패널 규약 준수
   - 공통 패널 배경은 `ModernUiTileImage + ModernUiRecipes.CommonPanel` 사용.
   - 단일 `Image.sprite` 패널 배경 직접 사용 금지.
9. Objective Journal UI 소유권 규칙 준수
   - 캐릭터 생성/커스터마이징 UI는 목표/퀘스트 UI가 아니므로 `ObjectiveJournalPanel`에 넣지 않는다.
   - MainMenu/SaveSlot 진입 전 플로우의 전용 패널로 설계한다.

## 현재 확인된 근거

- SPUM 로컬 패키지: `C:\Users\jdyj\Downloads\SPUM`, README 기준 v1.8.8
- SPUM 핵심:
  - `Core/Script/Data/SPUM_Prefabs.cs`
  - `Core/Script/SPUM_AnimationManager.cs`
  - `Sample/Script/PlayerObj.cs`
  - `Sample/Prefabs/SamplePlayer.prefab`
- SPUM 애니메이션은 자체 frame loop가 아니라 Unity `AnimatorOverrideController` 기반.
- SPUM 유닛 파츠 PPU는 주로 `spritePixelsToUnits: 32`.
- 기존 Pixelwood Player는 PPU 49.
- 현재 ROOTBORN Player:
  - `Assets/Scripts/Game/Player/PlayerController.cs`
  - `Assets/Scripts/Game/Player/GatherInteractor.cs`
  - `Assets/Scripts/Game/Managers/ResourceManager.cs`
  - `Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs`
- `Tool_StoneAxe`는 character part animation clip 연결 있음.
- `Tool_StonePickaxe`는 현재 YAML상 `_characterPartAnimationClip` 연결이 없음.
- 기존 캐릭터 선택/저장 슬롯 흐름은 `SaveSlotSelectPanel`과 CharacterPart 계열 PlayMode 테스트가 이미 있다.
- 기존 UI/캐릭터 선택 테스트 예:
  - `Assets/Tests/PlayMode/CharacterPartCompositionPlayModeTests.cs`
  - `Assets/Tests/EditMode/Family/CharacterPartAnimationIntegrationSourceTests.cs`

## 브레인스토밍 요청

다음 항목을 중심으로 구현 설계를 구체화하라.

### 1. 통합 경계 설계

다음 중 어떤 경계를 둘지 비교하고 추천하라.

- `ICharacterVisualView`
- `SpumCharacterVisualView`
- 기존 `CharacterPartComposer`/`CharacterPartAnimator` 확장
- Player 전용 adapter와 NPC 전용 adapter 분리

반드시 답해야 할 질문:

- `PlayerController`가 SPUM 타입을 직접 알지 않게 하려면 어떤 인터페이스가 필요한가?
- 기존 Pixelwood layered character와 SPUM visual을 동시에 지원하려면 어떤 추상화가 적절한가?
- root transform/physics를 건드리지 않고 SPUM visual child만 반전/스케일 조정하려면 어디가 책임져야 하는가?

### 2. Addressables 이관 설계

SPUM 원본 출력물을 ROOTBORN 경로로 이관하는 Editor 도구 설계를 제안하라.

고려할 것:

- 원본 SPUM 패키지는 제작 도구로만 취급
- 런타임은 `Managers.Resource.LoadAsync` 또는 직렬화 참조만 사용
- Addressables 그룹명 예: `CharacterVisuals`
- 이관 대상:
  - SPUM saved prefab
  - AnimationClip
  - Sprite/texture part
  - appearance SO
- 원본 `Resources` 경로가 빌드 런타임 의존으로 남지 않게 하는 검증

### 3. 게임 진입 전 SPUM 캐릭터 커스터마이징 UI 설계

MainMenu 또는 SaveSlot New Game 흐름에서 사용할 SPUM 파츠 선택 UI를 설계하라.

필수 기능:

- New Game 시작 전에 캐릭터 외관 선택
- 파츠 카테고리별 선택
  - Body/Skin
  - Eye
  - Hair
  - Outfit/Cloth
  - Accessory/Back/Helmet 등 SPUM 패키지에서 실제 지원하는 카테고리
  - Weapon/Tool preview는 선택 가능 여부와 게임 장착 도구 연동 여부를 분리해서 검토
- 이전/다음 버튼 또는 그리드 선택
- 랜덤 외관 생성
- 초기 기본값 제공
- 선택한 외관을 즉시 preview에 반영
- 저장 슬롯 생성 시 선택값 저장
- 게임 진입 후 Player visual에 동일 선택값 반영

UI 위치/흐름 후보를 비교하라.

- Option UI-A: 기존 `SaveSlotSelectPanel` 안에 Character Creator step 추가
- Option UI-B: `NewGameButton` 후 별도 `SpumCharacterCreatorPanel` 표시 후 Town/Farm 진입
- Option UI-C: MainMenu에서 별도 Character 메뉴 제공, 저장 슬롯 생성 시 연결

반드시 답해야 할 질문:

- 기존 Pixelwood layered character 선택 UI와 SPUM UI를 공용화할지, SPUM 전용 패널로 둘지?
- UI preview는 실제 Player prefab을 쓸지, UI 전용 preview root를 쓸지?
- SPUM 파츠 목록은 Addressables label에서 읽을지, `SpumPartCatalogDefinition` SO에서 읽을지?
- 선택한 파츠 조합의 save key/schema는 어떻게 구성할지?
- 커스터마이징 UI가 로딩 지연 없이 뜨도록 어떤 preload/caching을 둘지?

UI 규칙:

- PC 기준 1920x1080 Canvas Scaler.
- 패널은 Modern UI Style2 공통 패널 사용.
- 버튼은 아이콘/짧은 텍스트 조합 가능.
- 카드 안에 카드 중첩 금지.
- 기능 설명용 긴 안내문 대신 조작 가능한 UI를 우선한다.
- 텍스트가 버튼/패널 밖으로 넘치지 않게 한다.

### 4. 데이터 모델 설계

ScriptableObject 기반으로 다음 데이터를 어떻게 표현할지 설계하라.

- `CharacterAppearanceDefinition`
- `SpumAppearanceDefinition`
- `SpumPartCatalogDefinition`
- `SpumPartDefinition`
- `SpumCharacterCreatorPresetDefinition`
- NPC `NpcDefinition`과 SPUM 외관 연결
- Player save metadata와 SPUM 외관 연결
- `ToolDefinition` 또는 별도 `ToolVisualMappingDefinition`과 SPUM weapon slot/attack clip 연결

금지:

- NPC ID별 코드 분기
- Tool ID별 코드 분기
- SPUM prefab name/code별 코드 분기

추가로 설계할 save data:

- 저장 슬롯 metadata에 들어갈 appearance snapshot
- 파츠 catalog가 업데이트되어도 기존 save가 깨지지 않게 하는 fallback 정책
- 누락된 파츠 id가 있을 때 default part로 대체하는 규칙

### 5. PPU/scale 정책

Pixelwood PPU 49와 SPUM PPU 32 차이를 어떻게 처리할지 옵션을 비교하라.

옵션:

- SPUM import PPU를 49로 재조정
- SPUM visual child scale을 `32/49 ~= 0.653`로 보정
- 캐릭터별 visual bounds 기준으로 scale을 SO에 저장

각 옵션의 리스크:

- Animator pivot 깨짐
- collider/root 크기와 visual 불일치
- NPC 다수 배치 시 시각 일관성
- UI preview 크기와 월드 Player 크기 불일치

### 6. 공격/도구 애니메이션 매핑

기존 ROOTBORN 공격 흐름을 유지하면서 SPUM 공격 애니메이션을 붙이는 설계를 제안하라.

현재 계약:

- `PlayerController.BeginAttack`
- `_attackFrameDuration`
- 마지막 프레임 근처에서 `GatherInteractor.TriggerInteract()`
- `ToolDefinition.CharacterPartAnimationClip`
- held tool Addressables sub-sprite

설계해야 할 것:

- SPUM `PlayerState.ATTACK` clip index 매핑
- StoneAxe/StonePickaxe weapon visual slot 매핑
- 공격 타이밍은 ROOTBORN이 소유하고 SPUM은 visual만 담당하는 구조
- 도구별 분기 없이 SO 데이터로 처리하는 방식

### 7. 테스트 전략

TDD 계획을 작성하라. 실패 테스트 먼저 제안하라.

필수 테스트 후보:

- EditMode: ROOTBORN 런타임 신규 `Resources.Load*` 사용 금지
- EditMode: SPUM Addressables 이관 결과 검증
- EditMode: SPUM appearance SO와 tool visual mapping 검증
- EditMode: adapter가 motion/facing/action을 SPUM state로 변환하는지 검증
- PlayMode: 실제 키보드 입력으로 Player 이동 + SPUM visual MOVE/flip 확인
- PlayMode: 실제 좌클릭 공격 + `GatherInteractor` 상호작용 1회 확인
- PlayMode: StoneAxe/StonePickaxe 장착 변경이 SPUM 무기 외관에 반영
- PlayMode: `NpcDefinition` 기반 SPUM NPC 생성 + 대화/상호작용 유지
- EditMode: SPUM part catalog가 entity ID별 코드 분기 없이 SO 데이터로 구성되는지 검증
- EditMode: character creator save snapshot이 누락 파츠 fallback을 제공하는지 검증
- EditMode: Modern UI Style2 공통 패널 규약을 character creator UI가 지키는지 검증
- PlayMode: MainMenu/New Game에서 실제 UI 클릭으로 파츠 변경 후 preview가 바뀌는지 검증
- PlayMode: 실제 UI 클릭으로 랜덤 외관 생성 후 저장 슬롯 생성, Town/Farm 진입 후 Player 외관이 유지되는지 검증
- PlayMode: 저장/로드 후 SPUM appearance가 유지되는지 검증

## 산출물

코드 수정 없이 다음 문서 초안을 작성하라.

1. `docs/architecture/spum-character-integration-technical-design.md`
   - 컴포넌트 경계
   - 데이터 모델
   - Addressables 이관 흐름
   - Player/NPC runtime flow
   - 공격/도구 mapping
   - PPU/scale 정책
   - MainMenu/SaveSlot character creator UI 흐름
   - SPUM part catalog/save schema

2. `docs/superpowers/plans/YYYY-MM-DD-spum-character-integration-plan.md`
   - TDD 순서
   - 파일 단위 구현 순서
   - 각 단계 검증 명령
   - Unity Editor 실행이 필요한 단계와 필요한 승인 명시

3. `docs/ui/spum-character-creator-ui-spec.md`
   - 화면 구조
   - 입력/클릭 흐름
   - preview 갱신 흐름
   - save/load 흐름
   - Style2 패널 적용 지점
   - 필요한 PlayMode 테스트 시나리오

## 금지

- 코드 수정 금지
- Unity Editor 실행 금지
- SPUM 파일을 프로젝트로 복사 금지
- `Assets/**/*.cs` 직접 쓰기 금지
- SPUM 원본 UI/Manager를 ROOTBORN 런타임 UI로 그대로 편입 금지
- character creator UI에서 파츠 ID별 C# 분기 금지
- 목표/퀘스트/캠페인 UI처럼 ObjectiveJournalPanel 소유 영역에 커스터마이징 UI를 넣는 것 금지
- 테스트를 우회하는 내부 메서드 직접 호출만으로 완료 판정 금지

## 완료 보고

완료 보고에는 다음을 포함하라.

- 추천 설계 요약
- 캐릭터 커스터마이징 UI 흐름 요약
- 선택하지 않은 대안과 이유
- 남은 불확실성
- 구현 전 반드시 사용자 승인이 필요한 작업
- 성능 리스크와 검증 범위
