# Assets/Scripts/Game/** (게임플레이 로직) 규칙

> **한국어 요약**: MonoBehaviour/ScriptableObject 기반 게임플레이 코드 표준. `rules/coding-standards.md` 전체 규약 우선.

## 적용 경로
- `Assets/Scripts/Game/**`
- `Assets/Scripts/Combat/**`
- `Assets/Scripts/Progression/**`

## 필수
- MonoBehaviour 는 `sealed` + `[DisallowMultipleComponent]`
- 상태는 ScriptableObject 분리 (저장/로드 대비)
- 이벤트는 `event Action<T>` 사용 (UnityEvent 지양)

## 금지
- `GameObject.Find`, `FindObjectOfType` 런타임 호출
- Update 내 `new`, `GetComponent`, LINQ
- 플레이어 데이터 직접 `PlayerPrefs` 저장 (서버 SaveService 경유)

## 스토리 타입 매핑 (/gs-create-stories Type 결정 시)
- 게임 규칙·수치 변경 → Logic
- 여러 시스템 결합 → Integration
- 히트 이펙트·사운드 트리거 → Visual/Feel
