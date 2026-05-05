# Sprite Sheet 슬라이스 규칙

> **한국어 요약**: 외부 sprite sheet를 자동 슬라이스할 때 셀 크기를 추정하지 말고 실측. PPU도 sheet마다 다르게.

## Why — 실제 사례 (2026-05-04)

ROOTBORN 초기에 모든 Pixelwood sheet에 16×16을 일괄 적용한 결과:
- `Player Character/Idle/Down.png`는 실제로 **236×49** (4프레임 × 59×49 cell)
- 16으로 슬라이스 → 14개 잘못된 sub-sprite, 캐릭터의 일부만 잘려나옴
- `Idle_Down_1`을 PlayerSprite로 와이어링했지만 화면엔 **갈색 점/빈 공간**만 보임

원인: 파일명/폴더명에 `16x16`이 적혀있는 sheet(`crops 16x16.png`, `Items 16x16.png`)와 그렇지 **않은** sheet(`Down.png`, `Tile.png` 등)를 같은 셀 크기로 처리.

## 절대 원칙

### 1. 셀 크기는 sheet마다 실측

새 sprite sheet를 슬라이스 코드에 추가하기 전에 **다음 두 가지를 확인**:
- 텍스처 픽셀 크기 (예: 236×49)
- 한 프레임의 정확한 픽셀 크기 (예: 59×49)

확인 방법:
1. **Unity Editor**: 파일 클릭 → Inspector에서 가로/세로 표시
2. **CLI**: `magick identify path/to.png` 또는 `file path/to.png`
3. **Read 도구**: PNG를 시각으로 보면서 추정 (대략적)

**파일명에 적힌 숫자(`16x16`)도 우연일 수 있다 — 항상 실측**.

### 2. 셀 정수 분할 검증

```csharp
int cols = tex.width / cellW;
if (tex.width % cellW != 0)
    Debug.LogWarning($"{path}: width {tex.width} is not divisible by {cellW}, last partial cell will be lost.");
```

코드가 `Mathf.Max(1, ...)` 같은 무조건 fallback을 쓰면 잘못된 셀 크기가 조용히 통과한다 — `%` 검증으로 즉시 경고.

### 3. PPU(Pixels Per Unit)는 sheet 셀 크기에 맞춰

캐릭터 sheet(59×49)와 타일 sheet(16×16)에 같은 PPU 16을 적용하면 캐릭터가 4 world unit이 되어 타일 4개 크기로 보임 — 비율 깨짐.

규칙:
- 16×16 타일/아이콘 → PPU **16** (1 unit = 1 tile)
- 캐릭터 59×49 → PPU **49** (1 unit ≈ 1 캐릭터 키, 타일과 비슷한 크기)
- 통일된 1 world unit 기준 유지

`SliceTarget` 구조체에 `PixelsPerUnit` 필드 두고 sheet별 명시.

### 4. 신규 sheet 추가 절차

1. 텍스처 픽셀 크기 실측
2. 한 프레임 셀 크기 결정 (가로/세로)
3. 정수 분할 검증 (% == 0)
4. PPU 결정 (CellH 또는 CellW)
5. SliceTarget에 항목 추가
6. 슬라이스 후 sub-sprite 이름 한 개 골라 **Inspector에서 시각 확인**
7. Sub-sprite 이름이 game code (예: `DataManager.SubPlayerIdle`)와 매칭되는지 검증

### 5. Sub-sprite 인덱스 지정 시 주의

`Idle_Down_0`이 항상 "정상 캐릭터"라고 가정하지 말 것. 일부 sheet는 첫 프레임이 빈 칸이거나 특수 변형. 명확한 인덱스를 정한 후:
- 코드 상수로 고정 (`SubPlayerIdle = "Idle_Down_1"`)
- 변경 시 `Sprite Editor` 창에서 시각 검증

## 안티패턴 (금지)

```csharp
// ❌ 모든 sheet에 같은 셀 크기 강제
private const int CellSize = 16;
foreach (var sheet in sheets) Slice(sheet, CellSize, CellSize);

// ❌ Mathf.Max로 무조건 1셀 fallback
int cols = Mathf.Max(1, tex.width / cellW);  // 이러면 잘못된 셀 크기가 조용히 통과

// ❌ 파일명/폴더명에서 셀 크기 추정 ('16x16'이 파일명에 있다고 셀이 16일 거라 가정)
```

## 권장 패턴

```csharp
// ✅ Sheet별 실측한 셀 크기 명시
private struct SliceTarget {
    public string AssetPath;
    public int CellW, CellH;
    public int PixelsPerUnit; // 0이면 기본 16
}

// ✅ 정수 분할 검증
if (tex.width % target.CellW != 0 || tex.height % target.CellH != 0)
    Debug.LogWarning($"{target.AssetPath}: {tex.width}x{tex.height} is not exact multiple of {target.CellW}x{target.CellH}");

// ✅ Sub-sprite 검증 메뉴 (시각 확인용)
[MenuItem("Rootborn/Pixelwood/Verify Slice Result")]
public static void Verify() {
    // 첫 sub-sprite를 임시 GameObject로 띄워서 Scene 뷰에서 확인
}
```

### 6. Sheet 가 cell 균등 분할이 **안 되는** 경우 — 명시 rect 사용

파일명에 적힌 cell 크기와 sheet 실제 크기가 1픽셀 단위로 안 맞는 경우(예: "1 22x20.png" sheet 실제 22×99, 5 셀 = 5×20=100 ≠ 99) 가 흔하다. 이 때 균등 cell 슬라이스(SliceOne)를 그대로 적용하면:
- **마지막 cell이 잘려나가 sub-sprite 누락**
- **각 cell의 내용이 한 픽셀씩 어긋나 시각적으로 잘려 보임**

**해결**: sheet 별 전용 슬라이스 메서드 작성 — 명시 rect 으로 5개 셀을 위→아래로 분할, 마지막 셀에 leftover 픽셀 흡수.

```csharp
private static bool SliceBookmarkSheet()
{
    int W = tex.width, H = tex.height; // 22, 99
    int n = 5;
    int cellH = H / n;                 // 19
    var metas = new List<SpriteMetaData>();
    for (int i = 0; i < n; i++)
    {
        int yTop = i * cellH;
        int yBottom = H - yTop - cellH;
        if (i == n - 1) { cellH = H - yTop; yBottom = 0; } // 마지막 셀 leftover 흡수
        metas.Add(new SpriteMetaData {
            name = $"Bookmark_{i}",
            rect = new Rect(0, yBottom, W, cellH),
            ...
        });
    }
    importer.spritesheet = metas.ToArray();
    importer.SaveAndReimport();
}
```

**판별 기준**: sheet 실제 픽셀 크기 % cell 크기 != 0 일 때 즉시 명시 rect 메서드 작성.
파일명 추정값보다 **실제 sheet 픽셀 크기가 우선**.

## Sprite 원본 방향 (flipX 부호)

**Side-view 캐릭터 sprite는 원본이 어느 방향을 보는지 실측**해야 flipX 부호가 정확하다.

### 사례 (2026-05-04)
Pixelwood `Player Character/Idle/Side.png`, `Walk/Side.png`는 **원본이 왼쪽을 향함**.
처음에 일반적 관례(원본 = 오른쪽)로 가정해 `flipX = (input.x < 0)`로 작성 → 게임에서 좌우 반대로 보임.

### 규칙
1. 새 sprite 추가 시 **PNG를 직접 보고** 원본 방향 확인 (Read 도구로 텍스처 표시 가능)
2. 원본이 오른쪽을 향함 → `flipX = (input.x < 0)`
3. 원본이 왼쪽을 향함 → `flipX = (input.x > 0)` ← **Pixelwood Player**
4. 코드에 주석으로 어느 방향이 원본인지 명시:
   ```csharp
   // Pixelwood Side.png 원본은 왼쪽을 향함 → 오른쪽 입력일 때 flipX
   _renderer.flipX = _input.x > 0f;
   ```

## 외부 프레임 sprite 의 자식 UI 배치 — anchor 기준은 시각 영역, panel sizeDelta 아님

Pixelwood Page1.png 같은 sprite 는 **외부 어두운 프레임 + 갈색 spine 측면 + 내부 베이지 페이지** 가 한 sprite 에 통합돼있다. 자식 UI (북마크/슬롯/리본 등) 를 panel 의 외곽 (anchor 1.0 또는 0.0) 에 붙이면 회색 프레임 바깥에 떠있게 된다 — 사용자 의도는 거의 항상 **시각적 콘텐츠 영역(베이지 페이지) 가장자리** 기준.

### 사례 (2026-05-05)
ROOTBORN 책 UI 의 5색 북마크를 BookPanel anchor=(1, 0.5) 로 잡았더니, 북마크가 회색 외부 프레임 바깥쪽 빈 공간에 떠있었음. 사용자: "회색부분이 아니라 완전 옆면 책의 맨 뒤 갈색부분 보다 왼쪽으로 와야하는데". BookPanel sizeDelta=1248×792 = sprite 전체이고, 베이지 페이지 우측 끝은 sprite 픽셀 263/290 = UV **0.907** 에 있음.

### 안티패턴 (외부 panel 크기 = 시각 가장자리로 착각)
```csharp
rt.anchorMin = new Vector2(1f, 0.5f); // ← BookPanel 의 외부 회색 프레임 끝
rt.anchorMax = new Vector2(1f, 0.5f);
rt.pivot = new Vector2(0f, 0.5f);
rt.anchoredPosition = new Vector2(-22f, y); // 회색 프레임 위로 22px 좌측
// 결과: 북마크가 회색 프레임/갈색 측면 위에 떠있음 — 베이지 페이지에 안 닿음
```

### 권장 — sprite 픽셀 측정 후 UV 비율 사용
1. PNG 를 Read 도구로 시각 확인 (또는 Sprite Editor 에서 픽셀 좌표 측정).
2. 시각적 콘텐츠 영역 가장자리의 픽셀 좌표를 기록 (예: Page1.png 290×184 의 우측 베이지 페이지 끝 = x=263 → UV 263/290 = 0.907).
3. UV 비율을 anchor 로 사용:
```csharp
rt.anchorMin = new Vector2(0.907f, 0.5f); // 베이지 페이지 우측 끝
rt.anchorMax = new Vector2(0.907f, 0.5f);
rt.pivot = new Vector2(0f, 0.5f);
rt.anchoredPosition = new Vector2(-10f, y); // 베이지 안쪽으로 10px
```

### 일반화
- 9-slice 패널 sprite — sliced border 바깥 픽셀(외부 프레임)은 시각 영역 아님. 자식 anchor 는 border 안쪽 UV 기준.
- 외부 프레임이 두꺼운 모든 UI sprite 에서 자식 anchor 는 **panel sizeDelta 가장자리(0/1)** 가 아니라 **sprite 안쪽 시각 영역 UV** 사용.
- 사용자가 "프레임 위가 아니라 콘텐츠 옆에 붙여" 라고 지적하면 anchor UV 부터 점검.

## Pixelwood Valley 구체 데이터

| Sheet | 크기 | Cell | PPU | Frames |
|---|---|---|---|---|
| `Farm/Crops/crops 16x16.png` | 128×160 | 16×16 | 16 | 80 (8×10) |
| `Pixelwood Valley Icon Pack 1.0/1.0/Items 16x16.png` | 336×240 | 16×16 | 16 | 315 (21×15) |
| `Tiles/Tile.png` | 240×192 | 16×16 | 16 | 180 (15×12) |
| `Player Character/Idle/Down.png` | 236×49 | 59×49 | 49 | 4 (1행) |
| `Player Character/Idle/Side.png` | 236×49 | 59×49 | 49 | 4 |
| `Player Character/Idle/Up.png` | 236×49 | 59×49 | 49 | 4 |
| `Player Character/Walk/Down.png` | 236×49 | 59×49 | 49 | 4 |
| `Player Character/Walk/Side.png` | 236×49 | 59×49 | 49 | 4 |
| `Player Character/Walk/Up.png` | 236×49 | 59×49 | 49 | 4 |

신규 Pixelwood asset(NPC/건물/이펙트) 추가 시 위 표에 행 추가 후 SliceTarget 등록.

## 관련 코드/파일

- `Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs` — 슬라이스 진입점
- `Assets/Scripts/Game/Managers/DataManager.cs` — `SubPlayerIdle` 등 sub-sprite 이름 상수
- `Assets/Scripts/Game/Common/AddressableManifest.cs` — Addressables 주소·sub-name 매니페스트
