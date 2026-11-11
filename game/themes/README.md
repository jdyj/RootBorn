# UI 테마 폴더

게임의 UI 그림(패널, 버튼, 칸, 글자 칸, 대화창, 아이콘)은 테마 폴더 하나에서 온다. 화면 코드는 "패널", "버튼", "icon:close" 같은 **이름만** 말하고, 그 이름이 어느 PNG의 어디인지는 테마 폴더의 `theme.json`이 정한다. 새 UI 그림을 넣는 일은 폴더를 하나 만들고 PNG와 `theme.json`을 채우는 일이다. 코드는 고치지 않는다.

지금 있는 테마:

| 폴더 | 내용 |
|---|---|
| `urban/` | 도시형 몰딩(기본). Unity 판에서 쓰던 그림을 가리킨다 |
| `style2/` | Modern UI Style2 대체 스킨 |

## 새 UI 그림을 넣는 법

1. 폴더를 복사한다. 예: `game/themes/urban`을 `game/themes/<새 이름>`으로.
2. 새 PNG를 그 폴더에 넣는다. 크기는 자유다. 여러 그림을 한 장에 모아도 되고 따로 둬도 된다.
3. `theme.json`을 고친다. 그림마다 파일 이름, 그 안의 칸(`rect`), 9조각 테두리 두께(`border`)를 적는다(아래 "형식").
4. 띄워 본다: `dotnet run --project game/Rootborn.Host -- --ui-theme <새 이름> --save-root <세이브 폴더> --slot slot-0 --size 1920x1080`
   - 이름 대신 폴더 경로를 줘도 된다: `--ui-theme D:/art/my-theme`
   - 환경 변수 `ROOTBORN_UI_THEME`로도 고른다(실행 인자가 먼저). 아무것도 주지 않으면 `urban`.
   - 빠진 그림이나 잘못된 칸이 있으면 시작할 때 무엇이 빠졌는지 목록을 보이고 멈춘다.
5. 검사 테스트를 돌린다: `dotnet test game/tests/Rootborn.Host.Tests --filter "FullyQualifiedName~TestThemeScreensTests|FullyQualifiedName~UiSkinDataTests"`
   - 모든 화면을 시험용 테마로 그려 예외, 빠진 아이콘, 패널 밖으로 나간 글자를 본다. 보고서와 화면 그림은 `output/host-theme-evidence/`.
   - 새 테마 자체로 모든 화면을 찍어 보려면 4번처럼 띄우고 F12로 화면을 저장한다.

## 필요한 그림

### 스킨 5개 (모두 있어야 한다)

| 이름 | 쓰이는 곳 |
|---|---|
| `panel` | 창과 큰 상자의 바탕(가방, 상태, 목표 수첩, HUD 상자 등) |
| `button` | 누르는 단추 |
| `slot` | 아이템 칸, 작은 상자 |
| `field` | 글자를 치는 칸(이름, 검색) |
| `dialogue` | 대화창 |

같은 그림을 여러 스킨에 써도 된다(도시형 몰딩은 `dialogue`가 `panel`과, `field`가 `slot`과 같다).

### 아이콘 18개 (모두 있어야 한다)

| 키 | 쓰이는 곳 |
|---|---|
| `time` | 시계 HUD |
| `energy`, `focus` | 체력·집중 HUD, 상태 화면 |
| `stress` | 상태 화면 |
| `journal` | 추적 목표 HUD, 목표 수첩 "목표" 분류 |
| `plan` | 목표 수첩 "퀘스트" 분류, 일과 카드의 기본 아이콘 |
| `town_map` | 목표 수첩 "캠페인" 분류 |
| `discovery` | 목표 수첩 "힌트" 분류 |
| `daily-planner` | 일과 계획판 |
| `inventory-all`, `inventory-tools`, `inventory-consumables`, `inventory-misc` | 가방 탭(전체, 도구, 자원·씨앗, 기타) |
| `inventory-search`, `inventory-delete`, `inventory-filter` | 가방 검색칸과 옆 단추 |
| `inventory-empty` | 빈 가방 그림 |
| `close` | 닫기 단추 |

그 밖의 아이콘을 더 넣어도 된다. 정의별 아이콘(아래 `definitionIcons`)이 가리키는 아이콘은 테마 안에 있어야 한다(도시형 몰딩의 `chef`, `care`, `school` 등).

아이템 그림(가방 칸의 물건)은 테마가 아니라 아이템 데이터(`data/items`)가 정한다.

## 형식

```json
{
  "format": 1,
  "name": "my-theme",
  "about": "이 테마 설명(자유)",
  "skins": {
    "panel":  { "image": "panel.png", "border": [ 12, 12, 12, 12 ], "fill": "stretch", "pixelSize": 2 },
    "button": { "image": "ui.png", "rect": [ 0, 0, 48, 16 ], "border": [ 4, 4, 4, 4 ], "pixelSize": 2, "textColor": "#F4EDDA",
                "disabledTint": "#96929C", "disabledTextColor": "#E2DCCE" }
  },
  "icons": {
    "close": { "image": "icons.png", "rect": [ 32, 0, 16, 16 ] },
    "time":  { "image": "time.png" }
  },
  "definitionIcons": { "daily.practice.chef": "chef" },
  "textColors": { "ink": "#282434", "inkMuted": "#848096", "inkOnDark": "#EBE4F2", "inkDisabled": "#545062" }
}
```

- **`image`**: PNG 파일. 테마 폴더 기준 경로다. `repo:`로 시작하면 저장소 기준(`repo:Assets/...`, 지금 두 테마가 이렇게 Unity 판 그림을 가리킨다).
- **`rect`**: 그림 안에서 쓸 칸 `[x, y, 너비, 높이]`, 픽셀 단위, 왼쪽 위가 (0, 0). 적지 않으면 그림 전체.
- **`border`**: 9조각 테두리 두께 `[왼쪽, 위, 오른쪽, 아래]`, 픽셀. 모서리는 그대로 두고 변과 가운데만 늘이거나 반복한다. 테두리를 빼고도 가운데가 남아야 한다.
- **`fill`**: `stretch`(변과 가운데를 늘인다, 기본) 또는 `tile`(가운데 칸을 반복한다).
- **`pixelSize`**: 원본 1픽셀이 화면에서 몇 칸인가(1920x1080 기준 논리 크기). 적지 않으면 화면 크기에 맞춘 정수 배율(1080에서 3). 픽셀아트를 또렷하게 두려면 정수로.
- **`textColor`**: 이 그림 위에 놓이는 글자 색(예: 어두운 버튼 위 밝은 글자). `"#RRGGBB"` 또는 `"#RRGGBBAA"`. 흐린 글자는 반투명으로 그린다.
- **`disabledTextColor`**: 누를 수 없는 단추·줄에서 이 그림 위 글자 색. 적지 않으면 `textColor`를 조금 흐리게, `textColor`도 없으면 `textColors.inkDisabled`.
- **`disabledTint`**: 누를 수 없을 때 이 그림을 물들이는 색. 적지 않으면 게임 기본값(반투명 회색). 비활성 글자가 바탕 위에서 읽히는지 함께 본다.
- **`padding`**: 글자를 놓을 안쪽 여백 `[왼쪽, 위, 오른쪽, 아래]`(논리 크기). 적지 않으면 테두리 두께 × `pixelSize`.
- **`icons`**: 아이콘 키 → 그림(`image`, 있으면 `rect`).
- **`definitionIcons`**: 게임 데이터의 정의 id → 아이콘 키. 그 정의(예: 일과 카드 하나)에만 다른 아이콘을 보이고 싶을 때. 없으면 화면이 정한 기본 아이콘.
- **`textColors`**: 기본 글자 색 묶음. `ink`(보통 글자), `inkMuted`(흐린 글자), `inkOnDark`(어두운 바탕 위 글자), `inkDisabled`(누를 수 없는 단추·줄의 글자). 적지 않은 색은 게임 기본값.

### 알아 둘 것

- 화면 배치(창 크기, 단추 위치)는 테마가 바꾸지 않는다. 테두리를 아주 두껍게 그리면 글자가 테두리 위에 올라앉을 수 있다. 검사 테스트 보고서의 "테두리에 닿은 글자" 목록을 본다.
- 도시형 몰딩 `urban/theme.json`은 예전 데이터(`data/ui/UrbanUiThemeDefinition.json`, `data/atlases`)에서 만든 것이다. 그 데이터가 바뀌면 `dotnet test game/tests/Rootborn.Host.Tests --filter FullyQualifiedName~ThemeConversionWriter`로 다시 만든다. 새 테마는 손으로 쓰면 된다.
- 화면이 새 아이콘 키를 쓰게 되면 `game/Rootborn.Host/Rendering/ThemeRequirements.cs`의 목록과 이 문서의 표에 더하고, 모든 테마에 그 아이콘을 넣는다.
