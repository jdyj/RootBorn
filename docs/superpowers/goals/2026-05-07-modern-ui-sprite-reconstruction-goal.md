# Modern UI 16x16 Sprite Reconstruction Goal

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트의 현재 UI를 `Assets/modernuserinterface-win/16x16` 안의 16x16 sliced sprite 조합 방식으로 전면 복구해줘.

사용자가 제공한 기준 이미지:
- 설정창: https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk4NTA0OC5naWY=/original/ZFWQBO.gif
- 인벤토리창: https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk2MjY0OC5naWY=/original/dztsAE.gif
- 상태창: https://img.itch.zone/aW1hZ2UvMTg2NTgzOS8xMDk2MjY0Ni5naWY=/original/Z9nBBy.gif

핵심 문제:
- 현재 생성된 UI 중 일부가 16x16 slice sprite 하나를 거대하게 확대해서 사용하고 있다.
- 목표는 단일 sprite 확대가 아니라, 기준 이미지처럼 16x16 조각들을 찾아 이어붙여 전체 UI를 구성하는 것이다.
- 예: 상태창 gif의 background panel은 `Assets/modernuserinterface-win/16x16`의 3x3 조각
  `r0_c0 r0_c1 r0_c2 / r1_c0 r1_c1 r1_c2 / r2_c0 r2_c1 r2_c2`
  같은 9개 sprite 조합으로 만들어지는 부분이 있다. 다른 UI도 같은 방식으로 실제 조각을 식별해 재조립해야 한다.

절대 규칙:
- AGENTS.md 헌법을 지킨다.
- `Assets/**/*.cs` 직접 디스크 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 실패 테스트 먼저 작성하고 구현한다.
- UI를 임의 색상 Image, 거대한 단일 sprite scale, 임시 placeholder, SVG, 런타임 생성 Texture2D로 때우지 않는다.
- 16x16 source sprite sheet에서 slice된 sub-sprite를 찾아 조합한다.
- sprite sheet의 원본 픽셀 느낌이 보존되도록 `FilterMode.Point`, `CanvasScaler 1920x1080`, 정수 배율 또는 pixel-perfect에 가까운 배치를 유지한다.
- 기존 사용자 변경분은 되돌리지 않는다.
- 수동 눈검사만으로 완료 처리하지 않는다. 자동화된 PlayMode/EditMode 검증과 screenshot/pixel audit를 남긴다.

프로젝트 현재 컨텍스트:
- Modern UI sheet:
  - `Assets/modernuserinterface-win/16x16/Modern_UI_Style_1.png`
  - `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png`
  - `Assets/modernuserinterface-win/16x16/Modern_UI_Gamepad.png`
- 기존 slicing 규약:
  - `Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs`
  - sub-sprite 이름은 `ModernUI_16_Style1_r{row}_c{column}` 형식이다.
  - 예: `ModernUI_16_Style1_r0_c0`, `ModernUI_16_Style1_r0_c1`, ...
- Addressable 주소:
  - `Assets/Scripts/Game/Common/ModernUISpriteAddresses.cs`
  - `sprites/ui/modern/16/style-1`
  - `sprites/ui/modern/16/style-2`
  - `sprites/ui/modern/16/gamepad`
- 주요 기존 UI:
  - `Assets/Scripts/UI/HUD/StatusHud.cs` 내부 ResourceHud / InventoryView 등
  - `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
  - `Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs`
  - `Assets/Scripts/UI/Quests/DialoguePanel.cs`

목표:
1. 사용자가 기준 사진 또는 gif를 제공하면, 해당 이미지에 보이는 UI를 16x16 sub-sprite 조합으로 재현하는 작업 흐름을 만든다.
2. 설정창, 인벤토리창, 상태창을 우선 대상으로 잡고, 각 UI가 어떤 sheet/sub-sprite 좌표 조합으로 구성되는지 manifest로 문서화한다.
3. 단일 sprite를 크게 늘리는 방식 대신, 9-slice/타일 반복/모서리-엣지-센터 조합 방식으로 panel, button, slot, icon frame, scrollbar, tab, title bar를 만든다.
4. 현재 oversized 16x16 UI 사용처를 audit하고, 전면적으로 Modern UI 조립 컴포넌트로 교체한다.
5. 결과 UI가 기준 이미지와 구조적으로 유사해야 한다:
   - panel border와 corner가 맞는다.
   - slot grid가 16x16 조각 기준으로 균일하다.
   - 버튼/탭/닫기 버튼/스크롤/선택 표시가 실제 sprite 조합으로 구성된다.
   - 텍스트는 sprite panel 내부에서 잘리지 않는다.
6. UI 조립 데이터는 하드코딩 난립이 아니라 재사용 가능한 데이터 구조로 관리한다.
   - 예: `ModernUiTileSet`, `ModernUiPanelRecipe`, `ModernUiWindowRecipe`, `ModernUiSpriteKey`
   - sheet address + sub-sprite name + role(corner/edge/fill/button/slot/icon) 조합.
7. 테스트와 screenshot 검증으로 완료를 증명한다.

권장 접근 방식:

접근 A. 권장: 기준 이미지 기반 sprite recipe manifest를 먼저 만들고 UI 컴포넌트를 교체한다.
- 장점: 어떤 sub-sprite가 어떤 UI 조각인지 추적 가능하고, 이후 다른 사진을 줬을 때 같은 방식으로 확장할 수 있다.
- 단점: 초기에 sprite 좌표 조사와 manifest 작성 시간이 든다.

접근 B. 현재 UI 코드에서 보이는 부분만 즉시 치환한다.
- 장점: 빠르게 겉모습을 바꿀 수 있다.
- 단점: 또 다른 하드코딩/확대 문제가 생기기 쉽고, 사진 기반 재현 목표와 맞지 않는다.

접근 C. Unity built-in 9-slice Image border만 사용한다.
- 장점: 구현이 단순하다.
- 단점: 사용자가 요구한 `r0_c0` 등 개별 16x16 조각 조합 검증이 약해지고, 원본 sheet의 조각 단위 재현성이 떨어진다.

이번 goal은 접근 A를 따른다.

1차 시나리오 카탈로그:

UI-SPRITE-001:
- `Assets/modernuserinterface-win/16x16`의 Modern UI sheets가 16x16 Multiple sprite로 slice되어 있다.
- `ModernUiSliceSetup.BuildSpriteName` 규약과 실제 sub-sprite 이름이 일치한다.
- Style1/Style2/Gamepad 16x16 sheet의 첫/마지막 및 recipe에서 참조하는 모든 sub-sprite가 존재한다.

UI-RECIPE-001:
- 설정창, 인벤토리창, 상태창 각각에 대한 sprite recipe manifest가 존재한다.
- manifest는 sheet address, sub-sprite name, row/column, role, intended usage를 포함한다.
- 동일한 panel/button/slot 조각은 중복 하드코딩하지 않고 공용 recipe로 재사용된다.

UI-PANEL-001:
- background panel은 최소 3x3 조각 조합으로 만들어진다.
- corner는 늘리지 않고 원본 16x16 sprite 크기/비율을 유지한다.
- edge와 center는 필요한 칸 수만큼 tile 또는 반복 조합한다.
- 단일 16x16 sprite를 panel 전체 크기로 확대하는 사용처가 없어야 한다.

UI-INVENTORY-001:
- 인벤토리창은 기준 gif 구조를 따라 title/header, slot grid, item icon frame, 선택 표시, 하단/측면 controls를 sprite 조합으로 구성한다.
- 슬롯은 16x16 slot 조각 또는 slot frame recipe를 반복해 만든다.
- 기존 InventoryView가 거대한 Image panel/slot scale에 의존하지 않는다.

UI-STATUS-001:
- 상태창/HUD는 기준 gif 구조를 따라 background panel, status row, gauge/label/icon frame을 sprite 조합으로 구성한다.
- ResourceHud/StatusHud가 임의 색상 Image만으로 panel을 만들지 않는다.
- HUD 텍스트와 아이콘이 1920x1080 기준에서 잘리지 않는다.

UI-SETTINGS-001:
- 설정창은 기준 gif 구조를 따라 title bar, close button, tab/button row, option rows, sliders/toggles/buttons를 sprite 조합으로 구성한다.
- 설정창이 아직 없다면 최소 동작 가능한 SettingsPanel을 만들되, 실제 게임에서 열고 닫을 수 있어야 한다.

UI-VISUAL-001:
- PlayMode 또는 Editor test에서 주요 UI를 열고 screenshot을 저장/검증한다.
- screenshot 검증은 최소한 다음을 확인한다:
  - UI가 비어 있지 않다.
  - panel border/corner/center sprite가 존재한다.
  - 16x16 sprite가 비정상적으로 단일 확대된 흔적이 없다.
  - 주요 텍스트가 panel 밖으로 넘치지 않는다.

UI-AUDIT-001:
- `Assets/Scripts/UI`와 관련 runtime builder에서 oversized 16x16 sprite 사용처를 찾아 목록화한다.
- 치환 대상과 보류 대상을 명확히 분리한다.
- 보류 대상이 있으면 이유와 다음 작업을 문서화한다.

필수 구현 방향:
- `ModernUiSpriteResolver`
  - Addressables 또는 기존 preload/cache 방식을 통해 `sheetAddress + subSpriteName`으로 Sprite를 가져온다.
  - Runtime에서 `Resources.Load` 신규 사용을 늘리지 않는다.
- `ModernUiTileImage` 또는 동등 컴포넌트
  - RectTransform 안에 16x16 sprite Image children을 타일 방식으로 배치한다.
  - 3x3 panel recipe, horizontal/vertical repeat, fixed icon frame을 지원한다.
  - Layout rebuild 후에도 child tile 수와 위치가 결정적이어야 한다.
- `ModernUiRecipe` 계층
  - 공용 panel, button, slot, window, gauge recipe를 한 곳에서 관리한다.
  - 가능하면 ScriptableObject 또는 직렬화 가능한 asset으로 관리한다.
  - 최소한 테스트 가능한 manifest/registry가 있어야 한다.
- 기존 UI 교체
  - `StatusHud`, `ResourceHud`, `InventoryView`, `SaveSlotSelectPanel`, `QuestHudAutoFiller`, `DialoguePanel` 등 현재 이미지/패널 생성부를 audit한다.
  - 이번 goal의 1차 범위는 상태창/인벤토리창/설정창이다. SaveSlot/Quest/Dialogue는 같은 기반 컴포넌트로 확장 가능한 상태까지 정리하되, 시간 부족 시 별도 후속 범위로 문서화한다.

사진/기준 이미지 처리 방식:
1. 사용자가 사진/gif를 주면 먼저 이미지를 열어 전체 해상도와 UI 구성 요소를 기록한다.
2. `Assets/modernuserinterface-win/16x16` sheet를 열어 후보 sprite row/column을 찾는다.
3. 각 UI 영역을 다음 단위로 분해한다:
   - window background
   - title bar
   - border/corner
   - content fill
   - slot/grid
   - button states
   - close/settings/tab icons
   - gauge/slider/toggle
4. 후보 sub-sprite를 manifest에 기록한다.
5. Unity UI에서 조합해 screenshot을 찍는다.
6. 기준 이미지와 screenshot을 비교해 틀린 조각/간격/배율을 수정한다.
7. 반복 후 자동화 테스트를 통과시킨다.

필수 테스트:
1. EditMode
   - Modern UI sheet slicing 검증
   - recipe manifest가 참조하는 모든 sub-sprite 존재 검증
   - panel recipe가 corner/edge/fill 역할을 모두 갖는지 검증
   - oversized single-sprite panel 금지 source audit
2. PlayMode
   - Farm 또는 UI test scene에서 InventoryPanel 열기/닫기
   - StatusHud 표시
   - SettingsPanel 열기/닫기
   - screenshot capture 후 non-empty/panel-tile-count/text-bounds 검증
3. 기존 회귀
   - `Rootborn.Tests.EditMode`
   - `Rootborn.Tests.PlayMode`
4. 정적 게이트
   - 신규 runtime `Resources.Load`, `GameObject.Find`, entity ID 분기 추가 금지 게이트가 깨지지 않아야 한다.

완료 조건:
- 설정창, 인벤토리창, 상태창이 16x16 sprite 조합 방식으로 재구성되어 있다.
- 기준 gif의 주요 panel/button/slot/gauge 구조가 sprite recipe manifest에 매핑되어 있다.
- 단일 16x16 sprite를 거대한 UI panel로 확대하는 1차 범위 사용처가 제거되어 있다.
- screenshot 기반 검증이 존재하고 통과한다.
- 사용자가 새 사진을 추가로 주면 같은 workflow로 sprite 조합 goal을 이어갈 수 있도록 prompt/spec/manifest 구조가 남아 있다.
- 전체 EditMode/PlayMode 회귀가 통과한다.
- 마지막 보고에는 아래를 포함한다:
  - 분석한 기준 이미지 URL 또는 사용자 제공 사진 목록
  - 사용한 sheet와 sub-sprite 좌표 목록
  - 생성/수정한 recipe/manifest/assets/scripts
  - 치환한 UI 목록과 보류한 UI 목록
  - screenshot 검증 결과
  - 실행한 테스트와 결과
  - 남은 리스크와 다음 우선순위
```

## Scope Note

이번 goal은 UI 색상/레이아웃을 대충 예쁘게 바꾸는 작업이 아니라, 기준 이미지의 UI를 16x16 sliced sprite 조각 단위로 역분해하고 Unity UI에서 재조립하는 복구 작업이다. 핵심 산출물은 재사용 가능한 sprite recipe/manifest와 screenshot 검증이다.
