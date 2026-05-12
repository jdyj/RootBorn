using Rootborn.Game.Common;
using Rootborn.Game.Family;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.Game.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private SpriteRenderer _toolRenderer;
        [SerializeField] private CharacterPartComposer _partComposer;
        [SerializeField] private CharacterPartAnimator _partAnimator;
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private GatherInteractor _interactor;
        [SerializeField] private float _attackFrameDuration = 0.06f;
        [SerializeField] private Camera _camera;

        private InputAction _moveAction;
        private InputAction _attackAction;
        private InputAction _pointerAction;
        private Vector2 _input;
        private Vector2 _lastFacing = new Vector2(0f, -1f);
        private string _activeToolId;
        private string _activeToolSpritePrefix;
        private CharacterPartAnimationClipDefinition _activeCharacterPartAnimationClip;
        private bool _inventoryEventsBound;

        private bool _isAttacking;
        private float _attackTime;
        private Vector2 _attackFacing;
        private bool _attackTriggered;
        private bool _wasMouseAttackPressed;

        private static readonly int HashMoveX = Animator.StringToHash("MoveX");
        private static readonly int HashMoveY = Animator.StringToHash("MoveY");
        private static readonly int HashSpeed = Animator.StringToHash("Speed");

        public Vector2 LastFacing => _lastFacing;
        public Vector2 Input => _input;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_partComposer == null) _partComposer = GetComponent<CharacterPartComposer>();
            if (_partAnimator == null) _partAnimator = GetComponent<CharacterPartAnimator>();
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            if (_inventory == null) _inventory = GetComponent<PlayerInventory>();
            if (_interactor == null) _interactor = GetComponent<GatherInteractor>();
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            _wasMouseAttackPressed = false;
            TryBindInventory();
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            Vector2 screenPos = _pointerAction != null ? _pointerAction.ReadValue<Vector2>() : ReadPointerScreenPosition();
            BeginAttack(screenPos);
        }

        private void BeginAttack(Vector2 screenPos)
        {
            if (!HasToolSprites(_activeToolSpritePrefix)) return;
            if (_isAttacking) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            var worldPos = _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
            var dir = (Vector2)(worldPos - transform.position);
            if (dir.sqrMagnitude < 0.0001f) dir = _lastFacing;
            _attackFacing = dir.normalized;
            _lastFacing = _attackFacing;
            _isAttacking = true;
            _attackTime = 0f;
            _attackTriggered = false;
            if (_partAnimator != null && _activeCharacterPartAnimationClip != null)
            {
                _partAnimator.PlayClip(_activeCharacterPartAnimationClip);
            }
            if (Mathf.Abs(_attackFacing.x) > Mathf.Abs(_attackFacing.y))
            {
                ApplyFlipX(_attackFacing.x > 0f);
            }
        }

        private void TryBindInventory()
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<PlayerInventory>();
            }
            if (_inventory == null || _inventoryEventsBound) return;
            _inventory.OnEquipmentChanged += OnEquipmentChanged;
            _inventoryEventsBound = true;
            OnEquipmentChanged();
        }

        private void OnDisable()
        {
            DisposeAction(ref _moveAction);
            if (_attackAction != null)
            {
                _attackAction.performed -= OnAttackPerformed;
            }
            DisposeAction(ref _attackAction);
            DisposeAction(ref _pointerAction);

            if (_inventory != null && _inventoryEventsBound)
            {
                _inventory.OnEquipmentChanged -= OnEquipmentChanged;
                _inventoryEventsBound = false;
            }
        }

        private static void DisposeAction(ref InputAction action)
        {
            if (action == null)
            {
                return;
            }

            action.Disable();
            action.Dispose();
            action = null;
        }

        private void OnEquipmentChanged()
        {
            var tool = _inventory != null ? _inventory.EquippedToolItem : null;
            string newId = tool != null ? tool.Id : null;
            if (newId == _activeToolId) return;
            _activeToolId = newId;
            _activeToolSpritePrefix = tool != null ? tool.ToolSpritePrefix : null;
            _activeCharacterPartAnimationClip = null;
            var data = Rootborn.Game.Managers.Managers.Data;
            if (data != null && tool != null && data.ToolById.TryGetValue(tool.Id, out var toolDefinition))
            {
                _activeCharacterPartAnimationClip = toolDefinition.CharacterPartAnimationClip;
            }
            _isAttacking = false;
            _attackTime = 0f;

            if (_animator != null)
            {
                _animator.enabled = !HasToolSprites(_activeToolSpritePrefix);
            }
            if (!HasToolSprites(_activeToolSpritePrefix) && _toolRenderer != null)
            {
                _toolRenderer.sprite = null;
                _toolRenderer.enabled = false;
            }
        }

        private static bool HasToolSprites(string spritePrefix)
        {
            return !string.IsNullOrEmpty(spritePrefix);
        }

        private static string FacingToDirKey(Vector2 facing)
        {
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
            {
                return "Side";
            }
            return facing.y > 0f ? "Up" : "Down";
        }

        private void Update()
        {
            TryBindInventory();
            if (_partComposer == null) _partComposer = GetComponent<CharacterPartComposer>();
            if (_partAnimator == null) _partAnimator = GetComponent<CharacterPartAnimator>();

            _input = ReadMoveInput();
            UpdateMouseAttackInput();

            if (_rb == null)
            {
                transform.position += (Vector3)(_input * (_moveSpeed * UnityEngine.Time.deltaTime));
            }

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

            var facingForFlip = _isAttacking ? _attackFacing : _lastFacing;
            bool isSide = Mathf.Abs(facingForFlip.x) > Mathf.Abs(facingForFlip.y);
            ApplyFlipX(isSide && facingForFlip.x > 0f);

            if (_partAnimator != null)
            {
                _partAnimator.SetMotion(_isAttacking ? Vector2.zero : _input, facingForFlip);
                _partAnimator.Tick(UnityEngine.Time.deltaTime);
            }

            UpdateToolSprite();
        }

        private void UpdateMouseAttackInput()
        {
            var mouse = Mouse.current;
            bool pressed = mouse != null && mouse.leftButton.isPressed;
            if (pressed && !_wasMouseAttackPressed)
            {
                BeginAttack(ReadPointerScreenPosition());
            }
            _wasMouseAttackPressed = pressed;
        }

        private static Vector2 ReadPointerScreenPosition()
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        }

        private Vector2 ReadMoveInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        private void ApplyFlipX(bool flipX)
        {
            if (_renderer != null)
            {
                _renderer.flipX = flipX;
            }
            if (_toolRenderer != null)
            {
                _toolRenderer.flipX = flipX;
            }
            if (_partComposer != null)
            {
                _partComposer.SetFlipX(flipX);
            }
        }

        private SpriteRenderer EnsureToolRenderer()
        {
            if (_toolRenderer != null)
            {
                return _toolRenderer;
            }

            var child = transform.Find("Part_tool");
            if (child == null)
            {
                var go = new GameObject("Part_tool");
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            _toolRenderer = child.GetComponent<SpriteRenderer>();
            if (_toolRenderer == null)
            {
                _toolRenderer = child.gameObject.AddComponent<SpriteRenderer>();
            }
            _toolRenderer.sortingOrder = 10;
            _toolRenderer.enabled = false;
            return _toolRenderer;
        }

        private void UpdateToolSprite()
        {
            string prefix = _activeToolSpritePrefix;
            if (!HasToolSprites(prefix))
            {
                if (_toolRenderer != null)
                {
                    _toolRenderer.sprite = null;
                    _toolRenderer.enabled = false;
                }
                return;
            }

            var rm = Rootborn.Game.Managers.Managers.Resource;
            if (rm == null) return;

            var facing = _isAttacking ? _attackFacing : _lastFacing;
            string dir = FacingToDirKey(facing);
            string sheetAddr = $"sprites/player/tool/{prefix.ToLowerInvariant()}-{dir.ToLowerInvariant()}";

            int frame;
            if (_isAttacking)
            {
                _attackTime += UnityEngine.Time.deltaTime;
                int rawFrame = Mathf.FloorToInt(_attackTime / Mathf.Max(0.01f, _attackFrameDuration));
                int totalFrames = GetSheetFrameCount(prefix, dir, rm, sheetAddr);
                frame = rawFrame;

                if (!_attackTriggered && frame >= totalFrames - 1)
                {
                    _attackTriggered = true;
                    if (_interactor != null) _interactor.TriggerInteract();
                }
                if (frame >= totalFrames)
                {
                    _isAttacking = false;
                    _attackTime = 0f;
                    frame = 0;
                }
            }
            else
            {
                frame = 0;
            }

            string subName = $"{prefix}_{dir}_{frame}";
            var s = rm.GetCachedSubSprite(sheetAddr, subName);
            if (s == null && frame > 0)
            {
                s = rm.GetCachedSubSprite(sheetAddr, $"{prefix}_{dir}_0");
            }
            if (s != null)
            {
                EnsureToolRenderer();
                _toolRenderer.sprite = s;
                _toolRenderer.enabled = true;
            }
        }

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
            _rb.linearVelocity = _input * _moveSpeed;
        }

        public void Bind(Animator animator, SpriteRenderer renderer)
        {
            _animator = animator;
            _renderer = renderer;
        }
    }
}
