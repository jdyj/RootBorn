---
name: ui-nine-slice-skin
description: "ROOTBORN UI 시트(LimeZu Modern UI 등)의 테두리 그림 하나를 9조각으로 재서 게임 테마 스킨(panel, button, slot, field, dialogue)으로 쓴다. 모서리는 한 번, 변과 가운데는 반복해 아무 크기 창·대화창·채팅창으로 늘린다. 'UI 패널 그림 바꾸기', '9-slice', '9조각', '대화창 테두리', '테마 스킨', theme.json의 rect·border를 정할 때 사용."
---

# UI 그림 하나를 9조각 스킨으로

게임은 테마 스킨을 이렇게 그린다(`game/Rootborn.Ui/NineSlice.cs` `Build`, theme.json `"fill": "tile"`):

```
왼쪽 위 | 위 위 위 위 ... | 오른쪽 위        모서리 4개: 한 번만
왼쪽    | 가운데 반복 ... | 오른쪽          위·아래 변: 가로로 반복
왼쪽    | 가운데 반복 ... | 오른쪽          왼쪽·오른쪽 변: 세로로 반복
왼쪽 아래| 아래 아래 ...  | 오른쪽 아래      가운데: 양쪽으로 반복, 끝은 잘라 그린다
```

그래서 할 일은 **원본에서 `rect`(그림 칸)와 `border`(테두리 두께 왼·위·오·아래)를 정확히 재는 것**뿐이다. 코드는 고치지 않는다. 테마 형식은 `game/themes/README.md`.

## 하지 않는다

- `rect`·`border`를 눈대중이나 16px 격자로 짐작하기. 테두리는 5·6px처럼 칸과 안 맞는다. 반드시 아래 스크립트로 잰다.
- 변이나 가운데를 늘이기(`"fill": "stretch"`)로 때우기. 픽셀아트 테두리가 뭉개진다.
- 견본판을 열어 보지 않고 끝났다고 하기.
- 큰 창을 만들려고 원본 그림을 크게 다시 그리거나 여러 장을 이어 붙이기. 반복은 게임이 한다.

## 순서

1. **그림 고르기.** 사용자가 말한 시트와 그림의 대략 위치를 찾는다. 테두리 안쪽의 불투명한 점 하나의 좌표(PNG 왼쪽 위 기준)를 정한다. 좌표를 모르면 시트를 열어 보고 고른다.
   - LimeZu Modern UI: `Assets/modernuserinterface-win/16x16/Modern_UI_Style_1.png`(갈색), `Modern_UI_Style_2.png`(회백색). 32x32·48x48 판은 같은 그림의 2·3배다. 16x16 판을 쓰고 `pixelSize`로 키운다.
2. **재기.**
   ```bash
   python Tools/ui-nine-slice/nine_slice.py measure <시트.png> --at X,Y
   # 옆 그림과 붙어 있어 덩어리가 너무 크게 잡히면 칸을 직접: --rect X,Y,W,H
   ```
   출력 JSON의 `rect`, `border`, `theme`을 쓴다. 스크립트는 열(행) 전체가 똑같이 되풀이되는 구간을 찾아 그 앞뒤를 테두리로 정한다. 열 전체를 비교하므로 위 변·가운데·아래 변이 함께 이어진다.
   - `problems`가 나오면(되풀이 구간 없음) 이 그림은 반복 9조각이 안 된다. `rect`가 옆 그림까지 먹었는지 보고, 아니면 다른 그림을 고른다. 억지로 border를 넣지 않는다.
   - `warning`(무늬 주기 > 1px)이 나오면 그 스킨을 쓰는 패널의 가운데 폭·높이를 주기의 배수로 둬야 무늬가 안 끊긴다. 사용자에게 알린다.
3. **견본판 보기.** `preview` 경로의 PNG를 직접 연다(`output/ui-nine-slice/`). 볼 것:
   - 맨 위 확대 그림의 빨간 선이 모서리 장식(둥근 끝, 그림자, 강조 점)을 다 테두리 쪽에 두는지.
   - 대화창(가로로 긴 것), 큰 창, 홀수 크기에서 변에 이음매·줄무늬·끊긴 선이 없는지, 모서리 장식이 변 가운데에 되풀이돼 나오지 않는지.
   - 문제가 있으면 `--border`를 1px씩 바꿔 `preview`만 다시 그린다:
     ```bash
     python Tools/ui-nine-slice/nine_slice.py preview <시트.png> --rect X,Y,W,H --border L,T,R,B
     ```
4. **테마에 적기.** `game/themes/<테마>/theme.json`의 해당 스킨에:
   ```json
   "dialogue": { "image": "repo:Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png",
                 "rect": [ 8, 0, 32, 32 ], "border": [ 5, 13, 5, 6 ], "fill": "tile", "pixelSize": 3 }
   ```
   - `pixelSize`는 정수(원본 1px이 1920x1080 논리 화면에서 몇 칸인가). 16px 시트는 보통 3.
   - 글자가 테두리에 닿지 않게 필요하면 `padding`. 어두운 그림 위 글자는 `textColor`.
   - 새 테마 폴더를 만들 때는 `game/themes/README.md`의 필요한 스킨·아이콘을 모두 채운다.
5. **게임에서 확인.** 테마 검사와 화면 테스트를 돌리고 스크린샷을 직접 연다(`host-play-test` 스킬). 겹친 글자, 잘린 패널, 테두리 위 글자가 없는지 본다.
   ```bash
   dotnet test game/tests/Rootborn.Data.Tests --filter "FullyQualifiedName~FileReferenceTests"   # 테마가 가리키는 PNG가 있는지
   dotnet test game/tests/Rootborn.Host.Tests --filter "FullyQualifiedName~UiSkinDataTests|FullyQualifiedName~TestThemeScreensTests"
   ```
   - `UiSkinDataTests`는 지금 테마(`style2`, `urban`)의 좌표를 고정해 둔 테스트가 있다(`Style2_KeepsTheMeasuredCoordinates...`). 그 테마의 스킨을 바꾸면 이 테스트가 실패하는 것이 맞다. 새 값으로 고치고 왜 바꿨는지 적는다. 새 테마 폴더를 만들면 해당 없음.
   - `UrbanThemeConversion`(ThemeConversionWriter)은 `urban/theme.json`을 옛 데이터로 다시 쓰는 도구다. 이 일에 돌리지 않는다.
6. **보고.** 고른 그림(시트, rect), border, 견본판 경로, 게임 스크린샷 경로를 적는다. 근거 종류는 `.claude/rules/core-logic-and-play-evidence.md`.

## 알아 둘 것

- 스크립트의 견본판은 게임의 `NineSlice.Build`와 같은 계산으로 그린다. 게임 쪽 계산이 바뀌면 `build_pieces`도 같이 고친다.
- 가운데 칸은 되풀이 구간 전체로 잡는다. 1px로 잡으면 큰 창에서 그리기 조각이 수만 개가 된다.
- 그림 출처의 라이선스: LimeZu 팩은 게임에 넣어 쓸 수 있다. **Unity Asset Store에서 산 그림(예: Pixel Vault Fantasy Book UI)은 약관 2.2.1.1(g)가 판매자 동의 없이 AI의 입력으로 쓰는 것을 막는다.** 그런 그림은 열어 보지 말고 `measure`·`preview`만 돌려 숫자로 판단하거나, 사람에게 견본판 확인을 맡긴다.
