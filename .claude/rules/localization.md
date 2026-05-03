# 다국어 규약

> **한국어 요약**: 한국어/영어 2언어 필수. 키 기반 관리, 런타임 하드코딩 금지.

## 지원 언어 (P2 기준)
- 한국어 (ko) — 1차
- 영어 (en) — 2차
- 추가 언어: Parking Lot (실제 출시 계획 확정 후)

## 키 규약

- 네이밍: `{domain}.{screen}.{element}` (kebab-case)
  - 예: `combat.hud.hp-label`, `shop.gacha.pull-button`
- 저장: `Assets/Localization/{lang}.json` (혹은 Unity Localization 패키지 + Addressables)

## 코드 사용

```csharp
// OK
hpLabel.text = Localization.Get("combat.hud.hp-label");

// NG — 하드코딩
hpLabel.text = "HP";
```

## 에이전트 책임
- `t2-localization-lead` (P4b 추가 시): 키 체계 결정, 번역 품질 검수
- `/gs-create-stories`: UI 타입 스토리에 localization 키 목록 포함 강제

## 스토리 타입 매핑
- 신규 다국어 키 추가만 → Config/Data
- 신규 텍스트를 포함한 새 UI → UI
