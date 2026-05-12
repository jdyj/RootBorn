# UI/UX 표준 — Unity 2D PC

> **경로별 세부 규칙**: `rules/path-based/assets-ui.md` (본 파일 내용 준수 + 경로 한정 추가 규칙)
> **다국어**: `rules/localization.md`

헌법 `constitution.md`의 보조 규칙. 모든 UI 생성 에이전트는 이 파일을 따른다.

## 타겟 플랫폼

ROOTBORN은 **PC 데스크톱** 기준이다 (Windows 64-bit 1순위, macOS/Linux 후속).

- **기본 해상도**: 1920×1080 (Full HD, Steam Hardware Survey 1위)
- **최소 지원**: 1280×720
- **상한**: 2560×1440 / 3840×2160까지 자동 스케일
- **표시 모드**: Borderless Fullscreen 기본 (`fullscreenMode: 3`), 창 모드 토글 가능, resizable=true
- **종횡비**: 16:9 우선, 16:10 / 21:9 그레이스풀 디그레이드 (UI는 안전 영역에 정렬)

## Canvas Scaler 표준

모든 UI Canvas는 다음 설정 사용:

- `UI Scale Mode`: Scale With Screen Size
- `Reference Resolution`: 1920×1080
- `Screen Match Mode`: Match Width Or Height = 0.5 (균형)
- `Reference Pixels Per Unit`: 16 (Pixelwood 16x16 타일 기준)

## Modern UI Style2 공통 패널

공통 패널 배경은 반드시 `Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png`의 16x16 Style2 패널 블록을 사용한다.

- 소스 address: `sprites/ui/modern/16/style-2` (`ModernUISpriteAddresses.Style16Alt`)
- 구현 경로: `ModernUiTileImage + ModernUiRecipes.CommonPanel`
- 좌상단/상단/우상단: `ModernUI_16_Style2_r2_c0`, `ModernUI_16_Style2_r2_c1`, `ModernUI_16_Style2_r2_c2`
- 좌측/중앙 fill/우측: `ModernUI_16_Style2_r3_c0`, `ModernUI_16_Style2_r3_c1`, `ModernUI_16_Style2_r3_c2`
- 좌하단/하단/우하단: `ModernUI_16_Style2_r4_c0`, `ModernUI_16_Style2_r4_c1`, `ModernUI_16_Style2_r4_c2`
- `r3_c1`은 패널 내부 fill tile로 16x16 반복 배치한다.
- 공통 패널에 `Image.sprite`로 단일 corner/edge sprite를 직접 넣는 방식은 금지한다. 크기가 변하는 패널은 `ModernUiTileImage`가 3x3 역할 타일을 반복 생성해야 한다.

## PC 타이포그래피 (1920×1080 기준)

| 용도 | 최소 fontSize | 권장 fontSize |
|------|-------------|-------------|
| 제목 (Title) | 32 | 36-48 |
| 부제목 (Subtitle) | 24 | 24-32 |
| 본문 (Body) | 18 | 18-22 |
| 보조 텍스트 (Caption) | 14 | 14-16 |
| 버튼 텍스트 | 18 | 20-24 |
| 절대 하한선 | 14 | -- |

12px 이하 텍스트는 금지. 픽셀아트 폰트(Pixelwood 무드와 일치) 사용 시 16/24/32 단계로만 가는 것을 권장.

## 마우스/키보드 어포던스

- 버튼/클릭 가능 요소: 최소 32×32px, 권장 48×48px
- 호버 상태(`PointerEnter`)에 시각 변화 필수 (밝기 +10% 또는 outline)
- 누름 상태(`PointerDown`) 추가 시각 변화
- 툴팁: 호버 0.5초 후 표시
- 포커스 가능 요소는 키보드 네비게이션 지원 (`Selectable.navigation` 명시)
- 단축키 표기: 버튼 텍스트 옆 `[E]`, `[Esc]` 형식

## 인터랙티브 요소 가시성

- 버튼은 배경색, 테두리, 또는 그림자로 "누를 수 있음"을 시각 표현해야 한다
- 탭 바: 활성 탭은 AccentColor 배경 + Bold, 비활성 탭은 테두리(Outline) + 구분 가능한 배경색
- 텍스트와 버튼이 시각적으로 구분 불가능하면 안 된다
- 비활성 탭에도 최소한의 배경색(Surface보다 밝은)과 테두리를 적용한다
- **선택된 탭/북마크/카테고리 버튼은 위치 offset 또는 색조 강조 중 하나 이상**으로 시각 표시 의무 (정적 색만으로 부족 — 사용자가 어느 항목이 활성인지 한눈에 식별 가능해야 함)
  - 예: 선택된 북마크는 비선택 대비 24~32px 더 튀어나옴 + 채도 ↑

## 카테고리/탭 콘텐츠 컨테이너 분리

여러 카테고리(탭/북마크) 가 있는 UI 패널은 **카테고리당 별도 GameObject 컨테이너** 를 만들고, 카테고리 전환 시 SetActive 토글 한다.

### 금지
- 한 페이지에 모든 카테고리 콘텐츠를 혼재 시키고 일부만 enable 토글 (디버깅 어렵고 anchor 충돌)
- 카테고리 전환 시 sprite/text 만 갈아끼우는 패턴 (페이지 구조 자체가 카테고리마다 다른 경우 부적합)

### 허용
```csharp
// 카테고리당 컨테이너 1개. ApplyCategory 시 토글.
private readonly Dictionary<Category, GameObject> _pagesByCat = new();

void BuildItemsPage(...) { _pagesByCat[Category.Items] = leftItems; }
void BuildEquipmentPage(...) { _pagesByCat[Category.Equipment] = leftEquip; }

void ApplyCategory(Category cat) {
    foreach (var kv in _pagesByCat) {
        kv.Value.SetActive(kv.Key == cat);
    }
}
```

### 같은 콘텐츠를 여러 카테고리가 공유 시
같은 컨테이너를 여러 카테고리 키에 매핑 — 필터만 다른 케이스에서 유용.
```csharp
_pagesByCat[Category.All]      = sharedItemsPage;
_pagesByCat[Category.Resource] = sharedItemsPage; // 같은 GameObject
_pagesByCat[Category.Tool]     = sharedItemsPage;
```

## 직렬화 참조 완전성 (Serialized Reference Completeness)

Editor 스크립트가 UI를 절차적으로 생성할 때:
1. `AddComponent<T>()` 후 T의 모든 `[SerializeField]` 필드를 와이어링해야 한다
2. `_panel`만 설정하고 `_closeButton`, `_tabButton` 등을 누락하면 안 된다
3. 생성 완료 후 null 참조 검증 로그를 출력해야 한다

## 게임 수치는 추정 금지

슬롯 수, 그리드 크기, 재화량, 타이머 등 게임플레이 관련 숫자는:
- 디자인 문서 또는 ScriptableObject에서 참조
- 없으면 사용자에게 확인
- 에이전트가 임의 값을 넣으면 안 된다

## UI 정보 구조 확인

메뉴 계층 구조(별도 패널 vs 탭, 팝업 vs 전체 화면)는 사용자 확인 후 구현.
모호하면 추정하지 말고 질문한다.

## 입력 시스템

- New Input System 사용 (`Assets/InputSystem_Actions.inputactions`)
- 액션 맵: `Player` (이동/상호작용/도구 사용), `UI` (포커스/메뉴/창 토글)
- 키보드 + 마우스 기본, Gamepad는 후속 확장 옵션
- ESC = 메뉴/취소, F11 = 풀스크린 토글, F10 = 디버그 시간 가속
