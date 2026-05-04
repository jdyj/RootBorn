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
