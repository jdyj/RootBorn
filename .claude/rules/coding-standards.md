# 코딩 표준 — Unity 2D C#

> **경로별 세부 규칙**: `rules/path-based/assets-gameplay.md`, `rules/path-based/assets-data.md`, `rules/path-based/assets-addressables.md`, `rules/path-based/assets-ui.md`, `rules/path-based/server.md`
> **전 경로 적용 — 환경 설정**: `rules/environment-config.md` (host/port/url/credential/path 등 환경 의존 값 처리)

헌법 `constitution.md`의 보조 규칙. 모든 C# 코드 생성 에이전트는 이 파일을 따른다.

## 파일 구조

```csharp
// Assets/Scripts/Game/Player/PlayerController.cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>한 줄 요약.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private Rigidbody2D _rigidbody;

        private Vector2 _inputDirection;

        private void Reset()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            _rigidbody.velocity = _inputDirection * _moveSpeed;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            _inputDirection = context.ReadValue<Vector2>();
        }
    }
}
```

## 규약

### 클래스
- `sealed` 기본 (상속이 명확히 필요할 때만 해제)
- `MonoBehaviour`는 `[DisallowMultipleComponent]` 권장
- 순수 데이터: `[System.Serializable]` + `struct` 고려
- ScriptableObject: `[CreateAssetMenu(fileName, menuName)]` 필수

### 필드
- `[SerializeField] private` 우선. Inspector 노출 목적 외 `public` 필드 금지
- `[Header]`, `[Tooltip]`으로 Inspector UX 개선
- 직렬화 필드는 `_camelCase`

### 메서드
- `private` 기본, 필요시만 `public`
- Unity 메시지 메서드(Start/Update/…)는 `private`로 선언
- `Awake`는 자기 참조, `Start`는 타 컴포넌트 참조
- `Update` 최소화, `FixedUpdate`는 물리, 이벤트 기반 우선

### 성능
- `Update` 안에서 `GetComponent`, `Find`, `new` 금지 → 캐싱
- `string` 연결은 `$""` 또는 `StringBuilder` (핫패스에서만)
- 컬렉션은 capacity 지정: `new List<T>(64)`
- LINQ는 핫패스 금지, 프로토타입/Editor만 허용

### null & 라이프타임
```csharp
// Unity 오브젝트
if (target == null) return;   // Unity 오버로드 사용

// 일반 C# 객체
if (ReferenceEquals(handler, null)) return;
```

### 이벤트
`event Action<T>` 사용. UnityEvent는 Inspector 필요 시만.

### 에디터 분리
```csharp
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Foo : MonoBehaviour
{
    // ...

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector 값 변경 시 검증
    }
#endif
}
```

### Assembly Definition
- `Assets/Scripts/Game.asmdef` — 런타임 코드
- `Assets/Scripts/Editor/Game.Editor.asmdef` — 에디터 전용
- `Assets/Tests/EditMode/Game.Tests.EditMode.asmdef`
- `Assets/Tests/PlayMode/Game.Tests.PlayMode.asmdef`

## 금지 패턴

| 금지 | 이유 | 대안 |
|------|------|------|
| `public` 필드 | Inspector 직렬화 외에 API 경계 모호 | `[SerializeField] private` + 프로퍼티 |
| `Update` 내 `GetComponent` | 매 프레임 비용 | Awake/Start에서 캐싱 |
| `Find`/`FindObjectOfType` | O(n), 타입 안전성 없음 | 참조 주입, 서비스 로케이터, Addressables |
| `Resources.Load` (신규 코드) | 빌드 크기, 지연 로드 어려움 | Addressables |
| 매직 넘버 | 의도 불명, 튜닝 어려움 | `[SerializeField]` 또는 ScriptableObject |
| `#region` | 큰 클래스를 감추는 냄새 | 클래스 분리 |
