# Assets/UI/** 및 UI 관련 Prefab 규칙

> **한국어 요약**: 모바일 UI 전용 코딩·배치 표준. `rules/ui-standards.md` 의 상세 타이포·터치 타겟 기본, 경로 한정 규칙을 이 파일에 추가.

## 적용 경로
- `Assets/UI/**`
- `Assets/Scripts/UI/**`
- `Assets/Prefabs/UI/**`

## 필수
- Canvas Scaler: `UI Scale Mode = Scale With Screen Size`, Reference Resolution 1080x1920
- 모든 `TMP_Text.fontSize >= 40` (rules/ui-standards.md 타이포 표 참조)
- 버튼 터치 타겟 >= 96x96
- `[SerializeField] private` 필드는 Inspector 와이어링 100% 완료 후 커밋
- TextMeshPro 사용 (UnityEngine.UI Text 금지)

## 금지
- `UnityEngine.UI.Text`, `UnityEngine.UI.Button` 구형 → TextMeshPro `TMP_Text` / `Button`(TMP)
- 하드코딩 문자열 → `rules/localization.md` 기반 다국어 키
- Update 안에서 `GetComponentInChildren<TMP_Text>()` 등 룩업

## 연계 에이전트
- `t3-ui-programmer` (P4b 추가 시)
- `ui-standards.md` (기존 규칙)
