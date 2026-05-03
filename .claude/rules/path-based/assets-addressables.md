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

## 금지
- `Resources.Load<T>()` 신규 사용
  - **예외**: 부팅 실패 시 fallback (DataManager.InitAsync에서 Addressables 실패 시 Resources fallback 1회)
- 주소 문자열 하드코딩 — 상수 클래스(`DataManager.AddrRegistry` 등) 우선
- `LoadAssetAsync` 결과 await 없이 동기 접근
- 동일 address를 매 프레임 LoadAsync (캐시 미사용 = 메모리 누수)

## 기존 Resources 마이그레이션
- `Assets/Resources/GameDataRegistry.asset` 은 Addressables 등록과 병행 보관 (DataManager fallback용)
- 후속 PR에서 Resources 폴더 자체 제거 예정

## CI 게이트 (TODO)
- 후속: `Scripts/ci/check-no-resources-load.sh` — `Resources.Load` 신규 호출 금지 정규식
