# Addressables 도입 규칙

> **한국어 요약**: 동적 에셋 로딩은 Addressables 일원화. `Resources.Load` 신규 사용 금지. ROOTBORN은 SlimeMaster 차용 패턴(`Managers` 싱글톤 + `ResourceManager` async 래퍼).

## 적용 대상
- 모든 동적 로딩 에셋 (프리팹/이미지/오디오/SO)
- 작물·도구·자원·지식·세대 SO 등 GameDataRegistry 인스턴스

## 필수
- 주소 네이밍: `{domain}/{type}/{id}` (kebab-case 또는 snake_case)
  - 데이터: `data/registry`, `data/crop/wheat`
  - 스프라이트: `sprites/ground`, `sprites/player/idle_down`, `sprites/crop/wheat_0`
  - 프리팹: `prefabs/player`, `prefabs/resource/tree`
  - 타일: `tiles/ground`
  - 오디오: `audio/sfx/gather`, `audio/bgm/farm`
- 로드: `await Managers.Resource.LoadAsync<T>(address)` (Task 기반 async/await)
- 라벨 일괄 로드: `await Managers.Resource.LoadByLabelAsync<T>("PreLoad")`
- 사용 종료 시 `Managers.Resource.Release(address)` 또는 `ReleaseAll()` 호출 의무
- 그룹: `Data` / `Sprites` / `Prefabs` / `Tiles` / `Audio` (Local-only, 향후 Remote는 ADR로 결정)

## ROOTBORN 매니저 구조 (SlimeMaster 차용)
- `Rootborn.Game.Managers.Managers` — 싱글톤 진입점, `@Managers` GameObject + DontDestroyOnLoad
- `Rootborn.Game.Managers.ResourceManager` — Addressables 래퍼 (캐시, async/await, Release 명시)
- `Rootborn.Game.Managers.DataManager` — `GameDataRegistry` + 도메인별 Dictionary lookup
- 부팅: `await Managers.BootstrapAsync()` (GameBootstrap.Start에서 호출)

## Sub-sprite 로드 패턴 (Pixelwood multi-sprite PNG)

Pixelwood처럼 한 PNG 안에 여러 sub-sprite를 가진 sheet를 사용할 때:
1. **Editor**: `AddressablesSetup`이 sheet 자체를 그룹에 등록 (예: `sheet/Down`, `sheet/Tile`)
2. **Runtime**: `Managers.Resource.LoadSubSpriteAsync(sheetAddress, subName)` 호출
   - 내부적으로 `Addressables.LoadAssetAsync<IList<Sprite>>(sheetAddress)` → sub-sprite를 이름으로 매칭 + 캐시
3. **상수 위치**: `DataManager.AddrSheetTile`, `AddrSheetIdleDown`, `SubGroundTile`, `SubPlayerIdle`
   - 또는 `AddressableManifest` SO (사용자가 Inspector에서 sub-name 변경 가능)

## 금지
- `Resources.Load<T>()` 신규 사용
  - **예외**: 부팅 실패 시 fallback (DataManager.InitAsync에서 Addressables 실패 시 Resources fallback 1회)
- 주소 문자열 하드코딩 — 상수 클래스(`DataManager.AddrRegistry` 등) 우선
- `LoadAssetAsync` 결과 await 없이 동기 접근
- 동일 address를 매 프레임 LoadAsync (캐시 미사용 = 메모리 누수)

## 기존 Resources 마이그레이션
- `Assets/Resources/GameDataRegistry.asset` 은 Addressables 등록과 병행 보관 (DataManager fallback용)
- 후속 PR에서 Resources 폴더 자체 제거 예정

## 라이프사이클 — 동기 조회는 BootstrapAsync 완료 후

`ResourceManager.Load<T>(addr)` 는 **사전 로드된 캐시만 동기 조회** 하므로, 호출 시점에 `Managers.BootstrapAsync` 가 끝나있어야 한다.

### 안티패턴 (금지)
```csharp
private void Awake()
{
    BuildUI(); // ← 같은 씬 GameBootstrap.Start 가 BootstrapAsync 시작하는데
               //    Unity 라이프사이클 상 모든 Awake 가 모든 Start 보다 먼저 실행됨
               //    → 캐시 미스 → 모든 sprite null → 단색 fallback UI
}
```

### 권장 패턴
```csharp
private async void Start()
{
    await Rootborn.Game.Managers.Managers.BootstrapAsync(); // 멱등 (IsBootstrapped 캐시) — 중복 await 안전
    if (this == null || !isActiveAndEnabled) return;        // 그 사이 destroy 됐을 수 있음
    BuildUI();
}
```

`Awake` 단계의 작업은 sprite 가 필요 없는 것 (canvas 검색, 입력 시스템 등록 등) 으로 한정. UI 트리 빌드는 `Start` 이후로 미룬다.

## 일관성 검증 (필수 테스트)

UI sprite 주소 상수 파일 (`UISpriteAddresses`) 과 등록 파일 (`AddressablesSetup`) 이 **둘 다 수정되어야** 빌드 시 sprite 가 캐시에 들어간다. 한 쪽만 수정하면 런타임 캐시 미스 → 모든 UI 가 단색 fallback 으로 그려지는 회귀 사고 발생.

따라서 EditMode 테스트로 강제 (`Assets/Tests/EditMode/UISpriteAddressesTests.cs`):
- `AllSingleSprites_AreAllRegisteredInAddressablesSetup`
- `AllSheets_AreAllRegisteredInAddressablesSetup`
- `AllUiSpriteEntries_HaveAssetFileOnDisk`

신규 sprite 추가 시 두 파일 모두 갱신 + 위 테스트 통과 확인 의무.

## CI 게이트 (TODO)
- 후속: `Scripts/ci/check-no-resources-load.sh` — `Resources.Load` 신규 호출 금지 정규식
