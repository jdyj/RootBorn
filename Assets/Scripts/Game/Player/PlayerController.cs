using Rootborn.Game.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.Game.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private GatherInteractor _interactor;
        [SerializeField] private float _attackFrameDuration = 0.06f; // 휘두르기 frame 간격(초). 빠를수록 휙휙.
        [SerializeField] private Camera _camera; // 좌클릭 위치 → world 변환용. null 이면 Camera.main 자동.

        private InputAction _moveAction;
        private InputAction _attackAction;
        private InputAction _pointerAction;
        private Vector2 _input;
        private Vector2 _lastFacing = new Vector2(0f, -1f); // 기본 Down
        private string _activeToolId; // null = 도구 미장착 (기본 Idle/Walk sprite 사용 — Animator 그대로 동작).

        // 휘두르기 상태머신 — 좌클릭 시 활성. frame 0 → 끝까지 한 번 재생 후 idle 복귀.
        private bool _isAttacking;
        private float _attackTime;
        private Vector2 _attackFacing; // 휘두르는 동안 facing 고정 (마우스 클릭 순간 결정).
        private bool _attackTriggered;

        private static readonly int HashMoveX = Animator.StringToHash("MoveX");
        private static readonly int HashMoveY = Animator.StringToHash("MoveY");
        private static readonly int HashSpeed = Animator.StringToHash("Speed");

        public Vector2 LastFacing => _lastFacing;
        public Vector2 Input => _input;

        private void Awake()
        {
            // Animator/Renderer/Rigidbody2D 미설정 시 자동 검색 (FarmAutoFiller가 인스턴스화 후 와이어링 안 한 케이스)
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
            if (_interactor == null) _interactor = GetComponent<GatherInteractor>();
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            _moveAction = new InputAction(type: InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _moveAction.Enable();

            // 마우스 좌클릭 — 휘두르기 트리거.
            _attackAction = new InputAction(type: InputActionType.Button);
            _attackAction.AddBinding("<Mouse>/leftButton");
            _attackAction.performed += OnAttackPerformed;
            _attackAction.Enable();

            // 마우스 위치 — 휘두르는 방향 결정용 (read-only, 매 프레임 ReadValue).
            _pointerAction = new InputAction(type: InputActionType.Value, expectedControlType: "Vector2");
            _pointerAction.AddBinding("<Mouse>/position");
            _pointerAction.Enable();

            TryBindInventory();
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            // 도구 장착 + 캐시된 sheet 가 있는 경우에만 휘두르기. 미장착 시엔 GatherInteractor 의 E 키 흐름만.
            if (!HasToolSprites(_activeToolId)) return;
            if (_isAttacking) return; // 이미 휘두르는 중이면 무시.

            // 마우스 위치 → world → 캐릭터 기준 방향 벡터.
            var screenPos = _pointerAction != null ? _pointerAction.ReadValue<Vector2>() : Vector2.zero;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var worldPos = _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            var dir = (Vector2)(worldPos - transform.position);
            if (dir.sqrMagnitude < 0.0001f) dir = _lastFacing;
            _attackFacing = dir.normalized;
            _lastFacing = _attackFacing; // 휘두르는 방향이 곧 새 facing.
            _isAttacking = true;
            _attackTime = 0f;
            _attackTriggered = false;
            // flipX 즉시 반영 — 좌클릭 위치가 캐릭터 우측이면 flipX (Pixelwood Side 원본 = 왼쪽).
            if (_renderer != null && Mathf.Abs(_attackFacing.x) > Mathf.Abs(_attackFacing.y))
            {
                _renderer.flipX = _attackFacing.x > 0f;
            }
        }

        // PlayerInventory 가 PlayerController 보다 늦게 추가될 수 있음 (FarmAutoFiller 동적 와이어링).
        // OnEnable 에서 null 이면 매 프레임 재시도.
        private void TryBindInventory()
        {
            if (_inventory != null) return;
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null) return;
            _inventory.OnEquipmentChanged += OnEquipmentChanged;
            OnEquipmentChanged();
        }

        private void OnDisable()
        {
            if (_moveAction != null)
            {
                _moveAction.Disable();
                _moveAction.Dispose();
                _moveAction = null;
            }
            if (_attackAction != null)
            {
                _attackAction.performed -= OnAttackPerformed;
                _attackAction.Disable();
                _attackAction.Dispose();
                _attackAction = null;
            }
            if (_pointerAction != null)
            {
                _pointerAction.Disable();
                _pointerAction.Dispose();
                _pointerAction = null;
            }
            if (_inventory != null) _inventory.OnEquipmentChanged -= OnEquipmentChanged;
        }

        // 장착 도구 변경 시 호출. _activeToolId 갱신 + Animator 비활성/활성 (sprite 직접 swap 모드 충돌 회피).
        private void OnEquipmentChanged()
        {
            var tool = _inventory != null ? _inventory.EquippedToolItem : null;
            string newId = tool != null ? tool.Id : null;
            if (newId == _activeToolId) return;
            _activeToolId = newId;
            _isAttacking = false;
            _attackTime = 0f;

            // 도구 장착 중에는 Animator 가 sprite 채널을 덮어쓰지 않도록 비활성. 미장착 복귀 시 다시 활성.
            if (_animator != null)
            {
                _animator.enabled = !HasToolSprites(_activeToolId);
            }
        }

        // _activeToolId 가 sprite swap 가능한 도구인지 (Pixelwood Axe/Hoe/Pickaxe/Pickup 만 매핑됨).
        private static bool HasToolSprites(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return false;
            return toolId == "StoneAxe" || toolId == "StoneHoe"
                || toolId == "StonePickaxe" || toolId == "Axe"
                || toolId == "Hoe" || toolId == "Pickaxe" || toolId == "Pickup";
        }

        // _activeToolId → Pixelwood sheet prefix 매핑 ("StoneAxe" → "Axe", "StoneHoe" → "Hoe").
        private static string ToolSpritePrefix(string toolId)
        {
            if (string.IsNullOrEmpty(toolId)) return null;
            switch (toolId)
            {
                case "StoneAxe":     return "Axe";
                case "StoneHoe":     return "Hoe";
                case "StonePickaxe": return "Pickaxe";
                case "Axe":          return "Axe";
                case "Hoe":          return "Hoe";
                case "Pickaxe":      return "Pickaxe";
                case "Pickup":       return "Pickup";
                default: return null;
            }
        }

        // facing 방향 → 4방향 → "Down"/"Side"/"Up" 키 변환. 좌측은 Side + flipX 로 처리.
        private static string FacingToDirKey(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            {
                return "Side"; // 좌/우 모두 Side, flipX 가 좌우 처리.
            }
            return facing.y > 0f ? "Up" : "Down";
        }

        private void Update()
        {
            // _inventory 가 늦게 추가됐을 수 있으니 매 프레임 재시도 (이미 바인딩됐으면 즉시 return).
            TryBindInventory();

            _input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

            // Rigidbody2D 없는 경우(테스트·fallback): 기존 transform 이동.
            // Rigidbody2D 있으면 FixedUpdate 에서 MovePosition 사용 (collider 차단을 위함).
            if (_rb == null)
            {
                transform.position += (Vector3)(_input * (_moveSpeed * UnityEngine.Time.deltaTime));
            }

            // 휘두르는 동안에는 facing 잠금 (마우스 클릭 순간 결정된 방향 유지).
            if (!_isAttacking && _input.sqrMagnitude > 0.01f)
            {
                _lastFacing = _input.normalized;
            }

            if (_animator != null && _animator.enabled)
            {
                _animator.SetFloat(HashMoveX, _input.x);
                _animator.SetFloat(HashMoveY, _input.y);
                _animator.SetFloat(HashSpeed, _input.sqrMagnitude);
            }

            // flipX — 휘두르는 동안엔 _attackFacing 기준, 평소엔 입력 기준.
            // Side sprite 일 때만 flipX 적용 (원본 = 왼쪽 향함, 우측이면 flipX=true).
            // Up/Down sprite 일 때는 flipX=false 로 reset (이전 Side 잔재 제거).
            if (_renderer != null)
            {
                var facingForFlip = _isAttacking ? _attackFacing : _lastFacing;
                bool isSide = Mathf.Abs(facingForFlip.x) > Mathf.Abs(facingForFlip.y);
                if (isSide) _renderer.flipX = facingForFlip.x > 0f;
                else _renderer.flipX = false;
            }

            // 도구 장착 시: sprite 를 Pixelwood 도구 sheet 의 frame 으로 직접 swap (Animator 비활성).
            UpdateToolSprite();
        }

        // 도구 장착 시 _renderer.sprite 를 Pixelwood 도구 sheet 의 적절한 frame 으로 매 프레임 갱신.
        // Animator 가 OnEquipmentChanged 에서 비활성화 되어 sprite 채널 충돌 없음.
        //
        // 두 가지 상태:
        //  - Idle/Walk: frame 0 고정 (도구를 그냥 들고 다님)
        //  - Attack:    frame 0→끝 한 번 재생 (좌클릭 시), 끝나면 자동 idle 복귀 + 마지막 frame 에서 GatherInteractor 호출
        private void UpdateToolSprite()
        {
            if (_renderer == null) return;
            string prefix = ToolSpritePrefix(_activeToolId);
            if (prefix == null) return; // 도구 미장착 — Animator 가 sprite 관리.

            var rm = Rootborn.Game.Managers.Managers.Resource;
            if (rm == null) return;

            // 휘두르는 동안엔 _attackFacing 기준, 평소엔 _lastFacing 기준.
            var facing = _isAttacking ? _attackFacing : _lastFacing;
            string dir = FacingToDirKey(facing);
            string sheetAddr = $"sprites/player/tool/{prefix.ToLowerInvariant()}-{dir.ToLowerInvariant()}";

            int frame;
            if (_isAttacking)
            {
                _attackTime += UnityEngine.Time.deltaTime;
                int rawFrame = Mathf.FloorToInt(_attackTime / Mathf.Max(0.01f, _attackFrameDuration));
                // 실제 sheet frame 수 — Axe/Hoe/Pickaxe 6f, Hoe/Down 7f, Pickup 3f. 6 으로 가정 후 fallback.
                int totalFrames = GetSheetFrameCount(prefix, dir, rm, sheetAddr);
                frame = rawFrame;

                // 휘두르기 마지막 프레임에 도달하면 GatherInteractor 트리거 (한 번만).
                if (!_attackTriggered && frame >= totalFrames - 1)
                {
                    _attackTriggered = true;
                    if (_interactor != null) _interactor.TriggerInteract();
                }
                if (frame >= totalFrames)
                {
                    // 휘두르기 종료 — idle frame 0 으로 복귀.
                    _isAttacking = false;
                    _attackTime = 0f;
                    frame = 0;
                }
            }
            else
            {
                // Idle / Walk — frame 0 고정 (도구를 들고 있는 정적 포즈).
                frame = 0;
            }

            string subName = $"{prefix}_{dir}_{frame}";
            var s = rm.GetCachedSubSprite(sheetAddr, subName);
            // frame 인덱스가 sheet frame 수 초과 시 frame 0 fallback.
            if (s == null && frame > 0)
            {
                s = rm.GetCachedSubSprite(sheetAddr, $"{prefix}_{dir}_0");
            }
            if (s != null)
            {
                _renderer.sprite = s;
            }
        }

        // sheet 의 실제 frame 수 추정 — 0..N 중 첫 null 직전 인덱스. 결과는 8까지만 검사 (안전 상한).
        // 매 프레임 호출이라 micro-cost 무시 가능 (캐시 dictionary lookup 8회).
        private static int GetSheetFrameCount(string prefix, string dir, Rootborn.Game.Managers.ResourceManager rm, string sheetAddr)
        {
            for (int i = 0; i < 8; i++)
            {
                if (rm.GetCachedSubSprite(sheetAddr, $"{prefix}_{dir}_{i}") == null) return i;
            }
            return 8;
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;
            // Dynamic Rigidbody2D: velocity 직접 설정.
            // 정적 collider(돌)와 부딪치면 물리 엔진이 자동으로 멈춤.
            _rb.linearVelocity = _input * _moveSpeed;
        }

        public void Bind(Animator animator, SpriteRenderer renderer)
        {
            _animator = animator;
            _renderer = renderer;
        }
    }
}
