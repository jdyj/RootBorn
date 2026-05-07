# Character Part Composition And Save Goal

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트에서 플레이어 캐릭터를 완성된 단일 sprite가 아니라, 다른 16x16 sprite와 동일한 방식의 파츠 sprite 조합으로 구성하도록 만들어줘.

핵심 요구:
- 현재 캐릭터는 하나의 완성된 sprite로 취급되면 안 된다.
- 게임 시작 시 플레이어가 몸, 눈, 머리/헤어, 의상, 모자/악세사리 등 캐릭터 파츠를 각각 선택할 수 있어야 한다.
- 선택 결과는 저장 슬롯에 저장되고, 이후 로드하면 동일한 파츠 조합으로 캐릭터가 복원되어야 한다.
- 파츠 sprite는 16x16 기준으로 slice된 sprite 조각을 사용한다.
- 파츠 에셋은 이미 프로젝트에 존재하므로 먼저 character 관련 폴더를 조사하고, 실제 폴더/파일/슬라이스 구조에 맞춰 구현한다.

절대 규칙:
- AGENTS.md 헌법을 지킨다.
- `Assets/**/*.cs` 직접 디스크 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 실패 테스트 먼저 작성하고 구현한다.
- 게임 엔티티/선택 가능한 파츠 데이터는 데이터 드리븐으로 관리한다. 파츠 ID별 `if`, `switch`, enum 분기, 파츠별 C# 클래스 생성을 금지한다.
- 런타임 신규 `Resources.Load` 의존을 늘리지 않는다. 기존 Addressables/registry/cache 흐름을 따른다.
- 기존 사용자 변경분을 되돌리지 않는다.
- 캐릭터 파츠 sprite sheet는 파일명만 믿고 16x16이라고 가정하지 말고, 실제 텍스처 크기와 slice rect를 검증한다.

우선 조사할 에셋 위치:
- `Assets/moderninteriors-win/2_Characters/Character_Generator/`
  - `Accessories`
  - `Bodies`
  - `Eyes`
  - `Hairstyles`
  - `Outfits`
  - kids 계열은 1차 범위에서 조사하되 적용 여부를 명시한다.
- `Assets/Modern_Farm_v1.2/Farmer_Generator_Pieces/Character Pieces/`
  - `Accessories/16x16`
  - `Bodies/16x16`
  - `Eyes/16x16`
  - `Hairstyles/16x16`
  - `Outfits/16x16`
- 기존 캐릭터/플레이어 관련 코드와 프리팹:
  - `Assets/Prefabs/Player.prefab`
  - `Assets/Scripts/Game/Managers/DataManager.cs`
  - `Assets/Scripts/Game/Save/SaveService.cs`
  - `Assets/Scripts/Game/Generation/*`
  - `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
  - 현재 Player sprite/animator 와이어링 코드

목표 아키텍처:
1. 캐릭터 파츠 정의 데이터
   - `CharacterPartDefinition` 또는 동등한 ScriptableObject를 만든다.
   - SO 인스턴스 위치는 `Assets/Data/Family/CharacterParts/` 또는 기존 데이터 구조에 맞춘 위치로 둔다.
   - 각 파츠는 category, display localization key, sprite sheet address, sub-sprite name, layer order, compatible tags, unlock rule를 가진다.
   - 파츠 category는 최소 body, eyes, hair, outfit, accessory를 지원한다.
   - category 자체도 하드코딩 난립이 아니라 데이터/직렬화 가능한 값으로 관리한다. 단, 테스트 가능한 최소 범위를 우선한다.

2. 캐릭터 외형 값 객체
   - `CharacterAppearance` 또는 동등한 저장 가능한 값 객체를 만든다.
   - 저장 내용은 sprite 참조 자체가 아니라 안정적인 파츠 definition id/reference로 저장한다.
   - 빈 슬롯 또는 누락된 파츠에 대비해 default appearance를 제공한다.
   - 잘못된 저장 데이터가 들어오면 기존 저장을 파괴하지 않고 default/fallback을 적용하고 경고를 남긴다.

3. 런타임 파츠 조합 표시
   - Player prefab 또는 child hierarchy에 파츠별 `SpriteRenderer` 레이어를 둔다.
   - 같은 방향/애니메이션 프레임에서 body, outfit, hair, eyes, accessory가 같은 frame index로 갱신되어야 한다.
   - 단일 완성 캐릭터 sprite로 되돌리는 임시 구현을 금지한다.
   - Sorting layer/order는 파츠 간 가림 순서를 결정적으로 유지한다.
   - side-view 원본 방향과 flipX 부호를 실제 sprite 기준으로 검증한다.

4. 게임 시작 캐릭터 설정 UI
   - 새 저장 슬롯을 시작할 때 캐릭터 파츠 선택 화면이 나온다.
   - 각 category별 이전/다음 또는 grid 선택이 가능해야 한다.
   - 선택 즉시 미리보기 캐릭터에 반영된다.
   - 확정하면 저장 슬롯 초기 데이터에 appearance가 기록되고 Farm 진입 시 같은 외형으로 생성된다.
   - 기존 저장 슬롯/로딩 흐름을 깨지 않는다. 기존 세이브에는 default appearance를 적용한다.

5. 저장/로드
   - `SaveService` 또는 현재 저장 구조에 appearance 값을 포함한다.
   - 저장 후 로드, 앱 재시작 후 로드, 기존 save migration 경로를 테스트한다.
   - 저장 데이터에 중복/누락/삭제된 파츠가 있어도 인벤토리나 퀘스트 상태를 훼손하지 않는다.

권장 접근 방식:

접근 A. 권장: 파츠 카탈로그 SO + 저장 값 객체 + 레이어드 SpriteRenderer 조합
- 장점: ROOTBORN의 데이터 드리븐 원칙과 맞고, 이후 세대/가족/특성 시스템과 연결하기 쉽다.
- 단점: 초기 SO와 registry 와이어링, 테스트가 필요하다.

접근 B. Addressables sub-sprite 이름 배열만 코드에 두고 빠르게 조합
- 장점: 빠르게 화면에 띄울 수 있다.
- 단점: 엔티티 데이터 드리븐 원칙을 위반하기 쉽고, 파츠 추가/잠금/세대 연계가 어려워진다.

접근 C. 시작 화면에서 완성 캐릭터 프리셋만 선택
- 장점: 범위가 작다.
- 단점: 사용자가 요구한 몸/모자/악세사리 등 파츠별 선택과 맞지 않는다.

이번 goal은 접근 A를 따른다.

1차 시나리오 카탈로그:

CHAR-PART-001:
- Character Generator/Farmer Generator의 16x16 파츠 에셋을 조사한다.
- 사용 가능한 category, sheet path, sprite naming, frame layout, 정수 slice 여부를 문서화한다.
- 조사 결과를 `docs/art/character-part-inventory.md` 또는 동등 문서에 남긴다.

CHAR-PART-002:
- 각 category의 최소 2개 이상 파츠가 SO definition으로 등록된다.
- 모든 definition은 registry 또는 기존 데이터 로딩 경로에서 찾을 수 있다.
- definition이 참조하는 sprite sheet/sub-sprite가 실제로 존재한다.

CHAR-PART-003:
- `CharacterAppearance`는 category별 선택 파츠를 저장 가능한 형태로 가진다.
- 저장 데이터에는 UnityEngine.Object 직접 참조가 아니라 안정 식별자가 기록된다.
- 누락 category는 default part로 보정된다.

CHAR-PART-004:
- 새 게임 시작 시 캐릭터 설정 UI가 열린다.
- body, eyes, hair, outfit, accessory category를 각각 변경할 수 있다.
- 미리보기는 변경 즉시 파츠 레이어 조합으로 갱신된다.
- 확정 전에는 저장 슬롯이 시작 완료 상태로 기록되지 않는다.

CHAR-PART-005:
- 확정 후 Farm에 진입하면 Player prefab이 저장된 appearance로 표시된다.
- Player sprite는 단일 완성 sprite가 아니라 파츠별 SpriteRenderer 조합이다.
- 이동/idle 방향 전환과 flipX가 모든 파츠에 동일하게 적용된다.

CHAR-SAVE-001:
- 캐릭터 appearance 저장 후 같은 슬롯을 로드하면 동일한 파츠 조합이 복원된다.
- 기존 save에는 default appearance가 적용된다.
- 삭제되거나 누락된 파츠 id가 저장되어 있어도 로드는 실패하지 않고 fallback을 적용한다.

필수 구현 방향:
- 신규 SO 클래스/스크립트는 `Assets/Scripts/Game/Family/` 또는 프로젝트의 도메인 경계에 맞는 위치에 둔다.
- SO 인스턴스는 `Assets/Data/Family/CharacterParts/`에 둔다.
- registry 등록은 `Assets/Data/Registry/GameDataRegistry.asset` 또는 기존 데이터 registry 흐름을 따른다.
- Player prefab에는 파츠별 child renderer를 명확한 이름으로 둔다.
- UI 텍스트는 localization key 기반으로 준비한다.
- UI는 이후 Modern UI 16x16 panel reconstruction goal에서 교체 가능하도록 표시 로직과 외형 선택 로직을 분리한다.

필수 테스트:
1. EditMode
   - 파츠 definition registry 검증
   - definition sprite 참조 유효성 검증
   - appearance default/fallback/missing category 보정 검증
   - save payload serialize/deserialize round-trip 검증
   - entity id 분기 금지 정적 게이트 유지
2. PlayMode
   - 새 슬롯 생성 -> 캐릭터 설정 -> 파츠 변경 -> 확정 -> Farm 진입
   - 저장 후 로드 시 같은 파츠 조합 복원
   - 기존 저장 데이터 migration/fallback
3. 시각 검증
   - 파츠별 renderer가 모두 활성화되어 있고 단일 완성 sprite만 표시하지 않는지 확인
   - screenshot 또는 isolated render로 body/outfit/hair/accessory 레이어가 함께 보이는지 확인

완료 조건:
- 게임 시작 시 파츠별 캐릭터 설정이 가능하다.
- 선택한 파츠 조합이 저장 슬롯에 저장되고 로드 후 복원된다.
- Player는 body/eyes/hair/outfit/accessory 등 레이어드 sprite 조합으로 렌더링된다.
- 파츠 데이터는 SO/registry 기반이며 파츠 ID별 코드 분기가 없다.
- 기존 저장 슬롯과 Farm 진입 흐름이 깨지지 않는다.
- EditMode/PlayMode 테스트와 정적 게이트가 통과한다.
- 마지막 보고에는 아래를 포함한다:
  - 조사한 character/farmer generator 폴더 목록
  - 사용한 파츠 category와 definition 목록
  - 저장 포맷 변경점과 migration/fallback 정책
  - 수정한 prefab/UI/save/runtime 파일
  - 실행한 테스트와 결과
  - 남은 리스크와 후속 UI polish 범위
```

## Scope Note

이번 goal은 캐릭터 커스터마이징의 데이터/저장/렌더링 기반을 만드는 작업이다. 캐릭터 설정 화면의 최종 픽셀 UI 디자인은 `modern-ui-panel-reconstruction-goal`에서 16x16 panel recipe로 재작업할 수 있도록 분리한다.
