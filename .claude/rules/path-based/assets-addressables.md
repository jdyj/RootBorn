# Addressables 도입 규칙

> **한국어 요약**: 동적 에셋 로딩은 Addressables 일원화. `Resources.Load` 신규 사용 금지. 기존 `Assets/Resources/` 는 마이그레이션 대상.

## 적용 대상
- 모든 동적 로딩 에셋 (프리팹/이미지/오디오/SO)
- 스테이지·보스·장비·스킬 SO

## 필수
- 주소 네이밍: `{domain}/{type}/{id}` (예: `combat/enemy/boss-dragon`, `ui/panel/gacha`)
- 로드: `Addressables.LoadAssetAsync<T>()` + `await` (UniTask)
- 사용 종료 시 `Addressables.Release` 필수 (메모리 누수 방지)
- 그룹(`Local`, `Remote`, `InitialContent`) 은 `t3-unity-specialist` 가 결정

## 금지
- `Resources.Load<T>()` 신규 사용
- 주소 문자열 하드코딩 — 상수 클래스 `AddressableKeys`
- `LoadAssetAsync` 결과 await 없이 동기 접근

## 기존 Resources 마이그레이션
- 현재 `Assets/Resources/` 존재 (P0 시점). 마이그레이션은 P6 헌법 갱신 후 별도 스토리.
- 마이그레이션 전까지 `Assets/Resources/` 는 유지 허용 (헌법 예외).
