# Modern UI Style2 Icon Catalog Goal

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트의 `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png`에 포함된 모든 UI 버튼/아이콘/컨트롤 sprite를 직접 전수조사하고, 코드에서 안전하게 참조할 수 있는 Style2 UI semantic catalog와 enum을 구축해줘.

목표:
- Modern_UI_Style_2.png의 16x16 sliced sprite 전체를 실제 이미지 기준으로 조사한다.
- 잠금 버튼, 집 버튼, 방향키 버튼, 체크 버튼, 새로고침 버튼, 마우스 포인터, 토글, 탭, 슬롯, 스크롤바, 패널 조각, 아이콘류 등 화면에 보이는 모든 UI 요소를 의미 이름으로 매핑한다.
- 예: `r1_c13 = chair`, `r1_c14 = bed`처럼 row/column 좌표와 의미 이름을 연결한다.
- `r3_c42`, `r3_c43`, `r3_c44`처럼 같은 버튼이 높이/눌림 상태별로 3프레임 구성된 경우, 개별 sprite와 그룹을 모두 catalog에 기록한다.
- 최종적으로 런타임 UI 코드가 raw 좌표 문자열 대신 semantic enum/catalog를 사용하도록 기반을 만든다.

절대 규칙:
- AGENTS.md 헌법을 지킨다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 생성/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 실패 테스트 먼저 작성하고 구현한다.
- 게임 엔티티 ID enum 금지 규칙은 유지한다. 이번 enum은 게임플레이 엔티티 ID가 아니라 UI sprite semantic key 전용이어야 하며, 활동/작물/도구/퀘스트 등 게임 엔티티 분기에 사용하면 안 된다.
- `if (sprite == rX_cY)` 같은 raw 좌표 기반 런타임 분기를 만들지 않는다.
- 기존 사용자 변경분은 되돌리지 않는다.
- Modern UI Style2 공통 패널 규약을 유지한다. 공통 패널은 `r2_c0~r4_c2`, fill은 `r3_c1`이다.

우선 조사 대상:
- `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png`
- 기존 슬라이스 설정:
  - `Assets/Scripts/Editor/Tools/ModernUiSliceSetup.cs`
  - Style2는 49 columns x 34 rows, 총 1666 sprites
- 기존 catalog:
  - `Assets/Scripts/Game/Common/ModernUiStyle2Sprites.cs`
  - `docs/art/modern-ui-style2-sprite-map.json`
  - `docs/art/modern-ui-style2-reconstruction-manifest.json`
- 관련 테스트:
  - `Assets/Tests/EditMode/TownConcept/ModernUiStyle2RecipeTests.cs`
  - `Assets/Tests/EditMode/TownConcept/ModernUiStyle2RuntimeSourceAuditTests.cs`

필수 산출물:
1. 전수조사 문서
   - `docs/art/modern-ui-style2-icon-catalog.md`
   - 모든 확인된 버튼/아이콘/컨트롤을 표로 정리한다.
   - 컬럼 예시:
     - coordinate: `r1_c13`
     - spriteName: `ModernUI_16_Style2_r1_c13`
     - semanticId: `furniture.chair`
     - enumName: `FurnitureChair`
     - category: `furniture`, `button`, `toggle`, `cursor`, `direction`, `status`, `panel`, `slot`, `scrollbar` 등
     - stateGroupId: 같은 버튼의 normal/hover/pressed/disabled/height-frame 묶음이 있으면 기록
     - stateRole: `normal`, `hover`, `pressed`, `disabled`, `frame0`, `frame1`, `frame2` 등
     - confidence: `confirmed`, `probable`, `unknown`
     - note: 시각 판독 근거 또는 애매한 점
   - 의미가 불명확한 sprite는 억지 이름을 붙이지 말고 `unknown.r{row}.c{col}`로 남기고 confidence를 `unknown`으로 둔다.

2. 기계 판독용 JSON catalog
   - `docs/art/modern-ui-style2-icon-catalog.json`
   - 문서와 동일한 정보를 JSON으로 제공한다.
   - rows=34, columns=49, spriteCount=1666 메타데이터를 포함한다.
   - 모든 row/column 좌표가 누락 없이 들어가야 한다.
   - 의미를 확정한 항목과 unknown 항목을 모두 포함한다.

3. C# semantic catalog
   - `Assets/Scripts/Game/Common/ModernUiStyle2Icon.cs`
     - UI sprite semantic enum 또는 동등한 key 타입을 정의한다.
     - 게임 엔티티 ID enum이 아님을 주석으로 명확히 한다.
   - `Assets/Scripts/Game/Common/ModernUiStyle2IconCatalog.cs`
     - enum/key에서 `ModernUiSpriteKey` 또는 `ModernHudSpriteKey`로 변환하는 catalog를 제공한다.
     - raw coordinate string은 이 파일 내부의 catalog 데이터로만 제한한다.
     - 런타임 UI 구현부가 `ModernUI_16_Style2_rX_cY` 문자열을 직접 쓰지 않도록 한다.

4. 상태 그룹 모델
   - 같은 버튼/컨트롤의 상태별 sprite 묶음을 표현한다.
   - 예:
     - `button.someControl.normal = r3_c42`
     - `button.someControl.hoverOrRaised = r3_c43`
     - `button.someControl.pressedOrLowered = r3_c44`
   - `r3_c42/r3_c43/r3_c44`는 반드시 직접 시각 확인 후 같은 버튼의 높이/눌림 애니메이션 그룹으로 catalog에 포함한다.
   - 동일한 2프레임/3프레임/4프레임 유형이 다른 위치에도 있으면 모두 그룹화한다.

권장 접근:
1. Style2 원본 이미지를 실제로 열어 row/column grid 기준으로 전수조사한다.
2. 자동 이미지 분할/thumbnail contact sheet를 만들어 사람이 검토하기 쉬운 형태로 저장한다.
3. 현재 `ModernUiStyle2Sprites.cs`의 일부 semantic key를 새 catalog에 흡수하되, 기존 public API를 바로 깨지 않도록 호환 계층을 둔다.
4. 먼저 JSON/문서 catalog를 완성하고, 그 다음 C# enum/catalog를 생성한다.
5. 불확실한 아이콘은 unknown으로 남기고, 추측을 확정값처럼 쓰지 않는다.

필수 테스트:
- EditMode 테스트를 먼저 작성한다.
- catalog JSON이 rows=34, columns=49, spriteCount=1666을 만족하는지 검증한다.
- 모든 `r0_c0`부터 `r33_c48`까지 좌표가 JSON에 정확히 한 번씩 존재하는지 검증한다.
- 모든 catalog entry의 spriteName이 `ModernUI_16_Style2_r{row}_c{col}` 형식을 따르는지 검증한다.
- C# catalog의 모든 enum/key가 실제 JSON entry 및 sliced sprite와 연결되는지 검증한다.
- `r3_c42`, `r3_c43`, `r3_c44`가 같은 stateGroupId로 묶이고 stateRole이 구분되는지 검증한다.
- 기존 `ModernUiStyle2Sprites.CommonPanel`의 r2_c0~r4_c2 규약이 깨지지 않는지 회귀 테스트한다.
- 런타임 UI 주요 코드에서 raw `ModernUI_16_Style2_r` 문자열을 직접 참조하지 않는 source audit를 유지하거나 확장한다.
- `Scripts/ci/check-no-entity-id-branching.sh`를 실행해 엔티티 ID 분기 금지 규칙을 통과한다.

완료 조건:
- Style2 16x16 sheet의 1666개 좌표가 문서/JSON catalog에 모두 들어 있다.
- 버튼/아이콘/컨트롤로 식별 가능한 항목은 semanticId와 enumName이 부여되어 있다.
- 의미가 불명확한 항목은 unknown으로 보존되어 누락되지 않는다.
- 3프레임 버튼/눌림 애니메이션 유형이 stateGroup으로 표현되어 있다.
- `r3_c42`, `r3_c43`, `r3_c44` 그룹이 명시적으로 포함되어 있다.
- C# enum/catalog를 통해 UI 코드가 의미 기반으로 Style2 sprite를 참조할 수 있다.
- 기존 Style2 패널/recipe/Addressables 흐름이 깨지지 않는다.
- 최종 보고에는 다음을 포함한다:
  - 조사한 원본 이미지 경로
  - 총 좌표 수와 confirmed/probable/unknown 개수
  - 대표 매핑 예시 20개 이상
  - 3프레임/상태 그룹 목록
  - 생성/수정한 문서, JSON, C# 파일
  - 실행한 테스트와 결과
  - 아직 unknown으로 남은 항목의 후속 검토 기준
```

## Scope Note

이번 goal은 UI를 새로 구현하는 작업이 아니라, Modern UI Style2 sprite sheet를 좌표 단위로 전수조사해 의미 기반 catalog를 구축하는 작업이다. 런타임 UI 변경은 raw 좌표 문자열 의존을 줄이기 위한 호환 계층까지만 포함하고, 실제 화면 재구성은 후속 goal에서 다룬다.
